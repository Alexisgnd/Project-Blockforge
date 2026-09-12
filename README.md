# Project Blockforge

Jeu de construction et de combat de robots par blocs, dans l'esprit de Robocraft.
On assemble son robot dans un garage à partir d'un catalogue de pièces (châssis,
roues, propulseurs, armes, boucliers…), puis on ira l'affronter sur des cartes
comme Mars.

Moteur : **Unity 6 (6000.5.6f1) + HDRP**. Backend : **PocketBase** (comptes et
sauvegarde des robots). Sources 3D : **Blender**.

> Projet en cours de prototypage. Le garage est jouable, le combat n'existe pas
> encore. Voir [État du projet](#état-du-projet).

---

## Sommaire

1. [État du projet](#état-du-projet)
2. [Démarrer](#démarrer)
3. [Structure du dépôt](#structure-du-dépôt)
4. [Catalogue des blocs](#catalogue-des-blocs)
5. [Pipeline 3D (Blender → Unity)](#pipeline-3d-blender--unity)
6. [Contribuer](#contribuer)
   - [Code](#code-c--unity)
   - [3D](#3d-modélisation)
   - [Musique et son](#musique-et-son)
   - [Game design, UI, tests, docs](#game-design-ui-tests-docs)
   - [Workflow Git et commits](#workflow-git-et-commits)
7. [Licence](#licence)

---

## État du projet

| Système | État |
|---|---|
| Écran de démarrage + authentification PocketBase | fonctionnel |
| Menu principal (hangar, 3 slots de robots, choix du mode de jeu) | fonctionnel |
| Garage : grille 14×14, pince de pose, inventaire par catégorie et famille, recherche | fonctionnel |
| Garage : spray de peinture (deux palettes de 12 teintes) | fonctionnel, en cours d'affinage |
| Sauvegarde des robots sur PocketBase | fonctionnel |
| Catalogue de blocs avec vrais modèles 3D | 47 blocs modélisés, ~25 placeholders (voir le catalogue) |
| Carte Mars | scène et planète en place, gameplay à venir |
| Combat, physique des véhicules, multijoueur | pas commencé |

---

## Démarrer

### Prérequis

- **Git** et **Git LFS** (`git lfs install` *avant* de cloner : les FBX, PNG et .blend
  sont stockés en LFS, sans quoi vous obtiendrez des fichiers pointeurs de 130 octets).
- **Unity Hub** avec l'éditeur **6000.5.6f1** (une autre 6000.5.x devrait convenir).
- **Blender 5.x** uniquement si vous touchez aux sources 3D (`Tools/Blender`).

### Installation

```bash
git lfs install
git clone https://github.com/Alexisgnd/Project-Blockforge.git
```

1. Dans Unity Hub, *Add project from disk* → dossier du clone.
2. Première ouverture : l'import des assets prend plusieurs minutes. Les scripts
   éditeur `Blockforge` (`Assets/_Project/Scripts/Editor`) exécutent ensuite une fois
   leurs « setups » (matériaux, catalogue de blocs, scène Garage) et posent un
   marqueur dans `Library/`. On peut les relancer à la main via le menu
   **Blockforge > Setup …**.
3. Ouvrir `Assets/_Project/Scenes/Boot.unity` et lancer le Play mode.
   Boot → MainMenu → Garage.

### Compte et backend

L'authentification passe par une instance PocketBase hébergée par le mainteneur.
Demandez un compte de test pour jouer. En développement, un fichier
`dev_autologin.json` à la racine (ignoré par Git, ne jamais le committer) permet
l'auto-connexion.

---

## Structure du dépôt

```
Project-Blockforge/
├── Assets/
│   ├── _Project/            ← TOUT notre contenu (le préfixe _ le garde en tête de liste)
│   │   ├── Art/
│   │   │   ├── Models/Blocks/{Chassis,Movement,Weapons,Special}/   FBX des blocs
│   │   │   ├── Materials/{Blocks,Garage,Map}/                       matériaux HDRP/Lit
│   │   │   ├── Textures/{Blocks,Icons,Map}/                         textures et icônes 512 px
│   │   │   └── Animations/, Garage/, Hangar/, Shaders/, VFX/
│   │   ├── Audio/{Music,SFX,Mixers}/   (vide pour l'instant, voir Contribuer)
│   │   ├── Data/Blocks/                un asset BlockDefinition par bloc
│   │   ├── Data/{Robots,GameModes}/    presets de robots, modes de jeu
│   │   ├── Resources/                  matériau placeholder des blocs sans modèle
│   │   ├── Scenes/                     Boot, MainMenu, Garage, Map_MARS
│   │   ├── Scripts/Runtime/            code du jeu   (asmdef Blockforge.Runtime)
│   │   ├── Scripts/Editor/             outils éditeur (asmdef Blockforge.Editor)
│   │   └── UI/                         UXML/USS de l'écran d'auth, polices, sprites
│   ├── Settings/                       HDRP, Input System (du template Unity)
│   ├── TextMesh Pro/                   essentiels TMP (SDFFunctions.hlsl patché pour le ray tracing)
│   └── ThirdParty/                     assets externes, jamais mélangés à _Project
├── Packages/, ProjectSettings/         manifeste UPM et réglages Unity
├── Tools/Blender/                      sources .blend de tous les modèles + scripts de pipeline
├── ARCHITECTURE.md                     découpage du code et principes
└── README.md
```

Ignorés par Git : `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds-Profiles/`,
`*.blend1`, `.plastic/` (Unity Version Control n'est pas utilisé, le dépôt de
référence est GitHub).

---

## Catalogue des blocs

Le jeu est *data-driven* : chaque bloc est un asset `BlockDefinition`
(`Assets/_Project/Data/Blocks/Block_<Famille>_N<tier>_<Nom>.asset`) qui porte le nom,
la famille, la catégorie, les stats (coût CPU, masse, résistance, taille), l'icône
et le prefab d'aperçu. La liste canonique vit dans
`Scripts/Editor/GarageInventorySetup.cs` ; les scripts `<Famille>BlockSetup` la
resynchronisent avec les assets.

| Catégorie | Famille | Tiers | Modèle 3D |
|---|---|---|---|
| Châssis | Pièces pleines (cube, pentes, coins, cône, pyramide…) | 12 | ✅ |
| Châssis | Tiges (courte, longue, arc, diagonales 2D/3D) | 5 | ✅ |
| Mouvement | Roues — Scout → Monster | 6 | ✅ |
| Mouvement | Chenilles | 1 | placeholder |
| Mouvement | Pattes d'insecte — Walker, Soldier | 2 | ✅ |
| Mouvement | Lames de survol — Squall → Hurricane | 6 | placeholder |
| Mouvement | Hélices (rotors) — Recon, Invader, Assault | 3 | ✅ |
| Mouvement | Ailes | 1 | placeholder |
| Mouvement | Ailerons — Hawk → Bat | 7 | ✅ |
| Mouvement | Propulseurs — Lynx → Cheetah | 5 | ✅ |
| Armes | Lasers — Wasp → Leviathan | 6 | ✅ |
| Armes | Lanceurs de plasma — Pulser → Goliathon | 6 | placeholder |
| Armes | Canons électriques (rail) — Piercer → Erazer | 4 | placeholder |
| Armes | Lames de Tesla — Slicer → Nova | 3 | placeholder |
| Défense | Distributeurs nano — Blinder → Constructor | 3 | placeholder |
| Défense | Blindage | 1 | placeholder |
| Spécial | Radar | 1 | placeholder |
| Spécial | Disque de bouclier | 1 | ✅ |

Les « placeholders » sont des blocs jouables avec un cube gris : ce sont les
premières contributions 3D à prendre.

---

## Pipeline 3D (Blender → Unity)

Chaque famille de blocs suit le même chemin, entièrement automatisé côté Unity :

1. **Source** : un fichier `Tools/Blender/<Famille>.blend`, une collection par bloc,
   une racine `Empty` nommée comme le futur FBX, pièces en `snake_case`.
2. **Export FBX** : un FBX par bloc dans `Assets/_Project/Art/Models/Blocks/<Catégorie>/`
   (`Chassis_Cube.fbx`, `Wheel_Scout.fbx`, `Rudder_N3_Kestrel.fbx`…).
   Réglages Blender : axes par défaut (Forward -Z, Up Y), **Apply Transform décoché**
   (`bake_space_transform = False`, indispensable avec des Empties), animations
   uniquement si le bloc en a.
3. **Icône** : rendu 512×512 fond transparent dans
   `Assets/_Project/Art/Textures/Icons/Icon_<Nom>.png` (même nom de base que le FBX).
   `Tools/Blender/rudders_pipeline.py` montre un pipeline complet import GLB →
   renommage → export FBX → rendu des icônes.
4. **Matériaux** : un script éditeur `<Famille>MaterialSetup.cs` recrée les matériaux
   HDRP/Lit (`Art/Materials/Blocks/<Famille>_<Matériau>.mat`) à partir d'une table
   couleur / metallic / smoothness, et remappe les matériaux des FBX dessus.
5. **Catalogue** : `<Famille>BlockSetup.cs` relie FBX + icône aux assets
   `BlockDefinition` et met à jour la scène Garage.

Conventions à respecter (ouvrir `Tools/Blender/Chassis.blend` ou `Wheels.blend`
comme référence) :

- Échelle : un cube de châssis = une case de la grille du garage.
- Orientation : l'avant du bloc regarde vers **-Y Blender** (soit +Z dans Unity) ;
  par exemple la tuyère d'un propulseur pointe vers +Y.
- Pivot sur la racine Empty, posé au point d'ancrage du bloc sur la grille.
- Style low-poly stylisé, palette cohérente avec les blocs existants (armure blanche,
  graphite, acier, liserés cyan émissifs).
- Peu de matériaux par bloc, nommés explicitement : c'est ce nom que le script
  `MaterialSetup` utilise pour le remap.

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

1. Modéliser une famille encore en placeholder (voir le [catalogue](#catalogue-des-blocs)) :
   lames de survol, plasma, canons électriques, Tesla, nano, blindage, radar,
   chenilles, ailes.
2. Améliorer les blocs existants (topologie, détails, textures).
3. Décors : garage, hangar, carte Mars.

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

Pas encore définie. Le code et les assets restent pour l'instant la propriété de
leurs auteurs respectifs ; contactez le mainteneur avant toute réutilisation hors
du projet. Une licence sera choisie avant la première version publique.
