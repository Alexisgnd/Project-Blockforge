/// <reference path="../pb_data/types.d.ts" />

// =========================================================
// BLOCKFORGE — AUTHENTIFICATION STEAM (hooks PocketBase)
// Deux routes, verifiees aupres de Valve avec la cle Web API
// publisher (variable d'environnement STEAM_WEB_API_KEY, jamais
// dans le depot) :
//   POST /api/blockforge/steam-auth  { ticket, displayName, avatarHash }
//        -> connecte (ou cree) le record `users` du SteamID du
//           ticket et renvoie un token PocketBase standard.
//   POST /api/blockforge/steam-link  { ticket }        (authentifie)
//        -> rattache le SteamID du ticket au compte connecte.
// Le client n'envoie JAMAIS son SteamID comme preuve : seul le
// ticket verifie par Valve fait foi (ISteamUserAuth/
// AuthenticateUserTicket). Le SteamID retenu est celui du
// joueur reel (pas `ownersteamid` du partage familial).
// Les fonctions partagees sont dans steam_utils.js, charge DANS
// chaque handler (contexte isole par handler dans PocketBase).
// Installation : copier steam_auth.pb.js ET steam_utils.js dans
// <pb>/pb_hooks/ et redemarrer PocketBase (voir ../README.md).
// =========================================================

// ---------------------------------------------------------
// POST /api/blockforge/steam-auth
// ---------------------------------------------------------
routerAdd("POST", "/api/blockforge/steam-auth", (e) => {
    const steam = require(`${__hooks}/steam_utils.js`);

    const body = e.requestInfo().body || {};
    const params = steam.verifySteamTicket(body.ticket);
    const steamId = String(params.steamid);
    const displayName = steam.sanitizeName(body.displayName, "Pilot " + steamId.slice(-4));
    const avatarHash = steam.sanitizeHash(body.avatarHash);

    let record = steam.findUserBySteamId(steamId);
    if (!record) {
        const users = $app.findCollectionByNameOrId("users");
        record = new Record(users);
        record.set("steamId", steamId);
        // Compte sans email ni mot de passe connu : utilisable uniquement via Steam
        // tant qu'aucun email n'est rattache (cf. README : « email » non requis).
        record.setRandomPassword();
        record.set("verified", true);
        record.set("name", displayName);
        console.log("[steam] new player " + steamId + " (" + displayName + ")");
    }

    record.set("displayName", displayName);
    if (avatarHash) record.set("avatarHash", avatarHash);
    record.set("lastSeen", new Date().toISOString());
    $app.save(record);

    return $apis.recordAuthResponse(e, record, "steam");
});

// ---------------------------------------------------------
// POST /api/blockforge/steam-link  (compte connecte -> + Steam)
// ---------------------------------------------------------
routerAdd("POST", "/api/blockforge/steam-link", (e) => {
    const steam = require(`${__hooks}/steam_utils.js`);

    const body = e.requestInfo().body || {};
    const params = steam.verifySteamTicket(body.ticket);
    const steamId = String(params.steamid);

    const current = e.auth;
    if (!current) {
        throw new UnauthorizedError("Sign in first.");
    }

    const existing = steam.findUserBySteamId(steamId);
    if (existing && existing.id !== current.id) {
        throw new ApiError(409, "This Steam account is already linked to another BlockForge account.");
    }

    current.set("steamId", steamId);
    current.set("lastSeen", new Date().toISOString());
    $app.save(current);

    return e.json(200, {
        id: current.id,
        email: current.get("email"),
        name: current.get("name"),
        displayName: current.get("displayName"),
        steamId: steamId,
        avatarHash: current.get("avatarHash"),
    });
}, $apis.requireAuth("users"));
