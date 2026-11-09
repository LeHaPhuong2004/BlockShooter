using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    private Material shopCardSweepMaterial;
    private readonly List<ShopCardLightSweep> shopCardSweeps = new List<ShopCardLightSweep>();
    private bool shopCardSweepsBound;

    private void BindShopCardLightSweeps()
    {
        if (shopGoldViewport == null && shopBoosterViewport == null) return;
        shopCardSweepsBound = true;
        if (shopCardSweepMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("UI/ShopCardLightSweep");
            if (shader == null || !shader.isSupported) return;
            shopCardSweepMaterial = new Material(shader)
            {
                name = "Shop Card Light Sweep",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        shopCardSweeps.Clear();
        foreach (Image source in ShopCardSweepSources())
        {
            Transform overlay = source.transform.Find("Light Sweep");
            ShopCardLightSweep sweep = overlay != null ? overlay.GetComponent<ShopCardLightSweep>() : null;
            if (sweep == null) continue;
            sweep.Configure(source, shopCardSweepMaterial, shopCardSweeps.Count * .137f);
            shopCardSweeps.Add(sweep);
        }
    }

    private IEnumerable<Image> ShopCardSweepSources()
    {
        foreach (GameObject viewport in new[] { shopGoldViewport, shopBoosterViewport })
        {
            if (viewport == null) continue;
            foreach (Image source in viewport.GetComponentsInChildren<Image>(true))
            {
                Transform parent = source.transform.parent;
                if (source.sprite == null || parent == null || !source.enabled || !source.gameObject.activeSelf ||
                    (parent.name != "Shop Card Frames" && parent.name != "Booster Offer Packages")) continue;
                bool archived = false;
                for (Transform ancestor = parent; ancestor != null && ancestor != viewport.transform; ancestor = ancestor.parent)
                    if (ancestor.name == "Original Booster Offer Templates") { archived = true; break; }
                if (archived) continue;

                yield return source;
            }
        }
    }

#if UNITY_EDITOR
    public void PrepareShopCardLightSweepsForEditor()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Author shop effects outside Play Mode.");
        shopGoldViewport = SceneObject("Gold Shop Viewport");
        shopBoosterViewport = SceneObject("Booster Shop Viewport");
        int index = 0;
        foreach (Image source in ShopCardSweepSources())
        {
            Transform existing = source.transform.Find("Light Sweep");
            GameObject overlay = existing != null ? existing.gameObject
                : new GameObject("Light Sweep", typeof(RectTransform), typeof(Image), typeof(ShopCardLightSweep));
            overlay.transform.SetParent(source.transform, false);
            overlay.transform.SetAsFirstSibling();
            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            ShopCardLightSweep sweep = overlay.GetComponent<ShopCardLightSweep>();
            if (sweep == null) sweep = overlay.AddComponent<ShopCardLightSweep>();
            sweep.Configure(source, null, index++ * .137f);
            overlay.GetComponent<Image>().enabled = false;
        }
    }
#endif

    private void UpdateShopCardLightSweeps()
    {
        if (!shopCardSweepsBound) BindShopCardLightSweeps();
        if (activeTabIndex != 1 || shopCardSweepMaterial == null) return;
        float time = Time.unscaledTime;
        foreach (ShopCardLightSweep sweep in shopCardSweeps)
            if (sweep != null && sweep.isActiveAndEnabled) sweep.SetTime(time);
    }
}
