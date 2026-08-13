using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// =========================================================
// CLIENT POCKETBASE (REST via UnityWebRequest)
// Auth email/mot de passe sur la collection "users".
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
}

public class PocketBaseException : Exception
{
    public long StatusCode { get; }

    public PocketBaseException(long statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class PocketBaseClient
{
    // Session partagée entre les scènes (assignée par BootAuthController)
    public static PocketBaseClient Instance { get; set; }

    private readonly string baseUrl;

    public string Token { get; private set; }
    public PocketBaseUser User { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public PocketBaseClient(string baseUrl)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
    }

    // =========================================================
    // AUTH
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
        // on envoie les deux pour couvrir "username" (champ custom) et "name" (champ par défaut).
        public string username;
        public string name;
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
    }

    public async Awaitable<RobotRecord[]> ListRobotsAsync()
    {
        string json = await SendAsync("GET", "/api/collections/robots/records?perPage=30&sort=slot", null, authorized: true);
        return JsonUtility.FromJson<RobotListResponse>(json).items ?? Array.Empty<RobotRecord>();
    }

    // Cree le robot si recordId est vide, le met a jour sinon.
    public async Awaitable<RobotRecord> SaveRobotAsync(string recordId, string name, int slot,
                                                       RobotBlueprint blueprint, RobotStats stats)
    {
        string body = JsonUtility.ToJson(new RobotWriteRequest
        {
            owner = User?.id,
            name = name,
            slot = slot,
            data = blueprint,
            stats = stats,
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

    // Session en mémoire uniquement : le joueur se reconnecte à chaque lancement.
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
            throw new PocketBaseException(0, "Serveur injoignable. Vérifie ta connexion.");

        // Erreur API : on extrait le message renvoyé par PocketBase
        string message = "La requête a échoué.";
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
