# Red Canyon — terrain d'entraînement Blockforge

`Blockforge_Training_Arena.glb` : nouvelle carte intégrée, matériaux inclus, sans texture externe.

Le niveau représente une raffinerie dans un canyon. Les défis sont incorporés dans un réseau de routes et bâtiments : circuit au sol, montée sur la mesa, pont vers le réacteur, accès aux toitures, descente par la station de pompage et raccourcis. Les douze cibles sont réparties dans cet environnement. Quatre segments de viaduc offrent une ligne alternative de sauts. Façades, piliers, rochers et auvent permettent de préparer les tests d'adhérence.

- Échelle en mètres, axe vertical Y ; canyon initial d'environ 250 × 210 m, terrain complet d'environ 1,36 × 1,16 km.
- Départ : objet `SpawnPoint`, près de (-19, 0.3, -66). Adapter la hauteur au robot.
- Routes principales de 15 à 17 m de large ; passages secondaires de 11 à 13 m.
- Toits et voies hautes à 8, 9, 10 et 14 m.
- 567 maillages nommés ; 168 252 triangles ; environ 17,1 Mo.
- Matériaux PBR embarqués ; objets statiques séparés, coordonnées des sommets dans le repère de la carte.

Le GLB contient la carte 3D, sans logique de gameplay ni colliders Unity. Ajouter les collisions aux surfaces solides, sans les marquages décoratifs. Regrouper les éléments de chaque `Target_XX` pour leur comportement de tir. La praticabilité avec les dimensions, la suspension et l'adhérence du robot reste à tester dans le moteur.

Source : `../generate_canyon.py`, utilisant les fonctions de `../generate_arena.py`. L'aperçu est calculé à partir de la même géométrie.

## Décor extérieur

Le groupe `BACKGROUND_NON_PLAYABLE` contient un grand terrain continu, une jupe extérieure et cinq lots de détails regroupés par matériau. Le sol prolonge celui du canyon, avec plusieurs centaines de mètres presque plats, de légères ondulations puis de grosses collines près des limites. Deux lits asséchés serpentent dans la plaine ; les reliefs sont creusés de sillons d'érosion. Éboulis, blocs fracturés, graviers, 500 touffes de végétation sèche et 27 arbustes morts complètent le paysage. Les rochers suivent localement la hauteur du terrain.

La coupe extérieure se trouve derrière les crêtes. Le terrain possède des normales lissées et des couleurs par sommet (`COLOR_0`) pour les variations minérales, les berges et les strates. L'importeur GLB doit conserver ces couleurs. Les reliefs et petits objets sont de la géométrie ; aucune image de texture externe n'est utilisée. Les 560 maillages d'origine sont conservés à l'identique.

Ce décor est situé derrière les falaises du parcours et n'est pas conçu pour être parcouru. Les crêtes masquent les bords depuis une caméra proche du sol ; une caméra aérienne assez haute peut voir au-delà. Le groupe porte la métadonnée `nonPlayable: true`. Les métadonnées de scène indiquent une limite elliptique indicative de rayons 100 et 80 mètres. Ces métadonnées ne bloquent pas physiquement le robot : dans Unity, appliquer une limite de jeu ou des volumes de blocage, notamment pour les robots volants ou capables d'escalader. Le GLB seul ne fournit pas cette logique.

La génération actuelle du paysage est dans `../detailed_terrain.py` ; relancer `../generate_canyon.py` pour exporter l'ensemble. `Arena_Preview.png` montre l'ensemble et `Terrain_Detail.png` présente la plaine orientale de plus près. Ces aperçus utilisent un éclairage simplifié ; le GLB contient les normales lissées.

Vérifications : structure GLB et hiérarchie, coordonnées finies, absence de triangles dégénérés, empreinte binaire identique de la géométrie initiale, inspection visuelle de l'aperçu.
