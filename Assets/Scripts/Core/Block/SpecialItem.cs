using UnityEngine;

public enum SpecialType
{
    Bubble,
    FriedChicken,
    BanhMi
}

public class SpecialItem : MonoBehaviour
{
    public SpecialType type;

    public bool IsFood()
    {
        return type == SpecialType.FriedChicken ||
               type == SpecialType.BanhMi;
    }

    public void ReachBottom()
    {
        if (!IsFood())
            return;

        if (LevelManager.Instance != null)
        {
            BlockType foodType =
                type == SpecialType.BanhMi
                    ? BlockType.BanhMi
                    : BlockType.FriedChicken;

            LevelManager.Instance.CollectFood(
                foodType
            );
        }

        Destroy(gameObject);
    }
}