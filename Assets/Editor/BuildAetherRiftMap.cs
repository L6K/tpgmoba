using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Ability;
using Enigma.Objective;
using Enigma.UI;
using Enigma.Minion;
using Enigma.Core;

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
        var matRing        = GetOrCreateTransparentMat("TargetRing",   new Color(1f, 0.9f, 0f, 0.45f));
        var matTelegraph   = GetOrCreateTransparentMat("Telegraph",    new Color(1f, 0.1f, 0.1f, 0.35f));
        var matArrow       = GetOrCreateTransparentMat("DirArrow",     new Color(0.2f, 0.6f, 1f, 0.7f));
        var matAoeCircle   = GetOrCreateTransparentMat("AoeCircle",    new Color(0.2f, 0.6f, 1f, 0.4f));
        var matStackMarker = GetOrCreateTransparentMat("StackMarker",  new Color(1f, 0.85f, 0f, 0.5f));

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

        // projPrefab はこの時点では未生成なので後回し（後段で結線）
        PlaceTower("Tower_BTop",    new Vector3(-55f, 4f,  50f), matBlue, null);
        PlaceTower("Tower_BBot",    new Vector3(-55f, 4f, -50f), matBlue, null);
        PlaceTower("Tower_BMidTop", new Vector3(-80f, 4f,  50f), matBlue, null);
        PlaceTower("Tower_BMidBot", new Vector3(-80f, 4f, -50f), matBlue, null);
        PlaceTower("Tower_RTop",    new Vector3( 55f, 4f,  50f), matRed,  null);
        PlaceTower("Tower_RBot",    new Vector3( 55f, 4f, -50f), matRed,  null);
        PlaceTower("Tower_RMidTop", new Vector3( 80f, 4f,  50f), matRed,  null);
        PlaceTower("Tower_RMidBot", new Vector3( 80f, 4f, -50f), matRed,  null);

        PlaceCube("Base_Blue", new Vector3(-95f, 0.5f, 0f), new Vector3(20f, 1f, 30f), matBlue);
        PlaceCube("Base_Red",  new Vector3( 95f, 0.5f, 0f), new Vector3(20f, 1f, 30f), matRed);

        var blueTitanHc = PlaceTitan("Titan_Blue", new Vector3(-95f, 4f, 0f), matBlue);
        var redTitanHc  = PlaceTitan("Titan_Red",  new Vector3( 95f, 4f, 0f), matRed);

        // 4. ライティング
        var dirLight = new GameObject("Directional Light");
        var light = dirLight.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.color     = new Color(1f, 0.97f, 0.92f);
        light.intensity = 1.35f;
        dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // アニメ調スカイボックス + 環境光（URP）
        var skyMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/AnimeSky.mat");
        if (skyMat != null) RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.62f, 0.72f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.56f, 0.62f);
        RenderSettings.ambientGroundColor  = new Color(0.34f, 0.32f, 0.30f);

        // グローバルポスプロボリューム（URP）
        var postGo  = new GameObject("Global Post Volume");
        var volume  = postGo.AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/URP/EnigmaPost.asset");
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

        // 5b. Projectile プレハブをタワーの TowerAttack に結線（プレハブ生成後に実施）
        WireProjPrefabToTowers(projPrefab.GetComponent<Projectile>());

        // 8. SkillDefinition アセット生成（zeph: 既存流用、他4キャラは新規 or 既存）
        var skillSlash = GetOrCreateSkillDefinition("Skill_MagicSlash",
            "魔導斬撃", SkillTargeting.Directional, 25f, 25f, 0f, 4f, 30f);
        var skillAoe = GetOrCreateSkillDefinition("Skill_ExplosionCircle",
            "爆裂魔法陣", SkillTargeting.GroundAoe, 40f, 20f, 4f, 8f, 0f);
        var skillChase = GetOrCreateSkillDefinition("Skill_Chase",
            "追撃", SkillTargeting.Targeted, 30f, 15f, 0f, 6f, 0f);

        // garon
        var garonQ = GetOrCreateSkillDefinition("Skill_garon_Q",
            "シールドバッシュ", SkillTargeting.Directional, 15f, 15f, 0f, 5f, 25f);
        var garonW = GetOrCreateSkillDefinition("Skill_garon_W",
            "グランドスラム", SkillTargeting.GroundAoe, 30f, 12f, 5f, 9f, 0f);
        var garonE = GetOrCreateSkillDefinition("Skill_garon_E",
            "チェーンフック", SkillTargeting.Targeted, 20f, 12f, 0f, 7f, 0f);

        // veil
        var veilQ = GetOrCreateSkillDefinition("Skill_veil_Q",
            "アーケインボルト", SkillTargeting.Directional, 30f, 30f, 0f, 4f, 35f);
        var veilW = GetOrCreateSkillDefinition("Skill_veil_W",
            "量子爆発", SkillTargeting.GroundAoe, 50f, 22f, 5f, 10f, 0f);
        var veilE = GetOrCreateSkillDefinition("Skill_veil_E",
            "ヘックス", SkillTargeting.Targeted, 35f, 18f, 0f, 8f, 0f);

        // rin
        var rinQ = GetOrCreateSkillDefinition("Skill_rin_Q",
            "貫通矢", SkillTargeting.Directional, 28f, 35f, 0f, 3.5f, 45f);
        var rinW = GetOrCreateSkillDefinition("Skill_rin_W",
            "矢の雨", SkillTargeting.GroundAoe, 35f, 25f, 4.5f, 9f, 0f);
        var rinE = GetOrCreateSkillDefinition("Skill_rin_E",
            "狙撃", SkillTargeting.Targeted, 40f, 20f, 0f, 9f, 0f);

        // nova
        var novaQ = GetOrCreateSkillDefinition("Skill_nova_Q",
            "パルスウェーブ", SkillTargeting.Directional, 18f, 20f, 0f, 4f, 28f);
        var novaW = GetOrCreateSkillDefinition("Skill_nova_W",
            "リペアフィールド", SkillTargeting.GroundAoe, 20f, 18f, 5f, 8f, 0f);
        var novaE = GetOrCreateSkillDefinition("Skill_nova_E",
            "スタンボルト", SkillTargeting.Targeted, 15f, 15f, 0f, 6f, 0f);

        // CharacterData アセットへのスキル結線
        WireCharacterSkills("Char_zeph",  new[] { skillSlash, skillAoe, skillChase, null });
        WireCharacterSkills("Char_garon", new[] { garonQ, garonW, garonE, (SkillDefinition)null });
        WireCharacterSkills("Char_veil",  new[] { veilQ, veilW, veilE, (SkillDefinition)null });
        WireCharacterSkills("Char_rin",   new[] { rinQ, rinW, rinE, (SkillDefinition)null });
        WireCharacterSkills("Char_nova",  new[] { novaQ, novaW, novaE, (SkillDefinition)null });

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

        // プレイヤーは Blue チーム
        var playerTeamTag = player.AddComponent<TeamTag>();
        var soPlayerTeam  = new SerializedObject(playerTeamTag);
        soPlayerTeam.FindProperty("_team").enumValueIndex = (int)TeamId.Blue;
        soPlayerTeam.ApplyModifiedPropertiesWithoutUndo();

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

        // MatchBootstrap: ピック済みキャラのスキルを Start 時に注入する
        var bootstrap    = player.AddComponent<MatchBootstrap>();
        var soBootstrap  = new SerializedObject(bootstrap);
        soBootstrap.FindProperty("_skillCaster").objectReferenceValue = skillCaster;
        soBootstrap.ApplyModifiedPropertiesWithoutUndo();

        // MatchFlowController: タイタン死亡を監視して試合終了フローを起動する
        var matchFlowGo   = new GameObject("MatchFlow");
        var matchFlow     = matchFlowGo.AddComponent<Enigma.Core.MatchFlowController>();
        var soMatchFlow   = new SerializedObject(matchFlow);
        soMatchFlow.FindProperty("_blueTitan").objectReferenceValue = blueTitanHc;
        soMatchFlow.FindProperty("_redTitan").objectReferenceValue  = redTitanHc;
        soMatchFlow.ApplyModifiedPropertiesWithoutUndo();

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

        // プレイヤーの見た目: UnityChan モデル（© Unity Technologies Japan/UCL）
        AttachUnityChanModel(player);

        // OrbitCamera
        var soCam = new SerializedObject(orbitCam);
        soCam.FindProperty("_target").objectReferenceValue = player.transform;
        soCam.ApplyModifiedPropertiesWithoutUndo();

        // 11. ターゲットダミー 2体（現状維持）
        CreateDummy("Dummy_A", new Vector3(-40f, 1f, 50f), matDummy);
        CreateDummy("Dummy_B", new Vector3(-20f, 1f, 50f), matDummy);

        // 7b. TelegraphSector プレハブ（空 GO + MeshFilter + MeshRenderer + TelegraphSector）
        var sectorGo   = new GameObject("TelegraphSector");
        sectorGo.AddComponent<MeshFilter>();
        var sectorMr = sectorGo.AddComponent<MeshRenderer>();
        sectorMr.sharedMaterial = matTelegraph;
        sectorGo.AddComponent<TelegraphSector>();
        var sectorPrefabPath = PrefabDir + "/TelegraphSector.prefab";
        var sectorPrefab     = PrefabUtility.SaveAsPrefabAsset(sectorGo, sectorPrefabPath);
        Object.DestroyImmediate(sectorGo);

        // 7c. StackMarker プレハブ（小 Quad + HealthBarBillboard + StackMarker）
        var smGo   = GameObject.CreatePrimitive(PrimitiveType.Quad);
        smGo.name  = "StackMarker";
        smGo.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        smGo.GetComponent<Renderer>().sharedMaterial = matStackMarker;
        Object.DestroyImmediate(smGo.GetComponent<MeshCollider>());
        smGo.AddComponent<HealthBarBillboard>();
        smGo.AddComponent<StackMarker>();
        var smPrefabPath = PrefabDir + "/StackMarker.prefab";
        var smPrefab     = PrefabUtility.SaveAsPrefabAsset(smGo, smPrefabPath);
        Object.DestroyImmediate(smGo);

        // 12. ニュートラルボス（ボスピット中央）
        CreateBoss(telegraphPrefab, sectorPrefab, smPrefab, matBoss);

        // 13. ゲーム内 HUD
        var hudGo = new GameObject("GameHud");
        var hudDoc = hudGo.AddComponent<UIDocument>();

        // HomeScreenPanelSettings を流用（パネル設定が既存アセット）
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
            "Assets/_Project/UI/HomeScreenPanelSettings.asset");
        if (panelSettings != null)
            hudDoc.panelSettings = panelSettings;

        var hudUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/_Project/UI/GameHud.uxml");
        if (hudUxml != null)
            hudDoc.visualTreeAsset = hudUxml;
        EditorUtility.SetDirty(hudDoc);

        var hudCtrl = hudGo.AddComponent<GameHudController>();
        var soHudCtrl = new SerializedObject(hudCtrl);
        soHudCtrl.FindProperty("_uiDocument").objectReferenceValue   = hudDoc;
        soHudCtrl.FindProperty("_playerHealth").objectReferenceValue = healthComp;
        soHudCtrl.FindProperty("_skillCaster").objectReferenceValue  = skillCaster;
        soHudCtrl.ApplyModifiedPropertiesWithoutUndo();

        // 14. ミニオンプレハブ + スポーナー
        var minionPrefab = CreateMinionPrefab();
        PlaceMinionSpawners(minionPrefab, matBlue, matRed);

        // 15. シーン保存
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

        var shader = Shader.Find("Enigma/Toon") ?? Shader.Find("Universal Render Pipeline/Lit");
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

        // URP/Unlit の半透明設定（予兆・インジケーター系はライティング不要）
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat    = new Material(shader);
        mat.SetColor("_BaseColor", color);

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;

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

    // UnityChan モデルをプレイヤーの見た目として取り付ける。
    // 物理・操作はゲーム側（CharacterController/PlayerController）が持つため、
    // プレハブ付属の制御系コンポーネントは除去し、揺れもの・瞬きのみ残す
    private static void AttachUnityChanModel(GameObject player)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/UnityChan/Prefabs/for Locomotion/unitychan.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[BuildAetherRiftMap] unitychan.prefab が見つからないためカプセル表示のまま");
            return;
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name = "UnityChanModel";
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.SetParent(player.transform, false);
        model.transform.localPosition = new Vector3(0f, -1.05f, 0f);
        model.transform.localRotation = Quaternion.identity;

        // RequireComponent(Rigidbody) を持つ制御スクリプトを先に消さないと Rigidbody が除去できない
        foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            bool keep = n.Contains("Spring") || n.Contains("AutoBlink") || n.Contains("RandomWind");
            if (!keep) Object.DestroyImmediate(mb);
        }
        foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(rb);
        foreach (var col in model.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);

        var animator = model.GetComponent<Animator>();
        if (animator != null)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/UnityChan/Animators/UnityChanLocomotions.controller");
            if (ctrl != null) animator.runtimeAnimatorController = ctrl;
            // 位置移動は CharacterController が担うためルートモーションは切る
            animator.applyRootMotion = false;
        }

        var capsuleRenderer = player.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null) capsuleRenderer.enabled = false;

        var soPc = new SerializedObject(player.GetComponent<PlayerController>());
        soPc.FindProperty("_animator").objectReferenceValue = animator;
        soPc.ApplyModifiedPropertiesWithoutUndo();

        ApplyToonMaterials(model);
    }

    // 元マテリアルのメインテクスチャを引き継いだ Enigma/Toon マテリアルに差し替える
    private static void ApplyToonMaterials(GameObject model)
    {
        const string dir = "Assets/_Project/Materials/UnityChan";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "UnityChan");
        var toon = Shader.Find("Enigma/Toon");
        if (toon == null) return;

        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src == null) continue;

                string path = $"{dir}/Toon_{src.name}.mat";
                var dst = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (dst == null)
                {
                    dst = new Material(toon);
                    // 元マテリアルは Unity Toon Shader 前提でプロパティが読めないため、
                    // テクスチャはマテリアル名から直接対応付ける
                    var tex = FindUnityChanTexture(src.name)
                              ?? (src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null);
                    if (tex != null) dst.SetTexture("_BaseMap", tex);
                    dst.SetColor("_BaseColor", Color.white);
                    // body 等のアルファはスペキュラマスクなのでカットアウト対象にしない
                    dst.SetFloat("_Cutoff", UnityChanCutoff(src.name));
                    dst.SetFloat("_OutlineWidth", 0.0025f);  // キャラは細めの輪郭線
                    AssetDatabase.CreateAsset(dst, path);
                }
                mats[i] = dst;
            }
            r.sharedMaterials = mats;
        }
    }

    private static Texture FindUnityChanTexture(string materialName)
    {
        const string texDir = "Assets/UnityChan/Models/Texture/";
        string file = materialName switch
        {
            "body"      => "body_01.tga",
            "face"      => "face_00.tga",
            "eyebase"   => "face_00.tga",
            "eyeline"   => "eyeline_00.tga",
            "eye_L1"    => "eye_iris_L_00.tga",
            "eye_R1"    => "eye_iris_R_00.tga",
            "hair"      => "hair_01.tga",
            "mat_cheek" => "cheek_00.tga",
            "skin1"     => "skin_01.tga",
            "Left"      => "eyeline_00.tga",
            "Right"     => "eyeline_00.tga",
            _           => null,
        };
        return file == null ? null : AssetDatabase.LoadAssetAtPath<Texture>(texDir + file);
    }

    private static float UnityChanCutoff(string materialName) => materialName switch
    {
        "hair"      => 0.30f, // 毛先の透過
        "eyeline"   => 0.40f, // まつ毛
        "Left"      => 0.40f,
        "Right"     => 0.40f,
        "mat_cheek" => 0.90f, // 頬の赤み: ほぼ透過なので実質非表示
        _           => 0f,    // 不透明（body/face/skin/eye はアルファ=マスク用途）
    };

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

    private static void PlaceTower(string name, Vector3 pos, Material mat, Projectile projPrefab)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name              = name;
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(4f, 4f, 4f);
        SetStatic(go);
        SetMat(go, mat);

        // タワーは戦略オブジェクトなので HP と TeamTag を持たせる
        var hc = go.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = 500f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        var tt   = go.AddComponent<TeamTag>();
        var soTt = new SerializedObject(tt);
        // x < 0 = Blue, x > 0 = Red
        soTt.FindProperty("_team").enumValueIndex = pos.x < 0f ? (int)TeamId.Blue : (int)TeamId.Red;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        // TowerAttack: 射程内の敵を自動攻撃する
        var ta   = go.AddComponent<TowerAttack>();
        var muzzleGo = new GameObject("Muzzle");
        muzzleGo.transform.SetParent(go.transform, false);
        // タワーの localScale が 4 なので worldspace で頂部に来るよう localY+1 = world+4
        muzzleGo.transform.localPosition = new Vector3(0f, 1f, 0f);

        var soTa = new SerializedObject(ta);
        soTa.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab;
        soTa.FindProperty("_muzzle").objectReferenceValue           = muzzleGo.transform;
        soTa.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Projectile プレハブ生成後に全タワーの TowerAttack へ結線する。</summary>
    private static void WireProjPrefabToTowers(Projectile projPrefab)
    {
        var allTowerAttacks = Object.FindObjectsByType<TowerAttack>(FindObjectsSortMode.None);
        foreach (var ta in allTowerAttacks)
        {
            var so = new SerializedObject(ta);
            so.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// CharacterData アセット（Assets/_Project/Data/Characters/&lt;assetName&gt;.asset）の
    /// Skills[0..3] を SerializedObject 経由で結線する。アセットが存在しない場合はスキップ。
    /// </summary>
    private static void WireCharacterSkills(string assetName, SkillDefinition[] skills)
    {
        const string charDir = "Assets/_Project/Data/Characters";
        var path = $"{charDir}/{assetName}.asset";
        var cd   = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        if (cd == null)
        {
            Debug.LogWarning($"[BuildAetherRiftMap] CharacterData が見つかりません: {path}");
            return;
        }

        var so         = new SerializedObject(cd);
        var skillsProp = so.FindProperty("Skills");
        skillsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            skillsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                (skills != null && i < skills.Length) ? skills[i] : null;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cd);
    }

    // HealthComponent を返すことで MatchFlowController の結線に利用する
    private static HealthComponent PlaceTitan(string name, Vector3 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name              = name;
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(4f, 6f, 4f);
        SetStatic(go);
        SetMat(go, mat);

        var hc = go.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = 2000f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        var tt   = go.AddComponent<TeamTag>();
        var soTt = new SerializedObject(tt);
        soTt.FindProperty("_team").enumValueIndex = pos.x < 0f ? (int)TeamId.Blue : (int)TeamId.Red;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        return hc;
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

        // ダミーは中立（ミニオンの攻撃対象外）
        var dummyTeam = dummy.AddComponent<TeamTag>();
        var soDummyTeam = new SerializedObject(dummyTeam);
        soDummyTeam.FindProperty("_team").enumValueIndex = (int)TeamId.Neutral;
        soDummyTeam.ApplyModifiedPropertiesWithoutUndo();

        var td = dummy.AddComponent<TargetDummy>();

        var hpBar = new GameObject("HealthBar");
        hpBar.transform.SetParent(dummy.transform, false);
        hpBar.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        hpBar.AddComponent<HealthBarBillboard>();

        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "Background";
        bg.transform.SetParent(hpBar.transform, false);
        bg.transform.localScale = new Vector3(1.2f, 0.18f, 1f);
        var bgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
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
        var fillMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        fillMat.SetColor("_BaseColor", Color.green);
        fill.GetComponent<Renderer>().sharedMaterial = fillMat;
        Object.DestroyImmediate(fill.GetComponent<MeshCollider>());

        var soTd = new SerializedObject(td);
        soTd.FindProperty("_barFill").objectReferenceValue = fill.transform;
        soTd.ApplyModifiedPropertiesWithoutUndo();
    }

    private static MinionAI CreateMinionPrefab()
    {
        var prefabPath = PrefabDir + "/Minion.prefab";
        var existing   = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing.GetComponent<MinionAI>();

        // 小さめカプセル
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name            = "Minion";
        go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

        go.AddComponent<HealthComponent>();
        go.AddComponent<TeamTag>();
        var ai = go.AddComponent<MinionAI>();

        // 頭上 HP バー（TargetDummy と同じ構成）
        var hpBar = new GameObject("HealthBar");
        hpBar.transform.SetParent(go.transform, false);
        hpBar.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        hpBar.AddComponent<HealthBarBillboard>();

        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "Background";
        bg.transform.SetParent(hpBar.transform, false);
        bg.transform.localScale = new Vector3(1.2f, 0.18f, 1f);
        var bgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
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
        var fillMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        fillMat.SetColor("_BaseColor", Color.green);
        fill.GetComponent<Renderer>().sharedMaterial = fillMat;
        Object.DestroyImmediate(fill.GetComponent<MeshCollider>());

        // HealthComponent の maxHp を 50 に設定
        var soHc = new SerializedObject(go.GetComponent<HealthComponent>());
        soHc.FindProperty("_maxHp").floatValue = 50f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        // MinionAI に Fill を結線
        var soAi = new SerializedObject(ai);
        soAi.FindProperty("_barFill").objectReferenceValue = fill.transform;
        soAi.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<MinionAI>();
    }

    private static void PlaceMinionSpawners(MinionAI minionPrefab, Material matBlue, Material matRed)
    {
        // 西TOP → 東方向 (Blue)
        PlaceSpawner("Spawner_BlueTop",
            new Vector3(-88f, 0f, 50f),
            TeamId.Blue, matBlue, minionPrefab,
            new Vector3[] { new Vector3(0f, 0f, 50f), new Vector3(55f, 0f, 50f), new Vector3(88f, 0f, 50f) });

        // 西BOT → 東方向 (Blue)
        PlaceSpawner("Spawner_BlueBot",
            new Vector3(-88f, 0f, -50f),
            TeamId.Blue, matBlue, minionPrefab,
            new Vector3[] { new Vector3(0f, 0f, -50f), new Vector3(55f, 0f, -50f), new Vector3(88f, 0f, -50f) });

        // 東TOP → 西方向 (Red)
        PlaceSpawner("Spawner_RedTop",
            new Vector3(88f, 0f, 50f),
            TeamId.Red, matRed, minionPrefab,
            new Vector3[] { new Vector3(0f, 0f, 50f), new Vector3(-55f, 0f, 50f), new Vector3(-88f, 0f, 50f) });

        // 東BOT → 西方向 (Red)
        PlaceSpawner("Spawner_RedBot",
            new Vector3(88f, 0f, -50f),
            TeamId.Red, matRed, minionPrefab,
            new Vector3[] { new Vector3(0f, 0f, -50f), new Vector3(-55f, 0f, -50f), new Vector3(-88f, 0f, -50f) });
    }

    private static void PlaceSpawner(
        string spawnerName,
        Vector3 pos,
        TeamId team,
        Material teamMaterial,
        MinionAI minionPrefab,
        Vector3[] waypointPositions)
    {
        var spawnerGo = new GameObject(spawnerName);
        spawnerGo.transform.position = pos;

        var spawner    = spawnerGo.AddComponent<MinionSpawner>();
        var soSpawner  = new SerializedObject(spawner);

        soSpawner.FindProperty("_minionPrefab").objectReferenceValue = minionPrefab;
        soSpawner.FindProperty("_team").enumValueIndex               = (int)team;
        soSpawner.FindProperty("_teamMaterial").objectReferenceValue = teamMaterial;

        // ウェイポイント用の空 GO を生成して結線
        var wpArray = soSpawner.FindProperty("_waypoints");
        wpArray.arraySize = waypointPositions.Length;

        for (int i = 0; i < waypointPositions.Length; i++)
        {
            var wpGo = new GameObject($"{spawnerName}_WP{i}");
            wpGo.transform.SetParent(spawnerGo.transform, false);
            wpGo.transform.position = waypointPositions[i];
            wpArray.GetArrayElementAtIndex(i).objectReferenceValue = wpGo.transform;
        }

        soSpawner.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateBoss(
        GameObject telegraphPrefab,
        GameObject sectorPrefab,
        GameObject smPrefab,
        Material matBoss)
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

        // ボスは中立（ミニオンの攻撃対象外）
        var bossTeam   = boss.AddComponent<TeamTag>();
        var soBossTeam = new SerializedObject(bossTeam);
        soBossTeam.FindProperty("_team").enumValueIndex = (int)TeamId.Neutral;
        soBossTeam.ApplyModifiedPropertiesWithoutUndo();

        var bossCtrl = boss.AddComponent<NeutralBossController>();
        var soBossCtrl = new SerializedObject(bossCtrl);
        soBossCtrl.FindProperty("_telegraphPrefab").objectReferenceValue   = telegraphPrefab.GetComponent<TelegraphCircle>();
        soBossCtrl.FindProperty("_sectorPrefab").objectReferenceValue      = sectorPrefab.GetComponent<TelegraphSector>();
        soBossCtrl.FindProperty("_stackMarkerPrefab").objectReferenceValue = smPrefab.GetComponent<StackMarker>();
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
        var bgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
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
        var fillMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
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
