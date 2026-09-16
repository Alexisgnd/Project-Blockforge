using UnityEngine;

// =========================================================
// FABRIQUE DES VISUELS DE BLOCS
// Partagee entre l'apercu sur la pince, le ghost de pose et
// les blocs poses. Un bloc a modele (previewPrefab) est ajuste
// dans sa boite d'empreinte (BlockDefinition.footprint, en
// cases) puis plaque sur sa face d'ancrage ; le pivot du
// conteneur rendu est le centre de la case visee, la boite
// s'etendant autour selon BlockFootprint.MinOffset. Sans
// modele, un placeholder primitif d'une case est genere.
// =========================================================

public static class BlockPreviewFactory
{
    // Part de la boite laissee au modele. 1 = les blocs se touchent (les faces
    // communes de deux cubes sont internes, jamais visibles, donc pas de
    // z-fighting) ; un fill < 1 laisserait un creux visible entre voisins.
    public const float DefaultFill = 1f;

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

    // Cree le visuel d'un bloc (sans collider).
    // cellSize = taille monde d'une case ; fill = part de la boite occupee par
    // le modele ; centerOnPivot = boite centree sur le pivot (apercu sur la
    // pince) au lieu d'etre placee autour de la case visee (grille).
    // bottomDistance = distance du pivot au point le plus bas du visuel.
    public static GameObject CreateVisual(BlockDefinition def, float cellSize, out float bottomDistance,
                                          float fill = DefaultFill, bool centerOnPivot = false)
    {
        if (def.previewPrefab != null)
            return CreatePrefabVisual(def, cellSize, fill, centerOnPivot, out bottomDistance);

        var (primitive, scale) = PickShape(def.blockName);
        var go = GameObject.CreatePrimitive(primitive);

        var collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);

        float size = cellSize * fill;
        go.transform.localScale = scale * size;
        bottomDistance = HalfHeight(primitive, scale.y * size);

        var renderer = go.GetComponent<Renderer>();
        if (PlaceholderMaterial != null)
            renderer.sharedMaterial = PlaceholderMaterial;
        renderer.material.color = CategoryColor(def.category);
        return go;
    }

    // Instancie le vrai modele (FBX ou prefab variant) dans un conteneur :
    // - boite d'empreinte = footprint x cellSize, placee autour de la case visee
    //   (ou centree sur le pivot pour la pince) ;
    // - echelle uniforme maximale pour tenir dans la boite (moins la marge fill),
    //   ou echelle native 1 m = 1 case pour les tiges ;
    // - la face d'ancrage du modele est plaquee sur la face correspondante de
    //   la boite, le modele est centre sur les autres axes.
    private static GameObject CreatePrefabVisual(BlockDefinition def, float cellSize, float fill,
                                                 bool centerOnPivot, out float bottomDistance)
    {
        var wrapper = new GameObject($"Visual_{def.name}");
        var instance = Object.Instantiate(def.previewPrefab, wrapper.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        var box = BlockFootprint.LocalBox(def);
        Vector3 boxSize = box.size * cellSize;
        Vector3 boxMin = centerOnPivot ? -boxSize * 0.5f : box.min * cellSize;

        // Boite interieure : marge constante (1 - fill) / 2 case de chaque cote,
        // quelle que soit la taille de la boite (un rail de 6 cases garde le
        // meme jour avec ses voisins qu'un cube).
        Vector3 margin = Vector3.one * (cellSize * (1f - fill) * 0.5f);
        Vector3 innerMin = boxMin + margin;
        Vector3 innerSize = boxSize - margin * 2f;

        bottomDistance = -boxMin.y;

        // Bounds exacts calcules par le resync (sommets) si disponibles ; sinon
        // union des renderers, qui surestime les pieces tournees.
        Bounds bounds = def.modelBounds;
        if (bounds.size.sqrMagnitude < 1e-8f && !TryGetBounds(instance, out bounds))
            return wrapper;

        if (def.nativeScale)
        {
            // Echelle ET placement natifs : le pivot du FBX va au centre de la
            // boite, sans recentrage sur les bounds (les tiges sont modelisees
            // autour du centre de leur case, plaques en debord sur les faces).
            float native = cellSize * fill;
            Vector3 boxCenter = boxMin + boxSize * 0.5f;
            instance.transform.localScale = Vector3.one * native;
            instance.transform.localPosition = boxCenter;
            bottomDistance = -(boxCenter.y + bounds.min.y * native);
            return wrapper;
        }

        float scale = Mathf.Min(innerSize.x / Mathf.Max(bounds.size.x, 1e-4f),
                                innerSize.y / Mathf.Max(bounds.size.y, 1e-4f),
                                innerSize.z / Mathf.Max(bounds.size.z, 1e-4f));

        Vector3 scaledSize = bounds.size * scale;
        Vector3 targetMin = innerMin + (innerSize - scaledSize) * 0.5f; // centre par defaut
        switch (def.anchor)
        {
            case BlockAnchor.Bottom: targetMin.y = innerMin.y; break;
            case BlockAnchor.Right:  targetMin.x = innerMin.x + innerSize.x - scaledSize.x; break;
            case BlockAnchor.Left:   targetMin.x = innerMin.x; break;
            case BlockAnchor.Back:   targetMin.z = innerMin.z; break;
            case BlockAnchor.Center: break;
        }

        // Les bounds ont ete mesures avec l'instance a l'identite : apres mise a
        // l'echelle autour de son origine, son coin min vaut bounds.min * scale.
        instance.transform.localScale = Vector3.one * scale;
        instance.transform.localPosition = targetMin - bounds.min * scale;
        bottomDistance = -targetMin.y;
        return wrapper;
    }

    // Union des bounds monde des renderers d'une hierarchie (false si aucun)
    public static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        bounds = default;
        if (renderers.Length == 0)
            return false;

        bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);
        return true;
    }

    // Forme placeholder par nom de bloc (echelles en unites de case)
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
