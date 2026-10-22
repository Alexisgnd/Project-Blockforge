using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// KILLFEED DU MATCH PAR EQUIPES (design system, theme hud)
// Pose par TdmHud dans sa couche de combat : cachee pendant le
// compte a rebours, masquee en fin de match avec le reste du HUD.
// Bord droit de l'ecran, sous le roster ennemi (40 px du bord,
// 400 px du haut a 1080 p), lignes alignees a droite, 6 px
// d'ecart, la plus recente en haut, 5 au plus.
// Une ligne = avatar du tueur -> pictogramme de l'arme -> avatar
// de la victime. Aucun nom, aucun texte.
// - Plaque surface-hud, coin haut droit chanfreine (10 px) ;
//   teinte plate-self si la ligne concerne le joueur local.
// - Avatar 36 px, bordure 2 px a la couleur d'equipe (accent pour
//   le joueur local) : vignette du robot (KillfeedAvatars), ou
//   repli colore deterministe + picto user.
// - Victime : vignette en niveaux de gris (70 %) + badge crane
//   16 px en bas a droite, qui deborde de 6 px : c'est le badge
//   qui dit qui est mort, pas la couleur.
// - Arme : picto blanc 20 px dans 28 px, ombre portee ; table
//   famille -> picto dans CombatFeedbackTheme (repli : weapon).
// - Arene : tuile neutre (warning) + fleche vers le bas a 50 %.
//   Auto-destruction : picto danger puis la victime seule.
// Animation : entree depuis le bord droit (140 ms, ease-out), les
// autres descendent ; vie 6 s puis fondu 400 ms ; une 6e ligne
// fait s'effacer tout de suite la plus ancienne. Mouvements
// reduits : fondus seulement.
// =========================================================

public class TdmKillfeed : MonoBehaviour
{
    public static TdmKillfeed Active { get; private set; }

    private TdmMatch match;
    private CombatFeedbackTheme theme;
    private UIKit kit;
    private RectTransform area;

    private sealed class Line
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float width, height;
        public float born, fadeStart = -1f;
        public float enterStart;
        public float fromY, toY, moveStart, y;
    }

    // La plus recente en premier
    private readonly List<Line> lines = new();

    public TdmMatch Match => match;

    public static TdmKillfeed Create(UIKit kit, RectTransform parent, TdmMatch match)
    {
        var theme = CombatFeedbackTheme.Load();
        var rt = kit.Rect(parent, "Killfeed");
        UIKit.Place(rt, UIKit.TopRight, new Vector2(-theme.killfeedRight, -theme.killfeedTop), new Vector2(600f, 600f));
        var feed = rt.gameObject.AddComponent<TdmKillfeed>();
        feed.kit = kit;
        feed.match = match;
        feed.theme = theme;
        feed.area = rt;
        return feed;
    }

    private void OnEnable()
    {
        CombatEvents.Kill += Push;
        Active = this;
    }

    private void OnDisable()
    {
        CombatEvents.Kill -= Push;
        if (Active == this)
            Active = null;
    }

    private void Start()
    {
        // Vignettes des robots pendant le compte a rebours (une par image)
        StartCoroutine(KillfeedAvatars.Prepare(match, theme));
    }

    private void OnDestroy()
    {
        KillfeedAvatars.Clear();
    }

    // =========================================================
    // LIGNES
    // =========================================================

    public void Push(KillEvent e)
    {
        if (area == null || e.victim == null)
            return;
        float now = Time.unscaledTime;
        // Deja plein : la plus ancienne s'efface tout de suite
        int alive = 0;
        foreach (var l in lines)
        {
            if (l.fadeStart < 0f)
                alive++;
        }
        if (alive >= Mathf.Max(1, theme.killfeedMaxLines))
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i].fadeStart < 0f)
                {
                    lines[i].fadeStart = now;
                    break;
                }
            }
        }

        var line = BuildLine(e);
        line.born = now;
        line.enterStart = now;
        line.y = line.fromY = line.toY = 0f;
        line.moveStart = now;
        lines.Insert(0, line);
        Relayout(now);
    }

    private void Relayout(float now)
    {
        float y = 0f;
        foreach (var l in lines)
        {
            if (!Mathf.Approximately(l.toY, -y))
            {
                l.fromY = l.y;
                l.toY = -y;
                l.moveStart = now;
            }
            y += l.height + theme.killfeedLineGap;
        }
    }

    private Line BuildLine(KillEvent e)
    {
        var P = kit.P;
        var pad = theme.killfeedPadding;   // haut, bas, gauche, droite
        float avatar = theme.avatarSize;
        float gap = theme.killfeedElementGap;
        float height = pad.x + avatar + pad.y;

        var local = match != null ? match.Local : null;
        bool mine = (e.killer != null && e.killer.IsLocal) || e.victim.IsLocal;

        var plate = ChamferedPlate.Build(kit, area, "KillLine", PlateCorners.TopRight, theme.killfeedChamfer, P.SurfaceHud, P.PlateSelf);
        // Ligne du joueur local : teinte plate-self en plus ; sinon pas de teinte
        if (mine)
            plate.Tint.SetColors(P.PlateSelf, P.PlateSelf);
        else
            plate.Tint.SetColors(BlockforgeTheme.WithAlpha(P.PlateSelf, 0f), BlockforgeTheme.WithAlpha(P.PlateSelf, 0f));

        float x = pad.z;
        switch (e.cause)
        {
            case KillCause.Environment:
                NeutralTile(plate.Content, x, pad.x);
                x += avatar + gap;
                WeaponSlot(plate.Content, x, pad.x, "arrow-down", theme.environmentArrowAlpha);
                x += theme.weaponSlotSize + gap;
                break;
            case KillCause.SelfDestruct:
                WeaponSlot(plate.Content, x, pad.x, "danger", 1f);
                x += theme.weaponSlotSize + gap;
                break;
            default:
                if (e.killer != null)
                {
                    AvatarTile(plate.Content, x, pad.x, e.killer, local, false);
                    x += avatar + gap;
                }
                WeaponSlot(plate.Content, x, pad.x, theme.WeaponIcon(e.weapon), 1f);
                x += theme.weaponSlotSize + gap;
                break;
        }
        AvatarTile(plate.Content, x, pad.x, e.victim, local, true);
        x += avatar;
        float width = x + pad.w;

        UIKit.Place(plate.Rect, UIKit.TopRight, Vector2.zero, new Vector2(width, height));
        var line = new Line
        {
            rect = plate.Rect,
            group = kit.Group(plate.Rect),
            width = width,
            height = height,
        };
        line.group.interactable = false;
        line.group.blocksRaycasts = false;
        return line;
    }

    // Avatar : bordure d'equipe 2 px, vignette du robot (ou repli), crane pour la victime
    private void AvatarTile(RectTransform parent, float x, float top, TdmParticipant p, TdmParticipant local, bool victim)
    {
        var P = kit.P;
        float size = theme.avatarSize;
        bool ally = local != null ? p.Side == local.Side : p.Side == 0;
        Color border = p.IsLocal ? P.Accent : ally ? P.TeamAlly : P.TeamEnemy;

        var frame = kit.Shape(parent, victim ? "Victim" : "Killer", DSRadius.Xs, border);
        UIKit.Place(frame.rectTransform, UIKit.TopLeft, new Vector2(x, -top), new Vector2(size, size));

        // Interieur arrondi (masque) : l'image ne deborde pas des coins
        float inner = size - 2f * theme.avatarBorder;
        var mask = kit.Shape(frame.rectTransform, "Mask", DSRadius.Xs, Color.white);
        UIKit.Place(mask.rectTransform, UIKit.Center, Vector2.zero, new Vector2(inner, inner));
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        Sprite profile = p.IsLocal && theme.localUsesProfilePicture ? SessionService.Avatar : null;
        Texture2D thumb = profile == null ? KillfeedAvatars.Get(p, victim) : null;
        if (profile != null)
        {
            var img = kit.Image(mask.rectTransform, "Picture", Color.white, profile);
            img.preserveAspect = false;
            UIKit.Stretch(img.rectTransform);
        }
        else if (thumb != null)
        {
            var raw = kit.Rect(mask.rectTransform, "Picture").gameObject.AddComponent<RawImage>();
            raw.texture = thumb;
            raw.raycastTarget = false;
            UIKit.Stretch(raw.rectTransform);
        }
        else
        {
            // Repli : fond colore deterministe + picto user blanc a 75 %
            Color bg = KillfeedAvatars.FallbackColor(p);
            Color icon = BlockforgeTheme.WithAlpha(P.InkPrimary, theme.avatarFallbackIconAlpha);
            if (victim)
            {
                bg = Gray(bg, theme.victimBrightness);
                icon = Gray(icon, theme.victimBrightness);
            }
            var fill = kit.Image(mask.rectTransform, "Fallback", bg);
            UIKit.Stretch(fill.rectTransform);
            var user = kit.Icon(mask.rectTransform, "User", "user", theme.avatarFallbackIconSize, icon);
            UIKit.Place(user.rectTransform, UIKit.Center, Vector2.zero, new Vector2(theme.avatarFallbackIconSize, theme.avatarFallbackIconSize));
        }

        if (victim)
        {
            // Badge crane en bas a droite, deborde de l'avatar vers l'exterieur
            var badge = kit.Image(frame.rectTransform, "SkullBadge", theme.OutlineColor, HudShapes.Disc());
            UIKit.Place(badge.rectTransform, UIKit.BottomRight, new Vector2(theme.skullBadgeOverhang, -theme.skullBadgeOverhang),
                        new Vector2(theme.skullBadgeSize, theme.skullBadgeSize));
            var skull = kit.Icon(badge.rectTransform, "Skull", "skull", theme.skullBadgeIconSize, P.InkPrimary);
            UIKit.Place(skull.rectTransform, UIKit.Center, Vector2.zero, new Vector2(theme.skullBadgeIconSize, theme.skullBadgeIconSize));
        }
    }

    // Tuile neutre a la place du tueur (mort par l'arene)
    private void NeutralTile(RectTransform parent, float x, float top)
    {
        var P = kit.P;
        float size = theme.avatarSize;
        var frame = kit.Shape(parent, "Arena", DSRadius.Xs, P.TeamNeutral);
        UIKit.Place(frame.rectTransform, UIKit.TopLeft, new Vector2(x, -top), new Vector2(size, size));
        float inner = size - 2f * theme.avatarBorder;
        var fill = kit.Shape(frame.rectTransform, "Fill", DSRadius.Xs, theme.environmentTileColor);
        UIKit.Place(fill.rectTransform, UIKit.Center, Vector2.zero, new Vector2(inner, inner));
        var icon = kit.Icon(fill.rectTransform, "Warning", "warning", theme.avatarFallbackIconSize, P.Warning);
        UIKit.Place(icon.rectTransform, UIKit.Center, Vector2.zero, new Vector2(theme.avatarFallbackIconSize, theme.avatarFallbackIconSize));
    }

    // Emplacement de l'arme : picto blanc centre, ombre portee 1 px vers le bas
    private void WeaponSlot(RectTransform parent, float x, float top, string iconName, float alpha)
    {
        var P = kit.P;
        float slot = theme.weaponSlotSize;
        float size = theme.weaponIconSize;
        var rt = kit.Rect(parent, "Weapon");
        UIKit.Place(rt, UIKit.TopLeft, new Vector2(x, -(top + (theme.avatarSize - slot) * 0.5f)), new Vector2(slot, slot));
        var shadow = kit.Icon(rt, "Shadow", iconName, size, new Color(0f, 0f, 0f, theme.weaponShadowAlpha * alpha));
        UIKit.Place(shadow.rectTransform, UIKit.Center, new Vector2(0f, -1f), new Vector2(size, size));
        var icon = kit.Icon(rt, "Icon", iconName, size, BlockforgeTheme.WithAlpha(P.InkPrimary, alpha));
        UIKit.Place(icon.rectTransform, UIKit.Center, Vector2.zero, new Vector2(size, size));
    }

    private static Color Gray(Color c, float brightness)
    {
        float g = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * brightness;
        return new Color(g, g, g, c.a);
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private void Update()
    {
        if (lines.Count == 0)
            return;
        float now = Time.unscaledTime;
        bool reduced = theme.ReducedMotion;
        float enter = Mathf.Max(0.001f, theme.killfeedEnter);
        bool removed = false;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var l = lines[i];
            if (l.fadeStart < 0f && now - l.born >= theme.killfeedLife)
                l.fadeStart = now;

            float alpha = 1f;
            if (l.fadeStart >= 0f)
            {
                float f = (now - l.fadeStart) / Mathf.Max(0.001f, theme.killfeedFadeOut);
                if (f >= 1f)
                {
                    Destroy(l.rect.gameObject);
                    lines.RemoveAt(i);
                    removed = true;
                    continue;
                }
                alpha = 1f - f;
            }

            float e = Mathf.Clamp01((now - l.enterStart) / enter);
            float slide = 0f;
            if (reduced)
                alpha *= e;   // mouvements reduits : la ligne apparait en fondu
            else
                slide = (1f - EaseOut(e)) * (l.width + theme.killfeedRight);

            float m = Mathf.Clamp01((now - l.moveStart) / enter);
            l.y = reduced ? l.toY : Mathf.Lerp(l.fromY, l.toY, EaseOut(m));
            l.rect.anchoredPosition = new Vector2(slide, l.y);
            l.group.alpha = alpha;
        }
        if (removed)
            Relayout(now);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    // =========================================================
    // APERCU (menu de debug de l'editeur)
    // =========================================================

    public enum PreviewCase { AllyKills, EnemyKills, IKill, IDie, Arena, SelfDestruct }

    public void Preview(PreviewCase which)
    {
        if (match == null || match.Participants.Count == 0)
            return;
        var local = match.Local;
        int side = local != null ? local.Side : 0;
        TdmParticipant allyBot = null, enemyA = null, enemyB = null;
        foreach (var p in match.Participants)
        {
            if (p == local)
                continue;
            if (p.Side == side) { if (allyBot == null) allyBot = p; }
            else if (enemyA == null) enemyA = p;
            else if (enemyB == null) enemyB = p;
        }
        var me = local ?? allyBot;
        enemyB ??= enemyA;
        var e = which switch
        {
            PreviewCase.AllyKills => new KillEvent { killer = allyBot, victim = enemyA, cause = KillCause.Weapon },
            PreviewCase.EnemyKills => new KillEvent { killer = enemyA, victim = allyBot, cause = KillCause.Weapon },
            PreviewCase.IKill => new KillEvent { killer = me, victim = enemyB, cause = KillCause.Weapon },
            PreviewCase.IDie => new KillEvent { killer = enemyA, victim = me, cause = KillCause.Weapon },
            PreviewCase.Arena => new KillEvent { killer = null, victim = enemyA, cause = KillCause.Environment },
            _ => new KillEvent { killer = null, victim = me, cause = KillCause.SelfDestruct },
        };
        if (e.victim == null)
            return;
        Push(e);
    }
}
