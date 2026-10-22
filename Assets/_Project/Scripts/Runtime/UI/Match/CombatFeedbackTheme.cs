using UnityEngine;

// =========================================================
// THEME DES RETOURS DE COMBAT (hitmarker, chiffres de degats)
// Toutes les tailles et durees du design valide (prompt
// « killfeed + hitmarker » du design system) ; les couleurs
// viennent des jetons du theme hud (BlockforgeTheme.Hud), jamais
// en dur ici. Un asset Resources/CombatFeedbackTheme peut
// surcharger les valeurs ; sans asset, ces valeurs par defaut.
// Tailles en pixels a 1080 p (canvas mis a l'echelle sur la
// hauteur d'ecran).
// =========================================================

[CreateAssetMenu(menuName = "Blockforge/Combat Feedback Theme", fileName = "CombatFeedbackTheme")]
public class CombatFeedbackTheme : ScriptableObject
{
    // Trait du hitmarker, mesure le long de chaque diagonale depuis le centre,
    // en unites x/y d'une boite de 100 unites (rendue a boxPixels)
    [System.Serializable]
    public struct Stroke
    {
        public float from;
        public float to;
        [Tooltip("Epaisseur du trait de couleur (unites)")]
        public float width;
        [Tooltip("Epaisseur du trait sombre dessine dessous (unites)")]
        public float outlineWidth;

        public Stroke(float from, float to, float width, float outlineWidth)
        {
            this.from = from;
            this.to = to;
            this.width = width;
            this.outlineWidth = outlineWidth;
        }
    }

    [Header("Ecran")]
    public float referenceHeight = 1080f;
    [Tooltip("Au-dessus du HUD de match (RobotTestHUD : 10)")]
    public int sortingOrder = 11;

    [Header("Hitmarker")]
    public float boxPixels = 120f;
    public float boxUnits = 100f;
    public Stroke hit = new(17f, 24f, 2.5f, 5.5f);
    public Stroke weakSpot = new(16f, 28f, 3.5f, 6.5f);
    public Stroke kill = new(16f, 31f, 4f, 7.5f);
    [Tooltip("Tenue a pleine opacite (s)")]
    public float hitmarkerHold = 0.06f;
    [Tooltip("Agrandissement + fondu (s)")]
    public float hitmarkerFade = 0.18f;
    public float hitmarkerScaleTo = 1.15f;

    [Header("Point faible")]
    [Tooltip("Blocs qui doivent tomber (partie soutenue par le bloc detruit) pour qu'un impact compte comme point faible")]
    public int weakSpotMinBlocks = 3;

    [Header("Chiffres de degats")]
    public float numberSize = 22f;
    [Tooltip("Point faible et kill")]
    public float strongNumberSize = 26f;
    public int numberWeight = 600;
    [Tooltip("Contour sombre tout autour (px)")]
    public float numberOutline = 2f;
    [Tooltip("Depart du chiffre par rapport au centre du reticule (x vers la droite, y vers le haut)")]
    public Vector2 numberOffset = new(36f, 76f);
    public float numberRise = 30f;
    public float numberLife = 0.6f;
    [Tooltip("Impacts sur la meme cible a moins de ce delai : un seul chiffre qui s'additionne (s)")]
    public float mergeWindow = 0.3f;
    [Tooltip("L'ancien chiffre reste visible, plus petit, pendant son fondu")]
    public bool showMergedGhost = true;
    public float ghostSize = 18f;
    public float ghostAlpha = 0.55f;
    public float skullSize = 22f;
    public float skullGap = 4f;
    [Tooltip("Ombre douce sous le chiffre (0 3px 8px noir 45 %)")]
    public float softShadowOffsetY = 3f;
    public float softShadowAlpha = 0.45f;
    [Tooltip("Flou de l'ombre douce (adoucissement du SDF, 0..1)")]
    public float softShadowSoftness = 0.6f;
    [Tooltip("Chiffres affiches en meme temps au plus")]
    public int maxNumbers = 12;

    [Header("Killfeed")]
    [Tooltip("Bord droit de l'ecran -> lignes (px a 1080 p)")]
    public float killfeedRight = 40f;
    // Le design dit 400 px, mais a 400 px les 5 lignes (400 -> 664) couvraient la
    // minimap de TdmMiniMap (bord droit, a mi-hauteur : 424 -> 656). Roster ennemi :
    // 16 + 5 x 26 + 4 x 3 = 158 px ; la killfeed demarre 16 px dessous (176 -> 440)
    // et la minimap est descendue de 32 px (456 -> 688).
    [Tooltip("Haut de l'ecran -> premiere ligne, sous le roster ennemi (design : 400, voir commentaire)")]
    public float killfeedTop = 176f;
    public float killfeedLineGap = 6f;
    public int killfeedMaxLines = 5;
    [Tooltip("Marges de la plaque : haut, bas, gauche, droite")]
    public Vector4 killfeedPadding = new(6f, 6f, 8f, 10f);
    public float killfeedElementGap = 10f;
    [Tooltip("Coin haut droit chanfreine (tourne vers le bord de l'ecran)")]
    public float killfeedChamfer = 10f;
    public float avatarSize = 36f;
    public float avatarBorder = 2f;
    [Tooltip("Rendu des vignettes de robots (px, affichees a avatarSize)")]
    public int avatarRenderSize = 72;
    [Tooltip("Vrai : le joueur local garde sa photo de profil Steam ; faux : la vignette de son robot comme tout le monde")]
    public bool localUsesProfilePicture;
    [Tooltip("Repli sans image : pictogramme user blanc a cette opacite")]
    public float avatarFallbackIconAlpha = 0.75f;
    public float avatarFallbackIconSize = 20f;
    [Tooltip("Victime : niveaux de gris a cette luminosite")]
    public float victimBrightness = 0.7f;
    public float skullBadgeSize = 16f;
    public float skullBadgeIconSize = 10f;
    [Tooltip("Le badge crane deborde de l'avatar de tant, en bas a droite")]
    public float skullBadgeOverhang = 6f;
    public float weaponSlotSize = 28f;
    public float weaponIconSize = 20f;
    [Tooltip("Ombre portee du pictogramme d'arme (0 1px 1px noir 85 %)")]
    public float weaponShadowAlpha = 0.85f;
    [Tooltip("Pictogrammes du design system par famille d'arme (repli : weapon). Le DS n'a pour l'instant que 'weapon'")]
    public string laserIcon = "weapon";
    public string plasmaIcon = "weapon";
    public string railIcon = "weapon";
    public string teslaIcon = "weapon";
    public string fallbackWeaponIcon = "weapon";
    [Tooltip("Mort par l'arene : fleche vers le bas a cette opacite")]
    public float environmentArrowAlpha = 0.5f;
    [Tooltip("Fond de la tuile neutre (mort par l'arene)")]
    public Color environmentTileColor = new(0x2A / 255f, 0x2F / 255f, 0x38 / 255f, 1f);
    public float killfeedLife = 6f;
    public float killfeedFadeOut = 0.4f;
    public float killfeedEnter = 0.14f;

    [Header("Accessibilite")]
    [Tooltip("Mouvements reduits : fondus seulement (pas d'agrandissement ni de montee)")]
    public bool reducedMotion;

    // Reglage du theme ou preference du joueur / du systeme (BlockforgeTheme.ReduceMotion)
    public bool ReducedMotion => reducedMotion || BlockforgeTheme.ReduceMotion;

    public string WeaponIcon(BlockDefinition weapon)
    {
        if (weapon == null)
            return fallbackWeaponIcon;
        return WeaponFireController.Resolve(weapon).kind switch
        {
            WeaponFireController.Kind.Laser => laserIcon,
            WeaponFireController.Kind.Plasma => plasmaIcon,
            WeaponFireController.Kind.Rail => railIcon,
            WeaponFireController.Kind.Tesla => teslaIcon,
            _ => fallbackWeaponIcon,
        };
    }

    // ---------- Couleurs (jetons du theme hud) ----------
    public Color OutlineColor => BlockforgeTheme.Hud.SurfaceBase;   // #0C1017
    public Color HitColor => BlockforgeTheme.Hud.InkPrimary;        // blanc
    public Color WeakSpotColor => BlockforgeTheme.Hud.Warning;      // #FFBB55
    public Color KillColor => BlockforgeTheme.Hud.TeamEnemy;        // #FF8271
    public Color SoftShadowColor => new(0f, 0f, 0f, softShadowAlpha);

    public Stroke StrokeFor(CombatFeedbackHUD.HitKind kind) => kind switch
    {
        CombatFeedbackHUD.HitKind.Kill => kill,
        CombatFeedbackHUD.HitKind.WeakSpot => weakSpot,
        _ => hit,
    };

    public Color ColorFor(CombatFeedbackHUD.HitKind kind) => kind switch
    {
        CombatFeedbackHUD.HitKind.Kill => KillColor,
        CombatFeedbackHUD.HitKind.WeakSpot => WeakSpotColor,
        _ => HitColor,
    };

    // ---------- Chargement ----------
    private static CombatFeedbackTheme cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cached = null;

    public static CombatFeedbackTheme Load()
    {
        if (cached == null)
        {
            cached = Resources.Load<CombatFeedbackTheme>("CombatFeedbackTheme");
            if (cached == null)
            {
                cached = CreateInstance<CombatFeedbackTheme>();
                cached.name = "CombatFeedbackTheme (defaut)";
                cached.hideFlags = HideFlags.DontSave;
            }
        }
        return cached;
    }
}
