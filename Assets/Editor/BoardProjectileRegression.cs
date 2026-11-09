#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Tests aiming and projectile clipping against the actual saved Game board mask.</summary>
[InitializeOnLoad]
public static class BoardProjectileRegression
{
    private const string Request = "Temp/BoardProjectileRegression.request";
    private const string Result = "Temp/BoardProjectileRegression.result";
    private const string Output = "Temp/BoardProjectileValidation";
    private const int Size = 768;
    private static bool running;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static BoardProjectileRegression() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Visible Projectile Targets and Clipping")]
    public static void Run()
    {
        Scene source = default, preview = default;
        RenderTexture target = null;
        RenderTexture previousTarget = RenderTexture.active;
        var report = new StringBuilder();
        try
        {
            Directory.CreateDirectory(Output);
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GameObject[] roots = source.GetRootGameObjects();
            GridManager templateGrid = roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            SpriteMask templateMask = templateGrid.GetComponent<SpriteMask>();
            Require(templateMask != null && templateMask.enabled && templateMask.sprite != null, "Actual saved board mask missing.");

            preview = EditorSceneManager.NewPreviewScene();
            var boardObject = new GameObject("Projectile Test Board", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(boardObject, preview);
            boardObject.transform.position = templateGrid.transform.position;
            boardObject.transform.rotation = templateGrid.transform.rotation;
            boardObject.transform.localScale = templateGrid.transform.lossyScale;
            GridManager grid = boardObject.AddComponent<GridManager>();
            grid.width = templateGrid.width; grid.height = templateGrid.height; grid.cellSize = templateGrid.cellSize;
            SpriteMask mask = boardObject.AddComponent<SpriteMask>();
            EditorUtility.CopySerialized(templateMask, mask);
            Require(BoardViewport.TryGetBounds(grid, out Bounds board), "BoardViewport failed on the actual board mask.");
            report.AppendLine("Actual Game board: " + board.ToString("F5"));

            Camera sourceCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First(camera => camera.CompareTag("MainCamera"));
            GameObject cameraObject = UnityEngine.Object.Instantiate(sourceCamera.gameObject);
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; camera.scene = preview; camera.rect = new Rect(0, 0, 1, 1);
            camera.orthographic = true; camera.aspect = 1;
            camera.orthographicSize = Mathf.Max(board.extents.x, board.extents.y) + 1.1f;
            camera.transform.position = new Vector3(board.center.x, board.center.y, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.35f, .35f, .35f, 1);
            foreach (GameObject root in roots)
            {
                if (!root.GetComponentsInChildren<Component>(true).Any(component => component != null && component.GetType().Name == "Light2D")) continue;
                SceneManager.MoveGameObjectToScene(UnityEngine.Object.Instantiate(root), preview);
            }
            target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            target.Create(); camera.targetTexture = target;
            Color32[] baseline = Capture(camera, null);
            var red = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Red.asset");
            var blue = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Blue.asset");
            Require(red != null && blue != null, "Missing block colors.");

            Block visible = SpawnBlock(templateGrid.blockPrefab, grid, board.center, red, null);
            Block hidden = SpawnBlock(templateGrid.blockPrefab, grid, board.center + Vector3.up * (board.extents.y + 1), red, null);
            var group = new BlockGroup(); group.Add(visible); group.Add(hidden); group.InitializedType();
            Require(BoardViewport.IsBlockVisible(visible), "Visible block rejected.");
            Require(!BoardViewport.IsBlockVisible(hidden), "Fully hidden block accepted.");
            Require(!BoardViewport.TryGetTargetPoint(hidden, out _), "Hidden block returned an aim point.");

            Gun sourceGun = roots.SelectMany(root => root.GetComponentsInChildren<Gun>(true)).First(gun => gun.firePoint != null && gun.bulletPrefab != null);
            Gun gunCopy = UnityEngine.Object.Instantiate(sourceGun);
            SceneManager.MoveGameObjectToScene(gunCopy.gameObject, preview);
            gunCopy.gameObject.SetActive(true); gunCopy.currentColor = red;
            gunCopy.transform.position = new Vector3(board.center.x, board.min.y - .6f);
            foreach (Renderer renderer in gunCopy.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            Require(Matches(gunCopy, visible) && !Matches(gunCopy, hidden), "Gun color matching must reject hidden group members.");
            Require(FindPath(gunCopy, group, out Block chosen, out Vector2 aim, out List<Vector2> path) && chosen == visible,
                "Mixed group must target its visible member.");
            Require(Inside(board, aim), "Visible target aim point escaped board.");
            Require(path.Count >= 2 && Inside(board, path[path.Count - 1]), "Path must end inside board.");

            visible.transform.position = hidden.transform.position + Vector3.right;
            Require(!FindPath(gunCopy, group, out _, out _, out _), "Wholly hidden group must not be targetable.");
            visible.transform.position = new Vector3(board.center.x, board.max.y + .08f, 0);
            Require(BoardViewport.IsBlockVisible(visible) && BoardViewport.TryGetTargetPoint(visible, out Vector2 partialAim) && Inside(board, partialAim),
                "Partly visible block must aim within its visible intersection.");
            Require(FindPath(gunCopy, group, out chosen, out aim, out path) && chosen == visible && Inside(board, aim),
                "Gun must use a visible endpoint for a partial block.");
            visible.transform.position = board.center; gunCopy.currentColor = blue;
            Require(!Matches(gunCopy, visible), "Visibility restriction changed color matching.");
            gunCopy.currentColor = red;
            report.AppendLine("PASS: hidden-only groups and hidden members are rejected; partial endpoints stay inside the mask; color matching is preserved.");

            Block dual = SpawnBlock(templateGrid.dualBlockPrefab, grid, board.center, red, blue);
            var dualGroup = new BlockGroup { groupType = BlockType.Dual }; dualGroup.Add(dual); dualGroup.InitializedType();
            Require(Matches(gunCopy, dual), "Visible dual lost red targeting.");
            gunCopy.currentColor = blue; Require(Matches(gunCopy, dual), "Visible dual lost blue targeting.");
            dual.transform.position = hidden.transform.position;
            Require(!Matches(gunCopy, dual), "Hidden dual remains targetable.");
            dual.transform.position = board.center;
            foreach (Block block in new[] { visible, hidden, dual }) foreach (Renderer renderer in block.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;

            // A hidden same-group collider must not consume reserved damage.
            visible.spriteRenderer.enabled = true;
            group.ReserveDamage(1, red);
            Require(group.pendingDamage == 1, "Fixture reservation failed.");
            Bullet bullet = SpawnBullet(sourceGun.bulletPrefab, preview, board.center, visible, red);
            int originalHP = group.totalHP;
            Require((bool)typeof(Bullet).GetMethod("HasVisibleTarget", Private).Invoke(bullet, null), "New bullet must retain its visible target.");
            Vector3 visiblePosition = visible.transform.position;
            visible.transform.position = hidden.transform.position;
            Require(!(bool)typeof(Bullet).GetMethod("HasVisibleTarget", Private).Invoke(bullet, null), "In-flight bullet must reject a target that moves out of view.");
            visible.transform.position = visiblePosition;
            Collider2D hiddenCollider = hidden.GetComponentInChildren<Collider2D>();
            Require(hiddenCollider != null, "Hidden block collider missing.");
            Invoke(bullet, "OnTriggerEnter2D", hiddenCollider);
            Require(group.totalHP == originalHP && group.pendingDamage == 1, "Hidden collider damaged a group or consumed a reservation.");
            Vector3 projectilePosition = bullet.transform.position;
            bullet.transform.position = new Vector3(board.center.x, board.max.y + 1);
            Invoke(bullet, "OnTriggerEnter2D", visible.GetComponentInChildren<Collider2D>());
            Require(group.totalHP == originalHP && group.pendingDamage == 1, "Projectile outside board caused damage on a visible collider.");
            bullet.transform.position = projectilePosition;
            Invoke(bullet, "ReleaseReservation"); Invoke(bullet, "ReleaseReservation");
            Require(group.pendingDamage == 0 && group.totalHP == originalHP, "Canceled reservation must release exactly once without damage.");
            report.AppendLine("PASS: hidden same-group collider causes no damage; cancellation releases one reservation exactly once.");
            visible.spriteRenderer.enabled = false;

            BoardProjectileClipping.Apply(bullet, grid);
            Require(bullet.bulletSprite != null && bullet.bulletSprite.maskInteraction == SpriteMaskInteraction.None &&
                bullet.bulletSprite.sharedMaterial.HasProperty("_BoardClipRect"), "Bullet body must use its flight clipping shader.");
            if (bullet.fireOverlay != null) Require(bullet.fireOverlay.maskInteraction == SpriteMaskInteraction.None &&
                bullet.fireOverlay.sharedMaterial.HasProperty("_BoardClipRect"), "Fire overlay must use flight clipping.");
            if (bullet.bulletTrail != null) { bullet.bulletTrail.emitting = false; bullet.bulletTrail.Clear(); bullet.bulletTrail.enabled = false; }
            bullet.transform.position = board.center;
            Stats inside = RenderStats(camera, baseline, board, "bullet-inside", report);
            Require(inside.foreground > 30 && inside.outside <= 2, "Bullet must render only inside board.");
            Vector3[] edges = { new Vector3(board.center.x, board.max.y), new Vector3(board.center.x, board.min.y),
                new Vector3(board.min.x, board.center.y), new Vector3(board.max.x, board.center.y) };
            for (int i = 0; i < edges.Length; i++)
            {
                bullet.transform.position = edges[i];
                Stats edge = RenderStats(camera, baseline, board, "bullet-edge-" + i, report);
                Require(edge.foreground > 5 && edge.foreground < inside.foreground * .9f && edge.outside <= 2, "Bullet leaked or did not clip at edge " + i);
            }
            bullet.transform.position = new Vector3(board.center.x, board.max.y + .4f);
            Stats outside = RenderStats(camera, baseline, board, "bullet-outside", report);
            Require(outside.foreground <= 2, "Outside bullet must not be visible.");
            if (bullet.fireOverlay != null)
            {
                bullet.SetFireBullet(true);
                bullet.transform.position = edges[0];
                Stats fire = RenderStats(camera, baseline, board, "fire-overlay-edge", report);
                Require(fire.foreground > 5 && fire.outside <= 2, "Enabled fire overlay leaked above board.");
                report.AppendLine("PASS: enabling the authored fire overlay retains board clipping.");
            }
            else report.AppendLine("Actual bullet prefab has no separate fire overlay; all authored SpriteRenderers use flight clipping.");

            WaterSplashEffect.Play(visible, edges[0], Vector2.up, red.color);
            WaterSplashEffect[] splashes = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WaterSplashEffect>(true)).ToArray();
            Require(splashes.Length > 0, "Impact splash fixture did not initialize.");
            foreach (WaterSplashEffect splash in splashes)
            {
                SpriteRenderer[] drops = splash.GetComponentsInChildren<SpriteRenderer>(true);
                Require(drops.Length > 0 && drops.All(drop => drop.maskInteraction == SpriteMaskInteraction.VisibleInsideMask), "Splash drops must use the same board mask.");
                UnityEngine.Object.DestroyImmediate(splash.gameObject);
            }
            report.AppendLine("PASS: impact water drops retain board masking at the edge.");
            foreach (SpriteRenderer sprite in bullet.GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled = false;

            TrailRenderer trail = bullet.bulletTrail;
            Require(trail != null && trail.sharedMaterial != null, "Actual bullet trail missing.");
            trail.enabled = true; trail.emitting = false; trail.time = 1000; trail.Clear();
            trail.widthMultiplier = .2f;
            trail.colorGradient = new Gradient { colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) } };
            bullet.transform.position = board.center;
            var points = new[] { new Vector3(board.min.x - .5f, board.center.y), new Vector3(board.max.x + .5f, board.center.y) };
            trail.AddPositions(points);
            Stats trailHorizontal = RenderTrail(camera, trail, baseline, board, points, "trail-horizontal-edge", report);
            Require(trailHorizontal.foreground > 50 && trailHorizontal.outside <= 2, "Trail segments/caps leaked across horizontal board boundaries.");
            trail.Clear();
            points = new[] { new Vector3(board.center.x, board.min.y - .5f), new Vector3(board.center.x, board.max.y + .5f) };
            trail.AddPositions(points);
            Stats trailVertical = RenderTrail(camera, trail, baseline, board, points, "trail-vertical-edge", report);
            Require(trailVertical.foreground > 50 && trailVertical.outside <= 2, "Trail segments/caps leaked across vertical board boundaries.");
            trail.transform.SetParent(null, true);
            Stats detached = RenderTrail(camera, trail, baseline, board, points, "trail-detached", report);
            Require(detached.foreground > 50 && detached.outside <= 2, "Detached fading trail lost board clipping.");
            report.AppendLine("PASS: bullet sprite masking and the actual trail shader/property-block clipping pass all board edges, including detached fading-trail properties.");
            trail.enabled = false;

            // A launch below the board must remain visible through the bottom seam.
            Vector3 muzzle = new Vector3(board.center.x, board.min.y - .6f);
            visible.spriteRenderer.enabled = true;
            Bullet approach = SpawnBullet(sourceGun.bulletPrefab, preview, muzzle, visible, red);
            visible.spriteRenderer.enabled = false;
            Require(Vector2.Distance(approach.LaunchPosition, muzzle) < .0001f, "Bullet did not capture the original muzzle position.");
            Require(BoardProjectileClipping.TryGetFlightBounds(approach, grid, out Bounds flight), "Muzzle flight bounds missing.");
            Require(flight.min.y < muzzle.y && Mathf.Abs(flight.max.y - board.max.y) < .0001f &&
                Mathf.Abs(flight.min.x - board.min.x) < .0001f && Mathf.Abs(flight.max.x - board.max.x) < .0001f,
                "Flight clipping must extend only the lower edge to include the launch.");
            TrailRenderer approachTrail = approach.bulletTrail;
            Require(approachTrail != null, "Approach trail missing.");
            approachTrail.emitting = false; approachTrail.Clear(); approachTrail.enabled = false;
            Stats muzzleStats = RenderStats(camera, baseline, flight, "approach-at-muzzle", report);
            Require(muzzleStats.foreground > inside.foreground * .85f && muzzleStats.outside <= 2 && muzzleStats.minY < board.min.y - .2f,
                "Projectile must be visible immediately from the muzzle below the board.");
            float[] approachY = { muzzle.y + .2f, board.min.y - .15f, board.min.y, board.min.y + .15f, board.min.y + .35f };
            for (int frame = 0; frame < approachY.Length; frame++)
            {
                approach.transform.position = new Vector3(board.center.x, approachY[frame]);
                Stats sample = RenderStats(camera, baseline, flight, "approach-frame-" + frame, report);
                Require(sample.foreground > inside.foreground * .85f && sample.outside <= 2,
                    "Projectile disappeared or was cut while crossing the bottom seam at frame " + frame);
                Require(BoardProjectileClipping.TryGetFlightBounds(approach, grid, out Bounds afterMove) && afterMove == flight,
                    "Flight bounds must remain anchored to the original muzzle during movement.");
            }
            approach.transform.position = board.center;
            BoardProjectileClipping.Apply(approach, grid);
            var movedProperties = new MaterialPropertyBlock(); approach.bulletSprite.GetPropertyBlock(movedProperties);
            Require(Mathf.Abs(movedProperties.GetVector("_BoardClipRect").y - flight.min.y) < .0001f,
                "Reapplying projectile clipping after entry must preserve the original muzzle approach.");
            foreach (int edge in new[] { 0, 2, 3 })
            {
                approach.transform.position = edges[edge];
                Stats sample = RenderStats(camera, baseline, flight, "approach-strict-edge-" + edge, report);
                Require(sample.foreground > 5 && sample.foreground < inside.foreground * .9f && sample.outside <= 2,
                    "Muzzle launch must still clip the top and side edges.");
            }
            approach.transform.position = new Vector3(board.center.x, board.max.y + .4f);
            Require(RenderStats(camera, baseline, flight, "approach-above-board", report).foreground <= 2,
                "Muzzle launch must not expose a projectile above the board.");
            foreach (SpriteRenderer sprite in approach.GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled = false;

            approach.transform.position = muzzle;
            approachTrail.enabled = true; approachTrail.emitting = false; approachTrail.time = 1000;
            approachTrail.widthMultiplier = .2f; approachTrail.colorGradient = trail.colorGradient;
            approachTrail.Clear();
            points = new[] { new Vector3(board.min.x - .5f, muzzle.y), new Vector3(board.max.x + .5f, muzzle.y) };
            approachTrail.AddPositions(points);
            Stats belowTrail = RenderTrail(camera, approachTrail, baseline, flight, points, "approach-trail-below-board", report);
            Require(belowTrail.foreground > 50 && belowTrail.outside <= 2 && belowTrail.minY < board.min.y - .2f,
                "Trail must remain visible below the board while respecting the strict side limits.");
            approachTrail.Clear();
            points = new[] { new Vector3(board.center.x, flight.min.y - .5f), new Vector3(board.center.x, board.max.y + .5f) };
            approachTrail.AddPositions(points);
            Stats entryTrail = RenderTrail(camera, approachTrail, baseline, flight, points, "approach-trail-through-entry", report);
            Require(entryTrail.foreground > 50 && entryTrail.outside <= 2 && entryTrail.minY < board.min.y - .2f,
                "Trail must cross the bottom seam and stay clipped at the top.");
            approachTrail.transform.SetParent(null, true);
            var detachedProperties = new MaterialPropertyBlock(); approachTrail.GetPropertyBlock(detachedProperties);
            Vector4 detachedRect = detachedProperties.GetVector("_BoardClipRect");
            Require(Mathf.Abs(detachedRect.y - flight.min.y) < .0001f && Mathf.Abs(detachedRect.w - flight.max.y) < .0001f,
                "Detached approach trail lost the extended flight rectangle.");
            Stats detachedApproach = RenderTrail(camera, approachTrail, baseline, flight, points, "approach-trail-detached", report);
            Require(detachedApproach.foreground > 50 && detachedApproach.outside <= 2 && detachedApproach.minY < board.min.y - .2f,
                "Detached fading trail must retain its visible muzzle approach and strict top/side clipping.");
            report.AppendLine("PASS: muzzle launch and trail stay visible before entering the board, remain continuous across the bottom seam, and retain strict top/side clipping after movement and detachment.");
            report.AppendLine("No Scene, prefab, color data or player data was saved.");
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, report + "\nFAIL\n" + exception); throw; }
        finally
        {
            RenderTexture.active = previousTarget;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }

    private static Block SpawnBlock(Block template, GridManager grid, Vector3 position, BlockColorData first, BlockColorData second)
    {
        Block block = UnityEngine.Object.Instantiate(template, grid.transform, false);
        block.gameObject.SetActive(true); block.transform.position = position;
        if (second != null) block.SetDual(first, second);
        else { block.SetColor(first); block.SetBlockType(BlockType.Normal); }
        foreach (TMP_Text text in block.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);
        BoardBlockClipping.Bind(block);
        return block;
    }
    private static Bullet SpawnBullet(Bullet template, Scene scene, Vector3 position, Block target, BlockColorData color)
    {
        Bullet bullet = UnityEngine.Object.Instantiate(template);
        SceneManager.MoveGameObjectToScene(bullet.gameObject, scene);
        bullet.gameObject.SetActive(true); bullet.transform.position = position;
        bullet.Initialize(target, color, target.transform.position, new List<Vector2> { position, target.transform.position });
        return bullet;
    }
    private static bool Matches(Gun gun, Block block) => (bool)typeof(Gun).GetMethod("BlockMatchesColor", Private).Invoke(gun, new object[] { block });
    private static bool FindPath(Gun gun, BlockGroup group, out Block block, out Vector2 point, out List<Vector2> path)
    {
        object[] args = { group, null, Vector2.zero, null };
        bool found = (bool)typeof(Gun).GetMethod("FindPathToGroup", Private).Invoke(gun, args);
        block = (Block)args[1]; point = (Vector2)args[2]; path = (List<Vector2>)args[3]; return found;
    }
    private static void Invoke(Bullet bullet, string method, params object[] args) => typeof(Bullet).GetMethod(method, Private).Invoke(bullet, args);
    private static bool Inside(Bounds board, Vector2 point) => point.x >= board.min.x - .0001f && point.x <= board.max.x + .0001f && point.y >= board.min.y - .0001f && point.y <= board.max.y + .0001f;
    private struct Stats { public int foreground, outside; public float minY, maxY; }
    private static Stats RenderTrail(Camera camera, TrailRenderer trail, Color32[] baseline, Bounds board,
        Vector3[] points, string name, StringBuilder report)
    {
        // Native TrailRenderer does not necessarily update its render mesh while an
        // editor preview is paused. Bake its geometry explicitly before rendering.
        var mesh = new Mesh { name = "Actual Bullet Trail Snapshot" };
        Mesh controlledRibbon = null;
        GameObject snapshot = null;
        bool wasEnabled = trail.enabled;
        try
        {
            trail.BakeMesh(mesh, camera, true);
            report.AppendLine(name + " native positions=" + trail.positionCount + " baked vertices=" + mesh.vertexCount + " bounds=" + mesh.bounds);
            Color[] nativeColors = mesh.colors;
            Vector2[] nativeUvs = mesh.uv;
            report.AppendLine("  shader=" + trail.sharedMaterial.shader.name + " tint=" + trail.sharedMaterial.GetColor("_Color") +
                " triangles=" + mesh.triangles.Length + " colors=" + nativeColors.Length + " alphaMax=" +
                (nativeColors.Length == 0 ? -1 : nativeColors.Max(color => color.a)) + " uvCount=" + nativeUvs.Length +
                " uvY=" + (nativeUvs.Length == 0 ? "none" : nativeUvs.Min(uv => uv.y) + ".." + nativeUvs.Max(uv => uv.y)));
            snapshot = new GameObject("Trail Snapshot", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(snapshot, trail.gameObject.scene);
            // BakeMesh(useTransform:true) already includes the world transform.
            snapshot.transform.position = Vector3.zero;
            snapshot.transform.rotation = Quaternion.identity;
            snapshot.transform.localScale = Vector3.one;
            snapshot.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = snapshot.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = trail.sharedMaterial;
            renderer.sortingLayerID = trail.sortingLayerID; renderer.sortingOrder = trail.sortingOrder;
            var properties = new MaterialPropertyBlock(); trail.GetPropertyBlock(properties); renderer.SetPropertyBlock(properties);
            trail.enabled = false;
            Require(mesh.vertexCount > 0, "Editor preview did not bake native trail geometry.");
            var control = new MaterialPropertyBlock(); trail.GetPropertyBlock(control);
            control.SetFloat("_BoardClipEnabled", 0f);
            renderer.SetPropertyBlock(control);
            Stats unmasked = RenderStats(camera, baseline, board, name + "-unclipped-control", report);
            if (unmasked.foreground <= 50)
            {
                // Edit previews can bake a native trail whose simulated colors/UVs
                // remain transparent. Verify the actual trail material/property block
                // on an opaque rounded ribbon across the same board boundaries.
                controlledRibbon = BuildRibbon(points[0], points[points.Length - 1], .2f);
                snapshot.GetComponent<MeshFilter>().sharedMesh = controlledRibbon;
                report.AppendLine("  Native trail is not visibly simulated in edit preview; testing its actual shader and property block on a controlled rounded ribbon.");
                unmasked = RenderStats(camera, baseline, board, name + "-unclipped-control", report);
            }
            Require(unmasked.foreground > 50 && unmasked.outside > 50, "Unclipped trail material control must visibly cross the board edge.");
            renderer.SetPropertyBlock(properties);
            return RenderStats(camera, baseline, board, name, report);
        }
        finally
        {
            trail.enabled = wasEnabled;
            if (snapshot != null) UnityEngine.Object.DestroyImmediate(snapshot);
            if (controlledRibbon != null) UnityEngine.Object.DestroyImmediate(controlledRibbon);
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
    private static Mesh BuildRibbon(Vector3 from, Vector3 to, float width)
    {
        Vector3 direction = (to - from).normalized;
        Vector3 side = new Vector3(-direction.y, direction.x) * (width * .5f);
        var vertices = new List<Vector3> { from - side, from + side, to + side, to - side };
        var uvs = new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        var triangles = new List<int> { 0, 1, 2, 0, 2, 3 };
        foreach (Vector3 center in new[] { from, to })
        {
            int start = vertices.Count;
            vertices.Add(center); uvs.Add(new Vector2(.5f, .5f));
            for (int i = 0; i <= 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                Vector3 radial = direction * Mathf.Cos(angle) * width * .5f + side * Mathf.Sin(angle);
                vertices.Add(center + radial); uvs.Add(new Vector2(.5f, .5f + Mathf.Sin(angle) * .5f));
                if (i > 0) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
            }
        }
        var ribbon = new Mesh { name = "Controlled Round Trail Shader Probe" };
        ribbon.SetVertices(vertices); ribbon.SetUVs(0, uvs); ribbon.SetTriangles(triangles, 0);
        ribbon.colors = Enumerable.Repeat(Color.white, vertices.Count).ToArray();
        ribbon.RecalculateBounds();
        return ribbon;
    }
    private static Stats RenderStats(Camera camera, Color32[] baseline, Bounds board, string name, StringBuilder report)
    {
        Color32[] pixels = Capture(camera, name);
        float units = camera.orthographicSize * 2 / Size;
        float left = camera.transform.position.x - camera.orthographicSize, bottom = camera.transform.position.y - camera.orthographicSize;
        var stats = new Stats { minY = float.PositiveInfinity, maxY = float.NegativeInfinity };
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
        {
            int i = y * Size + x;
            if (Mathf.Max(Mathf.Abs(pixels[i].r - baseline[i].r), Mathf.Abs(pixels[i].g - baseline[i].g), Mathf.Abs(pixels[i].b - baseline[i].b)) < 18) continue;
            stats.foreground++;
            float worldX = left + (x + .5f) * units, worldY = bottom + (y + .5f) * units;
            stats.minY = Mathf.Min(stats.minY, worldY); stats.maxY = Mathf.Max(stats.maxY, worldY);
            if (worldX < board.min.x - units || worldX > board.max.x + units || worldY < board.min.y - units || worldY > board.max.y + units) stats.outside++;
        }
        report.AppendLine(name + ": pixels=" + stats.foreground + " outside=" + stats.outside);
        return stats;
    }
    private static Color32[] Capture(Camera camera, string name)
    {
        camera.Render(); RenderTexture.active = camera.targetTexture;
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        try { texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); texture.Apply(); if (name != null) File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG()); return texture.GetPixels32(); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
