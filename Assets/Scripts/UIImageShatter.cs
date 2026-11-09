using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIImageShatter : MonoBehaviour
{
    [Header("Shatter")]
    public int columns = 6;
    public int rows = 6;
    public float duration = 0.7f;
    public float force = 500f;
    public float randomForce = 220f;
    public float rotationSpeed = 360f;
    public bool destroyPiecesAfter = true;

    private Image sourceImage;
    private readonly List<GameObject> pieces = new();

    private void Awake()
    {
        sourceImage = GetComponent<Image>();
    }

    public void Shatter()
    {
        if (sourceImage == null) sourceImage = GetComponent<Image>();
        if (sourceImage.sprite == null) return;

        CreateTrianglePieces();

        sourceImage.enabled = false;

        foreach (GameObject piece in pieces)
        {
            StartCoroutine(AnimatePiece(piece));
        }
    }

    private void CreateTrianglePieces()
    {
        ClearOldPieces();

        RectTransform sourceRect = sourceImage.rectTransform;
        Rect rect = sourceRect.rect;

        float width = rect.width;
        float height = rect.height;

        float cellW = width / columns;
        float cellH = height / rows;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Vector2 p0 = new Vector2(rect.xMin + x * cellW, rect.yMin + y * cellH);
                Vector2 p1 = new Vector2(rect.xMin + (x + 1) * cellW, rect.yMin + y * cellH);
                Vector2 p2 = new Vector2(rect.xMin + x * cellW, rect.yMin + (y + 1) * cellH);
                Vector2 p3 = new Vector2(rect.xMin + (x + 1) * cellW, rect.yMin + (y + 1) * cellH);

                // Mỗi ô vuông chẻ thành 2 tam giác
                CreateTriangle(sourceRect, p0, p2, p1);
                CreateTriangle(sourceRect, p2, p3, p1);
            }
        }
    }

    private void CreateTriangle(RectTransform sourceRect, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 center = (a + b + c) / 3f;

        GameObject go = new GameObject("TrianglePiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(TrianglePieceGraphic));
        go.transform.SetParent(sourceRect.parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = sourceRect.anchorMin;
        rt.anchorMax = sourceRect.anchorMax;
        rt.pivot = sourceRect.pivot;
        rt.sizeDelta = sourceRect.sizeDelta;
        rt.localScale = sourceRect.localScale;
        rt.localRotation = sourceRect.localRotation;
        rt.anchoredPosition = sourceRect.anchoredPosition + center;

        TrianglePieceGraphic graphic = go.GetComponent<TrianglePieceGraphic>();
        graphic.raycastTarget = false;
        graphic.texture = sourceImage.sprite.texture;
        graphic.color = sourceImage.color;

        graphic.SetTriangle(
            a - center,
            b - center,
            c - center,
            GetUV(a),
            GetUV(b),
            GetUV(c)
        );

        pieces.Add(go);
    }

    private Vector2 GetUV(Vector2 localPos)
    {
        Sprite sprite = sourceImage.sprite;
        Rect spriteRect = sprite.textureRect;
        Texture tex = sprite.texture;

        Rect r = sourceImage.rectTransform.rect;

        float nx = Mathf.InverseLerp(r.xMin, r.xMax, localPos.x);
        float ny = Mathf.InverseLerp(r.yMin, r.yMax, localPos.y);

        float u = (spriteRect.x + spriteRect.width * nx) / tex.width;
        float v = (spriteRect.y + spriteRect.height * ny) / tex.height;

        return new Vector2(u, v);
    }

    private IEnumerator AnimatePiece(GameObject piece)
    {
        RectTransform rt = piece.GetComponent<RectTransform>();
        TrianglePieceGraphic graphic = piece.GetComponent<TrianglePieceGraphic>();

        Vector2 startPos = rt.anchoredPosition;

        Vector2 dir = startPos - sourceImage.rectTransform.anchoredPosition;
        if (dir.sqrMagnitude < 0.01f)
            dir = Random.insideUnitCircle.normalized;
        else
            dir.Normalize();

        Vector2 velocity = dir * force + Random.insideUnitCircle * randomForce;
        float rot = Random.Range(-rotationSpeed, rotationSpeed);

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;

            rt.anchoredPosition = startPos + velocity * k * k;
            rt.localRotation = Quaternion.Euler(0f, 0f, rot * k);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, k);

            //Color col = graphic.color;
            //col.a = Mathf.Lerp(sourceImage.color.a, 0f, k);
            //graphic.color = col;

            yield return null;
        }

        if (destroyPiecesAfter)
            Destroy(piece);
    }

    private void ClearOldPieces()
    {
        foreach (GameObject p in pieces)
        {
            if (p != null) Destroy(p);
        }

        pieces.Clear();
    }
}

public class TrianglePieceGraphic : MaskableGraphic
{
    public Texture texture;

    private Vector2 p0;
    private Vector2 p1;
    private Vector2 p2;

    private Vector2 uv0;
    private Vector2 uv1;
    private Vector2 uv2;

    public override Texture mainTexture
    {
        get
        {
            if (texture != null) return texture;
            return s_WhiteTexture;
        }
    }

    public void SetTriangle(
        Vector2 point0,
        Vector2 point1,
        Vector2 point2,
        Vector2 uvPoint0,
        Vector2 uvPoint1,
        Vector2 uvPoint2)
    {
        p0 = point0;
        p1 = point1;
        p2 = point2;

        uv0 = uvPoint0;
        uv1 = uvPoint1;
        uv2 = uvPoint2;

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        UIVertex v = UIVertex.simpleVert;
        v.color = color;

        v.position = p0;
        v.uv0 = uv0;
        vh.AddVert(v);

        v.position = p1;
        v.uv0 = uv1;
        vh.AddVert(v);

        v.position = p2;
        v.uv0 = uv2;
        vh.AddVert(v);

        vh.AddTriangle(0, 1, 2);
    }
}