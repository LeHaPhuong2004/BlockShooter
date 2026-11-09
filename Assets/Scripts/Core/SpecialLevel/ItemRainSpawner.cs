using UnityEngine;

public class ItemRainSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct DropItemData
    {
        public GameObject prefab; // Kéo Prefab tương ứng vào đây
        public int weight;        // Trọng số tỷ lệ (60, 5, 4, 5, 5, 5, 5, 6)
    }

    public DropItemData[] dropItemList;
    public float spawnInterval = 1.5f;
    public float minX = -3f;
    public float maxX = 3f;
    public float spawnY = 6f;

    private float timer;
    private int totalWeight;

    private void Start()
    {
        CalculateTotalWeight();
    }

    private void CalculateTotalWeight()
    {
        totalWeight = 0;
        foreach (var item in dropItemList)
        {
            totalWeight += item.weight;
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnRandomItem();
        }
    }

    void SpawnRandomItem()
    {
        if (dropItemList == null || dropItemList.Length == 0) return;

        // Thuật toán Random theo trọng số (Weight)
        int randomWeight = Random.Range(0, totalWeight);
        int currentWeight = 0;

        GameObject selectedPrefab = dropItemList[0].prefab;

        foreach (var item in dropItemList)
        {
            currentWeight += item.weight;
            if (randomWeight < currentWeight)
            {
                selectedPrefab = item.prefab;
                break;
            }
        }

        // Sinh vật phẩm tại vị trí ngẫu nhiên trên cao
        float randomX = Random.Range(minX, maxX);
        Vector3 spawnPos = new Vector3(randomX, spawnY, 0f);

        Instantiate(selectedPrefab, spawnPos, Quaternion.identity);
    }
}