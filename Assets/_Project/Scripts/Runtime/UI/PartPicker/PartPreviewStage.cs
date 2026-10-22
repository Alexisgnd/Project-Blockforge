using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// =========================================================
// APERCU 3D TOURNANT DU PART PICKER (render texture)
// Le prefab de la piece est instancie loin sous la scene, sur
// le calque reserve aux apercus (RobotThumbnailRenderer.Layer) :
// une camera dediee ne voit que lui et le rend dans une texture
// affichee par la colonne detail ; la camera du jeu ne le voit
// jamais. Il tourne sur lui-meme (theme : degres par seconde).
// - Fond : couleur claire a alpha nul -> le degrade de l'UI
//   transparait si le tampon couleur HDRP garde l'alpha
//   (R16G16B16A16) ; sinon la couleur de repli lue dans le USS
//   (--pp-preview-backdrop) remplit le fond.
// - Pas de flou de mouvement, de bloom ni de profondeur de champ
//   (meme reglage que les vignettes de robots).
// - Aucun script de la piece ne tourne : seuls les rendus et les
//   Animator (chenilles, radar) restent actifs.
// =========================================================

public sealed class PartPreviewStage
{
    private static readonly Vector3 StagePosition = new(0f, -4000f, 0f);
    private static readonly Vector3 ViewDirection = new Vector3(0.62f, 0.38f, 1f).normalized;
    private const float FieldOfView = 24f;
    private const float Margin = 1.08f;

    private Camera camera;
    private GameObject instance;
    private Transform pivot;
    private RenderTexture texture;
    private GameObject currentPrefab;

    public RenderTexture Texture => texture;
    public bool HasModel => instance != null;

    public void Show(GameObject prefab, int width, int height, Color backdrop)
    {
        EnsureTexture(width, height);
        EnsureCamera(backdrop);
        if (prefab == currentPrefab && instance != null)
            return;

        ClearModel();
        currentPrefab = prefab;
        if (prefab == null)
            return;

        pivot = new GameObject("PartPreviewPivot") { hideFlags = HideFlags.HideAndDontSave }.transform;
        pivot.position = StagePosition;

        // Instancie inactif : les scripts sont coupes avant l'activation
        instance = Object.Instantiate(prefab, pivot);
        instance.SetActive(false);
        instance.hideFlags = HideFlags.HideAndDontSave;
        foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
            body.isKinematic = true;
        SetLayer(instance.transform, RobotThumbnailRenderer.Layer);
        instance.SetActive(true);

        // Recentre le modele sur le pivot
        if (TryGetBounds(instance, out var bounds))
        {
            instance.transform.position += StagePosition - bounds.center;
            Frame(bounds);
        }
    }

    public void Spin(float degrees)
    {
        if (pivot != null)
            pivot.Rotate(Vector3.up, degrees, Space.World);
    }

    public void SetActive(bool active)
    {
        if (camera != null)
            camera.enabled = active && instance != null;
    }

    public void Dispose()
    {
        ClearModel();
        if (camera != null)
            Object.Destroy(camera.gameObject);
        camera = null;
        if (texture != null)
        {
            texture.Release();
            Object.Destroy(texture);
        }
        texture = null;
    }

    private void ClearModel()
    {
        currentPrefab = null;
        if (pivot != null)
            Object.Destroy(pivot.gameObject);
        pivot = null;
        instance = null;
        if (camera != null)
            camera.enabled = false;
    }

    private void EnsureTexture(int width, int height)
    {
        width = Mathf.Clamp(width, 64, 2048);
        height = Mathf.Clamp(height, 64, 2048);
        if (texture != null && texture.width == width && texture.height == height)
            return;
        if (texture != null)
        {
            texture.Release();
            Object.Destroy(texture);
        }
        texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "PartPreview",
            antiAliasing = 1,
            useMipMap = false,
            filterMode = FilterMode.Bilinear,
        };
        texture.Create();
        if (camera != null)
        {
            camera.targetTexture = texture;
            camera.aspect = (float)width / height;
        }
    }

    private void EnsureCamera(Color backdrop)
    {
        if (camera == null)
        {
            var go = new GameObject("PartPreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << RobotThumbnailRenderer.Layer;
            camera.fieldOfView = FieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;

            var hd = go.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            hd.customRenderingSettings = true;
            foreach (var field in new[]
                     {
                         FrameSettingsField.MotionBlur, FrameSettingsField.MotionVectors, FrameSettingsField.ObjectMotionVectors,
                         FrameSettingsField.DepthOfField, FrameSettingsField.Bloom, FrameSettingsField.ChromaticAberration,
                         FrameSettingsField.FilmGrain, FrameSettingsField.LensDistortion, FrameSettingsField.Vignette,
                     })
            {
                hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
                hd.renderingPathCustomFrameSettings.SetEnabled(field, false);
            }
            go.SetActive(true);
            if (texture != null)
            {
                camera.targetTexture = texture;
                camera.aspect = (float)texture.width / texture.height;
            }
        }

        var clear = new Color(backdrop.r, backdrop.g, backdrop.b, 0f);
        camera.backgroundColor = clear;
        if (camera.TryGetComponent<HDAdditionalCameraData>(out var data))
        {
            var linear = backdrop.linear;
            data.backgroundColorHDR = new Color(linear.r, linear.g, linear.b, 0f);
        }
    }

    // Distance minimale pour que les 8 coins de la boite rentrent dans le cadre,
    // quelle que soit l'orientation pendant la rotation (rayon de la sphere)
    private void Frame(Bounds bounds)
    {
        float radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
        float tanV = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * camera.aspect;
        float distance = radius / Mathf.Min(tanV, tanH) * Margin;
        var look = Quaternion.LookRotation(-ViewDirection, Vector3.up);
        camera.transform.SetPositionAndRotation(StagePosition + ViewDirection * distance, look);
        camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 1.5f);
        camera.farClipPlane = distance + radius * 1.5f;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayer(child, layer);
    }

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return any;
    }
}
