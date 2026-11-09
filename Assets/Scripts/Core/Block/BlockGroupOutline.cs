using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class GroupGlow : MonoBehaviour
{
    public float lineWidth = 0.04f;
    public Color lineColor = Color.black;

    private List<LineRenderer> lines = new List<LineRenderer>();

    public void Build(BlockGroup group, float cellSize)
    {
        Clear();

        if (group == null || group.blocks.Count == 0)
            return;

        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();

        foreach (Block block in group.blocks)
        {
            if (block == null)
                continue;

            cells.Add(
                new Vector2Int(
                    block.gridX,
                    block.gridY
                )
            );
        }

        foreach (Vector2Int cell in cells)
        {
            if (!cells.Contains(cell + Vector2Int.up))
            {
                CreateLine(
                    GetCorner(cell, 0, cellSize),
                    GetCorner(cell, 1, cellSize)
                );
            }

            if (!cells.Contains(cell + Vector2Int.right))
            {
                CreateLine(
                    GetCorner(cell, 1, cellSize),
                    GetCorner(cell, 2, cellSize)
                );
            }

            if (!cells.Contains(cell + Vector2Int.down))
            {
                CreateLine(
                    GetCorner(cell, 2, cellSize),
                    GetCorner(cell, 3, cellSize)
                );
            }

            if (!cells.Contains(cell + Vector2Int.left))
            {
                CreateLine(
                    GetCorner(cell, 3, cellSize),
                    GetCorner(cell, 0, cellSize)
                );
            }
        }
    }

    public void MoveBy(Vector3 delta, float duration)
    {
        foreach (LineRenderer line in lines)
        {
            if (line == null)
                continue;

            Vector3 start =
                line.GetPosition(0);

            Vector3 end =
                line.GetPosition(1);

            line.DOKill();

            Sequence sequence =
                DOTween.Sequence();

            sequence.Join(
                DOTween.To(
                    () => line.GetPosition(0),
                    value =>
                        line.SetPosition(0, value),
                    start + delta,
                    duration
                )
            );

            sequence.Join(
                DOTween.To(
                    () => line.GetPosition(1),
                    value =>
                        line.SetPosition(1, value),
                    end + delta,
                    duration
                )
            );

            sequence.SetEase(Ease.OutQuad);
        }
    }

    public Tween Zoom(
      Vector3 center,
      float maxScale,
      float upDuration,
      float downDuration)
    {
        if (lines == null || lines.Count == 0)
            return null;

        List<Vector3[]> originalPositions =
            new List<Vector3[]>();

        foreach (LineRenderer line in lines)
        {
            if (line == null)
            {
                originalPositions.Add(null);
                continue;
            }

            originalPositions.Add(
                new Vector3[]
                {
                line.GetPosition(0),
                line.GetPosition(1)
                }
            );

            line.DOKill();
        }

        float scale = 1f;

        Sequence sequence = DOTween.Sequence();

        sequence.Append(
            DOTween.To(
                () => scale,
                value =>
                {
                    scale = value;

                    for (int i = 0; i < lines.Count; i++)
                    {
                        LineRenderer line = lines[i];

                        if (line == null)
                            continue;

                        Vector3[] start =
                            originalPositions[i];

                        if (start == null)
                            continue;

                        Vector3 p0 =
                            center +
                            (start[0] - center) * scale;

                        Vector3 p1 =
                            center +
                            (start[1] - center) * scale;

                        line.SetPosition(0, p0);
                        line.SetPosition(1, p1);
                    }
                },
                maxScale,
                upDuration
            ).SetEase(Ease.OutQuad)
        );

        sequence.Append(
            DOTween.To(
                () => scale,
                value =>
                {
                    scale = value;

                    for (int i = 0; i < lines.Count; i++)
                    {
                        LineRenderer line = lines[i];

                        if (line == null)
                            continue;

                        Vector3[] start =
                            originalPositions[i];

                        if (start == null)
                            continue;

                        Vector3 p0 =
                            center +
                            (start[0] - center) * scale;

                        Vector3 p1 =
                            center +
                            (start[1] - center) * scale;

                        line.SetPosition(0, p0);
                        line.SetPosition(1, p1);
                    }
                },
                1f,
                downDuration
            ).SetEase(Ease.OutBack)
        );

        return sequence;
    }
    public void Rebuild(
        BlockGroup group,
        float cellSize)
    {
        Build(group, cellSize);
    }

    private Vector3 GetCorner(
        Vector2Int cell,
        int corner,
        float cellSize)
    {
        float offsetX =
            (GridManager.Instance.width - 1)
            * cellSize / 2f;

        float centerX =
            cell.x * cellSize -
            offsetX;

        float centerY =
            cell.y * cellSize +
            GridManager.Instance.gridOffsetY;

        float half =
            cellSize / 2f;

        switch (corner)
        {
            case 0:
                return new Vector3(
                    centerX - half,
                    centerY + half,
                    -0.1f
                );

            case 1:
                return new Vector3(
                    centerX + half,
                    centerY + half,
                    -0.1f
                );

            case 2:
                return new Vector3(
                    centerX + half,
                    centerY - half,
                    -0.1f
                );

            case 3:
                return new Vector3(
                    centerX - half,
                    centerY - half,
                    -0.1f
                );
        }

        return Vector3.zero;
    }

    private void CreateLine(
        Vector3 start,
        Vector3 end)
    {
        GameObject obj =
            new GameObject("GroupLine");

        obj.transform.SetParent(
            transform,
            false
        );

        LineRenderer line =
            obj.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.positionCount = 2;

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        Material mat =
            new Material(
                Shader.Find("Sprites/Default")
            );

        mat.color = lineColor;

        line.material = mat;

        line.startColor = lineColor;
        line.endColor = lineColor;

        line.numCapVertices = 2;

        lines.Add(line);
    }

    public void Clear()
    {
        foreach (LineRenderer line in lines)
        {
            if (line != null)
            {
                Destroy(line.gameObject);
            }
        }

        lines.Clear();
    }
}