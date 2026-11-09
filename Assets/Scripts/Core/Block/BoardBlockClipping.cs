using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Clips world-space HP labels to the same authored mask as the block sprites.</summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class BoardBlockClipping : MonoBehaviour
{
    private static readonly int ClipRect = Shader.PropertyToID("_ClipRect");
    private static readonly int SoftnessX = Shader.PropertyToID("_MaskSoftnessX");
    private static readonly int SoftnessY = Shader.PropertyToID("_MaskSoftnessY");
    private static readonly Dictionary<Material, ClipMaterial> materials = new Dictionary<Material, ClipMaterial>();

    private sealed class ClipMaterial
    {
        public Material material;
        public int references;
    }

    private sealed class Label
    {
        public TextMeshPro text;
        public Renderer renderer;
        public Material original;
        public Material clipped;
        public Matrix4x4 worldToLocal;
        public bool clipInitialized;
    }

    private readonly List<Label> labels = new List<Label>();
    private readonly Vector3[] corners = new Vector3[4];
    private MaterialPropertyBlock properties;
    private SpriteMask boardMask;
    private Sprite cachedMaskSprite;
    private Matrix4x4 maskLocalToWorld;
    private bool cornersInitialized;

    public static void Bind(Block block)
    {
        if (block == null) return;
        GridManager manager = block.GetComponentInParent<GridManager>();
        if (manager == null) return;
        SpriteMask mask = manager.GetComponent<SpriteMask>();
        if (mask == null || !mask.enabled || mask.sprite == null) return;

        var clipping = block.GetComponent<BoardBlockClipping>();
        if (clipping == null) clipping = block.gameObject.AddComponent<BoardBlockClipping>();
        clipping.boardMask = mask;
        DualBlockSpriteMask.Apply(block);
        if (clipping.labels.Count == 0) clipping.PrepareLabels(block);
        clipping.RefreshClipping();
    }

    private void PrepareLabels(Block block)
    {
        foreach (TextMeshPro text in block.GetComponentsInChildren<TextMeshPro>(true))
        {
            Material original = text.fontSharedMaterial;
            if (original == null || !original.HasProperty(ClipRect)) continue;
            if (!materials.TryGetValue(original, out ClipMaterial entry))
            {
                entry = new ClipMaterial
                {
                    material = new Material(original)
                    {
                        name = original.name + " (Board Clip)",
                        hideFlags = HideFlags.HideAndDontSave
                    }
                };
                // The current TMP mobile shader uses the UI clip keyword, not the legacy SetMask API.
                entry.material.EnableKeyword("UNITY_UI_CLIP_RECT");
                materials.Add(original, entry);
            }
            entry.references++;
            labels.Add(new Label { text = text, renderer = text.renderer, original = original, clipped = entry.material });
            text.fontSharedMaterial = entry.material;
        }
    }

    private void LateUpdate() => RefreshClipping(false);

    // TMP mesh regeneration explicitly reapplies the property block even when
    // the label did not move. The regular frame update only handles movement.
    public void RefreshClipping() => RefreshClipping(true);

    private void RefreshClipping(bool force)
    {
        if (boardMask == null || boardMask.sprite == null) return;
        Sprite maskSprite = boardMask.sprite;
        Transform maskTransform = boardMask.transform;
        Matrix4x4 currentMaskMatrix = maskTransform.localToWorldMatrix;
        bool maskChanged = !cornersInitialized || cachedMaskSprite != maskSprite ||
            !maskLocalToWorld.Equals(currentMaskMatrix);
        if (maskChanged)
        {
            Bounds bounds = maskSprite.bounds;
            corners[0] = currentMaskMatrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.min.y));
            corners[1] = currentMaskMatrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y));
            corners[2] = currentMaskMatrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.max.y));
            corners[3] = currentMaskMatrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.min.y));
            maskLocalToWorld = currentMaskMatrix;
            cachedMaskSprite = maskSprite;
            cornersInitialized = true;
        }
        if (properties == null) properties = new MaterialPropertyBlock();

        foreach (Label label in labels)
        {
            if (label.text == null) continue;
            if (label.renderer == null)
            {
                label.renderer = label.text.renderer;
                label.clipInitialized = false;
            }
            if (label.renderer == null) continue;
            Matrix4x4 currentLabelMatrix = label.text.transform.worldToLocalMatrix;
            if (!force && !maskChanged && label.clipInitialized && label.worldToLocal.Equals(currentLabelMatrix))
                continue;
            Vector3 min = currentLabelMatrix.MultiplyPoint3x4(corners[0]);
            Vector3 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 local = currentLabelMatrix.MultiplyPoint3x4(corners[i]);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            // Each label has its own local coordinates, even when sharing the same font material.
            Renderer renderer = label.renderer;
            renderer.GetPropertyBlock(properties);
            properties.SetVector(ClipRect, new Vector4(min.x, min.y, max.x, max.y));
            properties.SetFloat(SoftnessX, 0f);
            properties.SetFloat(SoftnessY, 0f);
            renderer.SetPropertyBlock(properties);
            label.worldToLocal = currentLabelMatrix;
            label.clipInitialized = true;
        }
    }

    private void OnDestroy()
    {
        foreach (Label label in labels)
        {
            if (label.text != null && label.text.fontSharedMaterial == label.clipped)
                label.text.fontSharedMaterial = label.original;
            if (label.original == null || !materials.TryGetValue(label.original, out ClipMaterial entry)) continue;
            if (--entry.references > 0) continue;
            materials.Remove(label.original);
            if (Application.isPlaying) Destroy(entry.material);
            else DestroyImmediate(entry.material);
        }
        labels.Clear();
    }
}
