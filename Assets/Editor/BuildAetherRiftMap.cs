using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Ability;
using Enigma.Objective;

public static class BuildAetherRiftMap
{
    private const string ScenePath   = "Assets/Scenes/AetherRift_Map.unity";
    private const string MatDir      = "Assets/_Project/Materials/Map";
    private const string PrefabDir   = "Assets/_Project/Prefabs";
    private const string SkillDir    = "Assets/_Project/Data/Skills";

    public static void Execute()
    {
        // ディレクトリ確保
        EnsureDir(MatDir);
        EnsureDir(PrefabDir);
        EnsureDir(SkillDir);
        EnsureDir("Assets/Scenes");

        // 1. 空シーン作成
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. マテリアル生成
        var matGround    = GetOrCreateMat("Ground",      new Color(0.16f, 0.18f, 0.16f));
        var matLane      = GetOrCreateMat("Lane",        new Color(0.45f, 0.45f, 0.42f));
        var matRiver     = GetOrCreateMat("River",       new Color(0.15f, 0.35f, 0.70f));
        var matPit       = GetOrCreateMat("Pit",         new Color(0.25f, 0.15f, 0.35f));
        var matJungle    = GetOrCreateMat("JungleWall",  new Color(0.12f, 0.30f, 0.16f));
        var matBlue      = GetOrCreateMat("TeamBlue",    new Color(0.18f, 0.42f, 0.95f));
        var matRed       = GetOrCreateMat("TeamRed",     new Color(0.85f, 0.25f, 0.25f));
        var matDummy     = GetOrCreateMat("Dummy",       Color.red);
        var matProj      = GetOrCreateMat("Projectile",  Color.cyan);
        var matBoss      = GetOrCreateMat("Boss",        new Color(0.25f, 0.10f, 0.35f));

        // 透明マテリアル（TargetRing / Telegraph 用）
        var matRing      = GetOrCreateTransparentMat("TargetRing",  new Color(1f, 0.9f, 0f, 0.45f));
        var matTelegraph = GetOrCreateTransparentMat("Telegraph",   new Color(1f, 0.1f, 0.1f, 0.35f));
        var matArrow     = GetOrCreateTransparentMat("DirArrow",    new Color(0.2f, 0.6f, 1f, 0.7f));
        var matAoeCircle = GetOrCreateTransparentMat("AoeCircle",   new Color(0.2f, 0.6f, 1f, 0.4f));

        // 3. ジオメトリ配置
        PlaceCube("Ground",   new Vector3(0f, -0.5f, 0f),   new Vector3(200f, 1f, 140f), matGround);
        PlaceCube("TopLane",  new Vector3(0f, 0.01f,  50f), new Vector3(200f, 0.1f, 12f), matLane);
        PlaceCube("BotLane",  new Vector3(0f, 0.01f, -50f), new Vector3(200f, 0.1f, 12f), matLane);
        PlaceCube("River",    new Vector3(0f, 0.02f, 0f),   new Vector3(14f, 0.1f, 140f), matRiver);

        var pit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pit.name = "BossPit";
        pit.transform.position   = new Vector3(0f, 0.03f, 0f);
        pit.transform.localScale = new Vector3(28f, 0.05f, 28f);
        SetStatic(pit);
        SetMat(pit, matPit);

        PlaceCube("WallNorth",  new Vector3(0f,  2f,  71f), new Vector3(200f, 4f, 2f), matJungle);
        PlaceCube("WallSouth",  new Vector3(0f,  2f, -71f), new Vector3(200f, 4f, 2f), matJungle);
        PlaceCube("WallEast",   new Vector3( 101f, 2f, 0f), new Vector3(2f, 4f, 140f), matJungle);
        PlaceCube("WallWest",   new Vector3(-101f, 2f, 0f), new Vector3(2f, 4f, 140f), matJungle);

        PlaceCube("JungleBorderTopN_W", new Vector3(-30f, 2f,  44f), new Vector3(46f, 4f, 2f), matJungle);
        PlaceCube("JungleBorderTopN_E", new Vector3( 30f, 2f,  44f), new Vector3(46f, 4f, 2f), matJungle);
        PlaceCube("JungleBorderTopS_W", new Vector3(-30f, 2f,  38f), new Vector3(46f, 4f, 2f), matJungle);
        PlaceCube("JungleBorderTopS_E", new Vector3( 30f, 2f,  38f), new Vector3(46f, 4f, 2f), matJungle);
        PlaceCube("JungleBorderBotN_W", new Vector3(-30f, 2f, -38f), new Vector3(46f, 4f, 2f), matJungle);
        PlaceCube("JungleBorderBotN_E", new Vector3( 30f, 2f, -38f), new Vector3(46f, 4f, 2f), matJungle);
        PlaceCube("JungleBorderBotS_W", new Vector3(-30f, 2f, -44f), new Vector3(46f, 4f, 2f), matJungle);
        PlaceCube("JungleBorderBotS_E", new Vector3( 30f, 2f, -44f), new Vector3(46f, 4f, 2f), matJungle);

        PlaceCube("JungleSideWest", new Vector3(-45f, 2f, 0f), new Vector3(2f, 4f, 40f), matJungle);
        PlaceCube("JungleSideEast", new Vector3( 45f, 2f, 0f), new Vector3(2f, 4f, 40f), matJungle);

        PlaceCube("PitWallNorth",  new Vector3(0f,  2f,  16f),   new Vector3(20f, 4f, 2f), matJungle);
        PlaceCube("PitWallSouth",  new Vector3(0f,  2f, -16f),   new Vector3(20f, 4f, 2f), matJungle);
        PlaceCube("PitWallWestN",  new Vector3(-10f, 2f,  10f),  new Vector3(2f, 4f, 12f), matJungle);
        PlaceCube("PitWallWestS",  new Vector3(-10f, 2f, -10f),  new Vector3(2f, 4f, 12f), matJungle);
        PlaceCube("PitWallEastN",  new Vector3( 10f, 2f,  10f),  new Vector3(2f, 4f, 12f), matJungle);
        PlaceCube("PitWallEastS",  new Vector3( 10f, 2f, -10f),  new Vector3(2f, 4f, 12f), matJungle);

        PlaceTower("Tower_BTop",    new Vector3(-55f, 4f,  50f), matBlue);
        PlaceTower("Tower_BBot",    new Vector3(-55f, 4f, -50f), matBlue);
        PlaceTower("Tower_BMidTop", new Vector3(-80f, 4f,  50f), matBlue);
        PlaceTower("Tower_BMidBot", new Vector3(-80f, 4f, -50f), matBlue);
        PlaceTower("Tower_RTop",    new Vector3( 55f, 4f,  50f), matRed);
        PlaceTower("Tower_RBot",    new Vector3( 55f, 4f, -50f), matRed);
        PlaceTower("Tower_RMidTop", new Vector3( 80f, 4f,  50f), matRed);
        PlaceTower("Tower_RMidBot", new Vector3( 80f, 4f, -50f), matRed);

        PlaceCube("Base_Blue", new Vector3(-95f, 0.5f, 0f), new Vector3(20f, 1f, 30f), matBlue);
        PlaceCube("Base_Red",  new Vector3( 95f, 0.5f, 0f), new Vector3(20f, 1f, 30f), matRed);

        PlaceTitan("Titan_Blue", new Vector3(-95f, 4f, 0f), matBlue);
        PlaceTitan("Titan_Red",  new Vector3( 95f, 4f, 0f), matRed);

        // 4. ライティング
        var dirLight = new GameObject("Directional Light");
        var light = dirLight.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.color     = Color.white;
        light.intensity = 130000f;
        dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Sky and Fog Volume
        var skyGo   = new GameObject("Sky and Fog Volume");
        var volume  = skyGo.AddComponent<Volume>();
        volume.isGlobal = true;
        var profilePath = "Assets/Settings/SkyandFogSettingsProfile.asset";
        var profile     = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile != null) volume.sharedProfile = profile;

        // 5. Projectile プレハブ
        var projGo  = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projGo.name = "Projectile";
        projGo.transform.localScale = Vector3.one * 0.3f;
        SetMat(projGo, matProj);
        var projCol = projGo.GetComponent<SphereCollider>();
        if (projCol != null) projCol.isTrigger = true;
        projGo.AddComponent<Projectile>();
        var projPrefabPath = PrefabDir + "/Projectile.prefab";
        var projPrefab     = PrefabUtility.SaveAsPrefabAsset(projGo, projPrefabPath);
        Object.DestroyImmediate(projGo);

        // 6. TargetRing プレハブ（半径1.2 の薄い円柱、半透明黄、コライダーなし）
        var ringGo  = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringGo.name = "TargetRing";
        ringGo.transform.localScale = new Vector3(2.4f, 0.02f, 2.4f); // 直径2.4
        SetMat(ringGo, matRing);
        Object.DestroyImmediate(ringGo.GetComponent<CapsuleCollider>());
        var ringPrefabPath = PrefabDir + "/TargetRing.prefab";
        var ringPrefab     = PrefabUtility.SaveAsPrefabAsset(ringGo, ringPrefabPath);
        Object.DestroyImmediate(ringGo);

        // 7. Telegraph プレハブ（薄い円柱、半透明赤、TelegraphCircle コンポーネント付き）
        var telegraphGo  = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        telegraphGo.name = "Telegraph";
        telegraphGo.transform.localScale = new Vector3(1f, 0.02f, 1f); // Init でスケール設定
        SetMat(telegraphGo, matTelegraph);
        Object.DestroyImmediate(telegraphGo.GetComponent<CapsuleCollider>());
        telegraphGo.AddComponent<TelegraphCircle>();
        var telegraphPrefabPath = PrefabDir + "/Telegraph.prefab";
        var telegraphPrefab     = PrefabUtility.SaveAsPrefabAsset(telegraphGo, telegraphPrefabPath);
        Object.DestroyImmediate(telegraphGo);

        // 8. SkillDefinition アセット生成
        var skillSlash = GetOrCreateSkillDefinition("Skill_MagicSlash",
            "魔導斬撃", SkillTargeting.Directional, 25f, 25f, 0f, 4f, 30f);
        var skillAoe = GetOrCreateSkillDefinition("Skill_ExplosionCircle",
            "爆裂魔法陣", SkillTargeting.GroundAoe, 40f, 20f, 4f, 8f, 0f);
        var skillChase = GetOrCreateSkillDefinition("Skill_Chase",
            "追撃", SkillTargeting.Targeted, 30f, 15f, 0f, 6f, 0f);

        // 9. プレイヤー
        var playerSpawnPos = new Vector3(-90f, 1.1f, 50f);
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag  = "Player";
        player.transform.position = playerSpawnPos;
        SetMat(player, matBlue);

        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;

        player.AddComponent<PlayerController>();
        var healthComp = player.AddComponent<HealthComponent>();
        // HP 200 は HealthComponent のデフォルト値と同じだが SerializedObject で明示
        var soHealth = new SerializedObject(healthComp);
        soHealth.FindProperty("_maxHp").floatValue = 200f;
        soHealth.ApplyModifiedPropertiesWithoutUndo();

        // SpawnPoint 空 GO
        var spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = playerSpawnPos;

        var respawn = player.AddComponent<PlayerRespawn>();
        var soRespawn = new SerializedObject(respawn);
        soRespawn.FindProperty("_spawnPoint").objectReferenceValue = spawnPoint.transform;
        soRespawn.ApplyModifiedPropertiesWithoutUndo();

        var targeting = player.AddComponent<TargetingSystem>();
        var soTargeting = new SerializedObject(targeting);
        // TargetRing プレハブを結線
        soTargeting.FindProperty("_targetRingPrefab").objectReferenceValue = ringPrefab;
        soTargeting.ApplyModifiedPropertiesWithoutUndo();

        var autoAttack = player.AddComponent<AutoAttack>();
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(player.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.6f, 0.6f);
        var soAutoAttack = new SerializedObject(autoAttack);
        soAutoAttack.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab.GetComponent<Projectile>();
        soAutoAttack.FindProperty("_muzzle").objectReferenceValue           = muzzle.transform;
        soAutoAttack.ApplyModifiedPropertiesWithoutUndo();

        // SkillCaster
        var skillCaster = player.AddComponent<SkillCaster>();

        // 方向インジケーター (細長 Quad)
        var dirArrowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dirArrowGo.name = "DirectionArrow";
        dirArrowGo.transform.SetParent(player.transform, false);
        dirArrowGo.transform.localPosition = new Vector3(0f, 0.1f, 2f);
        dirArrowGo.transform.localScale    = new Vector3(0.3f, 4f, 1f);
        SetMat(dirArrowGo, matArrow);
        Object.DestroyImmediate(dirArrowGo.GetComponent<MeshCollider>());
        dirArrowGo.SetActive(false);

        // AoE インジケーター (薄い円柱)
        var aoeCircleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        aoeCircleGo.name = "AoeCircle";
        aoeCircleGo.transform.SetParent(player.transform, false);
        aoeCircleGo.transform.localPosition = Vector3.zero;
        aoeCircleGo.transform.localScale    = new Vector3(8f, 0.02f, 8f); // 半径4 の円柱
        SetMat(aoeCircleGo, matAoeCircle);
        Object.DestroyImmediate(aoeCircleGo.GetComponent<CapsuleCollider>());
        aoeCircleGo.SetActive(false);

        var soSkillCaster = new SerializedObject(skillCaster);
        // _skills[0..2] 結線
        var skillsProp = soSkillCaster.FindProperty("_skills");
        skillsProp.arraySize = 4;
        skillsProp.GetArrayElementAtIndex(0).objectReferenceValue = skillSlash;
        skillsProp.GetArrayElementAtIndex(1).objectReferenceValue = skillAoe;
        skillsProp.GetArrayElementAtIndex(2).objectReferenceValue = skillChase;
        skillsProp.GetArrayElementAtIndex(3).objectReferenceValue = null;
        soSkillCaster.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab.GetComponent<Projectile>();
        soSkillCaster.FindProperty("_telegraphPrefab").objectReferenceValue  = telegraphPrefab.GetComponent<TelegraphCircle>();
        soSkillCaster.FindProperty("_directionIndicator").objectReferenceValue = dirArrowGo;
        soSkillCaster.FindProperty("_aoeIndicator").objectReferenceValue       = aoeCircleGo;
        soSkillCaster.FindProperty("_targeting").objectReferenceValue          = targeting;
        soSkillCaster.FindProperty("_muzzle").objectReferenceValue             = muzzle.transform;
        soSkillCaster.ApplyModifiedPropertiesWithoutUndo();

        // PlayerController のカメラ参照は後で設定

        // 10. Main Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        var orbitCam = camGo.AddComponent<OrbitCamera>();

        // PlayerController
        var soPlayer = new SerializedObject(player.GetComponent<PlayerController>());
        soPlayer.FindProperty("_cameraTransform").objectReferenceValue = camGo.transform;
        soPlayer.ApplyModifiedPropertiesWithoutUndo();

        // OrbitCamera
        var soCam = new SerializedObject(orbitCam);
        soCam.FindProperty("_target").objectReferenceValue = player.transform;
        soCam.ApplyModifiedPropertiesWithoutUndo();

        // 11. ターゲットダミー 2体（現状維持）
        CreateDummy("Dummy_A", new Vector3(-40f, 1f, 50f), matDummy);
        CreateDummy("Dummy_B", new Vector3(-20f, 1f, 50f), matDummy);

        // 12. ニュートラルボス（ボスピット中央）
        CreateBoss(telegraphPrefab, matBoss);

        // 13. シーン保存
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BuildAetherRiftMap] AetherRift_Map.unity を保存しました。");
    }

    // ---- ヘルパー ----

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private static Material GetOrCreateMat(string name, Color color)
    {
        var path     = $"{MatDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.SetColor("_BaseColor", color);
            return existing;
        }

        var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
        var mat    = new Material(shader);
        mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material GetOrCreateTransparentMat(string name, Color color)
    {
        var path     = $"{MatDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.SetColor("_BaseColor", color);
            return existing;
        }

        // HDRP/Unlit を使用して透明度を確実に設定
        var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
        var mat    = new Material(shader);
        mat.SetColor("_BaseColor", color);

        // SurfaceType=Transparent (1)、renderQueue
        mat.SetFloat("_SurfaceType", 1f);
        mat.SetFloat("_BlendMode", 0f);         // Alpha
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static SkillDefinition GetOrCreateSkillDefinition(
        string assetName, string skillName, SkillTargeting targeting,
        float damage, float range, float radius, float cd, float projSpeed)
    {
        var path     = $"{SkillDir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (existing != null) return existing;

        var so = ScriptableObject.CreateInstance<SkillDefinition>();
        so.SkillName       = skillName;
        so.Targeting       = targeting;
        so.Damage          = damage;
        so.Range           = range;
        so.Radius          = radius;
        so.CooldownSeconds = cd;
        so.ProjectileSpeed = projSpeed;
        AssetDatabase.CreateAsset(so, path);
        return so;
    }

    private static GameObject PlaceCube(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name              = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        SetStatic(go);
        SetMat(go, mat);
        return go;
    }

    private static void PlaceTower(string name, Vector3 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name              = name;
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(4f, 4f, 4f);
        SetStatic(go);
        SetMat(go, mat);
    }

    private static void PlaceTitan(string name, Vector3 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name              = name;
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(4f, 6f, 4f);
        SetStatic(go);
        SetMat(go, mat);
    }

    private static void SetStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go,
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.BatchingStatic);
    }

    private static void SetMat(GameObject go, Material mat)
    {
        var mr = go.GetComponent<Renderer>();
        if (mr != null) mr.sharedMaterial = mat;
    }

    private static void CreateDummy(string name, Vector3 pos, Material mat)
    {
        var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        dummy.name = name;
        dummy.transform.position = pos;
        SetMat(dummy, mat);

        dummy.AddComponent<HealthComponent>();
        var td = dummy.AddComponent<TargetDummy>();

        var hpBar = new GameObject("HealthBar");
        hpBar.transform.SetParent(dummy.transform, false);
        hpBar.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        hpBar.AddComponent<HealthBarBillboard>();

        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "Background";
        bg.transform.SetParent(hpBar.transform, false);
        bg.transform.localScale = new Vector3(1.2f, 0.18f, 1f);
        var bgMat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
        bgMat.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f));
        bg.GetComponent<Renderer>().sharedMaterial = bgMat;
        Object.DestroyImmediate(bg.GetComponent<MeshCollider>());

        var fillWrapper = new GameObject("FillWrapper");
        fillWrapper.transform.SetParent(hpBar.transform, false);
        fillWrapper.transform.localPosition = new Vector3(0f, 0f, -0.001f);

        var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "Fill";
        fill.transform.SetParent(fillWrapper.transform, false);
        fill.transform.localScale = new Vector3(1.16f, 0.14f, 1f);
        var fillMat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
        fillMat.SetColor("_BaseColor", Color.green);
        fill.GetComponent<Renderer>().sharedMaterial = fillMat;
        Object.DestroyImmediate(fill.GetComponent<MeshCollider>());

        var soTd = new SerializedObject(td);
        soTd.FindProperty("_barFill").objectReferenceValue = fill.transform;
        soTd.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateBoss(GameObject telegraphPrefab, Material matBoss)
    {
        var boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        boss.name             = "NeutralBoss";
        boss.transform.position   = new Vector3(0f, 3f, 0f);
        boss.transform.localScale = new Vector3(3f, 3f, 3f); // 大型
        SetMat(boss, matBoss);

        var bossHp = boss.AddComponent<HealthComponent>();
        var soBossHp = new SerializedObject(bossHp);
        soBossHp.FindProperty("_maxHp").floatValue = 1000f;
        soBossHp.ApplyModifiedPropertiesWithoutUndo();

        var bossCtrl = boss.AddComponent<NeutralBossController>();
        var soBossCtrl = new SerializedObject(bossCtrl);
        var telegraphComponent = telegraphPrefab.GetComponent<TelegraphCircle>();
        soBossCtrl.FindProperty("_telegraphPrefab").objectReferenceValue = telegraphComponent;
        soBossCtrl.ApplyModifiedPropertiesWithoutUndo();

        // ボスの頭上 HP バー（ダミーと同じ構成）
        var hpBar = new GameObject("HealthBar");
        hpBar.transform.SetParent(boss.transform, false);
        hpBar.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        hpBar.AddComponent<HealthBarBillboard>();

        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "Background";
        bg.transform.SetParent(hpBar.transform, false);
        bg.transform.localScale = new Vector3(1.2f, 0.18f, 1f);
        var bgMat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
        bgMat.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f));
        bg.GetComponent<Renderer>().sharedMaterial = bgMat;
        Object.DestroyImmediate(bg.GetComponent<MeshCollider>());

        var fillWrapper = new GameObject("FillWrapper");
        fillWrapper.transform.SetParent(hpBar.transform, false);
        fillWrapper.transform.localPosition = new Vector3(0f, 0f, -0.001f);

        var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "Fill";
        fill.transform.SetParent(fillWrapper.transform, false);
        fill.transform.localScale = new Vector3(1.16f, 0.14f, 1f);
        var fillMat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
        fillMat.SetColor("_BaseColor", Color.red);
        fill.GetComponent<Renderer>().sharedMaterial = fillMat;
        Object.DestroyImmediate(fill.GetComponent<MeshCollider>());

        // TargetDummy を流用してボスのバー更新（リスポーンなし）
        var td = boss.AddComponent<TargetDummy>();
        var soTd = new SerializedObject(td);
        soTd.FindProperty("_barFill").objectReferenceValue = fill.transform;
        soTd.ApplyModifiedPropertiesWithoutUndo();
    }
}
