# Architecture — Project Blockforge

Jeu de construction/combat de robots par blocs (type Robocraft), Unity 6 + HDRP.
Ce document décrit le découpage du code et les principes à respecter. Pour
l'installation, le catalogue des blocs, le pipeline 3D et le guide de
contribution, voir le [README](README.md).

## Arborescence

```
Assets/
├── _Project/            ← TOUT notre contenu (le préfixe _ le garde en tête de liste)
│   ├── Art/             Modèles 3D (Models/Blocks/<Catégorie>), matériaux, textures,
│   │                    icônes, shaders, VFX, animations
│   ├── Audio/           Musique, SFX, AudioMixers
│   ├── Data/            ScriptableObjects : blocs (Data/Blocks), robots, modes de jeu
│   ├── Prefabs/         Blocs, véhicules, projectiles, environnement, UI
│   ├── Resources/       Ressources chargées par nom (matériau placeholder des blocs)
│   ├── Scenes/          Boot, MainMenu, Garage, Map_MARS
│   ├── Scripts/
│   │   ├── Runtime/     Code du jeu (asmdef : Blockforge.Runtime)
│   │   └── Editor/      Outils éditeur (asmdef : Blockforge.Editor)
│   └── UI/              UXML/USS (écran d'auth), sprites et polices d'interface
├── Settings/            Config HDRP + InputSystem_Actions (venant du template)
├── TextMesh Pro/        Essentiels TMP (SDFFunctions.hlsl patché pour le ray tracing)
└── ThirdParty/          Assets du store et plugins — jamais mélangés à notre contenu
Tools/
└── Blender/             Sources .blend de tous les modèles + scripts de pipeline
```

**Règle d'or** : rien ne se range à la racine de `Assets/`. Notre contenu va dans
`_Project/`, tout ce qui vient de l'extérieur va dans `ThirdParty/`.

## Découpage du code (`Scripts/Runtime/`)

| Dossier     | Responsabilité                                                          |
|-------------|-------------------------------------------------------------------------|
| `Data/`     | ScriptableObjects : `BlockDefinition`, `RobotPreset`, `GameModeDefinition` ; état de session (`RobotSession`, `RobotBlueprint`) |
| `Building/` | Pince de pose (`PlierGarageController`) : inertie, orientation de l'outil |
| `Garage/`   | Grille de construction 14×14 (`GarageBuildController`), ghost, blocs posés, fabrique d'aperçus |
| `Player/`   | Contrôleur joueur du garage, switch pince/spray, molette de couleur du spray |
| `UI/`       | `Garage/` (inventaire, stats, toolbar, sauvegarde), `MainMenu/` (slots de robots, modes), `BootAuthController` |
| `Network/`  | Client PocketBase (`PocketBaseClient`) et garde d'authentification (`AuthGuard`) |
| `Vehicle/`  | `VehicleBlueprint` : design sérialisable d'un robot                    |
| `Core/`, `Combat/`, `SaveLoad/` | Réservés (bootstrap, dégâts/destruction, sérialisation locale) — vides pour l'instant |

## Découpage des outils (`Scripts/Editor/`)

Tous les outils suivent le même pattern « one-shot » :

- classe statique `[InitializeOnLoad]`, exécutée une fois après compilation puis
  neutralisée par un marqueur `Library/Blockforge<Nom>.done` ;
- entrée de menu **Blockforge > Setup …** pour la relancer à la main ;
- idempotente : elle recrée ou met à jour les assets, jamais de doublons.

| Script | Rôle |
|---|---|
| `GarageInventorySetup` | Catalogue canonique des blocs (`EnsureBlocks`) et UI de l'inventaire dans la scène Garage |
| `<Famille>BlockSetup` | Relie FBX + icône aux `BlockDefinition` d'une famille (Chassis, Wheel, InsectLeg, Thruster, Rudder, RotorBlade, LaserWeapon, ShieldDisk) |
| `<Famille>MaterialSetup` | Crée les matériaux HDRP/Lit partagés de la famille et remappe les FBX dessus |
| `PaintSprayMaterialSetup` | Matériaux, animation et câblage du spray de peinture |
| `GarageUISetup`, `MainMenuUISetup`, `BuildMenuUISetup`, `GameModesUISetup`, `BootAuthSceneSetup` | Génèrent les UI des scènes |
| `MainMenuHangarSetup`, `MapMarsSceneSetup`, `MapMarsMaterialSetup` | Décors du hangar et de la carte Mars |

## Principes

1. **Data-driven** : chaque bloc du jeu est un asset `BlockDefinition`
   (ScriptableObject) dans `Data/Blocks/`. Ajouter un bloc = créer un asset +
   un modèle, zéro code. Les stats (coût CPU, masse, résistance, taille) vivent
   dans la donnée. La liste canonique est tenue dans `GarageInventorySetup`.
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
- `Garage` — construction du robot (grille, pince, inventaire, spray)
- `Map_MARS` — future arène de combat

## Conventions

- Code : C# global (pas de namespace imposé), commentaires en français, en-tête
  `// ====` décrivant le rôle de chaque script.
- Blocs : `Block_<Famille>_N<tier>_<Nom>.asset` (`Block_Rudder_N3_Kestrel`) ;
  les blocs historiques gardent leur numéro (`Block_01_Cube`, `Block_07_Chenilles`).
- Modèles : `<Famille>_<Nom>.fbx` dans `Art/Models/Blocks/<Catégorie>/` ;
  icônes `Icon_<Nom>.png` (512×512) dans `Art/Textures/Icons/`.
- Matériaux : `<Famille>_<Matériau>.mat` (`Wheel_Rubber`, `Shield_CyanChannel`)
  dans `Art/Materials/Blocks/`, `Spray_*` dans `Art/Materials/Garage/`.
- Sources Blender : `Tools/Blender/<Famille>.blend`, une collection par bloc.
