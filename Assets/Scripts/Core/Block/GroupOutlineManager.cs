using UnityEngine;

public class GroupOutlineManager 
{
    private GridManager gridManager;
    public GroupOutlineManager(GridManager gridManager)
    {
        this.gridManager = gridManager;
    }
    public void CreateAllGroupLines()
    {
        foreach (BlockGroup group in gridManager.Groups)
        {
            if (group == null || group.blocks.Count == 0)
                continue;

            GameObject obj =
                new GameObject("GroupGlow");

            obj.transform.SetParent(gridManager.transform);

            GroupGlow glow =
                obj.AddComponent<GroupGlow>();

            glow.lineWidth = 0.04f;
            glow.lineColor = Color.black;

            glow.Build(group, gridManager.cellSize);

            //group.groupLine = obj;
        }
    }
}
