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

## Écran Play et transition Versus (16 octobre 2026)

Source : design « Blockforge — Play to Versus » (canvas Design, 3 artboards). Appliqué dans Unity par `Setup MainMenu Header`, `Setup MainMenu Play Screens` et `MatchIntro` :

- **Header** : bandeau `surface-raised` de 80 px, lockup à gauche, Tabs nav Home / Play / Build / Tech tree / Workshop / Store (souligné accent), à droite le solde de crédits (readout mono en `grade-masterwork`, pictogramme `credits`) et la puce joueur (Panel : avatar rond `surface-inset` avec pictogramme `user` en accent, pseudo en title-sm). Les sections non réalisées affichent une Notice « coming soon ».
- **Écran Play** : une seule vue. Colonne de gauche (520 px) : `display-xl` « Play », accroche, une Card par mode (puits `Inset` 44 px avec le pictogramme du mode, titre, ligne de chiffres, chevron sur la ligne sélectionnée = `surface-selected` + `border-accent`), puis « Your robot » : RobotSlot du robot choisi sur l'accueil + bouton ghost « Change ». Panneau de droite : aperçu de la carte avec deux puces HUD (`surface-hud`, coin coupé `hud-chamfer`, pictogramme + libellé), titre `display-lg`, description, trois readouts (Players, Target, Time), puis la rangée d'actions : « Play with bots » (primary `control-lg`, pictogramme `play`, touche Entrée en KeyHint), « Find a match » (secondary désactivé : `opacity-disabled` + cadenas + raison au survol), raison à droite.
- **Transition** (`MatchIntro`, thème hud) : 0 ms l'écran Play s'assombrit à 60 % en 140 ms ; 0–320 ms deux volets ease-out, bleu depuis la gauche, rouge depuis la droite, bord à 12° (`HudPolygon.slant*` = 230 px sur 1080) ; 320–420 ms le mark tombe sur la couture (1,15 → 1) ; à 420 ms les volets deviennent les sols de l'écran Versus, la couture la colonne VS, et les rosters arrivent en cascade (40 ms d'écart). Rosters : plaques `surface-hud` teintées `plate-ally` / `plate-enemy` (`plate-self` pour le joueur), coin extérieur coupé, vignette 80 px cadrée couleur d'équipe, nom, robot, piste de progression, puce « Bot », CPU en mono. La barre du bas se remplit des deux bords vers le centre (gauche = équipe du joueur, toujours). Échap / Cancel possible tant que la scène ne charge pas ; animations réduites = fondu de 140 ms sans volets.

## Fin de match et écrans du hub (17 octobre 2026)

Source : canvas « Blockforge — screens », pages Match end (Victory, Defeat, Results) et Hub screens (Workshop, Store, TechTree). Construits en code (uGUI + TMP) par `UIKit`, sans prefab.

- **Thème** : `BlockforgeTheme.Tokens.cs` est généré depuis `tokens.json` (Blockforge > Design System > Generate Theme Tokens) ; ne plus recopier une valeur à la main. `BlockforgeTheme.PaletteFor(DSTheme)` donne les jetons dark (hub) ou hud (fin de match) ; `DSElement.theme` choisit le thème d'un composant. Les mesures propres aux maquettes vivent dans `BlockforgeTheme.Layout.cs` ; aucun écran n'écrit de couleur ni de taille en dur.
- **Polices** : JetBrains Mono 600 et 700 ajoutées (scores, prix, readout-lg) ; `DSText.Apply(text, TypeSpec, …)` applique un style de jeton.
- **Plaques HUD** : `ChamferedPlate` dessine les coins coupés en maillage (`HudPolygon`), le dégradé d'équipe et, pour le 1er du podium, un liseré de 2 px qui suit les chanfreins derrière une carte opaque.
- **Overlay** : voile 62 %, bande de 8 px, emblème trophy / skull à 16 %, mot 240 px, score (seule occurrence), quatre Match awards, « Results in » + Skip. Animations réduites : fondu de 140 ms, rien ne bouge.
- **Results** : grille 96 px avec cellules qui pulsent, en-tête (seul endroit du score), podium 2e · 1er · 3e, Scoreboard 1 400 px, Rewards + « Back to the garage in 20 » + Continue ; pas de « Play again », Échap inerte.
- **Hub** : TopBar 80 px, grille 1fr + 440 px, titre display-xl, panneau de détail ; toute action indisponible porte opacity-disabled, un cadenas et sa raison au survol / au focus.
- **Focus** : anneau 2 px `focus-ring` décalé de 2 px (avant : 4 px) ; Tab / Maj+Tab parcourent les contrôles d'un écran, flèches et stick via la navigation uGUI.

