# Project Blockforge

Jeu de construction et de combat de robots par blocs, dans l'esprit de Robocraft :
on assemble son robot dans un garage à partir d'un catalogue de pièces, puis on
l'emmène au combat.

| | |
|---|---|
| Moteur | **Unity 6 (6000.5.6f1) + HDRP** |
| Backend | **PocketBase** sur VPS (comptes, sauvegarde des robots) |
| Plateforme | **Steam** (App ID 1926750) via Steamworks.NET, compte Steam optionnel |
| Sources 3D | **Blender 5.x** (blocs en FBX), décors en GLB (glTFast) |
| Sortie visée | **Accès anticipé gratuit, décembre 2026** : combat contre des bots sur une carte, pas de multijoueur |

---

## Sommaire

1. [État du projet](#état-du-projet)
2. [Steam et comptes](#steam-et-comptes)
3. [Démarrer](#démarrer)
4. [Structure du dépôt](#structure-du-dépôt)
5. [Catalogue des blocs](#catalogue-des-blocs)
6. [Pipeline 3D et décors](#pipeline-3d-et-décors)
7. [Contribuer](#contribuer)
   - [Code](#code-c--unity)
   - [3D](#3d-modélisation)
   - [Musique et son](#musique-et-son)
   - [Game design, UI, tests, docs](#game-design-ui-tests-docs)
   - [Workflow Git et commits](#workflow-git-et-commits)
8. [Licence](#licence)

---

## État du projet

*Mis à jour le 19 octobre 2026.*

| Domaine | État |
|---|---|
| **Compte Steam** : connexion automatique, pseudo et avatar, ticket vérifié par PocketBase | fonctionnel, validé de bout en bout |
| **Compte standard** (email / mot de passe) quand Steam n'est pas lancé | fonctionnel |
| **Mode hors ligne** : robots en cache local, synchro à la reconnexion, bouton *Reconnect* | fonctionnel |
| **Page Steam** (nom, textes EN/FR, genres, tags, accès anticipé, questionnaire de contenu) | publiée côté Steamworks ; images du magasin et build à faire |
| Menu principal : hangar 3D, 3 baies de robots, écrans Play / Build, header (TopBar) | fonctionnel |
| Écrans Tech tree, Workshop, Store | interface en place, données factices (pas encore d'économie) |
| Garage : grille, pince, inventaire, recherche, miroir, spray de peinture, historique | fonctionnel |
| Règle d'armement : 3 types d'armes / modules max par robot | fonctionnel (dialogue de remplacement dans le garage) |
| Garage V2 (vaisseau Mothership, baie 31 × 31 × 31) | généré par script, à valider en jeu |
| Combat : visée, tir, projectiles, dégâts par bloc, énergie, bots ennemis, arène | prototype jouable (scène de test) |
| Match : intro, HUD, minicarte, pause, fin de partie, résultats | prototype |
| Catalogue : 82 blocs, 17 familles, tous modélisés | fait (modèles procéduraux à améliorer) |
| Audio | gestionnaire de musique en place ; SFX à produire |
| Carte Mars | scène en place, pas de gameplay |
| Multijoueur, tests automatisés, licence | pas commencé / non définie |

L'interface du jeu est en **anglais** et suit le design system BlockForge
(`DESIGN_SYSTEM.md`, `Scripts/Runtime/UI/{Theme,Widgets,DesignSystem}`).

---

## Steam et comptes

Le jeu se lance **avec ou sans Steam**. Le serveur ne fait jamais confiance au
SteamID envoyé par le client : seul un ticket vérifié auprès de Valve fait foi.

```
Steam lancé ──► ticket Web API ──► PocketBase /api/blockforge/steam-auth ──► Valve (AuthenticateUserTicket)
                                          │
                                          └─► compte users trouvé ou créé par steamId ──► token PocketBase
Pas de Steam ──► écran email / mot de passe (compte standard)
Hors ligne   ──► cache local du dernier compte, jeu contre les bots, synchro au retour
```

| Brique | Rôle |
|---|---|
| `Steam/SteamBootstrap` | `SteamAPI.Init` tolérant (Steam absent = pas d'erreur), callbacks, arrêt |
| `Steam/SteamIdentityService` | SteamID, pseudo, avatar, ticket `GetAuthTicketForWebApi("blockforge-backend")` |
| `Account/SessionService` | états *None / Connecting / Online / Offline / Guest*, connexion Steam ou email, reconnexion |
| `Account/IdentityCache` | dernier compte connu et son avatar, sur le disque |
| `Account/RobotStore` | robots en local d'abord ; synchro : la dernière modification (`editedAt`) gagne, suppressions par pierre tombale, robots invité adoptés dans les baies libres |
| `UI/MainMenu/PlayerChip` | pseudo, avatar rond, pastille d'état, bouton *Reconnect* / *Sign in* |
| `Tools/PocketBase/pb_hooks` | routes `steam-auth` et `steam-link` (+ `steam_utils.js`) ; schéma et installation dans `Tools/PocketBase/README.md` |

Points à connaître :

- **Clé Web API publisher** : variable d'environnement `STEAM_WEB_API_KEY` du
  service PocketBase, jamais dans le dépôt ni dans le jeu.
- **`steam_appid.txt`** (1926750) à la racine sert à l'éditeur ; le compte Steam
  doit posséder l'app.
- Un compte Steam et un compte email restent **séparés**. La route `steam-link`
  permet de les rattacher, l'interface n'existe pas encore.
- Côté Steamworks : jeu gratuit en accès anticipé, développeur et éditeur
  « Swtch (Aka. Alexis GND) », anglais + français, exécutable `BlockForge.exe`.
  Le *Product Name* Unity doit passer à `BlockForge` avant le premier build
  (ce qui déplace aussi le dossier du cache local).

---

## Démarrer

**Prérequis** : Git + **Git LFS** (`git lfs install` *avant* de cloner, sinon les
FBX, PNG et .blend arrivent en pointeurs de 130 octets), Unity Hub avec
**6000.5.6f1**, Blender 5.x seulement pour les sources 3D.

```bash
git lfs install
git clone https://github.com/Alexisgnd/Project-Blockforge.git
```

1. Unity Hub → *Add project from disk*. Le premier import prend plusieurs minutes ;
   les scripts éditeur exécutent ensuite une fois leurs *setups* (relançables via
   **Blockforge > Setup …**).
2. Ouvrir `Assets/_Project/Scenes/Boot.unity` et lancer le Play mode
   (Boot → MainMenu → Garage).
3. Compte : Steam ouvert → connexion automatique ; sinon email / mot de passe
   (`dev_autologin.json` à la racine, ignoré par Git, remplit le formulaire) ;
   sinon *Play offline*.

Le cache local est dans
`%USERPROFILE%/AppData/LocalLow/<société>/<produit>/BlockForge/<compte>/`.
Depuis le garage, **P** lance la scène de test avec le robot en cours,
**ESC** deux fois pour revenir.

---

## Structure du dépôt

```
Project-Blockforge/
├── Assets/_Project/          tout notre contenu
│   ├── Art/                  modèles FBX/GLB, matériaux HDRP, textures, icônes, VFX
│   ├── Audio/                musique, SFX, mixers
│   ├── Data/                 BlockDefinition (un par bloc), modes de jeu
│   ├── Scenes/               Boot, MainMenu, Garage, Garage_V2, Map_MARS, Map_Test
│   ├── Scripts/Runtime/      jeu (asmdef Blockforge.Runtime) : Account, Steam, Network,
│   │                         Garage, Player, Vehicle, Combat, Meta, Audio, UI…
│   ├── Scripts/Editor/       outils et setups (asmdef Blockforge.Editor)
│   └── UI/                   UXML/USS de l'écran d'auth, polices, sprites du design system
├── Assets/ThirdParty/        assets externes, jamais mélangés à _Project
├── Tools/Blender/            sources .blend et scripts de pipeline
├── Tools/Maps/               générateurs Python des cartes de test
├── Tools/PocketBase/         hooks serveur (Steam) et schéma des collections
├── ARCHITECTURE.md           découpage du code et principes
├── DESIGN_SYSTEM.md          direction UI, palette, composants
└── steam_appid.txt
```

Ignorés par Git : `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds-Profiles/`,
`*.blend1`, `.plastic/`.

---

## Catalogue des blocs

Le jeu est *data-driven* : chaque bloc est un asset `BlockDefinition`
(`Data/Blocks/Block_<Famille>_N<tier>_<Nom>.asset`) qui porte stats (CPU, masse,
résistance), empreinte en cases (`footprint`), face d'ancrage (`anchor`), icône et
prefab. La liste canonique est dans `Scripts/Editor/GarageInventorySetup.cs`
(**Blockforge > Resync Block Definitions**).

Règles de pose :

- **Rien ne flotte** : seul le châssis se pose sur le sol de la baie, tout le
  reste s'accroche à une pièce existante (dessus, dessous ou flanc).
- **Centré sur son support** : les cases réservées sont arrondies au nombre impair
  supérieur sur les axes perpendiculaires à l'ancrage ; le modèle garde sa taille.
- **Le robot se soulève sur ses roues** : la pièce la plus basse pose sur le sol.
- Sauvegarde : case visée + orientation (face × 4 + quart de tour).

| Catégorie | Familles (tiers) |
|---|---|
| Châssis | pièces pleines (12), tiges (5) |
| Mouvement | roues (6), chenilles animées (4), pattes d'insecte (2), lames de survol (6), hélices (3), ailerons (7), propulseurs (5) |
| Armes | lasers (6), lanceurs de plasma (6), canons électriques (4), lames de Tesla (3) |
| Défense | distributeurs nano (3), électroplates (8) |
| Spécial | radar animé (1), disque de bouclier (1) |

Empreintes et ancrages détaillés : `GarageInventorySetup.cs`.

---

## Pipeline 3D et décors

**Blocs (Blender → FBX → Unity, automatisé côté Unity)** :

1. `Tools/Blender/<Famille>.blend`, une collection par bloc, racine `Empty` nommée
   comme le FBX.
2. Export d'un FBX par bloc dans `Art/Models/Blocks/<Catégorie>/` (Forward -Z, Up Y,
   **Apply Transform décoché**).
3. Icône 512 × 512 transparente dans `Art/Textures/Icons/Icon_<Nom>.png`.
4. `<Famille>MaterialSetup.cs` recrée les matériaux HDRP/Lit et les remappe.
5. `<Famille>BlockSetup.cs` relie FBX + icône aux `BlockDefinition`.
6. Blocs animés : clips dans le FBX, `AnimatorController` et prefab variant générés.

Conventions : 1 case = 1 m ; l'avant regarde vers **-Y Blender** (+Z Unity) ;
l'import FBX **inverse l'axe X** ; peu de matériaux, nommés explicitement ;
style low-poly (armure blanche, graphite, acier, liserés cyan émissifs).
**Blockforge > Report Block Bounds** vérifie l'ajustement.

**Décors GLB** (`Art/Models/*.glb`, importés par glTFast) :

- `ShipGarage_V2.glb` → **Blockforge > Setup Garage V2 Scene** (baie 31 m,
  empties `BuildBounds_*`, `BuildOrigin`, `PlayerSpawn`).
- `Map_Canyon.glb` (Red Canyon) et `Map_Arena.glb` → **Blockforge > Setup Map Test
  Scene** (colliders générés, décor non jouable sous `BACKGROUND_NON_PLAYABLE`).
- Contrat commun : mètres, Y vertical, sol vers y = 0, pas de collider dans le
  fichier. Générateurs et aperçus : `Tools/Maps/`.

---

## Contribuer

Toute aide est bienvenue, quel que soit le domaine. Le point d'entrée est toujours
une **issue GitHub** : dites ce que vous voulez faire (ou proposez-vous sur une issue
ouverte), on se met d'accord sur le périmètre, puis vous ouvrez une *pull request*
vers `prod`.

Règles communes :

- Ne committez jamais `Library/`, `Temp/`, `Builds*/`, `UserSettings/`, ni de
  fichiers d'identifiants. Le `.gitignore` s'en charge, ne le contournez pas.
- Chaque asset Unity va **avec son `.meta`**. Un asset sans `.meta` (ou l'inverse)
  casse les références de tout le monde.
- Fermez Unity, ou laissez-le refaire son import, avant de committer un gros lot
  d'assets : les `.meta` doivent être générés.
- Une PR = un sujet. Une famille de blocs, une fonctionnalité, un correctif.
- Vos contributions doivent être votre travail original, ou sous une licence
  compatible avec le projet (CC0, CC-BY avec crédit). Indiquez la provenance dans la PR.

### Code (C# / Unity)

- Lisez [ARCHITECTURE.md](ARCHITECTURE.md) : découpage des dossiers, principes
  (data-driven, blueprint ≠ véhicule, ids stables), assembly definitions.
- Le code du jeu va dans `Scripts/Runtime`, les outils dans `Scripts/Editor`
  (jamais l'inverse : l'asmdef Runtime ne peut pas référencer UnityEditor).
- Ajouter un bloc ne demande **aucun code** : un asset `BlockDefinition` + un modèle.
  Ajouter une famille de blocs modélisés = dupliquer une paire
  `<Famille>BlockSetup` / `<Famille>MaterialSetup` existante.
- Outils éditeur : pattern « one-shot » `[InitializeOnLoad]` avec marqueur dans
  `Library/` + entrée de menu **Blockforge > Setup …** pour le relancer.
- Style : C# idiomatique Unity, commentaires en français, un en-tête de bloc
  `// ====` qui explique le rôle du script (voir les scripts existants).
- Pas de test automatisé pour l'instant ; vérifiez en Play mode dans `Boot.unity`
  et décrivez dans la PR ce que vous avez testé.

### 3D (modélisation)

C'est le besoin numéro un du projet. Par ordre de priorité :

1. Améliorer les blocs existants (topologie, détails, textures) : toutes les
   familles du [catalogue](#catalogue-des-blocs) ont déjà un modèle procédural.
2. Décors : garage, hangar, carte Mars.

Livrables attendus pour une famille de blocs :

- le `.blend` source dans `Tools/Blender/<Famille>.blend` ;
- un FBX par bloc et une icône 512×512 par bloc, aux emplacements décrits dans le
  [pipeline 3D](#pipeline-3d-blender--unity) ;
- la liste des matériaux (nom, couleur, metallic, roughness, émissif éventuel) dans
  la PR ou l'issue : le mainteneur ou vous-même écrivez ensuite le script
  `MaterialSetup` correspondant.

Si vous ne voulez pas toucher à Git/Unity, une issue avec un lien vers vos fichiers
(GLB/FBX + rendus) suffit, on s'occupe de l'intégration.

### Musique et son

Il n'y a **aucun audio** dans le jeu pour l'instant : tout est à créer. Ambiance
visée : science-fiction mécanique, garage/hangar industriel, surface de Mars.

Ce dont on a besoin, par ordre de priorité :

1. Boucle musicale du menu principal et du garage.
2. SFX du garage : pose et retrait de bloc, rotation, sélection dans l'inventaire,
   spray de peinture, sauvegarde, erreur de placement.
3. UI : survol / clic / confirmation / retour.
4. Plus tard : moteurs, roues, propulseurs, armes, impacts, ambiance Mars.

Conventions :

- Dossiers : `Assets/_Project/Audio/Music`, `Audio/SFX`, `Audio/Mixers`.
- Nommage : `MUS_<Scène>_<Nom>` (`MUS_Garage_Loop01`), `SFX_<Catégorie>_<Nom>`
  (`SFX_Garage_BlockPlace`), `AMB_<Lieu>_<Nom>`.
- Formats : WAV 48 kHz 16 ou 24 bits pour les SFX (mono pour les sons positionnés
  en 3D, stéréo pour l'UI) ; WAV ou OGG pour la musique. Les fichiers audio passent
  automatiquement en Git LFS.
- Boucles sans clic aux points de bouclage, un peu de marge de niveau (pic vers
  -3 dBFS), pas de mastering « brickwall ».
- Une issue avec un lien d'écoute (SoundCloud, Drive…) avant la PR permet de valider
  la direction sans faire d'aller-retours sur des fichiers lourds.

### Game design, UI, tests, docs

- **Game design** : stats des blocs (`Data/Blocks`), équilibrage des tiers, modes de
  jeu (`Data/GameModes`). Proposez via une issue avec vos tableaux.
- **UI / UX** : le garage et le menu sont en uGUI + TextMesh Pro, l'écran d'auth en
  UI Toolkit. Maquettes bienvenues avant implémentation.
- **Tests** : jouer, casser, rapporter. Une issue de bug = étapes pour reproduire,
  résultat attendu / obtenu, capture ou vidéo, version de Unity.
- **Docs / traduction** : ce README, `ARCHITECTURE.md`, textes du jeu (français
  pour l'instant).

### Workflow Git et commits

1. Créez une branche depuis `prod` : `feat/lames-survol`, `art/plasma-launchers`,
   `audio/garage-sfx`, `fix/inventory-scroll`.
2. Commits au format **[Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/)** :

   ```
   <type>(<scope>): <description à l'impératif, sans majuscule ni point final>

   [corps optionnel : le pourquoi, les choix faits]

   [pied optionnel : BREAKING CHANGE: …, Refs #12]
   ```

   Types : `feat`, `fix`, `refactor`, `perf`, `docs`, `style`, `test`, `build`,
   `ci`, `chore`, `revert`. Un `!` après le type/scope signale une rupture
   (`feat(blocks)!: …`).

   Scopes utilisés : `blocks`, `garage`, `menu`, `boot`, `net`, `ui`, `art`,
   `audio`, `map`, `unity`, `tools`.

   Exemples tirés de l'historique :

   ```
   feat(blocks): add the 7 rudder blocks (Hawk to Bat)
   feat(garage): paint spray v2 with dual palettes and reworked materials
   refactor(blocks): remove the unused legacy BlockDefinition skeleton
   chore(unity): remove HDRP template readme and TextMesh Pro examples
   ```

3. Ouvrez une PR vers `prod` avec : ce que ça change, comment vous l'avez testé,
   des captures pour tout ce qui est visuel ou sonore.
4. Une relecture, puis merge (squash ou rebase, jamais de merge commit).

---

## Licence

Pas encore définie. Le code et les assets restent la propriété de leurs auteurs ;
contactez le mainteneur avant toute réutilisation hors du projet. Une licence sera
choisie avant la sortie en accès anticipé.
