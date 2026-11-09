using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class GunChain : MonoBehaviour
{
    private Gun gunA;
    private Gun gunB;

    private LineRenderer line;
    private EdgeCollider2D lineCollider;

    private readonly List<Vector2> colliderPoints =
        new List<Vector2>(2);

    private bool isDestroying;

    public bool IsPair =>
        gunA != null &&
        gunB != null;

    public void SetGuns(
        Gun a,
        Gun b
    )
    {
        if (a == null || b == null)
            return;

        if (
            a.gunType == GunType.Fire ||
            b.gunType == GunType.Fire
        )
        {
            return;
        }

        gunA = a;
        gunB = b;

        gunA.chain = this;
        gunB.chain = this;

        CreateLine();
        CreateGradient();

        UpdateLineAndCollider();
    }

    // =========================
    // CREATE LINE
    // =========================

    private void CreateLine()
    {
        if (line == null)
        {
            line = gameObject.AddComponent<LineRenderer>();

            line.positionCount = 2;
            line.startWidth = 0.08f;
            line.endWidth = 0.08f;

            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader != null)
            {
                line.material = new Material(shader);
            }
            else
            {
                line.material = new Material(
                    Shader.Find("Hidden/InternalErrorShader")
                );
            }

            line.sortingOrder = -1;
        }

        // Collider dùng để nhận diện đường dây
        if (lineCollider == null)
        {
            lineCollider =
                gameObject.AddComponent<EdgeCollider2D>();

            // Không ảnh hưởng va chạm vật lý
            lineCollider.isTrigger = true;

            // Tăng vùng có thể click
            lineCollider.edgeRadius = 0.08f;
        }
    }

    // =========================
    // CREATE GRADIENT
    // =========================

    private void CreateGradient()
    {
        if (
            line == null ||
            gunA == null ||
            gunB == null ||
            gunA.currentColor == null ||
            gunB.currentColor == null
        )
        {
            return;
        }

        Color colorA =
            gunA.currentColor.color;

        Color colorB =
            gunB.currentColor.color;

        Gradient gradient =
            new Gradient();

        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(colorA, 0f),
                new GradientColorKey(colorA, 0.5f),
                new GradientColorKey(colorB, 0.5f),
                new GradientColorKey(colorB, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        line.colorGradient = gradient;
    }

    // =========================
    // UPDATE LINE + COLLIDER
    // =========================

    private void LateUpdate()
    {
        UpdateLineAndCollider();
    }

    private void UpdateLineAndCollider()
    {
        if (
            gunA == null ||
            gunB == null ||
            line == null
        )
        {
            return;
        }

        Vector3 posA =
            gunA.transform.position;

        Vector3 posB =
            gunB.transform.position;

        // Cập nhật hình ảnh đường dây
        line.SetPosition(0, posA);
        line.SetPosition(1, posB);

        // Cập nhật Collider theo đường dây
        if (lineCollider != null)
        {
            Vector2 localA =
                transform.InverseTransformPoint(posA);

            Vector2 localB =
                transform.InverseTransformPoint(posB);

            colliderPoints.Clear();

            colliderPoints.Add(localA);
            colliderPoints.Add(localB);

            lineCollider.SetPoints(colliderPoints);
        }
    }

    // =========================
    // DETECT LINE CLICK
    // =========================

    public static bool TryCutAtWorldPoint(
        Vector2 worldPosition
    )
    {
        Collider2D[] hits =
            Physics2D.OverlapPointAll(worldPosition);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            // Chỉ nhận EdgeCollider2D của đường dây
            EdgeCollider2D edge =
                hit as EdgeCollider2D;

            if (edge == null)
                continue;

            GunChain chain =
                edge.GetComponent<GunChain>();

            if (chain == null)
                continue;

            if (chain.lineCollider != edge)
                continue;

            if (chain.isDestroying)
                continue;

            chain.PlayDestroyAnimation();

            return true;
        }

        return false;
    }

    // =========================
    // GET OTHER GUN
    // =========================

    public Gun GetOther(Gun gun)
    {
        if (gun == gunA)
            return gunB;

        if (gun == gunB)
            return gunA;

        return null;
    }

    // =========================
    // CLEAR REFERENCES
    // =========================

    private void ClearGunReferences()
    {
        if (gunA != null)
        {
            if (gunA.chain == this)
                gunA.chain = null;
        }

        if (gunB != null)
        {
            if (gunB.chain == this)
                gunB.chain = null;
        }

        gunA = null;
        gunB = null;
    }

    // =========================
    // BREAK CHAIN
    // =========================

    public void BreakChain()
    {
        ClearGunReferences();

        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    // =========================
    // DESTROY ANIMATION
    // =========================

    public void PlayDestroyAnimation()
    {
        if (isDestroying)
            return;

        isDestroying = true;

        // Ngắt liên kết giữa hai khẩu súng
        ClearGunReferences();

        if (line != null)
        {
            DOTween.Kill(line);

            DOTween.To(
                () => line != null
                    ? line.startWidth
                    : 0f,

                value =>
                {
                    if (line == null)
                        return;

                    line.startWidth = value;
                    line.endWidth = value;
                },

                0f,
                0.2f
            )
            .SetEase(Ease.InBack)
            .SetTarget(line)
            .SetLink(
                gameObject,
                LinkBehaviour.KillOnDestroy
            );
        }

        Destroy(gameObject, 0.22f);
    }
    private void OnDestroy()
    {
        DOTween.Kill(gameObject);

        if (line != null)
        {
            DOTween.Kill(line);

            if (line.material != null)
            {
                Destroy(line.material);
            }
        }

        ClearGunReferences();
    }
}