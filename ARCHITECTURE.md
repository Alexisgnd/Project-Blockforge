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
└── Maps/                Générateurs Python des cartes de test (arène, canyon) + aperçus
```

**Règle d'or** : rien ne se range à la racine de `Assets/`. Notre contenu va dans
`_Project/`, tout ce qui vient de l'extérieur va dans `ThirdParty/`.

## Découpage du code (`Scripts/Runtime/`)

| Dossier     | Responsabilité                                                          |
|-------------|-------------------------------------------------------------------------|
| `Data/`     | ScriptableObjects : `BlockDefinition`, `RobotPreset`, `GameModeDefinition` ; géométrie d'empreinte (`BlockFootprint` : boîte en cases, face d'ancrage, rotation, réflexion miroir) ; état de session (`RobotSession` : robot en cours, scène garage de retour, plan du miroir ; `RobotBlueprint` : baie max 31×31×31) |
| `Building/` | Pince de pose (`PlierGarageController`) : inertie, orientation de l'outil |
| `Garage/`   | Grille de construction (`GarageBuildController` : nombre de cases mesuré sur les lignes du vaisseau, 14×14 sur l'ancien, 31×31 sur le Mothership ; un bloc occupe toutes les cases de son empreinte orientée ; hors châssis, la face d'ancrage se plaque contre la face visée du support ; mode miroir M = jumeau par le plan de la ligne centrale, image miroir vraie), ghost, blocs posés, fabrique d'aperçus (`BlockPreviewFactory`) |
| `Player/`   | Contrôleur joueur du garage, switch pince/spray, molette de couleur du spray |
| `UI/`       | `Garage/` (inventaire, stats, toolbar, sauvegarde T, test P), `MainMenu/` (slots de robots, modes), `BootAuthController` |
| `Network/`  | Client PocketBase (`PocketBaseClient`) et garde d'authentification (`AuthGuard`) |
| `Vehicle/`  | Robot hors du garage : `RobotAssembler` (blueprint → GameObject, visuel + `BoxCollider` par bloc, pivot au centre de l'empreinte), `RobotTestSpawner` (spawn au Start, robot par défaut si aucun blueprint), `RobotTestDriver` (conduite char ZQSD sur Rigidbody piloté en vitesse), `RobotFollowCamera` (poursuite orbitale). `VehicleBlueprint` est l'ancien modèle de design, inutilisé : ne jamais importer `Blockforge.Vehicle` |
| `Core/`, `Combat/`, `SaveLoad/` | Réservés (bootstrap, dégâts/destruction, sérialisation locale) — vides pour l'instant |

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
| `GarageUISetup`, `MainMenuUISetup`, `BuildMenuUISetup`, `GameModesUISetup`, `BootAuthSceneSetup` | Génèrent les UI des scènes |
| `MainMenuHangarSetup`, `MapMarsSceneSetup`, `MapMarsMaterialSetup` | Décors du hangar et de la carte Mars |
| `BlockforgeScenes` | Chemins des scènes, `OpenForSetup`, `RegisterAllInBuildSettings` (liste globale **et** profil de build actif, qui la surcharge), `LoadModelAsset` (FBX ou GLB) |
| `GarageV2SceneSetup` | **Setup Garage V2 Scene** : copie de Garage en Garage_V2, vaisseau Mothership (GLB), ligne centrale du miroir, grille recâblée, joueur sur `PlayerSpawn` ; **MainMenu -> Garage V2 / (V1)** |
| `MapTestSceneSetup` | **Setup Map Test Scene** (Red Canyon) / **(arène plate)** : soleil, volume, caméra de poursuite, map GLB avec `MeshCollider` (marquages et décor non jouable exclus), spawn, `TestSceneBootstrap` |
| `BlockforgeBatch` | Pilotage externe de l'éditeur ouvert : `Library/BlockforgeRun.request` (menus, `open:<scène>`, `play`, `stop`) → `Library/BlockforgeRun.result` |

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

- `Boot` — authentification PocketBase (UI Toolkit), charge le menu
- `MainMenu` — hangar, 3 slots de robots, choix du mode de jeu
- `Garage` — construction du robot (grille 14×14 de l'ancien vaisseau, pince, inventaire, spray)
- `Garage_V2` — même contenu sur le vaisseau Mothership (baie 31×31×31, cases de 1 m) ;
  c'est la scène chargée par le menu principal depuis le 16/09/2026
- `Map_Test` — terrain d'essai (Red Canyon) : touche P du garage, le robot en cours
  apparaît sur le `SpawnPoint` et se conduit en ZQSD ; ESC ×2 ramène au garage d'origine
- `Map_MARS` — future arène de combat

## Conventions

- Code : C# global (pas de namespace imposé), commentaires en français, en-tête
  `// ====` décrivant le rôle de chaque script.
- Blocs : `Block_<Famille>_N<tier>_<Nom>.asset` (`Block_Rudder_N3_Kestrel`) ;
  les blocs historiques gardent leur numéro (`Block_01_Cube`, `Block_11_Ailes`).
- Modèles : `<Famille>_<Nom>.fbx` dans `Art/Models/Blocks/<Catégorie>/` ;
  icônes `Icon_<Nom>.png` (512×512) dans `Art/Textures/Icons/`. Décors en GLB à la
  racine de `Art/Models/` (`ShipGarage_V2.glb`, `Map_Canyon.glb`, `Map_Arena.glb`),
  importés par glTFast (`com.unity.cloud.gltfast`), stockés en LFS.
- Matériaux : `<Famille>_<Matériau>.mat` (`Wheel_Rubber`, `Shield_CyanChannel`)
  dans `Art/Materials/Blocks/`, `Spray_*` dans `Art/Materials/Garage/`.
- Blocs animés : `Art/Animations/<Nom>.controller` + prefab variant du FBX
  `Art/Prefabs/Blocks/<Nom>.prefab` (Animator câblé), pris comme `previewPrefab`
  à la place du FBX quand il existe.
- Sources Blender : `Tools/Blender/<Famille>.blend`, une collection par bloc.
