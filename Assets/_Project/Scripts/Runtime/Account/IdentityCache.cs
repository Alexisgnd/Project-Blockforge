using System;
using System.IO;
using UnityEngine;

// =========================================================
// CACHE LOCAL DES IDENTITES
// persistentDataPath/BlockForge/
//   last_identity.json        -> localKey du dernier compte connecte
//   <localKey>/identity.json  -> PlayerIdentity
//   <localKey>/avatar.png     -> avatar Steam (affiche hors ligne)
//   <localKey>/robots.json    -> robots (voir RobotStore)
// Sert d'identite hors ligne : un compte standard ne peut pas
// s'authentifier sans reseau, on reprend donc le dernier compte
// vu sur cette machine. Le token PocketBase n'est jamais ecrit.
// =========================================================

public static class IdentityCache
{
    private const string RootFolder = "BlockForge";
    private const string LastFile = "last_identity.json";
    private const string IdentityFile = "identity.json";
    private const string AvatarFile = "avatar.png";

    [Serializable]
    private class LastIdentity
    {
        public string localKey;
    }

    public static string RootPath => Path.Combine(Application.persistentDataPath, RootFolder);
    public static string FolderOf(string localKey) => Path.Combine(RootPath, Sanitize(localKey));
    public static string FileIn(string localKey, string fileName) => Path.Combine(FolderOf(localKey), fileName);

    public static void Save(PlayerIdentity identity, bool markAsLast = true)
    {
        if (identity == null)
            return;
        try
        {
            Directory.CreateDirectory(FolderOf(identity.LocalKey));
            File.WriteAllText(FileIn(identity.LocalKey, IdentityFile), JsonUtility.ToJson(identity, true));
            if (markAsLast)
            {
                Directory.CreateDirectory(RootPath);
                File.WriteAllText(Path.Combine(RootPath, LastFile),
                                  JsonUtility.ToJson(new LastIdentity { localKey = identity.LocalKey }));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IdentityCache] Ecriture impossible : {e.Message}");
        }
    }

    public static PlayerIdentity Load(string localKey)
    {
        try
        {
            string path = FileIn(localKey, IdentityFile);
            if (!File.Exists(path))
                return null;
            var identity = JsonUtility.FromJson<PlayerIdentity>(File.ReadAllText(path));
            return identity != null && identity.method != AuthMethod.None ? identity : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IdentityCache] Lecture impossible ({localKey}) : {e.Message}");
            return null;
        }
    }

    // Dernier compte connecte sur cette machine (null si aucun)
    public static PlayerIdentity LoadLast()
    {
        try
        {
            string path = Path.Combine(RootPath, LastFile);
            if (!File.Exists(path))
                return null;
            var last = JsonUtility.FromJson<LastIdentity>(File.ReadAllText(path));
            return string.IsNullOrEmpty(last?.localKey) ? null : Load(last.localKey);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IdentityCache] Lecture du dernier compte impossible : {e.Message}");
            return null;
        }
    }

    public static void ForgetLast()
    {
        try
        {
            string path = Path.Combine(RootPath, LastFile);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IdentityCache] Oubli du dernier compte impossible : {e.Message}");
        }
    }

    // ---------- Avatar (PNG) ----------

    public static void SaveAvatar(string localKey, Texture2D avatar)
    {
        if (avatar == null)
            return;
        try
        {
            Directory.CreateDirectory(FolderOf(localKey));
            File.WriteAllBytes(FileIn(localKey, AvatarFile), avatar.EncodeToPNG());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IdentityCache] Ecriture de l'avatar impossible : {e.Message}");
        }
    }

    public static Texture2D LoadAvatar(string localKey)
    {
        try
        {
            string path = FileIn(localKey, AvatarFile);
            if (!File.Exists(path))
                return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "CachedAvatar" };
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IdentityCache] Lecture de l'avatar impossible : {e.Message}");
            return null;
        }
    }

    // Les cles sont des identifiants sans caractere special, mais on reste prudent
    private static string Sanitize(string key)
    {
        if (string.IsNullOrEmpty(key))
            return PlayerIdentity.GuestKey;
        var chars = key.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                chars[i] = '_';
        }
        return new string(chars);
    }
}
