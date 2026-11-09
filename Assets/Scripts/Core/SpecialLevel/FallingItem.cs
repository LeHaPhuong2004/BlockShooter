using UnityEngine;
using DG.Tweening;

public enum RewardCategory
{
    Coin,
    Heart,
    Booster
}

public class FallingItem : MonoBehaviour
{
    [Header("Cấu hình phần thưởng")]
    public RewardCategory rewardCategory;
    public int rewardAmount = 1;
    public float fallSpeed = 3f;

    private bool isCollected = false;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (isCollected) return;

        // Rơi từ trên xuống
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime);

        // Giữ lại giới hạn an toàn phòng hờ rơi quá sâu khỏi màn hình
        if (transform.position.y < -15f)
        {
            Destroy(gameObject);
        }
    }

    // Khi người chơi click chuột hoặc chạm vào vật phẩm để nhặt
    private void OnMouseDown()
    {
        if (isCollected) return;
        CollectItem();
    }

    private void CollectItem()
    {
        isCollected = true;
        ApplyReward();

        // Hiệu ứng bay về thanh HUD
        Vector3 targetUIPos = GetTargetHudPosition();
        transform.DOMove(targetUIPos, 0.4f)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                Destroy(gameObject);
            });

        transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack);
    }

    // --- BẮT SỰ KIỆN CHẠM VẬT THỂ ĐÁY ---
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected) return;

        // Kiểm tra xem có chạm đúng vật thể đáy không (dựa vào Tag hoặc tên)
        if (collision.CompareTag("BottomGround") || collision.name.Contains("Bottom"))
        {
            isCollected = true;

            // Hiệu ứng thu nhỏ biến mất khi chạm đáy
            transform.DOScale(Vector3.zero, 0.2f).OnComplete(() =>
            {
                Destroy(gameObject);
            });
        }
    }

    private void ApplyReward()
    {
        switch (rewardCategory)
        {
            case RewardCategory.Coin:
                Debug.Log($"Nhận được {rewardAmount} tiền!");
                break;
            case RewardCategory.Heart:
                HeartManager.AddHearts(rewardAmount);
                Debug.Log("Nhận được 1 Tim!");
                break;
            case RewardCategory.Booster:
                Debug.Log($"Nhận được Booster số lượng: {rewardAmount}!");
                break;
        }
    }

    private Vector3 GetTargetHudPosition()
    {
        float hudX = (rewardCategory == RewardCategory.Heart) ? 0.7f : 0.35f;
        return mainCamera.ScreenToWorldPoint(new Vector3(Screen.width * hudX, Screen.height * 0.9f, 10f));
    }
}