using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    private static int sessionLevel;
    private static int loadedLevel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Instance = null;
        sessionLevel = 0;
        loadedLevel = 0;
    }

    [Header("Level")]
    [SerializeField] private int currentLevel = 1;

#if UNITY_EDITOR
    [SerializeField] private int testLevel = 1;
#endif

    [System.Serializable]
    public class FoodGoal
    {
        public BlockType type;
        public int required;
        public int collected;
        public UIManager ui;
    }

    public int CurrentLevel =>
        currentLevel;

    private List<FoodGoal> foodGoals =
        new List<FoodGoal>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeLevelProgress();
    }

    private void InitializeLevelProgress()
    {
        // Scene reloads continue the session; testLevel applies only at the start of Play.
        if (sessionLevel > 0)
        {
            currentLevel = sessionLevel;
            return;
        }

#if UNITY_EDITOR
        currentLevel = Mathf.Max(1, testLevel);
#else
        currentLevel = Mathf.Max(1, PlayerPrefs.GetInt("CurrentLevel", 1));
#endif
        sessionLevel = currentLevel;
    }

    public void PrepareRestartCurrentLevel()
    {
        // CompleteLevel advances progression before the win panel is shown.
        // Replaying must use the actual loaded level, including on that panel.
        if (loadedLevel > 0)
        {
            currentLevel = loadedLevel;
            sessionLevel = loadedLevel;
        }
    }
    public void LoadCurrentLevel()
    {
        if (currentLevel < 1)
            currentLevel = 1;

        TextAsset levelFile =
            Resources.Load<TextAsset>(
                $"LevelData/level_{currentLevel}"
            );

        if (levelFile == null)
        {
            Debug.LogError(
                $"Không tìm thấy LevelData/level_{currentLevel}.txt"
            );

            return;
        }

        LevelLoader loader =
            FindFirstObjectByType<LevelLoader>();

        if (loader == null)
        {
            Debug.LogError(
                "Không tìm thấy LevelLoader."
            );

            return;
        }

        foodGoals.Clear();
        loadedLevel = currentLevel;
        sessionLevel = currentLevel;

        Debug.Log(
            $"LOAD LEVEL {currentLevel}: {levelFile.name}"
        );

        loader.LoadLevel(
            levelFile
        );
    }

    public void SetFoodGoals(
        List<FoodGoal> goals
    )
    {
        foodGoals.Clear();

        Debug.Log(
            "[FOOD SET] SetFoodGoals() bắt đầu."
        );

        if (goals == null)
        {
            Debug.LogWarning(
                "[FOOD SET] goals = NULL."
            );

            return;
        }

        Debug.Log(
            $"[FOOD SET] goals nhận được = {goals.Count}"
        );

        foreach (FoodGoal goal in goals)
        {
            if (goal == null)
                continue;

            if (goal.required <= 0)
                continue;

            goal.collected = 0;
            goal.ui = null;

            Debug.Log(
                $"[FOOD SET] Adding goal: " +
                $"{goal.type} / {goal.required}"
            );

            foodGoals.Add(
                goal
            );
        }

        Debug.Log(
            $"[FOOD SET] FINAL COUNT = {foodGoals.Count}"
        );

        foreach (FoodGoal goal in foodGoals)
        {
            Debug.Log(
                $"[FOOD SET] " +
                $"{goal.type} = {goal.required}"
            );
        }

        RegisterFoodUIs();
    }

    private void RegisterFoodUIs()
    {
        if (foodGoals.Count == 0)
        {
            Debug.Log(
                "[FOOD UI] Không có Food Goal để đăng ký UI."
            );

            return;
        }

        UIManager[] uis =
            FindObjectsByType<UIManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (
            uis == null ||
            uis.Length == 0
        )
        {
            Debug.LogWarning(
                "[FOOD UI] Không tìm thấy UIManager."
            );

            return;
        }

        Debug.Log(
            $"[FOOD UI] Tìm thấy {uis.Length} UIManager."
        );

        int count =
            Mathf.Min(
                uis.Length,
                foodGoals.Count
            );

        for (int i = 0; i < count; i++)
        {
            if (uis[i] == null)
                continue;

            foodGoals[i].ui =
                uis[i];

            uis[i].Initialize(
                foodGoals[i].type,
                foodGoals[i].required
            );

            uis[i].UpdateUI(
                foodGoals[i].collected,
                foodGoals[i].required
            );

            Debug.Log(
                $"[FOOD UI] Register " +
                $"{foodGoals[i].type} -> " +
                $"{uis[i].name}"
            );
        }
    }

    public void RegisterFoodUIFromUI(
        UIManager ui
    )
    {
        if (ui == null)
            return;

        if (foodGoals.Count == 0)
        {
            Debug.Log(
                "[FOOD UI] UI đăng ký nhưng hiện chưa có Food Goal."
            );

            return;
        }

        foreach (FoodGoal goal in foodGoals)
        {
            if (goal == null)
                continue;

            if (goal.ui != null)
                continue;

            goal.ui =
                ui;

            ui.Initialize(
                goal.type,
                goal.required
            );

            ui.UpdateUI(
                goal.collected,
                goal.required
            );

            Debug.Log(
                $"[FOOD UI] UI tự đăng ký: " +
                $"{goal.type} -> {ui.name}"
            );

            return;
        }
    }

    public List<FoodGoal> GetFoodGoals()
    {
        return foodGoals;
    }

    public void CollectFood(
        BlockType type
    )
    {
        Debug.Log(
            $"[FOOD COLLECT] Request = " +
            $"{type}, Goals count = " +
            $"{foodGoals.Count}"
        );

        foreach (FoodGoal goal in foodGoals)
        {
            if (goal == null)
                continue;

            Debug.Log(
                $"[FOOD COLLECT] Existing goal = " +
                $"{goal.type} / " +
                $"{goal.required} / " +
                $"collected={goal.collected}"
            );
        }

        FoodGoal targetGoal =
            GetFoodGoal(
                type
            );

        if (targetGoal == null)
        {
            Debug.LogWarning(
                $"Không có Food Goal cho {type}"
            );

            return;
        }

        if (
            targetGoal.collected >=
            targetGoal.required
        )
        {
            return;
        }

        targetGoal.collected++;

        Debug.Log(
            $"[FOOD COLLECT] Đã collect " +
            $"{type}: " +
            $"{targetGoal.collected}/" +
            $"{targetGoal.required}"
        );

        if (targetGoal.ui == null)
        {
            Debug.Log(
                $"[FOOD UI] UI của {type} " +
                $"đang NULL -> đăng ký lại."
            );

            RegisterFoodUIs();

            targetGoal =
                GetFoodGoal(
                    type
                );
        }

        if (
            targetGoal != null &&
            targetGoal.ui != null
        )
        {
            targetGoal.ui.UpdateUI(
                targetGoal.collected,
                targetGoal.required
            );

            Debug.Log(
                $"[FOOD UI] UPDATE {type}: " +
                $"{targetGoal.collected}/" +
                $"{targetGoal.required} -> " +
                $"UI {targetGoal.ui.name}"
            );
        }
        else
        {
            Debug.LogError(
                $"[FOOD UI] Có Food Goal {type} " +
                $"nhưng KHÔNG có UIManager!"
            );
        }

        CheckFoodGoals();
    }

    private FoodGoal GetFoodGoal(
        BlockType type
    )
    {
        foreach (
            FoodGoal goal
            in foodGoals
        )
        {
            if (goal == null)
                continue;

            if (goal.type == type)
                return goal;
        }

        return null;
    }

    public bool IsFoodGoalComplete()
    {
        if (foodGoals.Count == 0)
            return true;

        foreach (
            FoodGoal goal
            in foodGoals
        )
        {
            if (
                goal.collected <
                goal.required
            )
            {
                return false;
            }
        }

        return true;
    }

    public void CheckFoodGoals()
    {
        if (!IsFoodGoalComplete())
            return;

        Debug.Log(
            $"Food Goals hoàn thành ở Level {currentLevel}"
        );
    }

    public void CompleteLevel()
    {
        currentLevel++;
        sessionLevel = currentLevel;

        Debug.Log(
            $"CompleteLevel -> Level tiếp theo: " +
            $"{currentLevel}"
        );

#if !UNITY_EDITOR
        PlayerPrefs.SetInt(
            "CurrentLevel",
            currentLevel
        );

        PlayerPrefs.Save();
#endif
    }

    public void SetLevel(
        int level
    )
    {
        if (level < 1)
            return;

        currentLevel =
            level;
        sessionLevel = currentLevel;

#if !UNITY_EDITOR
        PlayerPrefs.SetInt(
            "CurrentLevel",
            currentLevel
        );

        PlayerPrefs.Save();
#endif

        Debug.Log(
            $"Set Level: {currentLevel}"
        );
    }

    public bool HasNextLevel()
    {
        TextAsset nextLevel =
            Resources.Load<TextAsset>(
                $"LevelData/level_{currentLevel}"
            );

        bool exists =
            nextLevel != null;

        Debug.Log(
            $"Check Level " +
            $"{currentLevel + 1}: {exists}"
        );

        return exists;
    }
}
