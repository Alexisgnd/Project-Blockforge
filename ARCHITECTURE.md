# Architecture — Project Blockforge

Jeu de construction/combat de véhicules par blocs (type Robocraft), Unity 6 + HDRP.

## Arborescence

```
Assets/
├── _Project/            ← TOUT notre contenu (le préfixe _ le garde en tête de liste)
│   ├── Art/             Modèles 3D, matériaux, textures, shaders, VFX, animations
│   ├── Audio/           Musique, SFX, AudioMixers
│   ├── Data/            ScriptableObjects (définitions de blocs, armes, modes de jeu)
│   ├── Prefabs/         Blocs, véhicules, projectiles, environnement, UI
│   ├── Scenes/          Toutes les scènes
│   ├── Scripts/
│   │   ├── Runtime/     Code du jeu (asmdef : Blockforge.Runtime)
│   │   └── Editor/      Outils éditeur (asmdef : Blockforge.Editor)
│   └── UI/              Sprites et polices d'interface
├── Settings/            Config HDRP + InputSystem_Actions (venant du template)
└── ThirdParty/          Assets du store et plugins — jamais mélangés à notre contenu
```

**Règle d'or** : rien ne se range à la racine de `Assets/`. Notre contenu va dans
`_Project/`, tout ce qui vient de l'extérieur va dans `ThirdParty/`.

## Découpage du code (`Scripts/Runtime/`)

| Dossier     | Responsabilité                                                        |
|-------------|-----------------------------------------------------------------------|
| `Core/`     | Bootstrap, GameManager, changement de scène, événements globaux       |
| `Building/` | Grille de construction, placement/suppression, validation de connexité |
| `Blocks/`   | Définitions de blocs (ScriptableObjects) et comportements par catégorie |
| `Vehicle/`  | Blueprint (design sauvegardé), assemblage runtime, physique, masse    |
| `Combat/`   | Projectiles, dégâts, destruction de blocs, santé                      |
| `Player/`   | Input (Input System), caméras (build / conduite)                      |
| `UI/`       | HUD, inventaire de blocs, menus                                       |
| `SaveLoad/` | Sérialisation JSON des blueprints de véhicules                        |

## Principes

1. **Data-driven** : chaque bloc du jeu est un asset `BlockDefinition`
   (ScriptableObject) dans `Data/Blocks/`. Ajouter un bloc = créer un asset +
   un prefab, zéro code. Les stats (masse, PV, coût) vivent dans la donnée.
2. **Blueprint ≠ véhicule** : le design (`VehicleBlueprint`, pure donnée
   sérialisable) est séparé du véhicule instancié en jeu. C'est ce qui rend la
   sauvegarde, le partage de designs et un futur multijoueur possibles.
3. **Id stables** : les sauvegardes référencent les blocs par `BlockDefinition.Id`
   (string), jamais par référence d'asset. Ne jamais renommer un Id publié.
4. **Assembly definitions** : `Blockforge.Runtime` (le jeu) et
   `Blockforge.Editor` (outils, compilé uniquement dans l'éditeur). Compilation
   plus rapide et impossible de référencer du code éditeur dans un build.

## Scènes prévues

- `Boot` — initialisation (managers persistants), charge le menu
- `MainMenu`
- `Garage` — construction du véhicule
- `Arena` — combat/test
- `OutdoorsScene` — scène d'exemple HDRP du template (à supprimer plus tard)

## Conventions

- Namespaces : `Blockforge.<Dossier>` (ex. `Blockforge.Building`)
- Assets nommés par type : `Block_ArmorCube`, `Mat_Hull`, `SFX_Explosion01`
- Un prefab de bloc = racine avec collider + visuel enfant
