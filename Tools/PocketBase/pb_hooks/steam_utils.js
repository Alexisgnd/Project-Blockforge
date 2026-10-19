// =========================================================
// BLOCKFORGE — fonctions partagees des routes Steam
// Module charge par `require(`${__hooks}/steam_utils.js`)` A
// L'INTERIEUR de chaque handler de steam_auth.pb.js : PocketBase
// execute chaque handler dans un contexte isole, qui ne voit pas
// les fonctions declarees au niveau du fichier .pb.js.
// (Extension .js et non .pb.js : ce fichier n'est pas un hook,
// PocketBase ne l'execute pas tout seul.)
// =========================================================

const STEAM_APP_ID = "1926750";
const STEAM_IDENTITY = "blockforge-backend"; // = SteamIdentityService.WebApiIdentity (Unity)
const STEAM_TIMEOUT_SECONDS = 5;

// Verifie un ticket hexadecimal aupres de Valve ; renvoie les params Steam ou leve une ApiError.
function verifySteamTicket(ticketHex) {
    if (typeof ticketHex !== "string" || !/^[0-9a-fA-F]{20,}$/.test(ticketHex)) {
        throw new BadRequestError("Missing or malformed Steam ticket.");
    }

    const key = $os.getenv("STEAM_WEB_API_KEY");
    if (!key) {
        console.error("[steam] STEAM_WEB_API_KEY is not set on the server.");
        throw new ApiError(503, "Steam sign-in isn't configured on this server.");
    }

    const url = "https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/" +
        "?key=" + encodeURIComponent(key) +
        "&appid=" + STEAM_APP_ID +
        "&ticket=" + encodeURIComponent(ticketHex) +
        "&identity=" + encodeURIComponent(STEAM_IDENTITY);

    let res;
    try {
        res = $http.send({ url: url, method: "GET", timeout: STEAM_TIMEOUT_SECONDS });
    } catch (err) {
        console.error("[steam] Valve unreachable: " + err);
        throw new ApiError(502, "Steam can't be reached right now. Try again in a moment.");
    }

    if (res.statusCode !== 200) {
        // 403 = cle Web API invalide ou pas rattachee a l'app ; autre = incident Steam
        console.error("[steam] AuthenticateUserTicket HTTP " + res.statusCode);
        throw new ApiError(502, "Steam rejected the verification request (" + res.statusCode + ").");
    }

    const params = res.json && res.json.response && res.json.response.params;
    if (!params || params.result !== "OK" || !params.steamid) {
        const error = res.json && res.json.response && res.json.response.error;
        console.warn("[steam] ticket refused: " + JSON.stringify(error || res.json));
        throw new UnauthorizedError("Steam didn't accept this sign-in ticket. Restart the game and try again.");
    }

    if (params.vacbanned === true || params.publisherbanned === true) {
        throw new ForbiddenError("This Steam account can't sign in to BlockForge.");
    }

    return params; // { result, steamid, ownersteamid, vacbanned, publisherbanned }
}

function sanitizeName(value, fallback) {
    if (typeof value !== "string") return fallback;
    const trimmed = value.trim().slice(0, 64);
    return trimmed.length > 0 ? trimmed : fallback;
}

function sanitizeHash(value) {
    return typeof value === "string" && /^[0-9a-fA-F]{0,64}$/.test(value) ? value : "";
}

function findUserBySteamId(steamId) {
    try {
        return $app.findFirstRecordByData("users", "steamId", steamId);
    } catch (_) {
        return null;
    }
}

module.exports = {
    verifySteamTicket,
    sanitizeName,
    sanitizeHash,
    findUserBySteamId,
};
