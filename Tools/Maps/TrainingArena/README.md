# Blockforge — arène d'entraînement

Livrable : `Blockforge_Training_Arena.glb`, matériaux PBR inclus, sans texture externe.

- Emprise : 180 × 160 mètres. Convention : Y vertical, 1 unité = 1 mètre.
- Départ : centre (0, 0, 0), repère `SpawnPoint` à Y = 0,2 m. Ajuster la hauteur de spawn au robot.
- Pentes : 10°, 20°, 30° et 45°, largeur 13 m, longueur horizontale 22 m, palier supérieur 10 m.
- Tir : 9 cibles réparties sur 3 couloirs ; cibles orientées vers -Z.
- Escalade : murs verticaux de 12 et 20 m, angle intérieur, mur de 14 m avec plafond en surplomb.
- Plateformes : hauteurs de 2, 4, 7, 9, 11 et 15 m ; rampe d'accès à la première.
- 362 maillages nommés, 14 110 triangles, environ 1,2 Mo.

## Intégration

Le fichier contient la géométrie et les matériaux. Il ne contient pas de logique de gameplay, de colliders Unity, de dégâts, ni de système d'adhérence. Configurer les collisions sur les surfaces solides ; exclure les marquages et les liserés décoratifs. Pour les cibles, regrouper le support et les anneaux sous un objet de gameplay si nécessaire. Les coordonnées des sommets sont exprimées dans le repère de la carte.

L'aperçu `Arena_Preview.png` est un rendu orthographique de la géométrie générée. Le résultat n'a pas été testé en jeu avec la physique du robot.

## Source

`generate_arena.py` permet de régénérer le GLB et l'aperçu avec Python et Pillow (police Consolas Windows). Aucun téléchargement ni dépendance 3D n'est nécessaire.
