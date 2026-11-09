using UnityEngine;

public enum EffectType
{
    Fire,
    Frozen,
    Poison,
    Electric
}

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [Header("Effect Sprites")]
    public Sprite fireSprite;
    public Sprite frozenSprite;
    public Sprite poisonSprite;
    public Sprite electricSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public Sprite GetEffectSprite(EffectType type)
    {
        switch (type)
        {
            case EffectType.Fire:
                return fireSprite;

            case EffectType.Frozen:
                return frozenSprite;

            case EffectType.Poison:
                return poisonSprite;

            case EffectType.Electric:
                return electricSprite;
        }

        return null;
    }
}