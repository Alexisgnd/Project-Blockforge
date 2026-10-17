using UnityEngine;

// =========================================================
// METRIQUES D'ECRAN (px a la resolution de reference 1920 x 1080)
// Lues depuis les artboards du canvas « Blockforge — screens »
// (Victory, Defeat, Results, Workshop, Store, TechTree) et le
// prompt de fin de match / hub. Tout ce qui n'est pas un jeton
// du design system mais une mesure de maquette vit ici : les
// ecrans n'ecrivent aucune taille en dur.
// =========================================================

public static partial class BlockforgeTheme
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    // Anneau de focus : 2 px (control-focus-width) decale de 2 px du controle
    public const float FocusOffset = 2f;

    // Tailles de pictogrammes utilisees par les ecrans
    public static class IconSize
    {
        public const float Xs = 12f;
        public const float Sm = 14f;
        public const float Md = 16f;
        public const float Lg = 18f;
        public const float Xl = 20f;
        public const float Xxl = 24f;
        public const float Tile = 40f;   // tuile d'icone 40 px (recompenses, awards)
        public const float Emblem = 640f; // embleme de fond de l'overlay
    }

    // ----- Micro-ecarts des maquettes (entre les pas de spacing du DS) -----
    public static class Gap
    {
        public const float Hair = 2f;      // valeur / libelle empiles (readouts, puces)
        public const float Tight = 3f;     // libelle / nom d'un award
        public const float IconText = 5f;  // pictogramme 14 px / chiffre mono
        public const float Small = 6f;     // auteur, tete de voie, barre d'XP, chips
        public const float Medium = 10f;   // prix + ancien prix, Requires / valeur
    }

    // Interlettrage du libelle des apercus 3D (« [3D PREVIEW] ») : 0.06em en unites TMP
    public const float PlaceholderTracking = 6f;

    // ----- Petits controles du DS (mesures des composants du bundle) -----
    public static class Controls
    {
        public const float KeyCap = 22f;          // KeyHint : touche 22 px, min-width 22
        public const float ToggleWidth = 44f;
        public const float ToggleHeight = 24f;
        public const float ToggleThumb = 16f;
        public const float ToggleThumbInset = 3f;
        public const float AvatarBorder = 2f;     // bord d'un Avatar >= 48 px (1 px en dessous)
        public const float PartThumb = 40f;       // vignette d'une PartCard tile-md
        public const float TooltipHeight = ControlMd;
    }

    // ----- Ordre des canvases (uGUI sortingOrder) -----
    public static class Order
    {
        public const int Hud = 10;
        public const int Hub = 20;
        public const int MatchEnd = 800;   // au-dessus du HUD, sous la pause (900) et les notices (1000)
        public const int Results = 810;
        public const int Pause = 900;
        public const int Intro = 950;
        public const int Toast = 1000;
    }

    // ----- Barre de navigation du hub -----
    public static class TopBar
    {
        public const float Height = 80f;
        public const float LogoHeight = 36f;
        public const float LogoWidth = 190f;  // lockup horizontal, jamais sous 180 px
        public const float SidePadding = Space12;
        public const float TabLabelSize = 15f;
        public const float TabMinWidth = 110f;
        public const float AvatarSize = 28f;
        public const float ChipHeight = 40f;
        public const float ChipWidth = 180f;
        public const float ChipPadding = 6f;
        public const float CreditsValueSize = 16f;
        public const float CreditsWidth = 72f;
        public const float Underline = 2f;
    }

    // ----- Overlay de fin de match (Victory / Defeat / Draw) -----
    public static class Overlay
    {
        public const float DimAlpha = 0.62f;        // rgba(11, 14, 20, .62)
        public const float Band = 8f;               // bande du haut, couleur de l'equipe gagnante
        public const float EmblemY = 0.46f;         // centre a 50 % x 46 %
        public const float EmblemAlpha = 0.16f;
        public const float EmblemGlow = 60f;
        public const float EmblemGlowAlpha = 0.55f;
        public const float VerdictTop = 210f;
        public const float VerdictGap = 14f;
        public const float OverlineSize = 16f;
        public const float OverlineTracking = 0.2f;
        public const float WordSize = 240f;
        public const float WordLine = 210f;
        public const float WordTracking = 0.05f;
        public const float ScoreSize = 56f;
        public const float DashSize = 36f;
        public const float ScoreGap = 28f;
        public const float MetaSize = 16f;
        public const float AwardsTop = 750f;
        public const float AwardsGap = 14f;
        public const float AwardWidth = 330f;
        public const float AwardHeight = 64f;
        public const float AwardChamfer = 12f;
        public const float AwardPadLeft = 14f;
        public const float AwardPadRight = 18f;
        public const float AwardLabelSize = 10f;
        public const float AwardLabelTracking = 0.14f;
        public const float AwardAvatar = 22f;
        public const float AwardValueSize = 22f;
        public const float AwardTintEnd = 0.85f;    // degrade plate -> transparent a 85 %
        public const float FooterBottom = 40f;
        public const float FooterGap = 24f;
        public const float CountdownSize = 28f;
        public const float SkipHeight = 40f;
        public const float SoftShadow = 24f;
        // Chronologie (s)
        public const float AutoAdvance = 5f;
        public const float DimTime = 0.14f;
        public const float VerdictDelay = 0.14f;
        public const float VerdictTime = 0.22f;
        public const float VerdictSlide = 40f;
        public const float WordStampFrom = 1.1f;
        public const float AwardsDelay = 0.40f;
        public const float AwardStagger = 0.04f;
        public const float AwardTime = 0.14f;
        public const float CountdownDelay = 0.42f;
    }

    // ----- Ecran de resultats -----
    public static class Results
    {
        public const float Band = 8f;
        public const float PadTop = 32f;
        public const float PadSide = 80f;
        public const float PadBottom = 36f;
        public const float Gap = 20f;
        public const float Grid = 96f;
        public const float GridAlpha = 0.06f;
        public const float CellPeak = 0.07f;
        public const float CellStatic = 0.04f;
        public const float CellLoop = 6f;
        public const int CellCount = 14;
        public const float HeaderOverlineSize = 12f;
        public const float HeaderOverlineTracking = 0.16f;
        public const float WordSize = 64f;
        public const float WordLine = 60f;
        public const float WordTracking = 0.03f;
        public const float WordIcon = 40f;
        public const float ScoreSize = 56f;
        public const float DashSize = 32f;
        public const float ScoreGap = 16f;
        public const float ScoreOffset = 24f;
        public const float ModeSize = 24f;
        public const float HeaderHeight = 84f;
        // Podium
        public const float PodiumGap = 20f;
        public const float PodiumWidth = 360f;
        public const float PodiumFirstWidth = 420f;
        public const float PodiumChamfer = 16f;
        public const float PodiumFrame = 2f;
        public const float PodiumPad = 24f;
        public const float PodiumPadBottom = 20f;
        public const float PodiumFirstPadTop = 32f;
        public const float PodiumItemGap = 10f;
        public const float RankSize = 44f;
        public const float RankX = 20f;
        public const float RankY = 14f;
        public const float Avatar = 96f;
        public const float AvatarFirst = 120f;
        public const float AvatarIcon = 40f;
        public const float AvatarIconFirst = 52f;
        public const float NameSize = 24f;
        public const float NameFirstSize = 28f;
        public const float ScoreValueSize = 44f;
        public const float StatSize = 18f;
        public const float StatLabelSize = 10f;
        public const float StripGap = 8f;
        public const float StripPadTop = 12f;
        public const float HairlineAlpha = 0.12f;
        public const float MvpBadgeInset = 16f;
        // Tableau
        public const float BoardWidth = 1400f;
        // Pied de page
        public const float FooterChamfer = 16f;
        public const float FooterPadX = 24f;
        public const float FooterPadY = 16f;
        public const float FooterGap = 40f;
        public const float FooterHeight = 88f;
        public const float RewardIcon = 40f;
        public const float RewardValueSize = 22f;
        public const float RewardLabelSize = 11f;
        public const float XpWidth = 360f;
        public const float XpTrack = 6f;
        public const float XpGainAlpha = 0.6f;
        public const float XpLabelSize = 11f;
        public const float CountdownSize = 13f;
        public const float ContinueHeight = 48f;
        public const float ContinueIcon = 18f;
        // Chronologie (s)
        public const float AutoAdvance = 20f;
        public const float PodiumStagger = 0.08f;
        public const float PodiumTime = 0.22f;
        public const float PodiumRise = 24f;
        public const float RowStagger = 0.03f;
        public const float RowTime = 0.14f;
        public const float CountUp = 0.6f;
        public const float XpFill = 0.6f;
    }

    // ----- Scoreboard (widget partage, tableau de la touche Tab) -----
    public static class Board
    {
        public const float DefaultWidth = 900f;
        public const float TopHeight = 56f;
        public const float TopSide = 120f;
        public const float ScoreSize = 32f;
        public const float ModeSize = 24f;
        public const float MetaSize = 12f;
        public const float HeadHeight = 32f;
        public const float HeadPadX = 12f;
        public const float HeadTextSize = 11f;
        public const float TotalSize = 14f;
        public const float ColumnHeadHeight = 24f;
        public const float RowHeight = 26f;
        public const float CellPadX = 8f;
        public const float CellSize = 12f;
        public const float PlayerSize = 13f;
        public const float PlayerColumn = 0.46f;
        public const float StripeAlpha = 0.03f;
        public const float FooterLineAlpha = 0.12f;
        public const float HintSize = 12f;
        public const float HintHeight = 20f;
        public const float TeamPadBottom = 4f;
    }

    // ----- Ecrans du hub (Workshop, Store, Tech Tree) -----
    public static class Hub
    {
        public const float PadTop = Space8;
        public const float PadSide = Space12;
        public const float PadBottom = Space8;
        public const float ColumnGap = Space8;
        public const float DetailWidth = 440f;
        public const float TitleRowGap = Space6;
        public const float TitleSubGap = 6f;
        public const float SectionGap = Space5;
        public const float SearchWidth = 360f;
        public const float StoreSearchWidth = 320f;
        public const float SortWidth = 220f;
        public const float StoreSortWidth = 200f;
        public const float PreviewRadius = 4f;
        public const float DetailParagraphSize = 14f;
        public const float DetailParagraphLine = 20f;
        public const float DetailGap = Space5;
        public const float StatGap = Space3;
        public const float ChipHeight = 32f;
        public const float ChipPadX = 14f;
        public const float ChipGap = Space2;
        public const float ChipTextSize = 13f;
        public const float PagerTextSize = 13f;
        public const float CrossfadeTime = MotionScreen;
    }

    public static class Workshop
    {
        public const int Columns = 4;
        public const int Rows = 2;
        public const float CardGap = Space4;
        public const float CardRadius = 4f;
        public const float PreviewHeight = 150f;
        public const float BodyPadX = 14f;
        public const float BodyPadTop = 12f;
        public const float BodyPadBottom = 14f;
        public const float BodyGap = 6f;
        public const float TagInset = 10f;
        public const float NameSize = 16f;
        public const float BySize = 12f;
        public const float StatSize = 12f;
        public const float PriceSize = 14f;
        public const float DetailPreview = 260f;
        public const float GradeInset = 12f;
        public const float ActionsGap = 10f;
        public const int PageSize = Columns * Rows;
    }

    public static class Store
    {
        public const int Columns = 6;
        public const float CardGap = Space4;
        public const float ItemGap = Space2;
        public const float PricePadX = 4f;
        public const float PriceSize = 14f;
        public const float OwnedSize = 11f;
        public const float OwnedTracking = 0.06f;
        public const float WasSize = 12f;
        public const float BannerPadX = 28f;
        public const float BannerPadY = 24f;
        public const float BannerGap = Space8;
        public const float BannerCut = 24f;
        public const float BannerArtWidth = 300f;
        public const float BannerArtHeight = 150f;
        public const float BannerGlowAlpha = 0.10f;
        public const float BannerOverlineSize = 11f;
        public const float BannerOverlineTracking = 0.1f;
        public const float BannerNameSize = 36f;
        public const float BannerTextSize = 14f;
        public const float BannerTextWidth = 620f;
        public const float BannerPriceSize = 32f;
        public const float BannerPriceIcon = 22f;
        public const float BannerInnerGap = 10f;
        public const float ShelfTitleSize = 28f;
        public const float DetailPreview = 240f;
        public const float GradeBar = 3f;
        public const float GradesGap = 6f;
        public const float StepperValueWidth = 40f;
        public const float StepperValueSize = 16f;
        public const float BuyRowGap = Space3;
    }

    public static class Tech
    {
        public const int Tiers = 5;
        public const float LaneHeight = 150f;
        public const float LaneHeadWidth = 170f;
        public const float LaneHeadPadRight = Space4;
        public const float LaneTitleSize = 22f;
        public const float LaneTitleGap = 10f;
        public const float LaneSubSize = 12f;
        public const float NodeWidth = 176f;
        public const float NodeHeight = 108f;
        public const float NodeGap = 58f;        // connecteur
        public const float NodePadLeft = 16f;
        public const float NodePadRight = 12f;
        public const float NodePadTop = 12f;
        public const float NodePadBottom = 10f;
        public const float NodeEdgeInset = 10f;
        public const float NodeNameSize = 15f;
        public const float NodeFootSize = 12f;
        public const float NodeRadius = 4f;
        public const float LockedOpacity = 0.55f;
        public const float Connector = 2f;
        public const float TierColumn = NodeWidth + NodeGap;
        public const float TierLabelSize = 11f;
        public const float TierLabelTracking = 0.1f;
        public const float LegendSize = 13f;
        public const float LegendGap = 28f;
        public const float SelectedRing = 3f;
        public const float SelectedRingOffset = 2f;
        public const float ChipPadX = Space4;
        public const float ChipPadY = 10f;
        public const float ChipGap = Space3;
        public const float ChipTile = 32f;
        public const float ChipValueSize = 22f;
        public const float DetailPreview = 220f;
        public const float DetailRowSize = 13f;
        public const float DetailRowsGap = 10f;
    }
}
