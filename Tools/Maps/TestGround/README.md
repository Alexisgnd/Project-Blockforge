# BlockForge — Map_TestGround (terrain d'essai)

Terrain d'essai greybox de **600 × 600 m** pour tester mobilité, armes et vol des robots. Remplace à terme l'arène d'entraînement 180 × 160 m (`../TrainingArena`).

## Fichiers
| Fichier | Rôle |
|---|---|
| `generate_testground.py` | Script source de vérité : reconstruit toute la scène, les textures, le FBX, l'aperçu et le .blend |
| `parts/*.py` | Le même script découpé par étape (setup, base, rampes+tir, accidenté+aérien, structures, remplissage, export) |
| `Map_TestGround.blend` | Scène Blender générée |
| `Preview_Overview.png` | Aperçu (rendu Workbench) |
| `Assets/_Project/Art/Models/Environment/Map_TestGround.fbx` | Export Unity (~9 Mo, ~191 k triangles, ~2 000 meshes + empties) |
| `Assets/_Project/Art/Textures/Map/TestGround/T_TG_*.png` | Textures grille 1024 px (1 tuile = 10 m, ligne fine tous les 1 m, ligne moyenne à 5 m) |

Convention : 1 unité = 1 m, Z vertical dans Blender (Y dans Unity après export), origine = centre du hub.

## Disposition
- **Hub (centre, 80 × 80 m)** : `SpawnPoint` (0, 0, 0,5), anneau de spawn, 6 panneaux de zone.
- **Couloirs N et E** : pistes de vitesse de 27 m bordées de séparateurs béton tous les 12 m et de lampadaires, repères tous les 50 m.
- **NE — Rampes & marches** : 8 rampes 10°→45°, marches 0,25→3 m, escalier, mur 6 m, 3 dévers, 2 sauts, 4 ralentisseurs, slalom, pyramide 4 faces (10/20/30/40°), cible mobile au fond.
- **NW — Stand de tir** : ligne + pont de tir 3 m, 6 couloirs × 3 tailles de cible (10 → 200 m), 4 cibles sur piliers, 6 pop-ups, 3 rails mobiles, 2 tourniquets, 3 orbites de « drones ».
- **W — Complexe à étages** : ziggurat 2/4/6 m · **parking 125 × 70 m, RDC + 4 niveaux tous les 8 m (7,4 m sous plafond)**, rampes 15° dans des trémies alternées, atrium central ouvert, garde-corps, voitures, couverts, cibles et un rail mobile par niveau, penthouse à 37 m · tour A (16 m) reliée au niveau 2, tour B (24 m) reliée au niveau 3, rampe A → B à 22°, tourniquet sur la tour B.
- **S — Cour de combat** : conteneurs (certains empilés), couverts, 2 miradors 8 m, bunker, cibles, 8 pop-ups, 2 tourniquets + remplissage (sacs de sable, caisses, barils, cabanes…).
- **SW — Terrain accidenté** : relief progressif, bosses, tranchée, cratères, rochers, mesa 6 m, 5 ruines, 5 pop-ups.
- **SE — Parcours aérien** : tour d'altitude 120 m, 4 plateformes (5 à 30 m) avec pop-up, escalier aérien 3 → 18 m, 9 anneaux, piste avec cible mobile rapide, 3 orbites de drones.
- **Remplissage** (`16_Props`) : caisses, barils, blocs, tuyaux, cabanes, conteneurs, voitures, tremplins, lampadaires, cibles — placés sur une grille d'occupation qui garde **au moins 4 m de passage libre** entre chaque élément.
- **Remparts** : falaises tout autour, murs invisibles `Bounds_*` à ±372 m.

Environ 200 cibles disques (anneaux jaune / rouge / bleu / noir / blanc), dont 52 cibles mobiles.

## Cibles
Chaque cible est un disque dont l'origine est au centre (`<nom>`), avec pied et socle séparés (`<nom>_Post`, `<nom>_Base`). Pour les dégâts, mettre le script de cible sur le mesh disque.

## Cibles mobiles
Chaque cible mobile est une hiérarchie. Le **parent** (`MoveLinear_*`, `MoveRotor_*`, `MoveOrbit_*`, `MovePopup_*`) est fixe et porte des propriétés personnalisées FBX (exportées) :

| Type | `bf_move` | Partie qui bouge | Paramètres |
|---|---|---|---|
| Rail | `linear` | `<nom>_Carriage` va de la position du parent à `<nom>_End`, aller-retour | `bf_speed` (m/s), `bf_pingpong` |
| Tourniquet / orbite | `rotate` | `<nom>_Rotor` tourne autour de l'axe vertical | `bf_speed` (°/s, signe = sens) |
| Pop-up | `popup` | `<nom>_Hinge` pivote de `bf_angle` autour de l'axe `Hinge → <nom>_HingeAxis` | `bf_angle` (°) |

Dans Unity, tout est automatique :
- `MapTestGroundImporter` (Scripts/Editor) lit les propriétés `bf_*` à l'import du FBX et pose un composant **`MovingTarget`** (Scripts/Runtime/Combat) configuré sur chaque racine. Si les propriétés n'arrivent pas, le setup de la scène l'ajoute quand même, réglé d'après le préfixe du nom.
- En jeu, `MovingTarget` donne à la pièce mobile un Rigidbody kinematique (déplacé en FixedUpdate, les robots sont poussés proprement) et passe ses colliders en convexe. Chaque cible démarre avec un déphasage aléatoire.
- Pop-ups : cycle debout / couché automatique (durées réglables dans l'inspecteur) ; `Knock()` couche la cible puis la relève — à appeler depuis le futur système de dégâts. Si une cible bascule du mauvais côté, inverser le signe de `angle`.
- Sélectionner une cible dans l'éditeur affiche sa trajectoire (gizmo orange).

## Intégration Unity
Menu **Blockforge > Setup Map Test Scene (terrain d'essai 600 m)** : remplace la map de `Map_Test` par ce terrain, pose les MeshCollider (marquages au sol, chiffres et textes exclus), coupe le rendu des murs invisibles `Bounds_*`, prépare les cibles mobiles, branche le `SpawnPoint`. Le menu **Setup Map Test Scene** remet le Red Canyon.

## Régénérer
Blender 5.x → Scripting → ouvrir `generate_testground.py` → Run Script (graines aléatoires fixes : même résultat à chaque fois).

## Limites
Non testé en jeu avec la physique du robot. ~2 000 objets : si les performances posent problème, fusionner les props statiques par zone (Static Batching suffit en général).
