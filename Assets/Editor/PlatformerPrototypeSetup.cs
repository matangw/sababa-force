using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds Player, Level, Bullet prefab, and camera follow in the active scene.</summary>
public static class PlatformerPrototypeSetup
{
    public static readonly Color ThemeLevelBlue = new Color(0.2f, 0.45f, 0.95f, 1f);
    public static readonly Color ThemePlayerGreen = new Color(0.15f, 0.75f, 0.35f, 1f);

    /// <summary>32×32 white PNG imported as sprite (32 PPU = 1 world unit). Serialized on prefabs — do not use runtime Sprite.Create for these.</summary>
    public const string SquareSpriteAssetPath = "Assets/Art/Platformer/WhiteSquare.png";

    const string MenuPathTools = "Tools/2D Platformer/Setup Prototype Scene";
    const string MenuPathRoot = "Platformer/Setup Prototype Scene";
    const string MenuPathWindow = "Window/2D Platformer/Setup Prototype Scene";
    const string MenuPathGameObject = "GameObject/2D Platformer/Setup Prototype Scene";

    const string ThemeMenuTools = "Tools/2D Platformer/Apply Visual Theme";
    const string ThemeMenuRoot = "Platformer/Apply Visual Theme (Blue / Green / White)";
    const string ThemeMenuWindow = "Window/2D Platformer/Apply Visual Theme";
    const string ThemeMenuGameObject = "GameObject/2D Platformer/Apply Visual Theme";

    const string RebuildLevelMenu = "Platformer/Rebuild Dense Level Only";
    const string RebuildLevelMenuTools = "Tools/2D Platformer/Rebuild Dense Level Only";

    [MenuItem(RebuildLevelMenu, false, 21)]
    [MenuItem(RebuildLevelMenuTools, false, 21)]
    static void RebuildDenseLevelOnly()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
        {
            EditorUtility.DisplayDialog("Rebuild Level", "Ground layer not found. Run full Setup Prototype Scene first.", "OK");
            return;
        }

        GameObject levelRoot = GameObject.Find("Level");
        if (levelRoot == null)
        {
            levelRoot = new GameObject("Level");
            Undo.RegisterCreatedObjectUndo(levelRoot, "Create Level");
        }
        else
        {
            for (int i = levelRoot.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(levelRoot.transform.GetChild(i).gameObject);
        }

        Sprite square = GetSquareSprite();
        if (square == null)
        {
            EditorUtility.DisplayDialog("Rebuild Level", "Could not load WhiteSquare sprite at " + SquareSpriteAssetPath, "OK");
            return;
        }
        BuildDenseLevel(levelRoot.transform, square, groundLayer, ThemeLevelBlue);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Rebuild Level", "Dense level geometry replaced under Level.", "OK");
    }

    [MenuItem(MenuPathTools, false, 1)]
    [MenuItem(MenuPathRoot, false, 1)]
    [MenuItem(MenuPathWindow, false, 1)]
    [MenuItem(MenuPathGameObject, false, 1)]
    static void Setup()
    {
        int playerLayer = EnsureUserLayer("Player");
        int groundLayer = EnsureUserLayer("Ground");
        int projectileLayer = EnsureUserLayer("Projectile");
        if (playerLayer < 0 || groundLayer < 0 || projectileLayer < 0)
        {
            EditorUtility.DisplayDialog("Platformer Setup", "Could not add User layers (need free slots for Player, Ground, Projectile). Free slots in Tags & Layers, then run again.", "OK");
            return;
        }

        // Allow Projectile vs Player so bullets can hit players (PvE/PvP). Per-shooter ignores handle self-collision.
        Physics2D.IgnoreLayerCollision(projectileLayer, playerLayer, false);

        LayerMask groundMask = 1 << groundLayer;

        Sprite square = GetSquareSprite();
        if (square == null)
        {
            EditorUtility.DisplayDialog("Platformer Setup", "Missing sprite asset: " + SquareSpriteAssetPath, "OK");
            return;
        }

        GameObject player = new GameObject("Player");
        Undo.RegisterCreatedObjectUndo(player, "Create Player");
        player.tag = "Player";

        var sr = player.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = ThemePlayerGreen;

        var rb = player.AddComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 3f;

        var box = player.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.9f, 0.9f);

        player.layer = playerLayer;
        player.transform.position = new Vector3(0f, 2f, 0f);

        var playerController = player.AddComponent<PlayerController>();
        var shooter = player.AddComponent<Shooter>();

        SerializedObject pcSo = new SerializedObject(playerController);
        pcSo.FindProperty("groundLayers").intValue = groundMask;
        pcSo.ApplyModifiedPropertiesWithoutUndo();

        const string prefabDir = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabDir))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject bulletGo = new GameObject("Bullet");
        bulletGo.transform.localScale = new Vector3(0.22f, 0.22f, 1f);
        var bSr = bulletGo.AddComponent<SpriteRenderer>();
        bSr.sprite = square;
        bSr.color = ThemeLevelBlue;

        var bRb = bulletGo.AddComponent<Rigidbody2D>();
        bRb.gravityScale = 0f;
        bRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var bBox = bulletGo.AddComponent<BoxCollider2D>();
        bBox.size = new Vector2(0.35f, 0.35f);

        bulletGo.AddComponent<Bullet>();
        bulletGo.layer = projectileLayer;

        string bulletPath = $"{prefabDir}/Bullet.prefab";
        GameObject bulletPrefab = PrefabUtility.SaveAsPrefabAsset(bulletGo, bulletPath);
        Object.DestroyImmediate(bulletGo);

        SerializedObject shSo = new SerializedObject(shooter);
        shSo.FindProperty("bulletPrefab").objectReferenceValue = bulletPrefab;
        shSo.FindProperty("playerController").objectReferenceValue = playerController;
        shSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject levelRoot = GameObject.Find("Level");
        if (levelRoot == null)
        {
            levelRoot = new GameObject("Level");
            Undo.RegisterCreatedObjectUndo(levelRoot, "Create Level");
        }
        else
        {
            for (int i = levelRoot.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(levelRoot.transform.GetChild(i).gameObject);
        }

        BuildDenseLevel(levelRoot.transform, square, groundLayer, ThemeLevelBlue);

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Camera ortho");
            mainCam.orthographic = true;
            mainCam.orthographicSize = 8f;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = Color.white;
            var follow = mainCam.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = mainCam.gameObject.AddComponent<CameraFollow>();
                Undo.RegisterCreatedObjectUndo(follow, "Add CameraFollow");
            }
            SerializedObject cfSo = new SerializedObject(follow);
            cfSo.FindProperty("target").objectReferenceValue = player.transform;
            cfSo.ApplyModifiedPropertiesWithoutUndo();
        }

        string playerPrefabPath = $"{prefabDir}/Player.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(player, playerPrefabPath, InteractionMode.AutomatedAction);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = player;
        EditorUtility.DisplayDialog("Platformer Setup", "Player (green, saved as Prefabs/Player), Level (blue), Bullet prefab (blue), white camera background, and camera follow are set up.\n\nMenus: Platformer, Window, Tools, or GameObject.", "OK");
    }

    [MenuItem(ThemeMenuTools, false, 11)]
    [MenuItem(ThemeMenuRoot, false, 11)]
    [MenuItem(ThemeMenuWindow, false, 11)]
    [MenuItem(ThemeMenuGameObject, false, 11)]
    static void ApplyVisualThemeMenu()
    {
        ApplyVisualThemeToActiveScene(ensurePlayerPrefab: true);
        EditorUtility.DisplayDialog("Visual Theme", "Main Camera: white background.\nLevel sprites: blue.\nPlayer: green; saved/updated as Assets/Prefabs/Player.prefab.\nBullet prefab: blue.", "OK");
    }

    /// <summary>Blue level &amp; bullet, green player, white camera; optionally connects Player to Assets/Prefabs/Player.prefab.</summary>
    public static void ApplyVisualThemeToActiveScene(bool ensurePlayerPrefab)
    {
        Sprite square = GetSquareSprite();

        var mainCam = Camera.main;
        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Camera background");
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = Color.white;
        }

        var level = GameObject.Find("Level");
        if (level != null)
        {
            foreach (var r in level.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Undo.RecordObject(r, "Level sprite/color");
                if (square != null)
                    r.sprite = square;
                r.color = ThemeLevelBlue;
            }
        }

        const string prefabDir = "Assets/Prefabs";
        string bulletPath = $"{prefabDir}/Bullet.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(bulletPath) != null)
        {
            GameObject bulletContents = PrefabUtility.LoadPrefabContents(bulletPath);
            try
            {
                var bSr = bulletContents.GetComponent<SpriteRenderer>();
                if (bSr != null)
                {
                    Undo.RecordObject(bSr, "Bullet sprite/color");
                    if (square != null)
                        bSr.sprite = square;
                    bSr.color = ThemeLevelBlue;
                }
                PrefabUtility.SaveAsPrefabAsset(bulletContents, bulletPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(bulletContents);
            }
        }

        var player = GameObject.Find("Player");
        if (player != null)
        {
            var pSr = player.GetComponent<SpriteRenderer>();
            if (pSr != null)
            {
                Undo.RecordObject(pSr, "Player sprite/color");
                if (square != null)
                    pSr.sprite = square;
                pSr.color = ThemePlayerGreen;
            }

            if (ensurePlayerPrefab)
            {
                if (!AssetDatabase.IsValidFolder(prefabDir))
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                string playerPath = $"{prefabDir}/Player.prefab";
                if (PrefabUtility.IsPartOfPrefabInstance(player))
                    PrefabUtility.ApplyPrefabInstance(player, InteractionMode.AutomatedAction);
                else
                    PrefabUtility.SaveAsPrefabAssetAndConnect(player, playerPath, InteractionMode.AutomatedAction);
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    /// <summary>Dense playfield: thick base, tall vertical fills to the lower bound, and wide overlapping ledges.</summary>
    static void BuildDenseLevel(Transform parent, Sprite square, int groundLayer, Color color)
    {
        int id = 0;
        string NextName(string prefix) => $"{prefix}_{id++}";

        const float worldXMin = -12f;
        const float worldXMax = 118f;
        float worldCenterX = (worldXMin + worldXMax) * 0.5f;
        float worldSpanX = worldXMax - worldXMin;

        // Deep base slab — most of the lower half is solid geometry
        const float baseBottomY = -14f;
        const float baseTopY = -2.5f;
        float baseCenterY = (baseBottomY + baseTopY) * 0.5f;
        float baseHeight = baseTopY - baseBottomY;
        CreatePlatform(parent, NextName("Base"), square, groundLayer,
            new Vector3(worldCenterX, baseCenterY, 0f),
            new Vector3(worldSpanX + 24f, baseHeight, 1f), color);

        // Tall vertical ribs from the base upward (fill vertical space, read as “walls / columns”)
        const float pillarWidth = 3.2f;
        const float pillarBottomY = baseTopY;
        const float pillarTopY = 18f;
        float pillarHeight = pillarTopY - pillarBottomY;
        float pillarCenterY = (pillarBottomY + pillarTopY) * 0.5f;
        for (float x = worldXMin + 4f; x < worldXMax; x += 9.5f)
        {
            CreatePlatform(parent, NextName("Pillar"), square, groundLayer,
                new Vector3(x, pillarCenterY, 0f),
                new Vector3(pillarWidth, pillarHeight, 1f), color);
        }

        // Secondary thicker columns for even less empty space
        for (float x = worldXMin + 9f; x < worldXMax; x += 19f)
        {
            CreatePlatform(parent, NextName("Column"), square, groundLayer,
                new Vector3(x, pillarCenterY + 1f, 0f),
                new Vector3(4.5f, pillarHeight + 4f, 1f), color);
        }

        // Wide floor band sitting on top of the base (continuous run)
        const float groundDeckY = -1.8f;
        const float deckThickness = 1.2f;
        CreatePlatform(parent, NextName("GroundDeck"), square, groundLayer,
            new Vector3(worldCenterX, groundDeckY, 0f),
            new Vector3(worldSpanX + 18f, deckThickness, 1f), color);

        // Stacked wide walkways — staggered so horizontal coverage is almost continuous
        float[] ledgeHeights = { 0.8f, 2.8f, 5f, 7.5f, 10f, 12.5f, 15f, 17.5f };
        float ledgeWidth = 14f;
        float ledgeThick = 1f;
        for (int row = 0; row < ledgeHeights.Length; row++)
        {
            float y = ledgeHeights[row];
            float phase = (row % 3) * 4.5f;
            for (float x = worldXMin + phase; x < worldXMax + 8f; x += ledgeWidth * 0.92f)
            {
                CreatePlatform(parent, NextName("Ledge"), square, groundLayer,
                    new Vector3(x, y, 0f),
                    new Vector3(ledgeWidth, ledgeThick, 1f), color);
            }
        }

        // Large mid-level slabs (bulk fill)
        float[] bulkCentersX = { 8f, 28f, 48f, 68f, 88f, 108f };
        foreach (float bx in bulkCentersX)
        {
            CreatePlatform(parent, NextName("Bulk"), square, groundLayer,
                new Vector3(bx, 9f, 0f),
                new Vector3(18f, 2.8f, 1f), color);
            CreatePlatform(parent, NextName("BulkLo"), square, groundLayer,
                new Vector3(bx + 9f, 3.5f, 0f),
                new Vector3(16f, 2.2f, 1f), color);
        }

        // Upper canopy — closes off top of play space so the volume feels filled
        float canopyY = 20.5f;
        CreatePlatform(parent, NextName("Canopy"), square, groundLayer,
            new Vector3(worldCenterX, canopyY, 0f),
            new Vector3(worldSpanX + 20f, 2.4f, 1f), color);
    }

    static void CreatePlatform(Transform parent, string name, Sprite sprite, int layer, Vector3 position, Vector3 scale, Color color)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.layer = layer;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        go.AddComponent<BoxCollider2D>();
    }

    /// <summary>Loads the shared white square sprite from disk so references persist in scenes and prefabs.</summary>
    public static Sprite GetSquareSprite()
    {
        EnsureWhiteSquareTextureAsset();
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpriteAssetPath);
        if (s != null)
            return s;
        return AssetDatabase.LoadAllAssetsAtPath(SquareSpriteAssetPath).OfType<Sprite>().FirstOrDefault();
    }

    static void EnsureWhiteSquareTextureAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder("Assets/Art/Platformer"))
            AssetDatabase.CreateFolder("Assets/Art", "Platformer");

        if (!File.Exists(SquareSpriteAssetPath))
        {
            const int w = 32;
            var tex = new Texture2D(w, w, TextureFormat.RGBA32, false);
            var cols = new Color32[w * w];
            for (int i = 0; i < cols.Length; i++)
                cols[i] = Color.white;
            tex.SetPixels32(cols);
            tex.Apply();
            try
            {
                File.WriteAllBytes(SquareSpriteAssetPath, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(SquareSpriteAssetPath, ImportAssetOptions.ForceUpdate);
        }

        var ti = AssetImporter.GetAtPath(SquareSpriteAssetPath) as TextureImporter;
        if (ti == null)
            return;

        bool dirty = false;
        if (ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType = TextureImporterType.Sprite;
            dirty = true;
        }
        if (ti.spriteImportMode != SpriteImportMode.Single)
        {
            ti.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }
        if (Mathf.Abs(ti.spritePixelsPerUnit - 32f) > 0.001f)
        {
            ti.spritePixelsPerUnit = 32f;
            dirty = true;
        }
        if (ti.filterMode != FilterMode.Point)
        {
            ti.filterMode = FilterMode.Point;
            dirty = true;
        }
        if (dirty)
            ti.SaveAndReimport();
    }

    static int EnsureUserLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0)
            return existing;

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }
            if (sp.stringValue == layerName)
                return i;
        }

        return -1;
    }
}
