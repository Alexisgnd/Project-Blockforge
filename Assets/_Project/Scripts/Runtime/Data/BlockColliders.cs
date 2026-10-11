using UnityEngine;

// =========================================================
// COLLIDER D'EMPREINTE D'UN BLOC
// Un bloc miroir a une racine a echelle negative (image miroir
// vraie des meshes). Un BoxCollider n'accepte pas d'echelle
// negative (warning "BoxCollider does not support negative
// scale or size" a chaque bloc miroir). Dans ce cas le collider
// va sur un enfant "Collider" dont l'echelle annule le miroir :
// son echelle monde redevient positive, et le centre de la
// boite est reflechi pour garder exactement la meme position.
// Les raycasts remontent au PlacedBlockView par
// GetComponentInParent : rien ne change pour le gameplay.
// =========================================================

public static class BlockColliders
{
    public const string ChildName = "Collider";

    public static BoxCollider AddFootprint(Transform root, Vector3 center, Vector3 size)
    {
        var scale = root.localScale;
        var sign = new Vector3(Mathf.Sign(scale.x), Mathf.Sign(scale.y), Mathf.Sign(scale.z));
        if (sign == Vector3.one)
        {
            var direct = root.gameObject.AddComponent<BoxCollider>();
            direct.center = center;
            direct.size = size;
            return direct;
        }

        var child = new GameObject(ChildName);
        child.layer = root.gameObject.layer;
        child.transform.SetParent(root, false);
        child.transform.localScale = sign; // annule le miroir de la racine
        var collider = child.AddComponent<BoxCollider>();
        collider.center = Vector3.Scale(center, sign);
        collider.size = size;
        return collider;
    }

    // Visuel d'un bloc : le premier enfant qui n'est pas le collider
    public static Transform Visual(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.name != ChildName)
                return child;
        }
        return null;
    }
}
