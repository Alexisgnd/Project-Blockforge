using UnityEngine;

// =========================================================
// FABRIQUE DES VISUELS PLACEHOLDER DE BLOCS
// Partagee entre l'apercu sur la pince, le ghost de pose
// et les blocs poses. Sera remplacee par les vrais modeles
// via BlockDefinition.previewPrefab.
// =========================================================

public static class BlockPreviewFactory
{
    // CreatePrimitive assigne le materiau par defaut du pipeline, qui n'existe
    // que dans l'editeur avec HDRP (blocs roses en build) : on force un materiau
    // embarque via Resources.
    private static Material placeholderMaterial;

    private static Material PlaceholderMaterial
    {
        get
        {
            if (placeholderMaterial == null)
                placeholderMaterial = Resources.Load<Material>("BlockPlaceholder");
            return placeholderMaterial;
        }
    }

    // Cree le visuel d'un bloc (sans collider). unitSize = taille monde d'un bloc 1x1x1.
    public static GameObject CreateVisual(BlockDefinition def, float unitSize, out float halfHeight)
    {
        if (def.previewPrefab != null)
            return CreatePrefabVisual(def, unitSize, out halfHeight);

        var (primitive, scale) = PickShape(def.blockName);
        var go = GameObject.CreatePrimitive(primitive);

        var collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);

        go.transform.localScale = scale * unitSize;
        halfHeight = HalfHeight(primitive, scale.y * unitSize);

        var renderer = go.GetComponent<Renderer>();
        if (PlaceholderMaterial != null)
            renderer.sharedMaterial = PlaceholderMaterial;
        renderer.material.color = CategoryColor(def.category);
        return go;
    }

    // Instancie le vrai modele (FBX) dans un conteneur dont le pivot est le
    // centre de la cellule : le modele est mis a l'echelle uniforme pour tenir
    // dans unitSize, centre horizontalement et pose au fond de la cellule
    // (base a -unitSize/2), comme un bloc plein.
    private static GameObject CreatePrefabVisual(BlockDefinition def, float unitSize, out float halfHeight)
    {
        var wrapper = new GameObject($"Visual_{def.name}");
        var instance = Object.Instantiate(def.previewPrefab, wrapper.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        var renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = maxDim > 0.0001f ? unitSize / maxDim : 1f;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = new Vector3(
                -bounds.center.x * scale,
                -unitSize * 0.5f - bounds.min.y * scale,
                -bounds.center.z * scale);
        }

        halfHeight = unitSize * 0.5f;
        return wrapper;
    }

    // Forme placeholder par nom de bloc (echelles en unites de unitSize)
    public static (PrimitiveType, Vector3) PickShape(string blockName)
    {
        string n = blockName.ToLowerInvariant();

        if (n.Contains("tige")) return (PrimitiveType.Cylinder, new Vector3(0.12f, 0.6f, 0.12f));
        if (n.Contains("roue")) return (PrimitiveType.Cylinder, new Vector3(0.9f, 0.1f, 0.9f));
        if (n.Contains("chenille")) return (PrimitiveType.Cube, new Vector3(1.4f, 0.35f, 0.5f));
        if (n.Contains("patte")) return (PrimitiveType.Capsule, new Vector3(0.25f, 0.5f, 0.25f));
        if (n.Contains("survol")) return (PrimitiveType.Cube, new Vector3(1.1f, 0.12f, 0.6f));
        if (n.Contains("rotor")) return (PrimitiveType.Cylinder, new Vector3(1.2f, 0.05f, 1.2f));
        if (n.Contains("aile")) return (PrimitiveType.Cube, new Vector3(1.5f, 0.08f, 0.7f));
        if (n.Contains("propulseur")) return (PrimitiveType.Cylinder, new Vector3(0.35f, 0.45f, 0.35f));
        if (n.Contains("laser")) return (PrimitiveType.Cube, new Vector3(1.3f, 0.22f, 0.22f));
        if (n.Contains("plasma")) return (PrimitiveType.Capsule, new Vector3(0.3f, 0.55f, 0.3f));
        if (n.Contains("canon")) return (PrimitiveType.Cube, new Vector3(1.5f, 0.3f, 0.3f));
        if (n.Contains("tesla")) return (PrimitiveType.Cube, new Vector3(0.9f, 0.5f, 0.08f));
        if (n.Contains("nano")) return (PrimitiveType.Sphere, new Vector3(0.6f, 0.6f, 0.6f));
        if (n.Contains("blindage")) return (PrimitiveType.Cube, new Vector3(1f, 0.15f, 1f));
        if (n.Contains("radar")) return (PrimitiveType.Sphere, new Vector3(0.5f, 0.5f, 0.5f));
        if (n.Contains("bouclier") || n.Contains("disque")) return (PrimitiveType.Cylinder, new Vector3(0.7f, 0.05f, 0.7f));
        if (n.Contains("pente")) return (PrimitiveType.Cube, new Vector3(1f, 0.5f, 1f));
        if (n.Contains("coin")) return (PrimitiveType.Cube, new Vector3(0.55f, 0.55f, 0.55f));
        if (n.Contains("intérieur") || n.Contains("interieur")) return (PrimitiveType.Cube, new Vector3(0.9f, 0.9f, 0.9f));

        return (PrimitiveType.Cube, Vector3.one); // Cube et defaut
    }

    // Demi-hauteur monde selon la primitive (cylindre/capsule font 2 unites de haut)
    public static float HalfHeight(PrimitiveType primitive, float scaleY)
    {
        return primitive switch
        {
            PrimitiveType.Cylinder => scaleY,
            PrimitiveType.Capsule => scaleY,
            _ => scaleY * 0.5f,
        };
    }

    public static Color CategoryColor(BlockCategory category)
    {
        return category switch
        {
            BlockCategory.Chassis => new Color(0.75f, 0.78f, 0.82f),
            BlockCategory.Mouvement => new Color(1.00f, 0.62f, 0.20f),
            BlockCategory.Armes => new Color(0.95f, 0.30f, 0.30f),
            BlockCategory.Defense => new Color(0.35f, 0.85f, 0.50f),
            BlockCategory.Special => new Color(0.30f, 0.75f, 1.00f),
            _ => Color.white,
        };
    }
}
