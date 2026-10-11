# Blockforge — Mémo du design system

> **Référence actuelle : le design system Blockforge** (`Branding/blockforge-design-system.zip` : `README.md`, `tokens.json`, composants, icônes, logo). Ce mémo garde l'intention de départ ; en cas d'écart, le design system fait foi. Son intégration dans Unity est décrite en fin de document.

## Intention

Blockforge adopte une direction sci-fi sobre, géométrique et colorée, inspirée des références Robocraft fournies par Alexis. La technologie apparaît par petites touches ; le robot reste l'élément principal de chaque écran.

L'identité repose sur les constructions modulaires, les éclairages cyan et les environnements. L'interface sert de cadre : surfaces sombres, formes simples, pictogrammes lisibles et aperçus de pièces bien visibles. Le côté « forge industrielle » est secondaire.

## Base visuelle proposée

Les valeurs ci-dessous constituent une première base à vérifier sur les maquettes et en jeu.

| Rôle | Couleur | Usage |
|---|---|---|
| Fond | `#171B24` | Anthracite légèrement bleuté |
| Surface | `#242B36` | Panneaux et cartes ; inventaire majoritairement opaque |
| Accent principal | `#39B8E8` | Actions et éléments actifs |
| Texte principal | `#F2F5F7` | Titres, libellés et valeurs |
| Texte secondaire | `#ADB8C7` | Informations complémentaires |

- Bleu et cyan comme signature visuelle ; orange ponctuel pour les avertissements ou le décor.
- Rectangles, angles presque droits et bordures discrètes. Découpes angulaires occasionnelles, sans surcharge décorative.
- Typographie sans-serif lisible : titres courts en capitales, descriptions en casse normale, chiffres faciles à comparer. Police exacte à choisir.
- Pictogrammes simples et pleins pour les outils ; aperçus 3D pour les blocs.
- Espacements communs proposés : 4, 8, 12, 16, 24 et 32 unités, à adapter à l'échelle de l'interface.
- Couleurs vives réservées aux informations utiles ; animations et retours d'action rapides et discrets.

## Organisation des écrans

| Écran | Principes |
|---|---|
| Accueil | Robot en grand au centre, navigation en haut, sélection des robots en bas, statistiques compactes sur le côté. |
| Inventaire | Grille régulière de pièces, catégories par icônes, recherche et fiche détaillée à droite. |
| Garage | Centre dégagé pour construire, outils en bas, CPU et masse regroupés, aide aux commandes repliable. |
| Combat futur | Informations essentielles en périphérie, viseur léger, équipes immédiatement reconnaissables. |
| Connexion | Déclinaison des mêmes couleurs, champs, boutons et règles typographiques. |

Le garage est l'écran de référence pour établir le système : il réunit catalogue, sélection, statistiques, miroir, peinture et test du robot. Le HUD de combat sera une déclinaison ultérieure, adaptée aux mécaniques effectivement présentes.

## Composants et comportements

À définir de manière cohérente : boutons, onglets, champs de recherche et de saisie, cartes de pièces, emplacements de robots, lignes de statistiques, jauges, infobulles, raccourcis clavier et notifications.

- Chaque composant interactif possède des états explicites : normal, survol, focus clavier, sélection ou activation, indisponibilité ; chargement et erreur lorsque pertinents.
- Une carte de pièce conserve une structure constante : aperçu, nom, catégorie et statistiques utiles.
- La couleur seule ne transmet jamais une erreur, une pose invalide ou une information indispensable : ajouter symbole, texte ou forme distinctive.
- Préserver la lisibilité sur les décors clairs comme sombres, ainsi que le contraste des textes secondaires.
- Garder les zones de construction et de visée dégagées ; rendre les informations secondaires accessibles sans occuper constamment l'écran.

## Cohérence avec la 3D

Volumes francs, constructions modulaires, matériaux plutôt mats et touches lumineuses localisées sur les éléments technologiques. Robots et cartes peuvent être très colorés ; l'interface sombre les met en valeur.

## Références et suite

Les cinq captures Robocraft fournies dans la conversation servent de références visuelles : accueil et sélection de robots, inventaire en grille avec fiche latérale, HUD de combat discret et garage centré sur la construction.

Prochaine étape proposée : réaliser une planche de palette, typographie et composants (dont bouton et carte de bloc), puis une maquette du garage. Vérifier ensuite lisibilité, contraste, densité et mise à l'échelle avant la déclinaison aux autres écrans.

Ce mémo décrit une direction de conception ; il ne signifie pas que le thème a été intégré dans Unity. Le projet possède déjà une base sombre à accent bleu, avec une connexion en UI Toolkit et des interfaces de menu et de garage en uGUI : la future intégration devra partager les mêmes règles visuelles entre ces écrans.

## Intégration Unity du design system (10 octobre 2026)

Source : `Branding/blockforge-design-system.zip`. Tout ce qui suit applique ce design system à la lettre ; le bundle React n'est qu'une référence d'implémentation.

- **Langue** : interface en anglais, sentence case, deuxième personne, sans emoji (règle du DS). Les styles `display-*`, `label` et boutons passent le texte en capitales eux-mêmes. Libellés des écrans, notifications, dialogues, fiches des 82 blocs et des 4 modes de jeu traduits (y compris dans `GarageInventorySetup` et `GameModesUISetup`, pour qu'une régénération ne repasse pas en français).
- **Jetons** : `Scripts/Runtime/UI/BlockforgeTheme.cs` reprend `tokens.json` (thème `dark`, et `BlockforgeTheme.Hud` pour le futur HUD de match) : surfaces, bordures, encres, accent, statuts, équipes, catégories, grades, espacements `space-*`, rayons `radius-*`, tailles `control-*`, opacités, durées (80 / 140 / 220 ms, ease-out, réglage « reduce motion »). Les anciens noms restent en alias. Nombres : `BlockforgeTheme.Num()` groupe les milliers par une espace fine, `Qty()` / `QtyMuted()` ajoutent l'unité (en `ink-muted` pour les StatLine).
- **Typo** : Barlow (400/500/600/700), Barlow Condensed (600/700) et JetBrains Mono (chiffres) dans `UI/Fonts` (licence OFL jointe). Les font assets TMP dynamiques sont générés par `Blockforge > Design System > Build Font Assets` (lancé automatiquement). `DSText.Apply(texte, DSTextStyle.X)` applique un style du DS (display-xl … readout-sm, button-sm/md/lg, key).
- **Formes et assets** : `UI/DesignSystem/Resources/DesignSystem/` contient les rectangles arrondis 9-slice (radius-xs/sm/md/lg/pill : fond, contour 1 px, anneau de focus 2 px décalé de 4 px, pointillés de la baie vide, ombres `shadow-panel` / `shadow-overlay`), les 70 pictogrammes (blancs, teintés dans Unity) et le logo. Générés par `Tools/DesignSystem/build_ds_assets.py` ; réglages d'import automatiques (`DesignSystemAssetImporter`).
- **Composants uGUI** : `DSElement` porte le rendu d'un composant du DS sur l'Image de son objet — rôles `ButtonPrimary/Secondary/Ghost/Danger`, `IconButton`, `ToolRail`, `TabNav`, `TabSegment`, `Panel`, `PanelFloating`, `PanelEdge`, `Strip`, `Dialog`, `Scrim`, `Field`, `RobotSlot`, `PartCard`, `Card`, `KeyCap`, `Badge`, `Notice`, `Inset`, `Track`, `Fill` — avec tous les états (repos, survol, focus clavier avec anneau, pressé, sélectionné, désactivé à `opacity-disabled`, baie vide). Les scripts d'UI ne manipulent plus de couleurs : ils règlent `Selected`, `Empty` ou `Tone`.
- **Écrans** : `Blockforge > Apply Design System` applique tout cela à MainMenu (navigation à onglets soulignés + logo, baies RobotSlot 176 px avec CPU et masse, bloc de stats flottant, un seul bouton principal « Play », écran Build, modes de jeu, dialogue de suppression « Delete <robot> ») et au Garage (StatLine avec barres, ToolRail à pictogrammes et raccourcis, aide des commandes en KeyHint à `opacity-ghost` repliable avec H, inventaire avec onglets segment à icônes, tuiles `tile-md` avec barre de grade, champ de recherche, colonne de détail, dialogues « Name your robot » / « Leave the garage? »). Relançable ; les setups d'UI l'appellent à la fin.
- **Connexion (UI Toolkit)** : `UI/Boot/BootAuth.uss` / `.uxml` : un panneau centré, champs `control-lg`, boutons primary / secondary / ghost, IconButton œil (visible / hidden), statut sous forme de Notice (icône + couleur + fond).
- **Notifications** : `Toast.cs` affiche des Notice du DS (pictogramme + titre de la tonalité sur fond `*-soft`, corps en `ink-secondary`), deux au plus à l'écran, success/info ~4 s, warning 6 s, danger 10 s, clic pour fermer. Le message est coupé à son premier point : « Couldn't save Hey. Can't reach the server. » → titre + corps.
