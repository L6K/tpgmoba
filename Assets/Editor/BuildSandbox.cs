using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Enigma.Ability;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Core;
using Enigma.Sandbox;
using Enigma.UI;

// BuildAetherRiftMap の partial 拡張。プレイヤー構築用の private ヘルパー
// (AttachUnityChanModel / CreateWorldHealthBar / GetOrCreate*Mat / SetMat 等) を
// そのまま再利用してキャラ試用シーン Sandbox.unity を生成する。
public static partial class BuildAetherRiftMap
{
    private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";

    [MenuItem("Enigma/Build Sandbox Scene")]
    public static void BuildSandbox()
    {
        EnsureDir(MatDir);
        EnsureDir("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. 環境（地面 / ライト / 環境光 / フォグ） ─────────────
        var matGround = GetOrCreateMat("SandboxGround", new Color(0.20f, 0.23f, 0.28f));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position   = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(120f, 1f, 120f);
        SetMat(ground, matGround);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1.0f, 0.96f, 0.88f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.85f;
        lightGo.transform.rotation = Quaternion.Euler(48f, -38f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.55f, 0.60f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.50f);
        RenderSettings.ambientGroundColor  = new Color(0.20f, 0.21f, 0.24f);

        var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/AnimeSky.mat");
        if (skyMat != null) RenderSettings.skybox = skyMat;

        // ── 2. 共有プレハブ/マテリアルを読み込み ──────────────────
        var aaBeamPrefab    = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/AaBeam.prefab");
        var projPrefabGo    = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Projectile.prefab");
        var telegraphGo     = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Telegraph.prefab");
        var ringPrefab      = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/TargetRing.prefab");

        var matBlue = GetOrCreateMat("Blue", new Color(0.25f, 0.45f, 0.90f));
        var matRed  = GetOrCreateMat("Red",  new Color(0.90f, 0.30f, 0.25f));

        // 方向インジケーター（両面・半透明シアン）
        var matArrow = GetOrCreateTransparentMat("SandboxArrow", new Color(0.30f, 0.85f, 1f, 0.65f));
        matArrow.SetFloat("_Cull", 0f);
        var matAoeCircle = GetOrCreateTransparentMat("SandboxAoe", new Color(0.30f, 0.85f, 1f, 0.30f));

        // ── 3. プレイヤー（AetherRift のプレイヤー構築を簡略移植） ──
        var playerSpawnPos = new Vector3(0f, 1.1f, 0f);
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag  = "Player";
        player.transform.position = playerSpawnPos;
        SetMat(player, matBlue);

        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;

        player.AddComponent<PlayerController>();

        var playerTeamTag = player.AddComponent<TeamTag>();
        var soPlayerTeam  = new SerializedObject(playerTeamTag);
        soPlayerTeam.FindProperty("_team").enumValueIndex = (int)TeamId.Blue;
        soPlayerTeam.ApplyModifiedPropertiesWithoutUndo();

        var healthComp = player.AddComponent<HealthComponent>();
        var soHealth = new SerializedObject(healthComp);
        soHealth.FindProperty("_maxHp").floatValue = 200f;
        soHealth.ApplyModifiedPropertiesWithoutUndo();

        var targeting = player.AddComponent<TargetingSystem>();
        var soTargeting = new SerializedObject(targeting);
        soTargeting.FindProperty("_targetRingPrefab").objectReferenceValue = ringPrefab;
        soTargeting.ApplyModifiedPropertiesWithoutUndo();

        var autoAttack = player.AddComponent<AutoAttack>();
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(player.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.6f, 0.6f);
        var soAutoAttack = new SerializedObject(autoAttack);
        if (aaBeamPrefab != null)
            soAutoAttack.FindProperty("_projectilePrefab").objectReferenceValue = aaBeamPrefab.GetComponent<Projectile>();
        soAutoAttack.FindProperty("_muzzle").objectReferenceValue = muzzle.transform;
        soAutoAttack.ApplyModifiedPropertiesWithoutUndo();

        var rangeIndicator = player.AddComponent<AttackRangeIndicator>();
        var soRangeIndicator = new SerializedObject(rangeIndicator);
        soRangeIndicator.FindProperty("_autoAttack").objectReferenceValue = autoAttack;
        soRangeIndicator.ApplyModifiedPropertiesWithoutUndo();

        var skillCaster = player.AddComponent<SkillCaster>();

        var dirArrowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dirArrowGo.name = "DirectionArrow";
        dirArrowGo.transform.SetParent(player.transform, false);
        dirArrowGo.transform.localPosition = new Vector3(0f, 0.1f, 2f);
        dirArrowGo.transform.localScale    = new Vector3(0.3f, 4f, 1f);
        SetMat(dirArrowGo, matArrow);
        Object.DestroyImmediate(dirArrowGo.GetComponent<MeshCollider>());
        dirArrowGo.SetActive(false);

        var aoeCircleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        aoeCircleGo.name = "AoeCircle";
        aoeCircleGo.transform.SetParent(player.transform, false);
        aoeCircleGo.transform.localPosition = Vector3.zero;
        aoeCircleGo.transform.localScale    = new Vector3(8f, 0.02f, 8f);
        SetMat(aoeCircleGo, matAoeCircle);
        Object.DestroyImmediate(aoeCircleGo.GetComponent<CapsuleCollider>());
        aoeCircleGo.SetActive(false);

        var soSkillCaster = new SerializedObject(skillCaster);
        var skillsProp = soSkillCaster.FindProperty("_skills");
        skillsProp.arraySize = 4; // 初期スキルは CharacterSandbox が Start で適用する
        for (int i = 0; i < 4; i++)
            skillsProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
        if (projPrefabGo != null)
            soSkillCaster.FindProperty("_projectilePrefab").objectReferenceValue = projPrefabGo.GetComponent<Projectile>();
        if (telegraphGo != null)
            soSkillCaster.FindProperty("_telegraphPrefab").objectReferenceValue = telegraphGo.GetComponent<TelegraphCircle>();
        soSkillCaster.FindProperty("_directionIndicator").objectReferenceValue = dirArrowGo;
        soSkillCaster.FindProperty("_aoeIndicator").objectReferenceValue       = aoeCircleGo;
        soSkillCaster.FindProperty("_targeting").objectReferenceValue          = targeting;
        soSkillCaster.FindProperty("_muzzle").objectReferenceValue             = muzzle.transform;
        soSkillCaster.ApplyModifiedPropertiesWithoutUndo();

        var playerProgression = player.AddComponent<PlayerProgression>();

        var matBarGreen   = GetOrCreateBarMat("BarGreen", new Color(0.30f, 0.85f, 0.35f));
        var playerBarFill = CreateWorldHealthBar(player.transform, 1.05f, 0.65f, matBarGreen, 200f);

        var overheadUi   = player.AddComponent<PlayerOverheadUI>();
        var soOverheadUi = new SerializedObject(overheadUi);
        soOverheadUi.FindProperty("_barFill").objectReferenceValue         = playerBarFill;
        soOverheadUi.FindProperty("_healthComponent").objectReferenceValue = healthComp;
        soOverheadUi.FindProperty("_progression").objectReferenceValue     = playerProgression;
        soOverheadUi.ApplyModifiedPropertiesWithoutUndo();

        // ── 4. カメラ + ポスプロ ─────────────────────────────────
        // URPポスプロはカメラ側フラグ + グローバルVolume(EnigmaPost)の両方が無いと効かない
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var sandboxCam = camGo.AddComponent<Camera>();
        sandboxCam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camGo.AddComponent<AudioListener>();
        var orbitCam = camGo.AddComponent<OrbitCamera>();

        var postGo  = new GameObject("Global Post Volume");
        var sandboxVolume = postGo.AddComponent<UnityEngine.Rendering.Volume>();
        sandboxVolume.isGlobal = true;
        var postProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/URP/EnigmaPost.asset");
        if (postProfile != null) sandboxVolume.sharedProfile = postProfile;

        var soPlayer = new SerializedObject(player.GetComponent<PlayerController>());
        soPlayer.FindProperty("_cameraTransform").objectReferenceValue = camGo.transform;
        soPlayer.ApplyModifiedPropertiesWithoutUndo();

        // ── 5. 見た目（UnityChan）+ 攻撃モーター ──────────────────
        AttachUnityChanModel(player);

        var attackMotor = player.AddComponent<PlayerAttackMotor>();
        var soMotor = new SerializedObject(attackMotor);
        var unityChanModel = player.transform.Find("UnityChanModel");
        if (unityChanModel != null)
            soMotor.FindProperty("_modelRoot").objectReferenceValue = unityChanModel;
        var ucSwitcher = player.GetComponentInChildren<LocomotionClipSwitcher>();
        if (ucSwitcher != null)
            soMotor.FindProperty("_clipSwitcher").objectReferenceValue = ucSwitcher;
        soMotor.ApplyModifiedPropertiesWithoutUndo();

        WireMotor(skillCaster, "_motor", attackMotor);
        WireMotor(autoAttack, "_motor", attackMotor);
        WireMotor(player.GetComponent<PlayerController>(), "_motor", attackMotor);

        var soCam = new SerializedObject(orbitCam);
        soCam.FindProperty("_target").objectReferenceValue = player.transform;
        soCam.ApplyModifiedPropertiesWithoutUndo();

        // ── 6. HUD（スキルCD・HP表示）+ ダメージポップアップ ───────
        var hudGo  = new GameObject("GameHud");
        var hudDoc = hudGo.AddComponent<UIDocument>();
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
            "Assets/_Project/UI/HomeScreenPanelSettings.asset");
        if (panelSettings != null) hudDoc.panelSettings = panelSettings;
        var hudUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/GameHud.uxml");
        if (hudUxml != null) hudDoc.visualTreeAsset = hudUxml;
        EditorUtility.SetDirty(hudDoc);

        var hudCtrl   = hudGo.AddComponent<GameHudController>();
        var soHudCtrl = new SerializedObject(hudCtrl);
        soHudCtrl.FindProperty("_uiDocument").objectReferenceValue   = hudDoc;
        soHudCtrl.FindProperty("_playerHealth").objectReferenceValue = healthComp;
        soHudCtrl.FindProperty("_skillCaster").objectReferenceValue  = skillCaster;
        soHudCtrl.ApplyModifiedPropertiesWithoutUndo();

        hudGo.AddComponent<Enigma.UI.DamagePopupManager>();

        // 死亡時の被ダメージ内訳リキャップ
        var deathRecap   = player.AddComponent<PlayerDeathRecap>();
        var soDeathRecap = new SerializedObject(deathRecap);
        soDeathRecap.FindProperty("_health").objectReferenceValue = healthComp;
        soDeathRecap.FindProperty("_hud").objectReferenceValue    = hudCtrl;
        soDeathRecap.ApplyModifiedPropertiesWithoutUndo();

        // ── 7. ターゲットダミー3体（Red・死亡で自動復活） ──────────
        CreateSandboxDummy("Dummy_Center", new Vector3(0f, 1.1f, 10f), matRed, matBarRed());
        CreateSandboxDummy("Dummy_Left",   new Vector3(-6f, 1.1f, 12f), matRed, matBarRed());
        CreateSandboxDummy("Dummy_Right",  new Vector3(6f, 1.1f, 12f), matRed, matBarRed());

        // ── 8. CharacterSandbox 司令塔 ────────────────────────────
        var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(
            "Assets/_Project/Data/Characters/CharacterDatabase.asset");

        var sandboxGo = new GameObject("CharacterSandbox");
        var sandbox   = sandboxGo.AddComponent<CharacterSandbox>();
        var soSandbox = new SerializedObject(sandbox);
        soSandbox.FindProperty("_database").objectReferenceValue = database;
        soSandbox.FindProperty("_player").objectReferenceValue   = player;
        soSandbox.ApplyModifiedPropertiesWithoutUndo();

        // ── 9. 保存 ──────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene, SandboxScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BuildSandbox] Sandbox.unity を保存しました。");
    }

    private static Material matBarRed() =>
        GetOrCreateBarMat("BarRed", new Color(0.92f, 0.30f, 0.25f));

    private static void WireMotor(Object target, string prop, Object motor)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        so.FindProperty(prop).objectReferenceValue = motor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 試用用の的: 静的カプセル + HP + TargetDummy（死亡で 3 秒後に自動復活）。
    private static void CreateSandboxDummy(string name, Vector3 pos, Material matBody, Material matBar)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.position = pos;
        SetMat(go, matBody);

        var hc = go.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = 2000f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        var tt = go.AddComponent<TeamTag>();
        var soTt = new SerializedObject(tt);
        soTt.FindProperty("_team").enumValueIndex = (int)TeamId.Red;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        var bar = CreateWorldHealthBar(go.transform, 1.05f, 1.3f, matBar, 2000f);

        var td = go.AddComponent<TargetDummy>();
        var soTd = new SerializedObject(td);
        soTd.FindProperty("_barFill").objectReferenceValue = bar;
        soTd.ApplyModifiedPropertiesWithoutUndo();
    }
}
