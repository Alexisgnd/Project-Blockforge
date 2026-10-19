# Architecture — Project Blockforge

Jeu de construction/combat de robots par blocs (type Robocraft), Unity 6 + HDRP.
Ce document décrit le découpage du code et les principes à respecter. Pour
l'installation, le catalogue des blocs, le pipeline 3D et le guide de
contribution, voir le [README](README.md).

## Arborescence

```
Assets/
├── _Project/            ← TOUT notre contenu (le préfixe _ le garde en tête de liste)
│   ├── Art/             Modèles 3D (Models/Blocks/<Catégorie> en FBX, décors GLB à la
│   │                    racine de Models), matériaux, textures, icônes, shaders, VFX
│   ├── Audio/           Musique, SFX, AudioMixers
│   ├── Data/            ScriptableObjects : blocs (Data/Blocks), robots, modes de jeu
│   ├── Prefabs/         Blocs, véhicules, projectiles, environnement, UI
│   ├── Resources/       Ressources chargées par nom (matériau placeholder des blocs)
│   ├── Scenes/          Boot, MainMenu, Garage, Garage_V2, Map_MARS, Map_Test
│   ├── Scripts/
│   │   ├── Runtime/     Code du jeu (asmdef : Blockforge.Runtime)
│   │   └── Editor/      Outils éditeur (asmdef : Blockforge.Editor)
│   └── UI/              UXML/USS (écran d'auth), sprites et polices d'interface
├── Settings/            Config HDRP + InputSystem_Actions (venant du template)
├── TextMesh Pro/        Essentiels TMP (SDFFunctions.hlsl patché pour le ray tracing)
└── ThirdParty/          Assets du store et plugins — jamais mélangés à notre contenu
Tools/
├── Blender/             Sources .blend de tous les modèles + scripts de pipeline
├── Maps/                Générateurs Python des cartes de test (arène, canyon) + aperçus
└── PocketBase/          Hooks JS du serveur (auth Steam) + schéma des collections
steam_appid.txt          App ID Steam (1926750) lu par Steamworks.NET dans l'éditeur
```

**Règle d'or** : rien ne se range à la racine de `Assets/`. Notre contenu va dans
`_Project/`, tout ce qui vient de l'extérieur va dans `ThirdParty/`.

## Découpage du code (`Scripts/Runtime/`)

| Dossier     | Responsabilité                                                          |
|-------------|-------------------------------------------------------------------------|
| `Data/`     | ScriptableObjects : `BlockDefinition`, `GameModeDefinition` ; géométrie d'empreinte (`BlockFootprint` : cases réservées `OccupancySize` arrondies à l'impair hors axe d'ancrage, boîte du modèle `LocalBox` centrée sur la case visée, face d'ancrage, rotation, réflexion miroir) ; état de session (`RobotSession` : robot en cours, scène garage de retour, plan du miroir ; `RobotBlueprint` : baie max 31×31×31, plus `GridUnderfloor` cases sous le sol pour les roues) |
| `Building/` | Pince de pose (`PlierGarageController`) : inertie, orientation de l'outil |
| `Garage/`   | Grille de construction (`GarageBuildController` : nombre de cases mesuré sur les lignes du vaisseau, 14×14 sur l'ancien, 31×31 sur le Mothership ; un bloc occupe toutes les cases de son empreinte orientée ; hors châssis, la face d'ancrage se plaque contre la face visée du support et le bloc doit toucher une pièce posée ; le robot est relevé pour que roues et pattes posent sur le sol ; mode miroir M = jumeau par le plan de la ligne centrale, image miroir vraie), ghost, blocs posés, fabrique d'aperçus (`BlockPreviewFactory`) |
| `UI/Garage/LoadoutReplaceDialog` | Dialogue du DS (scrim + Dialog + PartCard) ouvert par `GarageBuildController` quand la pose ajouterait un 4e type : le type choisi est retire en entier puis le bloc est pose (une etape d'historique, A annule) ; Echap annule |
| `Player/`   | Contrôleur joueur du garage, switch pince/spray, molette de couleur du spray |
| `UI/`       | `Garage/` (inventaire, stats, toolbar, sauvegarde T, test P), `MainMenu/` (header à sections Home / Play / Build / Tech tree / Workshop / Store avec crédits et puce joueur — `PlayerChip` : pseudo, avatar Steam rond, pastille d'état en ligne / hors ligne / invité, clic = reconnexion ; slots de robots du joueur chargés depuis PocketBase, partagés entre Accueil et Construire ; écran Play : `PlayMenuController` liste des modes + fiche du mode sélectionné + baie « Your robot », `GameModeCard` ligne de mode, `HoverReason` raison d'une action indisponible au survol), `Match/` (`MatchIntro` : transition « Play with bots » → écran Versus 5 v 5 bleu / rouge → chargement de la scène, hôte DontDestroyOnLoad ; `HudPolygon` : Graphic uGUI à coins coupés / bords inclinés / dégradé ; `RobotTestHUD` : réticule + jauge d'énergie, santé, état du robot avec heatmap des blocs, slots d'armes, `MiniMap` ; `MatchPauseMenu` : Échap, reprendre / options / retour au garage ou « Leave match » + rappel des touches), `BootAuthController` |
| `Network/`  | Client PocketBase (`PocketBaseClient` : auth email / mot de passe, auth Steam par ticket via la route custom `/api/blockforge/steam-auth`, robots) et garde de session (`AuthGuard` : renvoie vers Boot seulement si aucune identité ; `RequireOnline` pour les fonctions réservées au mode en ligne) |
| `Steam/`    | `SteamBootstrap` (Steamworks.NET : `Init` tolérant — Steam est **optionnel** —, `RunCallbacks`, `Shutdown`, `IsAvailable` / `IsLoggedOn`, App ID 1926750) ; `SteamIdentityService` (SteamID64, pseudo, avatar 184 px en `Texture2D`/`Sprite`, ticket `GetAuthTicketForWebApi` en hexadécimal). Tout est sous `#if BLOCKFORGE_STEAM` (versionDefine de l'asmdef : défini quand le package `com.rlabrecque.steamworks.net` est présent) |
| `Account/`  | `SessionService` (machine d'états None / Connecting / Online / Offline / Guest ; chemin Steam si le client tourne, sinon compte standard ; `PreferStandardAccount` ; reconnexion ; `GoOffline` sur erreur réseau), `PlayerIdentity` (méthode, userId, pseudo, steamId, `LocalKey` du dossier de cache), `IdentityCache` (`persistentDataPath/BlockForge/<LocalKey>/identity.json`, `avatar.png`, dernier compte connu), `RobotStore` (robots **local d'abord** : `robots.json`, `clientId` GUID + `editedAt` client, pierres tombales, synchro à la reconnexion — dernière modification gagne, serveur à égalité —, adoption des robots Invité dans les baies libres). Les menus et le garage passent par `RobotStore`, jamais par `PocketBaseClient` directement |
| `Vehicle/`  | Robot hors du garage : `RobotAssembler` (blueprint → GameObject, visuel + `BoxCollider` par bloc, pivot au centre de l'empreinte), `RobotTestSpawner` (spawn au Start, robot par défaut si aucun blueprint), `RobotTestDriver` (conduite char ZQSD sur Rigidbody piloté en vitesse), `RobotLoadoutState` (slots d'armes/modules du match, touches 1-2-3, `SelectedChanged` pour le HUD et le futur tir), `RobotFollowCamera` (caméra de match façon Robocraft : œil juste au-dessus et derrière le robot, vise le réticule, ne traverse ni sol ni murs), `RobotGroundFX` (poussière des roues/chenilles/pattes : roulage, dérapage, accélération ; shader `Blockforge/DustParticle`), `RobotSpawnSequence` (apparition : assemblage holographique bloc par bloc en lévitation, matérialisation, chute, puis contrôles rendus), `RobotRecovery` (touche R : redressement façon Robocraft — lévitation, remise à plat, chute, anneaux et panache de particules, recharge ; seulement au sol), `HangarRobotShowcase` (robot sélectionné assemblé en vitrine sur la plateforme du menu d'accueil, rotation + flottement, sans physique). `VehicleBlueprint` est l'ancien modèle de design, inutilisé : ne jamais importer `Blockforge.Vehicle` |
| `Data/` (chargement) | `RobotLoadout` : regle des armes et modules — au plus **3 types** par robot (un type = un bloc du catalogue, quantite libre) ; arme = categorie Armes, module = categorie Special + famille Nano dispenser ; l'ordre d'apparition donne les slots 1-2-3 du match |
| `Combat/`   | `MovingTarget` : cibles mobiles du terrain d'essai (rail aller-retour, tourniquet/orbite, pop-up qui bascule ; pièce mobile en Rigidbody kinematique, `Knock()` pour coucher un pop-up sur impact). Dégâts/destruction à venir. `WeaponAimController` : les armes du slot sélectionné (`RobotLoadoutState`) deviennent des tourelles (pivot créé au montage sous la tête/l'axe d'élévation) et pointent vers le réticule ; les autres reviennent au repos. `RobotEnergy` : réserve d'énergie des armes (consommée par les tirs, remonte après un délai), jauge en deux moitiés autour du réticule. `WeaponFireController` : clic gauche, tirs des armes du slot actif à tour de rôle — laser (rayon instantané), plasma (`PlasmaProjectile`, boule à gravité réduite + dégâts de zone), rail (rayon lourd + recul), Tesla (arcs et dégâts de contact) ; traits `LineRenderer` (shader hologramme) et particules additives ; une cible touchée (`ShootableTarget`, posé au lancement sur tous les disques de la map) est détruite et réapparaît après 8 s, un robot touché perd des PV (`RobotHealth`). `RobotHealth` : PV bloc par bloc (impact → bloc le plus proche, zone pour le plasma), bloc détruit invisible et inerte (roue détruite = appui en moins), régénération après 6 s sans dégâts, robot détruit sous 15 % → `Destroyed` (le joueur réapparaît au spawn, l'ennemi dans la zone). `CombatArena` + `EnemyRobotAI` : zone rouge au début du Combat Yard avec un robot ennemi (blueprint fixe rouge, mêmes composants que le joueur en pilotage manuel) qui vise, tire par rafales et garde ses distances tant que le joueur est dans la zone |
| `UI/` (thème et widgets) | `BlockforgeTheme` partiel : `Theme/BlockforgeTheme.Tokens.cs` **généré** depuis `tokens.json` (couleurs dark + hud, `Palette` par thème, espacements, rayons, contrôles, opacités, ombres, styles de texte), `Theme/BlockforgeTheme.Layout.cs` (métriques des maquettes : TopBar, fin de match, résultats, hub), `BlockforgeTheme.cs` (mouvement, ReduceMotion, nombres). `Widgets/` : `UIKit` (construction uGUI en code, thème dark / hud), `DSWidgets` (Button, Badge, StatLine, Panel, Notice, CapacityMeter, KeyCap, Avatar, TeamMark, PartCard, SearchField, Select, Toggle, Stepper, SegmentTabs, ChipGroup, Pager, `DisabledReason`, `TabFocusCycler`), `ChamferedPlate` (plaque HUD à coins coupés, liseré qui suit les chanfreins), `ScoreboardWidget`, `TopBar` (onglets du hub, Q / E et gâchettes) |
| `UI/Match/` (fin de match) | `MatchEndFlow` (MatchEnded → HUD caché, arène figée → `MatchEndOverlay` 5 s / Espace → `ResultsScreen` 20 s / Continue → MainMenu ; installé par `RobotTestSpawner`), `MatchEndOverlay` (Victory / Defeat / Draw, awards), `ResultsScreen` (podium, Scoreboard, récompenses) |
| `UI/Hub/` | `HubScreen` (cadre commun 1fr + 440 px), `HubScreenHost` (écrans gardés vivants, fondu 220 ms), `WorkshopScreen`, `StoreScreen`, `TechTreeScreen` ; ouverts par `MainMenuController` sous le header. `UIPreview` : banc d'essai des six écrans |
| `Meta/` (`namespace Blockforge.Meta`) | Modèle des écrans meta (records `MatchResult`, `RobotListing`, `StoreItem`, `Bundle`, `TechNode`…), interfaces `IMatchResultSource`, `IWorkshopService`, `IStoreService`, `ITechTreeService`, `IWallet`, localisateur `MetaServices`, règles pures `MatchLogic` (awards, podium, connecteurs), mocks `MockServices` sur `MockMetaData` (ScriptableObject, état partagé : rechercher met la pièce au Store, acheter débite le portefeuille) |
| `Audio/`    | `MusicManager` : musique de fond persistante (créé tout seul au lancement, `DontDestroyOnLoad`), musique choisie par scène dans `Resources/MusicLibrary.asset` (`MusicLibrary` : clip + volume par `MusicTrack`, scène → musique ; scène non listée = la musique continue) ; changement = fondu enchaîné à puissance constante en temps réel, la musique sortante devenue muette est arrêtée et remise au début ; `MusicManager.PlayTrack()` pour un lancement explicite (Launch Sequence au clic sur Play with bots) ; AudioListener de secours pour les scènes qui n'en ont pas (Boot, MainMenu) |
| `Core/`, `SaveLoad/` | Réservés — vides pour l'instant (le bootstrap Steam et le cache local vivent dans `Steam/` et `Account/`) |

## Découpage des outils (`Scripts/Editor/`)

Tous les outils suivent le même pattern « one-shot » :

- classe statique `[InitializeOnLoad]`, exécutée une fois après compilation puis
  neutralisée par un marqueur `Library/Blockforge<Nom>.done` ;
- entrée de menu **Blockforge > Setup …** pour la relancer à la main ;
- idempotente : elle recrée ou met à jour les assets, jamais de doublons.

| Script | Rôle |
|---|---|
| `GarageInventorySetup` | Catalogue canonique des blocs (`EnsureBlocks` : stats, empreintes, ancrages, bounds exacts) et UI de l'inventaire dans la scène Garage |
| `BlockFootprintSetup` | **Resync Block Definitions** (rejoue `EnsureBlocks` sans régénérer l'UI) et **Report Block Bounds** (`Library/BlockBoundsReport.txt` : chaque modèle ajusté comparé à sa boîte, 4 rotations) |
| `<Famille>BlockSetup` | Relie FBX + icône aux `BlockDefinition` d'une famille (Chassis, Wheel, InsectLeg, Thruster, Rudder, RotorBlade, LaserWeapon, ShieldDisk) |
| `<Famille>MaterialSetup` | Crée les matériaux HDRP/Lit partagés de la famille et remappe les FBX dessus |
| `PaintSprayMaterialSetup` | Matériaux, animation et câblage du spray de peinture |
| `GarageUISetup`, `MainMenuUISetup`, `BuildMenuUISetup`, `GameModesUISetup`, `BootAuthSceneSetup` | Génèrent les UI des scènes. `MainMenuUISetup` : **Setup MainMenu UI** (tout) et **Setup MainMenu Header** (header seul, dans la scène existante) ; `GameModesUISetup` : **Setup MainMenu Play Screens** (écran Play du design « Play to Versus » + assets `Mode_TeamDeathmatch` / `Mode_BattleArena`, et `ApplyStyle` appelé par Apply Design System) |
| `MainMenuHangarSetup`, `MapMarsSceneSetup`, `MapMarsMaterialSetup` | Décors du hangar et de la carte Mars |
| `BlockforgeScenes` | Chemins des scènes, `OpenForSetup`, `RegisterAllInBuildSettings` (liste globale **et** profil de build actif, qui la surcharge), `LoadModelAsset` (FBX ou GLB) |
| `GarageV2SceneSetup` | **Setup Garage V2 Scene** : copie de Garage en Garage_V2, vaisseau Mothership (GLB), ligne centrale du miroir, grille recâblée, joueur sur `PlayerSpawn` ; **MainMenu -> Garage V2 / (V1)** |
| `MapTestSceneSetup` | **Setup Map Test Scene** (Red Canyon) / **(arène plate)** / **(terrain d'essai 600 m)** : soleil, volume, caméra de poursuite, map GLB avec `MeshCollider` (marquages et décor non jouable exclus), spawn, `TestSceneBootstrap` |
| `MapTestGroundImporter` | Import de `Map_TestGround.fbx` (terrain d'essai généré par `Tools/Maps/TestGround`) : réglages du modèle, propriétés FBX `bf_*` → `MovingTarget`, remise d'aplomb des pièces mobiles (enfants d'empty mal exportés par Blender avec `bake_space_transform`) ; `PrepareInstance` coupe le rendu des murs invisibles `Bounds_*` |
| `LoadoutDebugMenu` | **Debug > Garage : robot test 3 armes-modules** (Play mode, garage) : charge un mur de cubes portant deja 3 types pour tester le dialogue de remplacement |
| `BlockforgeBatch` | Pilotage externe de l'éditeur ouvert : `Library/BlockforgeRun.request` (menus, `open:<scène>`, `play`, `stop`) → `Library/BlockforgeRun.result` |
| `HubSetup` | **Setup Hub Screens** (MainMenu à l'échelle sur la hauteur, polices mono 600 / 700, données mock), **Meta > Create Mock Data** (`Resources/Meta/MockMetaData.asset`), **Setup UIPreview Scene** (scène de test hors build + tailles de Game view 16:9 / 21:9 / 16:10) |
| `MetaDebugMenu` | **Debug > Match end** : Fire MatchEnded (victory / defeat / draw), Open Results ; **Debug > Hub** : +1 000 crédits, +500 TP ; **Debug > Toggle ReduceMotion** |
| `DesignSystem/ThemeTokensGenerator` | **Design System > Generate Theme Tokens** : relit `tokens.json` dans l'archive du DS et réécrit `BlockforgeTheme.Tokens.cs` (même gabarit que `Tools/DesignSystem/gen_theme_tokens.py`) |
| `MusicSetup` | **Setup Music** : réglages d'import des `MUS_*.mp3` (Streaming, Vorbis 70) et création/complément de `Resources/MusicLibrary.asset` (clips, volumes égalisés, musique par scène) sans écraser les réglages faits dans l'Inspector |

Les setups du garage (`GarageUISetup`, `GarageInventorySetup`) s'appliquent à la
**scène garage active** (Garage ou Garage_V2), sinon à `Garage.unity` ; les
`<Famille>BlockSetup` one-shot ciblent toujours `Garage.unity`.

## Principes

1. **Data-driven** : chaque bloc du jeu est un asset `BlockDefinition`
   (ScriptableObject) dans `Data/Blocks/`. Ajouter un bloc = créer un asset +
   un modèle, zéro code. Les stats (coût CPU, masse, résistance), l'empreinte en
   cases et la face d'ancrage vivent dans la donnée ; la géométrie de pose en
   découle (`BlockFootprint`). La liste canonique est tenue dans
   `GarageInventorySetup`.
2. **Blueprint ≠ véhicule** : le design (`RobotBlueprint` / `VehicleBlueprint`,
   pure donnée sérialisable) est séparé du véhicule instancié en jeu. C'est ce qui
   rend la sauvegarde, le partage de designs et un futur multijoueur possibles.
3. **Ids stables** : les sauvegardes référencent les blocs par `blockId`, qui est
   le **nom de l'asset** `BlockDefinition` (`Block_Wheel_N1_Scout`). Ne jamais
   renommer un asset de bloc publié.
4. **Assembly definitions** : `Blockforge.Runtime` (le jeu) et
   `Blockforge.Editor` (outils, compilé uniquement dans l'éditeur). Compilation
   plus rapide et impossible de référencer du code éditeur dans un build.

## Scènes

- `Boot` — compte du joueur (UI Toolkit, `BootAuthController`) : panneau Steam (connexion automatique, Retry / Play offline / compte email), panneau email / mot de passe, inscription ; charge le menu
- `MainMenu` — hangar, 3 slots de robots, écran Play (modes, fiche, « Play with bots » → `MatchIntro` → `Map_Test` avec le robot sélectionné ; Échap → « Leave match » ramène ici)
- `Garage` — construction du robot (grille 14×14 de l'ancien vaisseau, pince, inventaire, spray)
- `Garage_V2` — même contenu sur le vaisseau Mothership (baie 31×31×31, cases de 1 m) ;
  c'est la scène chargée par le menu principal depuis le 16/09/2026
- `UIPreview` — banc d'essai (hors build) : 1 Victory, 2 Defeat, 0 Draw, 3 Results, 4 Workshop, 5 Store, 6 Tech Tree, M ReduceMotion
- `Map_Test` — terrain d'essai (Red Canyon) : touche P du garage, le robot en cours
  apparaît sur le `SpawnPoint` et se conduit en ZQSD ; ESC ×2 ramène au garage d'origine
- `Map_MARS` — future arène de combat (map v2 : `Map_Mars_v2.glb` depuis le 18/10/2026, l'ancien `Map_Mars.fbx` n'est plus instancié)

## Conventions

- Code : C# global (pas de namespace imposé), commentaires en français, en-tête
  `// ====` décrivant le rôle de chaque script.
- Blocs : `Block_<Famille>_N<tier>_<Nom>.asset` (`Block_Rudder_N3_Kestrel`) ;
  les blocs historiques gardent leur numéro (`Block_01_Cube`, `Block_11_Ailes`).
- Modèles : `<Famille>_<Nom>.fbx` dans `Art/Models/Blocks/<Catégorie>/` ;
  icônes `Icon_<Nom>.png` (512×512) dans `Art/Textures/Icons/`. Décors en GLB à la
  racine de `Art/Models/` (`ShipGarage_V3.glb`, `Map_Canyon.glb`, `Map_Arena.glb`, `Map_Mars_v2.glb`),
  importés par glTFast (`com.unity.cloud.gltfast`), stockés en LFS.
- Matériaux : `<Famille>_<Matériau>.mat` (`Wheel_Rubber`, `Shield_CyanChannel`)
  dans `Art/Materials/Blocks/`, `Spray_*` dans `Art/Materials/Garage/`.
- Blocs animés : `Art/Animations/<Nom>.controller` + prefab variant du FBX
  `Art/Prefabs/Blocks/<Nom>.prefab` (Animator câblé), pris comme `previewPrefab`
  à la place du FBX quand il existe.
- Sources Blender : `Tools/Blender/<Famille>.blend`, une collection par bloc.

## Tests

`Assets/_Project/Tests/PlayMode` (asmdef `Blockforge.Tests.PlayMode`, Window > General > Test Runner > PlayMode) : `MetaLogicTests` (awards, podium, connecteurs, portefeuille) et `MatchEndUITests` (overlay Victory / Defeat, HUD caché, score affiché une fois, liseré MVP, ReduceMotion).
