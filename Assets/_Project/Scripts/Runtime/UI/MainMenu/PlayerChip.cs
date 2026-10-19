using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =========================================================
// PUCE JOUEUR DU HEADER (Right/Player du TopBar)
// Relie la puce serialisee (Avatar + Icon + Name) a la session :
// - pseudo = SessionService.DisplayName (Steam ou compte standard) ;
// - avatar Steam rond (live ou en cache) a la place du
//   pictogramme « user » quand il existe ;
// - pastille d'etat au coin de l'avatar : success = en ligne,
//   ink-muted = hors ligne, warning = invite ;
// - bouton « Reconnect » (secondary, pictogramme connection) a
//   gauche du bloc credits + puce, visible seulement hors ligne ;
//   « Sign in » en invite. Il retente la connexion (Steam ou
//   token) ; si un mot de passe est necessaire, retour a Boot.
//   Un clic sur la puce fait la meme chose (raccourci).
// Un toast resume l'etat au premier affichage hors ligne.
// =========================================================

public sealed class PlayerChip : MonoBehaviour
{
    private Image avatarImage;
    private Image avatarIcon;
    private Image photo;
    private Image statusDot;
    private TMP_Text nameText;
    private ButtonWidget reconnectButton;
    private bool reconnecting;
    private static bool offlineToastShown;

    public static PlayerChip Attach(Transform playerRoot)
    {
        if (playerRoot == null)
            return null;
        var chip = UIKit.GetOrAdd<PlayerChip>(playerRoot.gameObject);
        chip.Bind();
        return chip;
    }

    private void Bind()
    {
        var kit = UIKit.Dark;
        var avatar = transform.Find("Avatar");
        avatarImage = avatar != null ? avatar.GetComponent<Image>() : null;
        avatarIcon = avatar != null ? avatar.Find("Icon")?.GetComponent<Image>() : null;
        nameText = transform.Find("Name")?.GetComponent<TMP_Text>();

        if (avatarImage != null)
        {
            // Cadre rond qui decoupe la photo (Mask) ; le fond surface-inset reste visible
            avatarImage.sprite = DSAssets.Shape(DSRadius.Pill, "fill");
            avatarImage.type = Image.Type.Sliced;
            var mask = UIKit.GetOrAdd<Mask>(avatarImage.gameObject);
            mask.showMaskGraphic = true;

            var existing = avatar.Find("Photo");
            photo = existing != null ? existing.GetComponent<Image>() : kit.Image(avatar, "Photo", Color.white);
            photo.type = Image.Type.Simple;
            photo.preserveAspect = true;
            photo.raycastTarget = false;
            UIKit.Stretch(photo.rectTransform);
            photo.gameObject.SetActive(false);

            // Pastille d'etat, hors du masque (enfant de la puce, calee sur le coin de l'avatar)
            var dotT = transform.Find("Status");
            statusDot = dotT != null ? dotT.GetComponent<Image>() : kit.Shape(transform, "Status", DSRadius.Pill, kit.P.InkMuted);
            statusDot.raycastTarget = false;
            var dot = statusDot.rectTransform;
            var avatarRect = (RectTransform)avatar;
            dot.anchorMin = dot.anchorMax = avatarRect.anchorMin;
            dot.pivot = new Vector2(0.5f, 0.5f);
            float size = Mathf.Max(8f, Mathf.Round(avatarRect.sizeDelta.x * 0.32f));
            dot.sizeDelta = new Vector2(size, size);
            dot.anchoredPosition = avatarRect.anchoredPosition + new Vector2(avatarRect.sizeDelta.x - size * 0.5f, -avatarRect.sizeDelta.y * 0.5f + size * 0.5f);
            // Liseret de la couleur du fond de la puce pour detacher la pastille de l'avatar
            var ring = statusDot.transform.Find("Ring")?.GetComponent<Image>() ?? kit.Shape(statusDot.transform, "Ring", DSRadius.Pill, kit.P.SurfacePanel);
            UIKit.Stretch(ring.rectTransform, -2f, -2f, -2f, -2f);
            ring.transform.SetAsFirstSibling();
        }

        // La puce reste cliquable (raccourci de reconnexion)
        if (GetComponent<Image>() != null)
        {
            var button = UIKit.Clickable(this);
            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
        }

        CreateReconnectButton(kit);
        Refresh();
    }

    // Bouton explicite, frere du bloc de droite du header (Right) :
    // - Right avec LayoutGroup (TopBar.Build) : premier enfant de la rangee ;
    // - Right positionne a la main (header serialise de MainMenu) : colle a
    //   gauche du bloc, centre verticalement.
    private void CreateReconnectButton(UIKit kit)
    {
        var right = transform.parent as RectTransform;
        if (right == null)
            return;

        var existing = right.parent != null ? right.parent.Find("Reconnect") : null;
        if (existing == null)
            existing = right.Find("Reconnect");
        if (existing != null)
            Destroy(existing.gameObject);

        bool hasLayout = right.GetComponent<LayoutGroup>() != null;
        reconnectButton = kit.Button(hasLayout ? right : right.parent, "Reconnect", ButtonVariant.Secondary,
                                     "Reconnect", OnClicked, "connection", BlockforgeTheme.ControlMd);
        if (hasLayout)
        {
            reconnectButton.Rect.SetAsFirstSibling();
        }
        else
        {
            // Meme parent que Right : ancre sur le bord gauche de Right
            var rt = reconnectButton.Rect;
            rt.anchorMin = rt.anchorMax = right.anchorMin;
            rt.pivot = new Vector2(1f, 0.5f);
            float rightLeftEdge = right.anchoredPosition.x - right.sizeDelta.x * right.pivot.x;
            rt.anchoredPosition = new Vector2(rightLeftEdge - BlockforgeTheme.Space3, right.anchoredPosition.y);
        }
        reconnectButton.Rect.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        SessionService.StateChanged += OnStateChanged;
        SessionService.IdentityChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        SessionService.StateChanged -= OnStateChanged;
        SessionService.IdentityChanged -= Refresh;
    }

    private void OnStateChanged(SessionState state) => Refresh();

    private void Refresh()
    {
        if (this == null)
            return;
        var kit = UIKit.Dark;

        if (nameText != null)
            nameText.text = SessionService.DisplayName;

        var sprite = SessionService.Avatar;
        if (photo != null)
        {
            photo.sprite = sprite;
            photo.gameObject.SetActive(sprite != null);
        }
        if (avatarIcon != null)
            avatarIcon.gameObject.SetActive(sprite == null);

        if (statusDot != null)
        {
            statusDot.color = SessionService.State switch
            {
                SessionState.Online => kit.P.Success,
                SessionState.Guest => kit.P.Warning,
                SessionState.Connecting => kit.P.Accent,
                _ => kit.P.InkMuted,
            };
        }

        RefreshReconnectButton();

        if (!offlineToastShown && (SessionService.State == SessionState.Offline || SessionService.State == SessionState.Guest))
        {
            offlineToastShown = true;
            Toast.Info(SessionService.State == SessionState.Guest
                ? "Playing as a guest: your robots are saved on this computer. Use Sign in to go online."
                : "You're offline: your robots are saved on this computer. Use Reconnect to go back online.");
        }
    }

    private void RefreshReconnectButton()
    {
        if (reconnectButton == null || reconnectButton.Rect == null)
            return;

        var state = SessionService.State;
        bool show = reconnecting || state == SessionState.Offline || state == SessionState.Guest || state == SessionState.Connecting;
        reconnectButton.Rect.gameObject.SetActive(show);
        if (!show)
            return;

        string label = reconnecting || state == SessionState.Connecting ? "Reconnecting…"
                     : state == SessionState.Guest ? "Sign in"
                     : "Reconnect";
        if (reconnectButton.Text != label)
        {
            reconnectButton.Text = label;
            // Le bouton garde la largeur de son libelle
            reconnectButton.Label.ForceMeshUpdate(true, true);
            float padX = BlockforgeTheme.Space4;
            float width = UIKit.PreferredWidth(reconnectButton.Label) + 2f * padX
                          + (reconnectButton.Icon != null ? BlockforgeTheme.IconSize.Lg + BlockforgeTheme.Space2 : 0f);
            reconnectButton.Rect.sizeDelta = new Vector2(width, reconnectButton.Rect.sizeDelta.y);
            UIKit.Fixed(reconnectButton.Rect, width, reconnectButton.Rect.sizeDelta.y);
        }
        reconnectButton.Interactable = !(reconnecting || state == SessionState.Connecting);
    }

    private async void OnClicked()
    {
        if (reconnecting)
            return;
        if (SessionService.IsOnline)
        {
            Toast.Info($"Signed in as {SessionService.DisplayName}.");
            return;
        }

        reconnecting = true;
        RefreshReconnectButton();
        var toast = Toast.Loading("Reconnecting…");
        try
        {
            bool ok = await SessionService.TryReconnectAsync();
            if (ok)
            {
                toast.Success($"Back online. Welcome back, {SessionService.DisplayName}.");
                return;
            }

            if (SessionService.Identity != null && SessionService.Identity.IsSteam)
            {
                toast.Warning(SteamBootstrap.IsLoggedOn
                    ? "Still offline. The server can't be reached right now."
                    : "Steam is in offline mode. Go online in Steam, then try again.");
                return;
            }

            // Compte standard sans token (ou invite) : il faut l'ecran de connexion
            toast.Info("Sign in to go online.");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(AuthGuard.BootSceneName);
        }
        finally
        {
            reconnecting = false;
            if (this != null)
                RefreshReconnectButton();
        }
    }
}
