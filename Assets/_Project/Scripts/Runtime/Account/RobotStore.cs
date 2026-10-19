using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// =========================================================
// STORE DES ROBOTS — cache local d'abord, serveur ensuite
// Remplace l'acces direct a PocketBaseClient depuis les menus
// et le garage :
// - toute sauvegarde ecrit d'abord robots.json dans le dossier
//   de l'identite (IdentityCache), puis pousse vers PocketBase
//   si la session est en ligne ; sinon le robot reste `dirty` ;
// - une suppression hors ligne laisse une pierre tombale
//   (deletedAt) jusqu'a la synchro, pour ne pas ressusciter le
//   robot au retour en ligne ;
// - a la reconnexion, SyncAsync rapproche local et serveur robot
//   par robot (cle = clientId, sinon id) : present d'un seul cote
//   -> copie vers l'autre (tombale -> supprime des deux cotes) ;
//   des deux cotes -> editedAt le plus recent gagne ; egalite ->
//   le serveur gagne ;
// - les robots du profil Invite sont adoptes par le premier compte
//   qui se connecte (baies libres uniquement).
// Les menus continuent de manipuler des RobotRecord.
// =========================================================

[Serializable]
public class LocalRobot
{
    public string id;        // record PocketBase (vide tant que jamais pousse)
    public string clientId;  // GUID stable cote client
    public string name;
    public int slot;
    public RobotBlueprint data;
    public RobotStats stats;
    public string editedAt;  // ISO 8601 UTC ("o")
    public string deletedAt; // pierre tombale (vide = vivant)
    public bool dirty;       // modification locale pas encore sur le serveur

    public bool IsDeleted => !string.IsNullOrEmpty(deletedAt);
}

[Serializable]
public class LocalRobotFile
{
    public List<LocalRobot> robots = new();
    public string syncedAt;
}

public struct RobotSaveResult
{
    public RobotRecord record;
    public bool online; // true = sur le serveur ; false = sur ce PC seulement (dirty)
}

public static class RobotStore
{
    private const string RobotsFile = "robots.json";

    private static LocalRobotFile cache;
    private static string cacheKey;
    private static bool synced;         // synchro faite pour la session en ligne courante
    private static bool syncing;
    private static bool hooked;

    public static event Action Changed;

    private static void Hook()
    {
        if (hooked)
            return;
        hooked = true;
        SessionService.StateChanged += state =>
        {
            // Une nouvelle session en ligne doit resynchroniser
            if (state != SessionState.Online)
                synced = false;
        };
        SessionService.IdentityChanged += () => EnsureCache();
    }

    // Nombre de modifications locales en attente du serveur
    public static int PendingChanges
    {
        get
        {
            EnsureCache();
            int n = 0;
            foreach (var r in cache.robots)
                if (r.dirty || r.IsDeleted) n++;
            return n;
        }
    }

    public static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    public static string NewClientId() => Guid.NewGuid().ToString("N");

    // =========================================================
    // LECTURE
    // =========================================================

    // Robots du joueur, tries par baie. En ligne : synchronise d'abord (une fois par
    // session). Toute erreur reseau bascule la session hors ligne et renvoie le cache.
    public static async Awaitable<RobotRecord[]> ListAsync()
    {
        EnsureCache();
        if (SessionService.IsOnline && !synced)
            await SyncAsync();
        return Snapshot();
    }

    private static RobotRecord[] Snapshot()
    {
        var list = new List<RobotRecord>();
        foreach (var r in cache.robots)
            if (!r.IsDeleted)
                list.Add(ToRecord(r));
        list.Sort((a, b) => a.slot.CompareTo(b.slot));
        return list.ToArray();
    }

    // =========================================================
    // ECRITURE
    // =========================================================

    public static async Awaitable<RobotSaveResult> SaveAsync(string recordId, string clientId, string name, int slot,
                                                              RobotBlueprint blueprint, RobotStats stats)
    {
        EnsureCache();

        if (string.IsNullOrEmpty(clientId))
            clientId = NewClientId();

        var local = FindLocal(clientId, recordId);
        if (local == null)
        {
            local = new LocalRobot { clientId = clientId };
            cache.robots.Add(local);
        }
        local.id = string.IsNullOrEmpty(recordId) ? local.id : recordId;
        local.clientId = clientId;
        local.name = name;
        local.slot = slot;
        local.data = blueprint;
        local.stats = stats;
        local.editedAt = Now();
        local.deletedAt = null;
        local.dirty = true;
        Persist();

        if (!SessionService.IsOnline)
            return new RobotSaveResult { record = ToRecord(local), online = false };

        try
        {
            await Push(local);
            Persist();
            return new RobotSaveResult { record = ToRecord(local), online = true };
        }
        catch (PocketBaseException e) when (e.IsNetworkError)
        {
            SessionService.GoOffline("sauvegarde : " + e.Message);
            return new RobotSaveResult { record = ToRecord(local), online = false };
        }
        // Autres erreurs (400, 403...) : remontees a l'appelant, la copie locale reste dirty
    }

    public static async Awaitable DeleteAsync(RobotRecord record)
    {
        if (record == null)
            return;
        EnsureCache();

        var local = FindLocal(record.clientId, record.id);
        if (local == null)
        {
            // Robot connu du serveur seulement : tombale locale pour porter la suppression
            local = new LocalRobot { id = record.id, clientId = record.clientId ?? record.id, name = record.name, slot = record.slot };
            cache.robots.Add(local);
        }

        if (string.IsNullOrEmpty(local.id))
        {
            // Jamais pousse : rien a supprimer cote serveur
            cache.robots.Remove(local);
            Persist();
            Changed?.Invoke();
            return;
        }

        local.deletedAt = Now();
        local.dirty = false;
        Persist();

        if (!SessionService.IsOnline)
        {
            Changed?.Invoke();
            return;
        }

        try
        {
            await DeleteOnServer(local.id);
            cache.robots.Remove(local);
            Persist();
            Changed?.Invoke();
        }
        catch (PocketBaseException e) when (e.IsNetworkError)
        {
            SessionService.GoOffline("suppression : " + e.Message);
            Changed?.Invoke();
        }
    }

    // =========================================================
    // SYNCHRONISATION
    // =========================================================

    public static async Awaitable<bool> SyncAsync()
    {
        EnsureCache();
        var client = SessionService.Client;
        if (!SessionService.IsOnline || client == null || !client.IsAuthenticated || syncing)
            return false;

        syncing = true;
        try
        {
            var server = await client.ListRobotsAsync();

            var serverByKey = new Dictionary<string, RobotRecord>();
            foreach (var s in server)
                serverByKey[KeyOf(s.clientId, s.id)] = s;

            var matchedServer = new HashSet<string>();

            foreach (var local in new List<LocalRobot>(cache.robots))
            {
                RobotRecord match = null;
                if (!string.IsNullOrEmpty(local.clientId) && serverByKey.TryGetValue(local.clientId, out var byClient))
                    match = byClient;
                else if (!string.IsNullOrEmpty(local.id))
                {
                    foreach (var s in server)
                        if (s.id == local.id) { match = s; break; }
                }
                if (match != null)
                    matchedServer.Add(KeyOf(match.clientId, match.id));

                if (local.IsDeleted)
                {
                    if (match != null)
                        await DeleteOnServer(match.id);
                    cache.robots.Remove(local);
                    continue;
                }

                if (match == null)
                {
                    if (!string.IsNullOrEmpty(local.id) && !local.dirty)
                    {
                        // Supprime sur le serveur (autre machine) et intact ici : on suit le serveur
                        cache.robots.Remove(local);
                        continue;
                    }
                    local.id = null; // le record a pu disparaitre : on recree
                    await Push(local);
                    continue;
                }

                int cmp = CompareDates(local.editedAt, match.editedAt);
                if (local.dirty && cmp > 0)
                {
                    local.id = match.id;
                    await Push(local);
                }
                else
                {
                    Apply(local, match);
                }
            }

            foreach (var s in server)
            {
                if (matchedServer.Contains(KeyOf(s.clientId, s.id)))
                    continue;
                var local = new LocalRobot();
                Apply(local, s);
                cache.robots.Add(local);
            }

            cache.syncedAt = Now();
            Persist();
            synced = true;

            await AdoptGuestRobots();
            Changed?.Invoke();
            return true;
        }
        catch (PocketBaseException e)
        {
            Debug.LogWarning($"[RobotStore] Synchro impossible ({e.StatusCode}) : {e.Message}");
            Persist();
            if (e.IsNetworkError)
            {
                SessionService.GoOffline("synchro : " + e.Message);
            }
            else
            {
                // Refus du serveur (400, 403...) : on n'insiste pas a chaque ecran, la
                // prochaine session en ligne retentera (StateChanged remet `synced` a false)
                synced = true;
                Toast.Error($"Couldn't sync your robots. {e.Message}");
            }
            return false;
        }
        finally
        {
            syncing = false;
        }
    }

    // Robots crees en Invite : rattaches au compte connecte, dans ses baies libres
    private static async Awaitable AdoptGuestRobots()
    {
        var identity = SessionService.Identity;
        if (identity == null || identity.IsGuest)
            return;

        var guestFile = LoadFile(PlayerIdentity.GuestKey);
        if (guestFile == null || guestFile.robots.Count == 0)
            return;

        var usedSlots = new HashSet<int>();
        foreach (var r in cache.robots)
            if (!r.IsDeleted) usedSlots.Add(r.slot);

        bool moved = false;
        foreach (var guest in new List<LocalRobot>(guestFile.robots))
        {
            if (guest.IsDeleted)
            {
                guestFile.robots.Remove(guest);
                continue;
            }
            if (usedSlots.Contains(guest.slot))
                continue; // reste en Invite : la baie est prise

            guest.id = null;
            guest.dirty = true;
            guest.editedAt = Now();
            if (string.IsNullOrEmpty(guest.clientId))
                guest.clientId = NewClientId();
            cache.robots.Add(guest);
            usedSlots.Add(guest.slot);
            guestFile.robots.Remove(guest);
            moved = true;

            try
            {
                await Push(guest);
            }
            catch (PocketBaseException e)
            {
                Debug.LogWarning($"[RobotStore] Robot invite non pousse ({e.StatusCode}) : {e.Message}");
                if (e.IsNetworkError)
                    break;
            }
        }

        if (moved)
        {
            SaveFile(PlayerIdentity.GuestKey, guestFile);
            Persist();
            Toast.Info("Your guest robots were added to your hangar.");
        }
    }

    // =========================================================
    // SERVEUR
    // =========================================================

    private static async Awaitable Push(LocalRobot local)
    {
        var client = SessionService.Client;
        if (client == null || !client.IsAuthenticated)
            throw new PocketBaseException(401, "Not signed in.");

        RobotRecord saved;
        try
        {
            saved = await client.SaveRobotAsync(local.id, local.clientId, local.name, local.slot, local.data, local.stats, local.editedAt);
        }
        catch (PocketBaseException e) when (e.StatusCode == 404 && !string.IsNullOrEmpty(local.id))
        {
            // Le record a ete supprime ailleurs : on le recree
            saved = await client.SaveRobotAsync(null, local.clientId, local.name, local.slot, local.data, local.stats, local.editedAt);
        }

        local.id = saved.id;
        if (!string.IsNullOrEmpty(saved.clientId))
            local.clientId = saved.clientId;
        local.dirty = false;
    }

    private static async Awaitable DeleteOnServer(string recordId)
    {
        var client = SessionService.Client;
        if (client == null || !client.IsAuthenticated)
            throw new PocketBaseException(401, "Not signed in.");
        try
        {
            await client.DeleteRobotAsync(recordId);
        }
        catch (PocketBaseException e) when (e.StatusCode == 404)
        {
            // Deja supprime : c'est le resultat voulu
        }
    }

    // =========================================================
    // CACHE LOCAL
    // =========================================================

    private static void EnsureCache()
    {
        Hook();
        string key = SessionService.Identity?.LocalKey ?? PlayerIdentity.GuestKey;
        if (cache != null && cacheKey == key)
            return;
        cacheKey = key;
        cache = LoadFile(key) ?? new LocalRobotFile();
        synced = false;
    }

    private static void Persist() => SaveFile(cacheKey, cache);

    private static LocalRobotFile LoadFile(string key)
    {
        try
        {
            string path = IdentityCache.FileIn(key, RobotsFile);
            if (!File.Exists(path))
                return null;
            var file = JsonUtility.FromJson<LocalRobotFile>(File.ReadAllText(path));
            if (file == null)
                return null;
            file.robots ??= new List<LocalRobot>();
            return file;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RobotStore] Cache illisible ({key}) : {e.Message}");
            return null;
        }
    }

    private static void SaveFile(string key, LocalRobotFile file)
    {
        if (file == null || string.IsNullOrEmpty(key))
            return;
        try
        {
            Directory.CreateDirectory(IdentityCache.FolderOf(key));
            File.WriteAllText(IdentityCache.FileIn(key, RobotsFile), JsonUtility.ToJson(file, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RobotStore] Ecriture du cache impossible ({key}) : {e.Message}");
        }
    }

    private static LocalRobot FindLocal(string clientId, string recordId)
    {
        if (!string.IsNullOrEmpty(clientId))
        {
            foreach (var r in cache.robots)
                if (r.clientId == clientId) return r;
        }
        if (!string.IsNullOrEmpty(recordId))
        {
            foreach (var r in cache.robots)
                if (r.id == recordId) return r;
        }
        return null;
    }

    private static void Apply(LocalRobot local, RobotRecord server)
    {
        local.id = server.id;
        local.clientId = string.IsNullOrEmpty(server.clientId) ? (local.clientId ?? server.id) : server.clientId;
        local.name = server.name;
        local.slot = server.slot;
        local.data = server.data ?? new RobotBlueprint();
        local.stats = server.stats ?? new RobotStats();
        local.editedAt = string.IsNullOrEmpty(server.editedAt) ? local.editedAt : server.editedAt;
        local.deletedAt = null;
        local.dirty = false;
    }

    private static RobotRecord ToRecord(LocalRobot local) => new()
    {
        id = local.id,
        owner = SessionService.Identity?.userId,
        name = local.name,
        slot = local.slot,
        data = local.data,
        stats = local.stats,
        clientId = local.clientId,
        editedAt = local.editedAt,
    };

    private static string KeyOf(string clientId, string id) => string.IsNullOrEmpty(clientId) ? (id ?? "") : clientId;

    // > 0 : a plus recent ; < 0 : b plus recent ; 0 : egalite ou dates absentes
    private static int CompareDates(string a, string b)
    {
        bool okA = DateTime.TryParse(a, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var da);
        bool okB = DateTime.TryParse(b, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var db);
        if (!okA && !okB) return 0;
        if (!okA) return -1;
        if (!okB) return 1;
        return da.CompareTo(db);
    }
}
