# Blockforge — implement the Match End flow and the Hub screens

You are working in the Unity project **Blockforge**, a Robocraft-inspired robot-building combat game. Implement two feature sets, end to end, in the project's existing UI stack:

1. **Match end** — a Victory / Defeat overlay shown over the arena when a match ends, then a full-screen Results screen (top 3 + full scoreboard + rewards).
2. **Hub screens** — Workshop (community robots), Store (parts and bundles) and Tech Tree (research), reachable from the top navigation.

The designs are final and live in two places. Treat them as the source of truth:

- `Branding/blockforge-design-system.zip` — tokens (`tokens.json`, `tokens.css`), the brand book (`README.md`), the component specs (`components/<Comp>/README.md`), the icon set (`assets/Icons/*.svg`) and the logo (`assets/Logo/*.svg`).
- The design canvas "Blockforge — screens", pages **Match end** and **Hub screens** (artboards `Victory`, `Defeat`, `Results`, `Workshop`, `Store`, `TechTree`, 1920×1080). Each artboard is a self-contained HTML file whose markup and inline styles describe the exact layout; read them when a measurement is not stated below.

Work in this order: (1) inspect the project and report what UI stack, input system and fonts it already uses; (2) build the theme and shared widgets; (3) match end; (4) hub screens; (5) wire navigation and the mock data; (6) run the acceptance checks. Commit after each step with a short message. Ask me only if something below contradicts the codebase; otherwise decide and note the assumption in your final summary.

---

## 0. Ground rules

- **UI stack**: use what the project already uses. If there is a `UIDocument` / `.uxml` anywhere → UI Toolkit. If screens are `Canvas` + `RectTransform` → uGUI with TextMeshPro. If nothing exists yet → UI Toolkit (Unity 6). Do not mix the two in these features.
- **Reference resolution** 1920×1080, 16:9. Scale with screen height (UITK `PanelSettings` scale mode *Scale With Screen Size*, match 1 on height; uGUI `CanvasScaler` the same). Nothing clips at 21:9; at 16:10 the layout may letterbox vertically.
- **Fonts**: Barlow (400/500/600/700), Barlow Condensed (600/700) and a monospaced face for figures (JetBrains Mono if present, else the project's mono). Create the TMP / UITK font assets if they do not exist. Every number that a player compares (scores, CPU, damage, credits) is set in the mono face.
- **Copy**: English, sentence case, no emoji, thin space as the thousands separator (`18 420`). Labels name the action: `Continue`, `Download for 2 400`, `Research for 350 TP`.
- **No hard-coded colours or sizes in screen code.** Everything comes from the theme (section 1). If a value is missing from the theme, add the token there first.
- **Colour never carries a message alone.** Every team mark pairs a colour with a silhouette (ally = filled disc, enemy = diamond, neutral = hollow ring); every state pairs a colour with an icon or a word.
- **Icons**: import `assets/Icons/*.svg` (white, 24-unit grid) as sprites (Vector Graphics package) or as 24/48/96 px PNG exports, and tint at runtime. Never recolour the files. The names are the ids used below (`trophy`, `skull`, `ally`, `enemy`, `user`, `credits`, `electronics`, `cube`, `weapon`, `health`, `group`, `check`, `lock`, `unlock`, `download`, `test`, `store`, `search`, `chevron-left`, `chevron-right`, `objective`, `play`, `home`, `create`, `info`, `question`, `remove`, `add`).
- **Motion**: ease-out everywhere; hover/focus 80 ms, panels 140 ms, screen transitions 220 ms; honour a `ReduceMotion` setting (all durations → 0, no slides, fades only).
- **Accessibility**: every control is focusable and operable with keyboard and gamepad; focus ring 2 px `focus-ring`, offset 2 px; text ≥ 4.5:1 on its ground (the tokens already guarantee this when used as described).

---

## 1. Theme and shared widgets

### 1.1 Theme asset

Generate a `BlockforgeTheme` from `tokens.json` (a `ScriptableObject` for uGUI, or a USS file with CSS variables for UITK — generate it from the JSON with an editor script so it can be regenerated). Two themes: `dark` (menus) and `hud` (anything drawn over the arena: the match-end overlay and the Results screen). Key values, for reference:

| token | dark | hud | use |
|---|---|---|---|
| surface-base | `#171b24` | `#0c1017` | screen background |
| surface-raised | `#1f2531` | `#141a24` | top bar |
| surface-panel | `#242b36` | `#1a212c` | panels, cards |
| surface-inset | `#11151c` | `#080b10` | wells, avatar grounds |
| surface-hover / surface-selected | `#2c3441` / `#16394b` | `#232c3a` / `#123244` | |
| surface-hud | `#171b24b8` | `#0c1017a6` | translucent HUD plate |
| plate-ally / plate-enemy / plate-self | `#3e73d966` / `#d9483966` / `#39b8e85c` | `#4c8df666` / `#ff6f5e59` / `#4ecbf75c` | team tints over surface-hud |
| border-subtle / border-strong | `#303948` / `#74829a` | `#3a4557` / `#76859c` | |
| focus-ring | `#7fdcff` | `#9ae6ff` | |
| ink-primary / ink-secondary / ink-muted | `#f2f5f7` / `#b3bdcb` / `#959fb0` | `#ffffff` / `#c8d2de` / `#a6b2c2` | |
| ink-on-accent / ink-on-bright | `#04121a` / `#0d1117` | same | text on accent / on any light fill |
| accent / accent-hover / accent-press / accent-soft | `#39b8e8` / `#63cdf5` / `#1f97c6` / `#16394b` | `#4ecbf7` / `#7ddcff` / `#2aa9dc` / `#12303f` | |
| success / warning / danger / danger-ink | `#7fe8b8` / `#f0a93f` / `#c9332f` / `#f5756d` | `#96f0c8` / `#ffbb55` / `#d93b36` / `#ff8a82` | |
| team-ally / team-enemy / team-neutral | `#6ba2ff` / `#ff6f5e` / `#c2cddb` | `#85b4ff` / `#ff8271` / `#d4dde8` | |
| category structure / mobility / weapon / electronics / cosmetic | `#b0bccd` / `#4fd8cf` / `#f77aa1` / `#bf99ff` / `#ffd166` | lighter variants in tokens.json | |
| grade raw / forged / tempered / hardened / masterwork | `#9aa7b8` / `#62c48a` / `#62aef5` / `#cb92f5` / `#f0b23c` | lighter variants | 3 px edge bar on a part tile |
| shadow-hud | `0 1px 2px rgba(0,0,0,.85)` | | on all text over the arena |
| spacing | 4 / 8 / 12 / 16 / 20 / 24 / 32 / 48 | | |
| radius xs / sm / md / lg / pill | 2 / 3 / 6 / 10 / 999 px | | |
| control sm / md / lg | 28 / 36 / 44 px | | |
| hud-chamfer | 10 px | | corner cut on HUD plates |
| tile-md | 96 px | | part tile |

Type styles: `display-xl` 700 48/46 condensed caps · `display-lg` 700 34/34 · `display-md` 600 24/26 · `title-lg` 600 20/26 · `title-md` 600 16/22 · `title-sm` 600 14/20 · `body` 400 15/22 · `body-sm` 400 13/19 · `caption` 400 12/16 · `label` 600 11/14, letter-spacing .09em, rendered in caps · `readout-lg` 600 24/26 mono · `readout-md` 500 15/20 mono · `readout-sm` 500 12/16 mono.

### 1.2 Shared widgets (build once, reuse everywhere)

- **TopBar** (80 px, `surface-raised`, 1 px `border-subtle` bottom): logo `logo-horizontal.svg` at 36 px high on the left; centred nav tabs **Home · Play · Build · Tech Tree · Workshop · Store** (underline 2 px `accent` under the active one, label style in caps); on the right the credits readout (`credits` icon + mono figure in `grade-masterwork`) and the profile chip (28 px round avatar on `surface-inset`, name). The tab bar is one component with an `Active` enum.
- **Panel**: `surface-panel`, 1 px `border-subtle`, radius md, padding 16, optional title (`title-md`) and subtitle (`caption`).
- **Button**: primary (accent fill, `ink-on-accent`), secondary (transparent, 1 px `border-strong`), ghost; sizes 36 / 44; optional leading icon; states rest / hover / focus / pressed / disabled (opacity .38 + reason).
- **Badge**: pill, one scale at a time — status, category, grade, team — outline by default, `solid` variant for team and grade.
- **StatLine**: label (caption) left, value (readout-md) right, optional ratio bar underneath.
- **PartCard**: `tile-md` square, part preview, name, category icon, one figure; 3 px grade edge bar on the left; selected = `border-accent` + `surface-selected`; locked = opacity .38 + `lock`.
- **ChamferedPlate**: a `surface-hud` plate with one or more corners cut by `hud-chamfer` (which corners is a parameter). Used by every HUD-style element. Implement it as a 9-slice sprite or a generated mesh/`VisualElement` with a clip; do not fake it with a rotated square.
- **Avatar**: square, radius 4, `surface-base` ground, 2 px border in the team colour (accent for the local player), `user` icon as the placeholder until profile pictures exist.
- **TeamMark**: `ally` disc / `enemy` diamond / `neutral` ring icon tinted with the team colour.
- **Scoreboard**: the same widget as the in-match Tab board — a top plate (mode name, map · time) and two tables side by side, player's team on the left; columns are configurable; rows: TeamMark + name, figures right-aligned in mono; the local player's row tinted `plate-self`; a `Team` footer row with sums; no "down" state on the Results screen.

---

## 2. Data model and services

Create plain C# types (no MonoBehaviours) in `Blockforge.Meta`:

```csharp
enum Team { Ally, Enemy }                     // Ally = the local player's team, always drawn on the left
enum MatchOutcome { Victory, Defeat, Draw }
enum AwardKind { BestDamage, BestHealing, BestKills, BestAssists }

record PlayerMatchStats(string PlayerId, string DisplayName, Team Team, bool IsLocal, bool IsBot,
                        int Kills, int Deaths, int Assists, int Damage, int Healing, int Score);
record Award(AwardKind Kind, string PlayerId, int Value);
record MatchResult(string ModeName, string MapName, TimeSpan Duration, MatchOutcome Outcome,
                   int AllyScore, int EnemyScore, IReadOnlyList<PlayerMatchStats> Players,
                   IReadOnlyList<Award> Awards, MatchRewards Rewards);
record MatchRewards(int Credits, int TechPoints, int Xp, int Level, int XpInLevel, int XpForNextLevel);

record RobotListing(string Id, string Name, string Author, int Cpu, int Mass, int TopSpeedMph, int Health,
                    int Downloads, int PriceCredits, Grade Grade, Category MainCategory,
                    IReadOnlyDictionary<Category,int> PartCounts, int OwnedPartCount, int TotalPartCount, string Description);
record StoreItem(string Id, string Name, Category Category, Grade Grade, int Cpu, int PriceCredits, int Owned, bool RequiresResearch,
                 IReadOnlyList<(string label, string value, float ratio)> Stats, string Description);
record Bundle(string Id, string Name, string Description, int PriceCredits, int WasPriceCredits, TimeSpan EndsIn,
              IReadOnlyList<(Category category, int count)> Contents);
enum TechState { Researched, Available, Locked }
record TechNode(string Id, string Name, Category Lane, int Tier, Grade Grade, TechState State, int CostTechPoints,
                string RequiresId, string UnlocksId, int StorePriceAfter, string Description,
                IReadOnlyList<(string label, string value, float ratio)> Stats);
```

Interfaces, each with a **mock implementation backed by ScriptableObjects** so every screen runs in an empty scene:

- `IMatchResultSource` — `event Action<MatchResult> MatchEnded;` plus `MatchResult Last`.
- `IWorkshopService` — `Task<IReadOnlyList<RobotListing>> Browse(shelf, cpuFilter, sort, page)`, `Task Download(id)`.
- `IStoreService` — `Task<IReadOnlyList<StoreItem>> Shelf(category)`, `Task<Bundle> Featured()`, `Task Buy(id, qty)`.
- `ITechTreeService` — `Task<IReadOnlyList<TechNode>> Nodes()`, `Task Research(id)`, `int TechPoints`.
- `IWallet` — `int Credits`, `int TechPoints`, change events.

Awards are computed server-side in the real game; the mock computes them from the stats (max of each column; ties → the player with the higher score). Podium = top 3 players **across both teams by Score** (a losing-team player can be on it).

---

## 3. Match end

### 3.1 Flow

`MatchEnded` fires → the in-match HUD is hidden entirely (rosters, score bar, health, reticle, slots, name plates — nothing from the HUD stays visible) → **MatchEndOverlay** shows over the frozen arena → after 5 s, or on any confirm input (Space / A / click on Skip), fade to **ResultsScreen** → after 20 s, or on *Continue*, load the Hub (Home). Escape on the Results screen does nothing (there is no "back" to the arena).

### 3.2 MatchEndOverlay (one prefab, `Outcome` drives the variant)

Rendered in the `hud` theme, over the arena with a full-screen dim `rgba(11,14,20,.62)`.

- Top edge: an 8 px band across the full width in the winning team's colour (`team-ally` on Victory, `team-enemy` on Defeat).
- **Background emblem**, centred at 50 % × 46 %, drawn *behind* the text: `trophy` on Victory, `skull` on Defeat, 640 px, team colour, 16 % opacity, soft glow (drop shadow 60 px of the same colour at ~55 %).
- Verdict block, centred, top 210 px, items stacked with 14 px gaps, all text with `shadow-hud` plus a 24 px soft shadow:
  - over-line `label` style, `ink-secondary`, .2em tracking: TeamMark + `Blue team wins` / `Red team wins`;
  - the word **VICTORY** / **DEFEAT**, Barlow Condensed 700, 240 px / 210 line-height, .05em tracking, team colour;
  - the score `25 – 19`, mono 600 56 px, ally figure in `team-ally`, enemy figure in `team-enemy`, the dash `ink-muted` at 36 px — this is the only place the score appears on the overlay;
  - meta line `body`, `ink-secondary`: `Team Deathmatch · Hex valley · 9:12 played`.
- **Match awards** row, top 750 px, centred: an over-line `Match awards`, then four cards 330 × 64 px, 14 px apart, in this order: **Best damage** (`weapon` icon), **Best healing** (`health`), **Best kills** (`skull`), **Best assists** (`group`). Each card is a ChamferedPlate (top-left corner cut 12 px) tinted with the winner's team plate colour, holding: 40 px icon tile on `surface-base`; label (10 px caps, `ink-muted`); 22 px Avatar + player name (`title-md`, `accent` if local); the value right-aligned in mono 22 px. When nobody qualifies (value 0) the card shows `—`.
- Bottom, centred at 40 px from the edge: `Results in` (caps, `ink-secondary`) + the countdown in mono 28 px + a ghost `Skip` button with the `Space` key cap.
- Timing: 0 ms the last kill lands → HUD hidden, arena dims over 140 ms; 140–360 ms the verdict block slides down 40 px and fades in, the word stamps 1.1 → 1.0; the emblem fades in with it; awards cascade in left to right, 40 ms apart, from 400 ms; the countdown starts at 420 ms. Reduce-motion: 140 ms fade, no movement.
- Draw (if the mode allows it): the word `DRAW` in `ink-primary`, no emblem, no band.

### 3.3 ResultsScreen

Full screen, `hud` theme, background gradient `#141a24 → #0c1017` with a 96 px grid at 6 % `ink-primary`. Padding 32 px top, 80 px sides, 36 px bottom; vertical stack with 20 px gaps. 8 px band on the top edge in the winner's colour.

- **Animated grid cells**: ~14 of the 96 px grid cells, absolutely positioned, pulse in a 6 s loop with staggered delays (0 → 7 % → 0 opacity, 40–60 % hold); white in the middle third, `team-ally` on the left third, `team-enemy` on the right third. Reduce-motion: static at 4 %.
- **Header row**: left, an over-line `Match results` (12 px caps, `ink-muted`) and, on one baseline, the `trophy` icon (40 px) + **VICTORY** (Barlow Condensed 700, 64/60, team colour) + the score `25 – 19` in mono 600 56 px, 24 px to the right of the word, same baseline. Right-aligned: mode name (Barlow Condensed 600 24 caps) over `Hex valley · 9:12 played · vs bots` (`body`, `ink-secondary`). **The score appears here only** — not in the scoreboard top plate, not in the team headers.
- **Podium**: three cards centred, bottoms aligned, 20 px apart, order 2nd · 1st · 3rd. Cards 360 px wide (1st: 420), ChamferedPlate with both top corners cut 16 px, tinted with the player's team plate; rank numeral top-left (Barlow Condensed 700 44 px, `ink-muted`; `accent` on the 1st); Avatar 96 px (1st: 120); name `title-lg` 24 px (1st: 28 px, `accent`); team Badge solid (`Blue team`, `Blue team · you`, `Red team`); score (mono 700 44 px + `score` caption); a 2-column stat strip under a hairline: `K / D / A` and `damage`. **No streak stat.** The 1st card also carries an `MVP` status badge (success, `trophy` icon) top-right and a **2 px `accent` frame that follows the chamfers** — draw the frame as a second, 2 px larger chamfered shape behind an *opaque* card, never as a rectangular outline (it would be cut at the corners) and never let the frame colour show through the card.
- **Full board**: the shared Scoreboard widget, centred, 1 400 px wide, columns **Kills · Deaths · Assists · Damage · Score**, sorted by score, local row tinted, no score in its top plate or team headers, hint `Sorted by score`. Every player is drawn alive (no skull / down state here).
- **Footer plate** (ChamferedPlate, both bottom corners cut 16 px, `surface-hud`): `Rewards` over-line; `+1 240` credits (`credits` icon, `grade-masterwork`); `+85` tech points (`electronics` icon, `accent`); XP bar 360 px — `Level 14`, `+620 XP · 2 450 / 3 000`, 6 px track on `surface-base`, filled `accent` with the newly earned part in `accent-hover` at 60 %; on the right `Back to the garage in 20` (mono countdown) and one primary 48 px button **Continue** with a `chevron-right`. **No "Play again" button.**
- Timing: podium rises 2nd, 3rd, 1st, 80 ms apart; board rows fade in top to bottom 30 ms apart; rewards count up over 600 ms; XP bar fills over 600 ms after the count. Reduce-motion: fades only, final values immediately.

---

## 4. Hub screens

All three share: TopBar (active tab set), `dark` theme, body grid **`minmax(0,1fr) 440px`** with 32 px gaps and 32/48 px padding, a `display-xl` title with a one-line `body` subtitle in `ink-secondary`, and a 440 px detail Panel on the right that reflects the current selection. Selection is keyboard/gamepad navigable; the first item is selected on open.

### 4.1 Workshop (community robots)

- Title `Workshop`, subtitle `Robots built by other players. Download one into a bay and make it yours.`; a 360 px search field (`Search robots or builders`) and a `Sort` select (`Most downloaded`, `Best rated`, `Newest`, `Cheapest`) on the title row.
- Segmented tabs **Featured · New · Top rated · Friends · Mine (count)**; on the same row, right-aligned, a `CPU` label and four filter chips (`Under 1 000`, `1 000 – 1 500`, `1 500 – 2 000`, `Fits my bay`) — pill, 32 px, `border-strong`, selected = `accent` border + `surface-selected`.
- Grid **4 columns × 2 rows** of robot cards (Panel look, radius 4): 150 px preview area with the main-category Badge top-left; name `title-md` (ellipsis); author line (`user` icon + name, `ink-muted`); a bottom row with CPU (`cpu` icon, mono), downloads (`download` icon, mono) and the price in credits (`credits` icon, mono 600, `grade-masterwork`). Hover = `surface-hover` + `border-strong`; selected = `surface-selected` + `accent` border.
- Pager under the grid: `Showing 8 of 1 240 robots` left; `‹ 1 / 155 ›` icon buttons right.
- Detail panel: title = robot name, subtitle `by <author> · updated 2 days ago`; 260 px 3D preview (rotating render-texture; grade Badge solid bottom-right); description `body-sm`; four StatLines with ratio bars (CPU, Mass kg, Top speed mph, Health); category Badges with counts (`212 blocks`, `6 hovers`, `2 plasma`, `1 shield`); an info Notice `You own 198 of 221 parts. The missing ones are bought with the robot.`; primary 44 px **Download for 2 400** (`download` icon, full width), then a row with secondary **Test drive** (`test`), and icon buttons `trophy` (rate) and `external` (share). Download is disabled with a reason when credits are short.

### 4.2 Store (parts and bundles)

- Title `Store`, subtitle `Parts you have researched are here to buy. Everything else is in the Tech Tree first.`; a 320 px search field and a secondary **Get credits** button (`credits` icon).
- Segmented tabs **Featured · Blocks · Movement · Weapons · Electronics · Cosmetics · Bundles** with the category icons.
- **Featured bundle banner**: Panel with the bottom-right corner cut 24 px; 300 × 150 art on the left; over-line `Featured · ends in 2 d 06 h` in `accent` + a warning Badge `−20%`; name Barlow Condensed 700 36 caps; one-line description; content Badges by category; on the right the old price struck through (mono 12, `ink-muted`) above the price (mono 600 32, `grade-masterwork`, `credits` icon) and a primary 44 px **Buy bundle**.
- Shelf header: category name (Barlow Condensed 700 28 caps) left; a `Hide owned` toggle and a sort select right. Grid **6 columns** of PartCards; under each card the price (`credits` icon, mono, `grade-masterwork`) left and `Owned N` / `Research first` (caps 11 px, `ink-muted`) right; a part that needs research is locked (opacity .38 + `lock`) and cannot be selected for purchase.
- Detail panel: name + `Weapon · Hardened`; 240 px preview with a 3 px left bar in the grade colour; description; five StatLines (Damage, Fire rate /s, Range m, CPU, Mass kg); a `Grades` row of grade Badges, the owned grade solid, unresearched grades with a `lock` icon; an info Notice titled `Owned: 2` that states the CPU impact on the current robot (`A third launcher adds 165 CPU to your Race car — 1 978 of 2 000`, turn the figure `danger-ink` when it would exceed the budget); a quantity stepper (`remove` / value / `add`, 1 px `border-strong`) beside a primary 44 px **Buy for 1 450** (full width). Buying updates the wallet and the `Owned` counts without leaving the screen; a short success Notice confirms.

### 4.3 Tech Tree (research)

- Title `Tech Tree`, subtitle `Research a part to put it in the Store. Each lane unlocks left to right; tech points come from matches.`; a tech-points chip (32 px `electronics` tile on `surface-inset` in `accent`, over-line `Tech points`, mono 600 22 figure) and a secondary **How research works** (`question`) on the title row.
- A tier header row **Tier I … Tier V** (caps 11 px, `ink-muted`) aligned to the node columns.
- **Four lanes**, 150 px tall, separated by a `border-subtle` hairline: Structure (`structure` icon, `category-structure`), Movement (`mobility`), Weapons (`weapon`), Electronics (`electronics`). Lane head 170 px wide: icon + name (Barlow Condensed 700 22 caps, category colour) over `n of 5 researched`. Five nodes per lane joined by 58 px connectors.
- **Node** 176 × 108 px, Panel look with a 1 px `border-strong`, radius 4, a 3 px grade bar on the left edge; top row: lane icon left, state icon right; name `title-sm` 15 px; foot row (mono 12): grade name left, `Researched` / `<cost> TP` right. States: **Researched** = `accent` border, `accent-soft` fill, `check`; **Available** = default border, `unlock`, cost shown; **Locked** = opacity .55, `lock`, not researchable. Connector = `accent` when both ends are researched, `border-strong` when the left end is researched, `border-subtle` otherwise. The selected node takes the focus ring (3 px `focus-ring`, offset 2).
- Legend under the lanes: `✓ Researched` (accent) · `unlock Available — the cost is in tech points` · `lock Locked — research the part before it` · `Edge bar colour = the part's grade`.
- Detail panel: name + `Weapons · Tier III · Tempered`; 220 px preview with the grade bar; description; four StatLines; a `Requires` row (`check` + the prerequisite, `accent`), `Unlocks next` (`Flak cannon · 600`), `Store price after research` (`credits` + mono figure in `grade-masterwork`); a CapacityMeter `Tech points` (value = cost, max = cost, note `350 needed · you have 1 240`; over budget → `danger` segment and `danger-ink` figure); primary 44 px **Research for 350 TP** (`unlock`), disabled with a reason when locked or short of points. Researching flips the node to Researched, re-colours its connectors, unlocks the next node, debits the wallet and makes the part appear in the Store (the two services share state in the mock).

---

## 5. Navigation and scenes

- One **HubScene** hosting the TopBar and a screen container; Home, Play, Build (existing), Tech Tree, Workshop and Store are screens inside it, switched by the TopBar with a 220 ms crossfade. Keep the screens alive between switches (state, scroll, selection).
- **MatchEndOverlay** and **ResultsScreen** live in the match scene's UI root, above the HUD root, and are inactive until `MatchEnded`.
- Input: confirm (Space / Enter / gamepad South) skips the overlay countdown and triggers the primary action on Results; cancel (Esc / East) closes a detail or clears a selection on hub screens, never leaves the Results screen. Tab bar: Q/E or bumpers cycle tabs.
- Expose a **debug menu** (editor only) with: *Fire MatchEnded (victory)*, *Fire MatchEnded (defeat)*, *Open Results*, *Add 1 000 credits*, *Add 500 TP*, *Toggle ReduceMotion*.

---

## 6. Acceptance checks (do these before you report done)

1. A `UIPreview` scene opens each of the six screens with mock data at 1920×1080, 2560×1080 and 1280×800 — nothing overlaps or clips, the score on Results appears once, the MVP frame is 2 px all around including the chamfers, the card behind it is opaque.
2. Victory and Defeat are the same prefab; switching `Outcome` changes only band, emblem, word, colours and the award winners.
3. With the HUD visible mid-match, firing `MatchEnded` hides *every* HUD element before the overlay appears.
4. Skip (Space) and the 5 s timer both lead to Results; Continue and the 20 s timer both lead to the Hub; Escape does nothing on Results.
5. ReduceMotion on: no element moves; everything still appears.
6. Tab + arrows / gamepad reach every control on every screen in a sensible order; focus is visible.
7. Buying a part updates the wallet and `Owned`; researching a node unlocks its neighbour and surfaces the part in the Store.
8. No hex value or pixel size is written in screen code — `grep` for `#[0-9a-f]{6}` outside the theme returns nothing.
9. All copy is English sentence case with thin-space thousands; no emoji.
10. Play-mode tests cover: award computation (ties → higher score), podium ordering across teams, connector states for every neighbour combination, wallet debit on buy/research.

Report at the end: the UI stack detected, the files/prefabs created, any token you added to the theme, and every assumption you made.
