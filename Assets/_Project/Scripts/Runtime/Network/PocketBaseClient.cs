using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// =========================================================
// CLIENT POCKETBASE (REST via UnityWebRequest)
// Deux portes d'entree sur la collection "users" :
// - email / mot de passe (auth-with-password, compte standard) ;
// - ticket Steam verifie par le serveur (route custom
//   /api/blockforge/steam-auth, voir Tools/PocketBase/pb_hooks).
// Les deux renvoient le meme token : le reste du jeu ne fait
// pas la difference. Les robots passent par RobotStore (cache
// local + synchro), qui appelle les methodes ci-dessous.
// =========================================================

[Serializable]
public class PocketBaseUser
{
    public string id;
    public string email;
    public string username;
    public string name;
    public bool verified;
    public string created;
    public string updated;

    // Champs ajoutes pour Steam (vides pour un compte standard)
    public string steamId;
    public string displayName;
    public string avatarHash;
}

[Serializable]
public class PocketBaseAuthResponse
{
    public string token;
    public PocketBaseUser record;
}

[Serializable]
public class RobotRecord
{
    public string id;
    public string owner;
    public string name;
    public int slot;
    public RobotBlueprint data;
    public RobotStats stats;

    // Identite stable du robot cote client (GUID) : permet de rapprocher un robot
    // cree hors ligne de son record serveur. Les anciens records n'en ont pas.
    public string clientId;
    // Derniere modification posee par le client (ISO 8601 UTC, format "o") :
    // c'est elle qui departage une synchro, pas `updated` du serveur.
    public string editedAt;
}

public class PocketBaseException : Exception
{
    public long StatusCode { get; }

    // 0 = erreur reseau (serveur injoignable, DNS, TLS, delai) ; sinon code HTTP
    public bool IsNetworkError => StatusCode == 0;

    public PocketBaseException(long statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class PocketBaseClient
{
    // Session partagee entre les scenes (assignee par BootAuthController)
    public static PocketBaseClient Instance { get; set; }

    private readonly string baseUrl;

    public string BaseUrl => baseUrl;
    public string Token { get; private set; }
    public PocketBaseUser User { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public PocketBaseClient(string baseUrl)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
    }

    // =========================================================
    // AUTH — compte standard
    // =========================================================

    [Serializable]
    private class LoginRequest
    {
        public string identity;
        public string password;
    }

    public async Awaitable<PocketBaseAuthResponse> LoginAsync(string identity, string password)
    {
        string body = JsonUtility.ToJson(new LoginRequest { identity = identity, password = password });
        string json = await SendAsync("POST", "/api/collections/users/auth-with-password", body, authorized: false);

        var auth = JsonUtility.FromJson<PocketBaseAuthResponse>(json);
        SaveAuth(auth);
        return auth;
    }

    [Serializable]
    private class RegisterRequest
    {
        public string email;
        public string password;
        public string passwordConfirm;
        // Les champs inconnus du schéma sont ignorés par PocketBase :
        // on envoie les trois pour couvrir "username" (champ custom), "name" (champ
        // par défaut) et "displayName" (pseudo affiche, commun aux comptes Steam).
        public string username;
        public string name;
        public string displayName;
    }

    public async Awaitable<PocketBaseUser> RegisterAsync(string email, string username, string password, string passwordConfirm)
    {
        string body = JsonUtility.ToJson(new RegisterRequest
        {
            email = email,
            password = password,
            passwordConfirm = passwordConfirm,
            username = username,
            name = username,
            displayName = username,
        });

        string json = await SendAsync("POST", "/api/collections/users/records", body, authorized: false);
        return JsonUtility.FromJson<PocketBaseUser>(json);
    }

    [Serializable]
    private class PasswordResetRequest
    {
        public string email;
    }

    public async Awaitable RequestPasswordResetAsync(string email)
    {
        string body = JsonUtility.ToJson(new PasswordResetRequest { email = email });
        await SendAsync("POST", "/api/collections/users/request-password-reset", body, authorized: false);
    }

    // Renouvelle le token courant (reconnexion d'un compte standard apres une coupure)
    public async Awaitable<PocketBaseAuthResponse> AuthRefreshAsync()
    {
        if (!IsAuthenticated)
            throw new PocketBaseException(401, "No session to refresh.");
        string json = await SendAsync("POST", "/api/collections/users/auth-refresh", null, authorized: true);
        var auth = JsonUtility.FromJson<PocketBaseAuthResponse>(json);
        SaveAuth(auth);
        return auth;
    }

    // =========================================================
    // AUTH — Steam (route custom, ticket verifie aupres de Valve)
    // =========================================================

    [Serializable]
    private class SteamAuthRequest
    {
        public string ticket;      // ticket GetAuthTicketForWebApi en hexadecimal
        public string displayName; // pseudo Steam (affichage)
        public string avatarHash;  // empreinte de l'avatar (affichage)
    }

    public async Awaitable<PocketBaseAuthResponse> LoginWithSteamAsync(string ticketHex, string displayName, string avatarHash)
    {
        string body = JsonUtility.ToJson(new SteamAuthRequest
        {
            ticket = ticketHex,
            displayName = displayName ?? "",
            avatarHash = avatarHash ?? "",
        });
        string json = await SendAsync("POST", "/api/blockforge/steam-auth", body, authorized: false);

        var auth = JsonUtility.FromJson<PocketBaseAuthResponse>(json);
        SaveAuth(auth);
        return auth;
    }

    // Rattache un SteamID au compte connecte (compte standard qui passe a Steam)
    public async Awaitable<PocketBaseUser> LinkSteamAsync(string ticketHex)
    {
        string body = JsonUtility.ToJson(new SteamAuthRequest { ticket = ticketHex, displayName = "", avatarHash = "" });
        string json = await SendAsync("POST", "/api/blockforge/steam-link", body, authorized: true);
        var user = JsonUtility.FromJson<PocketBaseUser>(json);
        if (user != null && !string.IsNullOrEmpty(user.id))
            User = user;
        return user;
    }

    public void Logout()
    {
        Token = null;
        User = null;
    }

    // =========================================================
    // ROBOTS (collection "robots")
    // =========================================================

    [Serializable]
    private class RobotListResponse
    {
        public RobotRecord[] items;
    }

    [Serializable]
    private class RobotWriteRequest
    {
        public string owner;
        public string name;
        public int slot;
        public RobotBlueprint data;
        public RobotStats stats;
        public string clientId;
        public string editedAt;
    }

    public async Awaitable<RobotRecord[]> ListRobotsAsync()
    {
        string json = await SendAsync("GET", "/api/collections/robots/records?perPage=50&sort=slot", null, authorized: true);
        return JsonUtility.FromJson<RobotListResponse>(json).items ?? Array.Empty<RobotRecord>();
    }

    // Cree le robot si recordId est vide, le met a jour sinon.
    public async Awaitable<RobotRecord> SaveRobotAsync(string recordId, string clientId, string name, int slot,
                                                       RobotBlueprint blueprint, RobotStats stats, string editedAt)
    {
        string body = JsonUtility.ToJson(new RobotWriteRequest
        {
            owner = User?.id,
            name = name,
            slot = slot,
            data = blueprint,
            stats = stats,
            clientId = clientId ?? "",
            editedAt = editedAt ?? "",
        });

        string json = string.IsNullOrEmpty(recordId)
            ? await SendAsync("POST", "/api/collections/robots/records", body, authorized: true)
            : await SendAsync("PATCH", $"/api/collections/robots/records/{recordId}", body, authorized: true);

        return JsonUtility.FromJson<RobotRecord>(json);
    }

    public async Awaitable DeleteRobotAsync(string recordId)
    {
        await SendAsync("DELETE", $"/api/collections/robots/records/{recordId}", null, authorized: true);
    }

    // Session en mémoire uniquement : le token n'est jamais ecrit sur le disque.
    private void SaveAuth(PocketBaseAuthResponse auth)
    {
        Token = auth.token;
        User = auth.record;
    }

    // =========================================================
    // TRANSPORT HTTP
    // =========================================================

    [Serializable]
    private class ErrorResponse
    {
        public int code;
        public string message;
    }

    private async Awaitable<string> SendAsync(string method, string path, string jsonBody, bool authorized)
    {
        using var request = new UnityWebRequest(baseUrl + path, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 15; // secondes — evite de bloquer l'UI si le serveur ne repond pas

        if (!string.IsNullOrEmpty(jsonBody))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        if (authorized && IsAuthenticated)
            request.SetRequestHeader("Authorization", Token);

        var operation = request.SendWebRequest();
        while (!operation.isDone)
            await Awaitable.NextFrameAsync();

        if (request.result == UnityWebRequest.Result.Success)
            return request.downloadHandler.text;

        // Erreur réseau (serveur injoignable, DNS, TLS...)
        if (request.result != UnityWebRequest.Result.ProtocolError)
            throw new PocketBaseException(0, "Can't reach the server. Check your connection.");

        // Erreur API : on extrait le message renvoyé par PocketBase
        string message = "The request failed.";
        try
        {
            var error = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            if (!string.IsNullOrEmpty(error?.message))
                message = error.message;
        }
        catch
        {
            // Corps non-JSON : on garde le message générique
        }

        throw new PocketBaseException(request.responseCode, message);
    }
}
