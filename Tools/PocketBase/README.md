# PocketBase — serveur BlockForge

Backend du jeu : comptes (`users`) et robots (`robots`). Ce dossier contient ce
qui doit être installé sur le serveur (VPS) en plus de l'exécutable PocketBase :
les hooks JavaScript de `pb_hooks/`.

Côté Unity, tout passe par `Scripts/Runtime/Network/PocketBaseClient.cs` et
`Scripts/Runtime/Account/` (session, cache local, synchro). La conception complète
est dans la note de projet *claude/steam-integration.md*.

## Installation des hooks

```bash
# sur le VPS, à côté de l'exécutable pocketbase
mkdir -p pb_hooks
cp steam_auth.pb.js steam_utils.js pb_hooks/   # les deux fichiers
export STEAM_WEB_API_KEY="<clé Web API publisher Steamworks>"
./pocketbase serve   # ou redémarrer le service systemd
```

`STEAM_WEB_API_KEY` se crée dans Steamworks → *Comptes et permissions* →
*Gérer les groupes* → groupe éditeur → *Créer une clé Web API* (clé **publisher**,
rattachée à l'app 1926750). Elle ne va **jamais** dans le dépôt ni dans le jeu :
le client Unity n'envoie qu'un ticket, c'est le serveur qui interroge Valve.

Avec systemd, poser la variable dans le service (`Environment=STEAM_WEB_API_KEY=…`)
ou dans un fichier `EnvironmentFile=` lisible par le seul utilisateur PocketBase.

## Schéma des collections

### `users` (collection auth existante)

| Champ         | Type   | Options                                   | Rôle |
|---------------|--------|-------------------------------------------|------|
| `email`       | email  | **non requis** (comptes Steam sans email) | compte standard |
| `password`    | —      | auth par mot de passe **activée**         | compte standard |
| `name`        | text   | existant                                  | pseudo historique |
| `displayName` | text   | max 64                                    | pseudo affiché (Steam : synchronisé à chaque connexion) |
| `steamId`     | text   | **unique**, indexé, optionnel             | SteamID64 du joueur réel |
| `avatarHash`  | text   | max 64                                    | empreinte de l'avatar Steam |
| `lastSeen`    | date   |                                           | dernière connexion |

Dans les options de la collection : *Identity/Password auth* reste activé
(comptes standard). Décocher *Required* sur `email` : un compte créé via Steam n'en
a pas tant que le joueur n'en rattache pas un.

Index : `CREATE UNIQUE INDEX idx_users_steamId ON users (steamId) WHERE steamId != ''`.

Règles d'API de `users` : lecture `id = @request.auth.id` ; création
`@request.body.steamId:isset = false` ; mise à jour
`id = @request.auth.id && @request.body.steamId:isset = false`. Un joueur ne peut
donc **jamais** écrire `steamId` lui-même (sinon il pourrait réserver le SteamID
de quelqu'un d'autre et récupérer son compte) : seules les routes du hook, qui
passent par `$app.save`, le posent après vérification du ticket.

### `robots` (collection existante)

| Champ      | Type   | Options                     | Rôle |
|------------|--------|-----------------------------|------|
| `owner`    | relation `users` | existant          | propriétaire |
| `name`, `slot`, `data`, `stats` | existants | | blueprint et stats |
| `clientId` | text   | max 64, indexé              | GUID stable généré par le jeu : rapproche un robot créé hors ligne de son record |
| `editedAt` | text   | max 40                      | dernière modification posée par le **client** (ISO 8601 UTC, format `o`) — c'est elle qui départage une synchro, pas `updated` |

Index : `CREATE UNIQUE INDEX idx_robots_owner_client ON robots (owner, clientId) WHERE clientId != ''`
(un même GUID ne peut exister qu'une fois par joueur ; le `WHERE` laisse passer les
anciens records sans GUID), en plus de l'index existant `(owner, slot)`. Les anciens records sans
`clientId` sont rattrapés par le jeu (clé de repli = `id`).

Règles d'API (inchangées) : `owner = @request.auth.id` en liste/lecture/écriture/
suppression ; à la création, `@request.body.owner = @request.auth.id`.

## Routes ajoutées par `steam_auth.pb.js`

| Route | Auth | Corps | Réponse |
|-------|------|-------|---------|
| `POST /api/blockforge/steam-auth` | aucune | `{ ticket (hex), displayName, avatarHash }` | réponse d'auth PocketBase standard (`token`, `record`) |
| `POST /api/blockforge/steam-link` | token `users` | `{ ticket (hex) }` | record mis à jour (`steamId` rattaché) ; 409 si ce SteamID appartient déjà à un autre compte |

Vérification : `GET https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/`
avec `key`, `appid=1926750`, `ticket`, `identity=blockforge-backend` (la même chaîne
que `SteamIdentityService.WebApiIdentity` côté Unity). Réponses :

- `result != OK` → 401 (ticket expiré, déjà utilisé ou émis pour une autre identité) ;
- `vacbanned` / `publisherbanned` → 403 ;
- Valve injoignable → 502 ; clé absente → 503.

## Test rapide

1. Lancer le jeu depuis l'éditeur avec Steam ouvert (`steam_appid.txt` = 1926750
   à la racine du projet ; le compte Steam doit posséder l'app).
2. Le panneau *Signing in with Steam* doit passer au menu en moins de 2 s.
   Sinon, lire les logs PocketBase (`[steam] …`) : clé invalide → HTTP 403 chez
   Valve ; identité différente → `result` ≠ OK.
3. Dans le Dashboard, le record `users` du joueur a `steamId` renseigné.
