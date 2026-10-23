# Audio — arborescence des effets sonores

Rangement des SFX de BlockForge. Les dossiers sont en anglais (comme le reste du
projet), un dossier = une famille de sons qui partagent un mixage et un usage.

## Sons en place (code : `Scripts/Runtime/Audio/Sfx*.cs`)

Lecture par `SfxManager.Play(SfxId.X)`, réglages par son dans
`Resources/SfxLibrary` (créée par **Blockforge > Setup SFX**).

| Fichier | SfxId | Joué quand |
|---|---|---|
| `Match/Alerts/SFX_Match_Alert.wav` | `MatchAlert` | 1:00, 0:30, 0:10 restantes ; une équipe à un kill de la victoire |
| `Damage/Regen/SFX_Robot_RegenCountdown_Loop.wav` | `RegenCountdown` | en boucle pendant les 5 dernières secondes avant la régénération (régénération après 10 s sans dégâts) |
| `Damage/Regen/SFX_Robot_RegenInterrupted.wav` | `RegenInterrupted` | une fois, quand un dégât casse ces 5 secondes d'attente |
| `Damage/Regen/SFX_Robot_Regen.wav` | `RobotRegen` | la régénération démarre |
| `Weapons/Common/SFX_Weapon_NoEnergy.wav` | `WeaponNoEnergy` | tir refusé faute d'énergie (jamais superposé) |
| `Garage/Build/SFX_Garage_BlockPlace.wav` | `GarageBlockPlace` | bloc posé (miroir : un seul son) |
| `Garage/Build/SFX_Garage_BlockRemove.wav` | `GarageBlockRemove` | bloc retiré (miroir : un seul son) |
| `Match/Respawn/SFX_Match_RespawnTick.wav` | `RespawnTick` | robot du joueur détruit : chaque seconde du « Respawn in » (0:09 → 0:01), puis une fois quand Respawn est disponible |

Régénération et énergie suivent **le robot que regarde la caméra de match** : le
sien, le coéquipier suivi après une destruction, le bot suivi en mode spectateur.
Caméra libre : aucun de ces sons. L'alerte du match est jouée pour tout le monde.

> **Temporaires** : ces 8 fichiers, et les jingles `Audio/Music/MUS_Match_RobotSpawn.wav`
> et `MUS_Mars_FinalTransition.wav`, viennent de Robocraft (extraction). À remplacer
> par des sons libres de droits ou créés avant toute version publique : même nom
> de fichier = aucun code à toucher.

Priorité : **EA** = nécessaire pour l'Early Access de décembre (TDM 5v5 contre
bots sur Mars, garage, menus) · **+** = bonus si le temps le permet ·
**Plus tard** = famille verrouillée en EA ou fonctionnalité post-EA.

## Conventions

- Nom : `SFX_<Catégorie>_<Nom>[_Loop][_NN]` — la catégorie est le sous-dossier
  quand il est sans ambiguïté (`SFX_Laser_Fire_01`, `SFX_Wheels_Roll_Loop`,
  `SFX_Match_LastMinute`). Ambiances : `AMB_<Lieu>_<Nom>` dans `Audio/Ambience`.
- Variantes : `_01`, `_02`, `_03` (au moins 3 pour tout son répété : tirs,
  impacts, pas, clics) — le code en tire une au hasard + léger pitch aléatoire.
- Boucles : suffixe `_Loop`, sans clic au point de bouclage.
- Une famille d'armes = un jeu de sons ; les tiers (N1 → N6) se distinguent par
  pitch / couche grave / volume, pas par des fichiers séparés. Un fichier
  `_T5` dédié seulement si un tier doit vraiment sonner différemment.
- Formats : WAV 48 kHz 16/24 bits, **mono** pour tout ce qui est positionné en 3D
  (armes, impacts, mouvement, modules, environnement), **stéréo** pour UI, HUD,
  Match et ambiances. Pic vers -3 dBFS, **jamais au-dessus de 0 dBFS** : un WAV
  32 bits flottant qui dépasse 1,0 est écrêté à l'import et grésille (cas de
  `NoEnergy`, `RegenInterrupted` et `Regen`, ramenés à -1 dBFS).
- Sources de travail (projets DAW, enregistrements bruts) : hors de `Assets/`.

## Impact = deux couches

Un tir qui touche joue **deux** sons : la signature de l'arme
(`Weapons/<Famille>/..._Hit`) + la surface touchée (`Impacts/<Surface>`).
5 familles × 5 surfaces sans multiplier les fichiers par 25.

## Dossiers

### Weapons — tirs (3D, mono)
| Dossier | Contenu attendu | Prio |
|---|---|---|
| `Laser` | `Fire` (×3+), `Hit`, `Overheat` / `Cooldown` si mécanique de chauffe | EA |
| `Plasma` | `Fire`, `Projectile_Loop` (boule en vol), `Hit` / `Explosion` | EA |
| `Rail` | `Charge`, `Fire`, `Hit`, `Ready` | EA |
| `Tesla` | `Activate`, `Arc_Loop`, `Hit`, `Deactivate` | EA |
| `Nano` | `Beam_Loop` (Mender / Constructor), `Heal_Tick`, `Start`, `Stop` | Plus tard |
| `Common` | `Flyby` (projectile qui passe près de la caméra), `Empty` / `NoEnergy`, `Reload` | EA |

### Impacts — surface touchée (3D, mono)
| Dossier | Contenu attendu | Prio |
|---|---|---|
| `Block` | impact sur bloc de châssis (métal) | EA |
| `Electroplate` | impact sur blindage (plus mat, électrique) | EA |
| `Shield` | impact sur bouclier | Plus tard |
| `Terrain` | roche / sable de Mars | EA |
| `Structure` | décor métallique (mesa, pont, viaduc…) | + |

### Damage — dégâts et destruction
| Dossier | Contenu attendu | Prio |
|---|---|---|
| `Taken` | **dégâts subis par MON robot** (2D, stéréo, distinct de l'impact 3D), version légère / lourde | EA |
| `BlockDestroyed` | bloc qui éclate | EA |
| `BlockDetached` | bloc qui se décroche (arrachement, sans explosion) | EA |
| `Debris` | débris qui tombent / rebondissent | + |
| `RobotDestroyed` | explosion du robot entier (3D) + version « c'est moi » (2D) | EA |
| `Critical` | **alarme santé critique** de mon robot (boucle) | EA |
| `Regen` | attente (bips en boucle), attente interrompue, début de régénération du robot regardé (2D) | EA — en place |

### Movement — déplacement (3D, mono, surtout des boucles)
| Dossier | Contenu attendu | Prio |
|---|---|---|
| `Wheels` | `Roll_Loop`, `Skid`, moteur | EA |
| `Tracks` | `Roll_Loop`, `Start`, `Stop` | EA |
| `Legs` | `Step` (×3+), servo | EA |
| `Hover` | `Hum_Loop` des lames de survol | EA |
| `Rotors` | `Spin_Loop` (pitch selon la poussée) | EA |
| `Thrusters` | `Thrust_Loop`, `Ignite`, `Cutoff` | EA |
| `Aero` | vent selon la vitesse, sifflement des ailerons | + |
| `Collisions` | choc contre le décor, atterrissage, frottement | EA |

### Modules — verrouillés en EA
| Dossier | Contenu attendu | Prio |
|---|---|---|
| `Shield` | `Deploy`, `Hum_Loop`, `Break`, `Recharge` | Plus tard |
| `Radar` | `Unfold`, `Ping`, `Detect` | Plus tard |
| `EMP` | `Charge`, `Blast`, `Disabled_Loop` (robot touché) | Plus tard |

### Match — déroulé de la partie (2D, stéréo)
| Dossier | Contenu attendu | Prio |
|---|---|---|
| `Flow` | début de match, fin de match, `Victory`, `Defeat`, `Draw` | EA |
| `Countdown` | 3-2-1-Go avant le match, ticks des 10 dernières secondes | EA |
| `Alerts` | **alarme dernière minute** (1:00, 0:30, 0:10) et équipe à un kill de la victoire — un seul son pour les deux, prolongation | EA — en place |
| `Objectives` | **objectif bientôt atteint** (allié / ennemi, deux sons distincts), prise / perte de la tête | EA |
| `Kills` | ennemi détruit, allié détruit, assistance, série | EA |
| `Respawn` | tick du compte à rebours, `Ready`, matérialisation du robot | EA |

### HUD — retours en combat (2D, stéréo)
| Dossier | Contenu attendu | Prio |
|---|---|---|
| `Targeting` | hit marker, confirmation de tir au but | EA |
| `Loadout` | changement de slot (1/2/3), slot verrouillé refusé | EA |
| `Warnings` | énergie faible, sortie de zone, robot en danger | EA |

### UI — menus (2D, stéréo)
| Dossier | Contenu attendu | Prio |
|---|---|---|
| `Navigation` | `Hover`, `Click`, `Back`, changement d'onglet, ouverture / fermeture de panneau | EA |
| `Feedback` | `Confirm`, `Error`, `Locked` (élément à cadenas), saisie | EA |
| `Notifications` | toast, popup, match trouvé | + |
| `Progression` | déblocage, points de l'arbre techno, monnaie gagnée | Plus tard |

### Garage
| Dossier | Contenu attendu | Prio |
|---|---|---|
| `Build` | `BlockPlace`, `BlockRemove`, `Rotate`, `InvalidPlacement`, pince (prise / lâcher), miroir M | EA |
| `Inventory` | sélection, changement de catégorie / famille, recherche | EA |
| `Paint` | `Spray_Loop`, choix de teinte | EA |
| `Robot` | sauvegarde, chargement d'un slot, entrée en test (P) | EA |

### Environment — sons ponctuels liés à une carte (3D, mono)
`Mars` (rafales, chutes de roches, machinerie), `Garage` / `Hangar` (portes de
baie, outils, bras mécaniques). Prio **+**.

## Audio/Ambience (hors SFX, préfixe AMB_)
Boucles de fond stéréo : `Mars`, `Garage`, `Hangar`, `Menu`. Mars en **EA**.

## Mixer (à faire dans `Audio/Mixers`)
Groupes calqués sur les dossiers : Master → Music · Ambience · SFX (Weapons,
Impacts, Damage, Movement, Match, HUD, UI, Garage). `Match` et `Critical` doivent
passer au-dessus du reste (ducking des tirs pendant une alerte).
