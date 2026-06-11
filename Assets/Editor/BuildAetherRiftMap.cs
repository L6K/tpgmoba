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
using Enigma.Minimap;
using Enigma.Minion;
using Enigma.Core;
using Enigma.Item;

public static class BuildAetherRiftMap
{
    private const string ScenePath   = "Assets/Scenes/AetherRift_Map.unity";
    private const string MatDir      = "Assets/_Project/Materials/Map";
    private const string PrefabDir   = "Assets/_Project/Prefabs";
    private const string SkillDir    = "Assets/_Project/Data/Skills";
    private const string ItemDir     = "Assets/_Project/Data/Items";

    public static void Execute()
    {
        // ディレクトリ確保
        EnsureDir(MatDir);
        EnsureDir(PrefabDir);
        EnsureDir(SkillDir);
        EnsureDir(ItemDir);
        EnsureDir("Assets/Scenes");

        // 1. 空シーン作成
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. マテリアル生成
        var matGround    = GetOrCreateMat("Ground",      new Color(0.40f, 0.58f, 0.32f));
        var matLane      = GetOrCreateMat("Lane",        new Color(0.62f, 0.55f, 0.42f));
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

        // 3. ジオメトリ配置（円形マップ）
        // ---- レイアウト定数 ----
        // プレイフィールド半径70、レーンアーク半径R=45、レーン幅10
        // 本拠地中心(±56,0,0) 半径11

        // Ground: Cylinder scale(150,1,150)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Ground";
            // Cylinder メッシュは高さ2のため scaleY=0.5 で天面が y=0 になる
            ground.transform.position   = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(150f, 0.5f, 150f);
            UseFlatMeshCollider(ground, keepCollider: true);
            SetStatic(ground);
            // 草原グリーン
            matGround.SetColor("_BaseColor", new Color(0.40f, 0.58f, 0.32f));
            SetMat(ground, matGround);
        }

        // 川: 縦帯 Cube (両レーンに届く長さ92)
        // 同一平面のチラつき防止のため レーン(0.02) < 川(0.05) < ベイスン(0.12) < ピット(0.18) と階段状にする
        PlaceCube("River", new Vector3(0f, 0.05f, 0f), new Vector3(14f, 0.1f, 92f), matRiver);

        // レーン色を土色に更新
        matLane.SetColor("_BaseColor", new Color(0.62f, 0.55f, 0.42f));

        // レーンアーク: TOP (θ=0..180°) と BOT (θ=180..360°) を 7.5° 刻みで Cube セグメント配置
        const float R = 45f;
        const float stepDeg = 7.5f;
        for (int si = 0; si < 48; si++)
        {
            float theta = si * stepDeg; // 0..352.5°
            string laneName = theta < 180f ? "LaneArc_Top" : "LaneArc_Bot";
            float rad = theta * Mathf.Deg2Rad;
            float x = R * Mathf.Cos(rad);
            float z = R * Mathf.Sin(rad);
            var seg = PlaceCube($"{laneName}_{si:D2}", new Vector3(x, 0.02f, z), new Vector3(6.5f, 0.1f, 10f), matLane);
            // 接線方向: pos=(Rcosθ,0,Rsinθ) の接線 forward = (-sinθ, 0, cosθ)
            var forward = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            if (forward != Vector3.zero)
                seg.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        // 中央ベイスン（ボスの足場）: 大円 + 小円、壁なし
        {
            var basin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basin.name = "Basin";
            basin.transform.position   = new Vector3(0f, 0.12f, 0f);
            basin.transform.localScale = new Vector3(32f, 0.06f, 32f);
            UseFlatMeshCollider(basin, keepCollider: false);
            SetStatic(basin);
            SetMat(basin, matRiver);

            var pit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pit.name = "BossPit";
            pit.transform.position   = new Vector3(0f, 0.18f, 0f);
            pit.transform.localScale = new Vector3(16f, 0.04f, 16f);
            UseFlatMeshCollider(pit, keepCollider: false);
            SetStatic(pit);
            SetMat(pit, matPit);
        }

        // 外周リング: 36分割、半径72に岩壁配置
        {
            const int ringSegs = 36;
            // 優先順位: cliff_block_rock > cliff_blockCave_rock > Cube フォールバック
            string[] cliffCandidates = {
                "Assets/External/Kenney/Nature/cliff_block_rock.fbx",
                "Assets/External/Kenney/Nature/cliff_blockCave_rock.fbx",
                "Assets/External/Kenney/Castle/cliff_block_rock.fbx",
            };
            GameObject cliffModel = null;
            foreach (var cp in cliffCandidates)
            {
                cliffModel = AssetDatabase.LoadAssetAtPath<GameObject>(cp);
                if (cliffModel != null) break;
            }

            for (int ri = 0; ri < ringSegs; ri++)
            {
                float phi = ri * (360f / ringSegs) * Mathf.Deg2Rad;
                float rx = 72f * Mathf.Cos(phi);
                float rz = 72f * Mathf.Sin(phi);
                var wallPos = new Vector3(rx, 0f, rz);

                GameObject wallGo;
                if (cliffModel != null)
                {
                    wallGo = (GameObject)PrefabUtility.InstantiatePrefab(cliffModel);
                    wallGo.transform.position   = wallPos;
                    wallGo.transform.localScale = Vector3.one * 6f;
                    // 中心向き回転
                    wallGo.transform.rotation = Quaternion.LookRotation(-wallPos.normalized, Vector3.up);
                    // BoxCollider をレンダラー境界に合わせて追加
                    var bounds = new Bounds(Vector3.zero, Vector3.zero);
                    bool boundsInit = false;
                    foreach (var r in wallGo.GetComponentsInChildren<Renderer>())
                    {
                        if (!boundsInit) { bounds = r.bounds; boundsInit = true; }
                        else bounds.Encapsulate(r.bounds);
                    }
                    var bc = wallGo.AddComponent<BoxCollider>();
                    if (boundsInit)
                    {
                        bc.center = wallGo.transform.InverseTransformPoint(bounds.center);
                        bc.size   = Vector3.Scale(bounds.size, new Vector3(
                            1f / wallGo.transform.lossyScale.x,
                            1f / wallGo.transform.lossyScale.y,
                            1f / wallGo.transform.lossyScale.z));
                    }
                }
                else
                {
                    wallGo = PlaceCube($"RingWall_{ri:D2}", wallPos, new Vector3(13f, 6f, 4f), matJungle);
                    wallGo.transform.rotation = Quaternion.LookRotation(-wallPos.normalized, Vector3.up);
                }
                wallGo.name = $"RingWall_{ri:D2}";
                SetStatic(wallGo);
            }
        }

        // ジャングル樹木: System.Random(42) で各象限に26本（計104本）
        // クラスタ中心3〜4箇所 + 一様散布、木同士最小間隔1.2m
        // スケール 3.8〜6.2 のランダム、コライダー半径も比例
        // 追加樹種: tree_pineRoundA/B/C, tree_detailed, tree_tall
        {
            string[] treeFbxNames = {
                "tree_pineTallA", "tree_pineTallB", "tree_default", "tree_oak", "tree_fat",
                "tree_pineRoundA", "tree_pineRoundB", "tree_pineRoundC", "tree_detailed", "tree_tall"
            };
            var treeModelList = new System.Collections.Generic.List<GameObject>();
            foreach (var fname in treeFbxNames)
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/External/Kenney/Nature/{fname}.fbx");
                if (m != null) treeModelList.Add(m);
            }
            GameObject[] treeModels = treeModelList.ToArray();

            // 岩モデル: FindAssets で検索してフォールバックは Cube
            string[] rockSearchFilters = { "rock_large", "stone_large" };
            var rockModelList = new System.Collections.Generic.List<GameObject>();
            foreach (var filter in rockSearchFilters)
            {
                var guids = AssetDatabase.FindAssets($"{filter} t:Model");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var rm   = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (rm != null) rockModelList.Add(rm);
                }
            }

            var rng = new System.Random(42);

            // 4象限: Q0=(+x,+z), Q1=(-x,+z), Q2=(-x,-z), Q3=(+x,-z)
            for (int q = 0; q < 4; q++)
            {
                // 配置済み木の位置リスト（木同士の最小間隔チェック用）
                var placedPositions = new System.Collections.Generic.List<Vector2>();

                // --- クラスタ生成 ---
                int clusterCount = rng.Next(3, 5); // 3 または 4 クラスタ
                var clusterCenters = new System.Collections.Generic.List<Vector2>();

                for (int ci = 0; ci < clusterCount; ci++)
                {
                    // クラスタ中心候補を最大50回試行（棄却条件は木と同じ）
                    for (int ca = 0; ca < 50; ca++)
                    {
                        float cr    = (float)(rng.NextDouble() * 18.0 + 20.0);
                        float cang  = (float)(rng.NextDouble() * 90.0 + q * 90.0);
                        float crad  = cang * Mathf.Deg2Rad;
                        float cx    = cr * Mathf.Cos(crad);
                        float cz    = cr * Mathf.Sin(crad);
                        if (Mathf.Abs(cx) < 9f) continue;
                        float distFromArcC = Mathf.Abs(Mathf.Sqrt(cx * cx + cz * cz) - R);
                        if (distFromArcC < 7f) continue;
                        if (IsNearAnyJunglePath(new Vector3(cx, 0f, cz), 4.5f)) continue;
                        if (IsNearAnyCamp(new Vector3(cx, 0f, cz), 6f)) continue;
                        clusterCenters.Add(new Vector2(cx, cz));
                        break;
                    }
                }

                int totalGoal   = 26;
                int clusterGoal = (int)(totalGoal * 0.65f); // 約17本をクラスタに
                // 残り(9本)は一様散布で補完
                int placed = 0;

                // クラスタ内に木を配置
                foreach (var clusterCenter in clusterCenters)
                {
                    if (placed >= clusterGoal) break;
                    int perCluster = rng.Next(4, 7); // 4〜6本
                    int cPlaced = 0;
                    int cAttempts = 0;
                    while (cPlaced < perCluster && placed < clusterGoal && cAttempts < 80)
                    {
                        cAttempts++;
                        // クラスタ中心から半径5以内にランダム配置
                        float offsetAngle = (float)(rng.NextDouble() * 360.0) * Mathf.Deg2Rad;
                        float offsetDist  = (float)(rng.NextDouble() * 5.0);
                        float tx = clusterCenter.x + offsetDist * Mathf.Cos(offsetAngle);
                        float tz = clusterCenter.y + offsetDist * Mathf.Sin(offsetAngle);

                        if (Mathf.Abs(tx) < 9f) continue;
                        float distFromArc = Mathf.Abs(Mathf.Sqrt(tx * tx + tz * tz) - R);
                        if (distFromArc < 7f) continue;
                        if (IsNearAnyJunglePath(new Vector3(tx, 0f, tz), 4.5f)) continue;
                        if (IsNearAnyCamp(new Vector3(tx, 0f, tz), 6f)) continue;

                        // 木同士の最小間隔 1.2m
                        bool tooClose = false;
                        foreach (var pp in placedPositions)
                        {
                            if (Vector2.Distance(new Vector2(tx, tz), pp) < 1.2f) { tooClose = true; break; }
                        }
                        if (tooClose) continue;

                        PlaceOneTree(treeModels, rng, tx, tz, q, placed, matJungle);
                        placedPositions.Add(new Vector2(tx, tz));
                        cPlaced++;
                        placed++;
                    }
                }

                // 残りを一様散布
                int uAttempts = 0;
                while (placed < totalGoal && uAttempts < 600)
                {
                    uAttempts++;
                    float r     = (float)(rng.NextDouble() * 18.0 + 20.0);
                    float angle = (float)(rng.NextDouble() * 90.0 + q * 90.0);
                    float rad2  = angle * Mathf.Deg2Rad;
                    float tx    = r * Mathf.Cos(rad2);
                    float tz    = r * Mathf.Sin(rad2);

                    if (Mathf.Abs(tx) < 9f) continue;
                    float distFromArc = Mathf.Abs(Mathf.Sqrt(tx * tx + tz * tz) - R);
                    if (distFromArc < 7f) continue;
                    if (IsNearAnyJunglePath(new Vector3(tx, 0f, tz), 4.5f)) continue;
                    if (IsNearAnyCamp(new Vector3(tx, 0f, tz), 6f)) continue;

                    bool tooClose = false;
                    foreach (var pp in placedPositions)
                    {
                        if (Vector2.Distance(new Vector2(tx, tz), pp) < 1.2f) { tooClose = true; break; }
                    }
                    if (tooClose) continue;

                    PlaceOneTree(treeModels, rng, tx, tz, q, placed, matJungle);
                    placedPositions.Add(new Vector2(tx, tz));
                    placed++;
                }

                // --- 岩の配置（各象限4個）---
                int rocksPlaced = 0;
                int rockAttempts = 0;
                while (rocksPlaced < 4 && rockAttempts < 200)
                {
                    rockAttempts++;
                    float rr    = (float)(rng.NextDouble() * 18.0 + 20.0);
                    float rang  = (float)(rng.NextDouble() * 90.0 + q * 90.0);
                    float rrad  = rang * Mathf.Deg2Rad;
                    float rx    = rr * Mathf.Cos(rrad);
                    float rz    = rr * Mathf.Sin(rrad);

                    if (Mathf.Abs(rx) < 9f) continue;
                    float distFromArcR = Mathf.Abs(Mathf.Sqrt(rx * rx + rz * rz) - R);
                    if (distFromArcR < 7f) continue;
                    if (IsNearAnyJunglePath(new Vector3(rx, 0f, rz), 4.5f)) continue;
                    if (IsNearAnyCamp(new Vector3(rx, 0f, rz), 6f)) continue;

                    bool tooClose = false;
                    foreach (var pp in placedPositions)
                    {
                        if (Vector2.Distance(new Vector2(rx, rz), pp) < 1.2f) { tooClose = true; break; }
                    }
                    if (tooClose) continue;

                    float rockScale = (float)(rng.NextDouble() * 2.0 + 5.0); // 5〜7
                    float rockYaw   = (float)(rng.NextDouble() * 360.0);

                    GameObject rockGo;
                    if (rockModelList.Count > 0)
                    {
                        var rm = rockModelList[rng.Next(0, rockModelList.Count)];
                        rockGo = (GameObject)PrefabUtility.InstantiatePrefab(rm);
                        rockGo.transform.position   = new Vector3(rx, 0f, rz);
                        rockGo.transform.localScale = Vector3.one * rockScale;
                        rockGo.transform.rotation   = Quaternion.Euler(0f, rockYaw, 0f);
                        var bc = rockGo.AddComponent<BoxCollider>();
                        // 境界ボックスをレンダラーから計算
                        var bounds = new Bounds(Vector3.zero, Vector3.zero);
                        bool boundsInit = false;
                        foreach (var renderer in rockGo.GetComponentsInChildren<Renderer>())
                        {
                            if (!boundsInit) { bounds = renderer.bounds; boundsInit = true; }
                            else bounds.Encapsulate(renderer.bounds);
                        }
                        if (boundsInit)
                        {
                            bc.center = rockGo.transform.InverseTransformPoint(bounds.center);
                            bc.size   = Vector3.Scale(bounds.size, new Vector3(
                                1f / rockGo.transform.lossyScale.x,
                                1f / rockGo.transform.lossyScale.y,
                                1f / rockGo.transform.lossyScale.z));
                        }
                    }
                    else
                    {
                        rockGo = PlaceCube($"Rock_Q{q}_{rocksPlaced:D2}",
                            new Vector3(rx, 0f, rz),
                            new Vector3(3f, 2f, 3f), matJungle);
                        rockGo.transform.localScale = new Vector3(3f * rockScale / 6f, 2f * rockScale / 6f, 3f * rockScale / 6f);
                        rockGo.transform.rotation   = Quaternion.Euler(0f, rockYaw, 0f);
                    }
                    rockGo.name = $"Rock_Q{q}_{rocksPlaced:D2}";
                    SetStatic(rockGo);

                    placedPositions.Add(new Vector2(rx, rz));
                    rocksPlaced++;
                }
            }
        }

        // ---- ジャングルパス（4本）& キャンプ ----
        var matJunglePath = GetOrCreateMat("JunglePath", new Color(0.68f, 0.60f, 0.46f));
        PlaceJunglePathsAndCamps(matJunglePath);

        // 本拠地: Cylinder scale(22,1,22) pos(±56,0.5,0)
        {
            var baseBlue = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseBlue.name = "Base_Blue";
            baseBlue.transform.position   = new Vector3(-56f, 0.5f, 0f);
            baseBlue.transform.localScale = new Vector3(22f, 0.5f, 22f);
            UseFlatMeshCollider(baseBlue, keepCollider: true);
            SetStatic(baseBlue);
            SetMat(baseBlue, matBlue);

            var baseRed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseRed.name = "Base_Red";
            baseRed.transform.position   = new Vector3(56f, 0.5f, 0f);
            baseRed.transform.localScale = new Vector3(22f, 0.5f, 22f);
            UseFlatMeshCollider(baseRed, keepCollider: true);
            SetStatic(baseRed);
            SetMat(baseRed, matRed);
        }

        // タイタン: pos(±56, 4, 0)
        var blueTitanHc = PlaceTitan("Titan_Blue", new Vector3(-56f, 4f, 0f), matBlue);
        var redTitanHc  = PlaceTitan("Titan_Red",  new Vector3( 56f, 4f, 0f), matRed);

        // タワー8基: Kenney tower-square.fbx (フォールバック: Cylinder)
        // TOP: θ=160°,140° (Blue)、θ=40°,20° (Red)
        // BOT: θ=200°,220° (Blue)、θ=320°,340° (Red)
        {
            var towerModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/External/Kenney/Castle/tower-square.fbx");

            (string name, float theta, Material mat)[] towerDefs =
            {
                ("Tower_BTop",    160f, matBlue),
                ("Tower_BMidTop", 140f, matBlue),
                ("Tower_RTop",     40f, matRed),
                ("Tower_RMidTop",  20f, matRed),
                ("Tower_BBot",    200f, matBlue),
                ("Tower_BMidBot", 220f, matBlue),
                ("Tower_RBot",    320f, matRed),
                ("Tower_RMidBot", 340f, matRed),
            };

            foreach (var (tname, theta, tmat) in towerDefs)
            {
                float tr  = theta * Mathf.Deg2Rad;
                float tx  = R * Mathf.Cos(tr);
                float tz  = R * Mathf.Sin(tr);
                var tPos  = new Vector3(tx, 0f, tz);

                PlaceTower(tname, tPos + Vector3.up * 4f, tmat, null, towerModel);

                // 足元チーム色リング
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = $"{tname}_Ring";
                ring.transform.position   = new Vector3(tx, 0.08f, tz);
                ring.transform.localScale = new Vector3(7f, 0.05f, 7f);
                UseFlatMeshCollider(ring, keepCollider: false);
                SetStatic(ring);
                SetMat(ring, tmat);
            }
        }

        // projPrefab はこの時点では未生成なので後段で結線

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
        var playerSpawnPos = new Vector3(-52f, 1.1f, 10f);
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

        // XP・レベル成長コンポーネント
        player.AddComponent<PlayerProgression>();

        // ゴールドとアイテム管理
        player.AddComponent<PlayerWallet>();
        player.AddComponent<PlayerItems>();

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

        // KDA 集積: MatchStatsTracker を同じ GO に追加
        matchFlowGo.AddComponent<Enigma.Core.MatchStatsTracker>();

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

        // 11. ターゲットダミー 2体
        CreateDummy("Dummy_A", new Vector3(-32f, 1f, 30f), matDummy);
        CreateDummy("Dummy_B", new Vector3(-26f, 1f, 36f), matDummy);

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

        // 12. ニュートラルボス（ベイスン中央、壁なし）
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

        // ShopController: ショップオーバーレイ制御・購入処理（catalog 結線はステップ15の後）
        var shopCtrl   = hudGo.AddComponent<ShopController>();
        var soShopCtrl = new SerializedObject(shopCtrl);
        soShopCtrl.FindProperty("_uiDocument").objectReferenceValue = hudDoc;
        soShopCtrl.FindProperty("_player").objectReferenceValue     = player.transform;
        // _shopCenter は青本拠地中心 (-56, 0, 0)。デフォルト値と同じだが明示して堅牢化
        soShopCtrl.FindProperty("_shopCenter").vector3Value         = new Vector3(-56f, 0f, 0f);
        soShopCtrl.ApplyModifiedPropertiesWithoutUndo();

        // MinimapController: ミニマップドットを毎フレーム更新する
        var minimapCtrl   = hudGo.AddComponent<MinimapController>();
        var soMinimapCtrl = new SerializedObject(minimapCtrl);
        soMinimapCtrl.FindProperty("_uiDocument").objectReferenceValue = hudDoc;
        soMinimapCtrl.ApplyModifiedPropertiesWithoutUndo();

        // 14. ミニオンプレハブ + スポーナー
        var minionPrefab = CreateMinionPrefab();
        PlaceMinionSpawners(minionPrefab, matBlue, matRed);

        // 15. ItemData 6種生成 & ItemShopCatalog
        var catalogItems = new System.Collections.Generic.List<ItemData>
        {
            GetOrCreateItemData("Item_LongSword",    "ロングソード",    350,  10f, 0f,   0f,   "基本的な攻撃力強化アイテム。",          new Color(0.80f, 0.65f, 0.20f)),
            GetOrCreateItemData("Item_MagicBlade",   "魔導の刃",        800,  25f, 0f,   0f,   "魔力が宿った刃。攻撃力を大幅に底上げする。", new Color(0.55f, 0.25f, 0.85f)),
            GetOrCreateItemData("Item_VitalStone",   "体力の石",        400,  0f,  50f,  0f,   "生命力を高める霊石。",                  new Color(0.20f, 0.65f, 0.30f)),
            GetOrCreateItemData("Item_GiantBelt",    "巨人の帯",        900,  0f,  120f, 0f,   "タイタンの力を宿す大きな帯。",           new Color(0.65f, 0.35f, 0.15f)),
            GetOrCreateItemData("Item_WindBoots",    "疾風のブーツ",    300,  0f,  0f,   12f,  "風の力で足取りを軽くするブーツ。",       new Color(0.30f, 0.75f, 0.90f)),
            GetOrCreateItemData("Item_StormSword",   "嵐剣エニグマ",   1500, 35f, 60f,  0f,   "エニグマの力が凝縮された究極の剣。",     new Color(0.90f, 0.30f, 0.40f)),
        };

        var catalog = GetOrCreateItemShopCatalog(catalogItems);

        // ShopController に catalog を結線（ステップ15でアセット生成後に行う）
        var soShopCtrlLate = new SerializedObject(shopCtrl);
        soShopCtrlLate.FindProperty("_catalog").objectReferenceValue = catalog;
        soShopCtrlLate.ApplyModifiedPropertiesWithoutUndo();

        // 16. シーン保存
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

    // Cylinder プリミティブ付属の CapsuleCollider は扁平スケールで球面ドーム状になり
    // 床として機能しない（プレイヤーが滑落する）ため MeshCollider に差し替える
    private static void UseFlatMeshCollider(GameObject cylinderGo, bool keepCollider)
    {
        Object.DestroyImmediate(cylinderGo.GetComponent<CapsuleCollider>());
        if (keepCollider) cylinderGo.AddComponent<MeshCollider>();
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

    /// <summary>
    /// タワーを配置する。towerModel が null の場合は Cylinder フォールバック。
    /// ジオメトリ節から呼ばれる overload（Kenney モデル対応版）。
    /// </summary>
    private static void PlaceTower(string name, Vector3 pos, Material mat, Projectile projPrefab,
        GameObject towerModel = null)
    {
        GameObject go;
        if (towerModel != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(towerModel);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * 4.5f;
            // CapsuleCollider 付与（既存コライダーは除去してから追加）
            foreach (var c in go.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = 1.5f;
            cap.height = 8f;
            cap.center = new Vector3(0f, 4f, 0f);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(4f, 4f, 4f);
            SetMat(go, mat);
        }
        go.name = name;
        SetStatic(go);

        // HP
        var hc = go.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = 500f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        // チーム
        var tt   = go.AddComponent<TeamTag>();
        var soTt = new SerializedObject(tt);
        soTt.FindProperty("_team").enumValueIndex = pos.x < 0f ? (int)TeamId.Blue : (int)TeamId.Red;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        // TowerAttack
        var ta      = go.AddComponent<TowerAttack>();
        var muzzleGo = new GameObject("Muzzle");
        muzzleGo.transform.SetParent(go.transform, false);
        // Kenney モデルはワールド localScale 4.5 → top ≈ y+6（世界座標） → localY ≈ 6/4.5 ≈ 1.33
        muzzleGo.transform.localPosition = new Vector3(0f, towerModel != null ? 1.33f : 1f, 0f);

        var soTa = new SerializedObject(ta);
        soTa.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab;
        soTa.FindProperty("_muzzle").objectReferenceValue           = muzzleGo.transform;
        soTa.ApplyModifiedPropertiesWithoutUndo();

        // タワー撃破で100XP付与
        var towerReward = go.AddComponent<XpReward>();
        var soTowerReward = new SerializedObject(towerReward);
        soTowerReward.FindProperty("_amount").floatValue = 100f;
        soTowerReward.ApplyModifiedPropertiesWithoutUndo();

        // タワー撃破で150G付与
        var towerGold = go.AddComponent<GoldReward>();
        var soTowerGold = new SerializedObject(towerGold);
        soTowerGold.FindProperty("_amount").intValue = 150;
        soTowerGold.ApplyModifiedPropertiesWithoutUndo();
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

        // ミニオン撃破で20XP付与
        var minionReward = go.AddComponent<XpReward>();
        var soMinionReward = new SerializedObject(minionReward);
        soMinionReward.FindProperty("_amount").floatValue = 20f;
        soMinionReward.ApplyModifiedPropertiesWithoutUndo();

        // ミニオン撃破で20G付与
        var minionGold = go.AddComponent<GoldReward>();
        var soMinionGold = new SerializedObject(minionGold);
        soMinionGold.FindProperty("_amount").intValue = 20;
        soMinionGold.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<MinionAI>();
    }

    private static void PlaceMinionSpawners(MinionAI minionPrefab, Material matBlue, Material matRed)
    {
        // アーク半径 R=45 上のウェイポイント計算ヘルパー
        static Vector3 ArcPt(float deg) {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(45f * Mathf.Cos(r), 0f, 45f * Mathf.Sin(r));
        }

        // BlueTop: 出発(-50,0,10)→ θ=160,135,90,45,20 のアーク→終点(50,0,8)
        PlaceSpawner("Spawner_BlueTop",
            new Vector3(-50f, 0f, 10f),
            TeamId.Blue, matBlue, minionPrefab,
            new Vector3[] {
                ArcPt(160f), ArcPt(135f), ArcPt(90f), ArcPt(45f), ArcPt(20f),
                new Vector3(50f, 0f, 8f)
            });

        // RedTop: 出発(50,0,10)→ θ=20,45,90,135,160 のアーク→終点(-50,0,8)
        PlaceSpawner("Spawner_RedTop",
            new Vector3(50f, 0f, 10f),
            TeamId.Red, matRed, minionPrefab,
            new Vector3[] {
                ArcPt(20f), ArcPt(45f), ArcPt(90f), ArcPt(135f), ArcPt(160f),
                new Vector3(-50f, 0f, 8f)
            });

        // BlueBot: z 符号反転版（出発(-50,0,-10)→ θ=200,225,270,315,340→終点(50,0,-8)）
        PlaceSpawner("Spawner_BlueBot",
            new Vector3(-50f, 0f, -10f),
            TeamId.Blue, matBlue, minionPrefab,
            new Vector3[] {
                ArcPt(200f), ArcPt(225f), ArcPt(270f), ArcPt(315f), ArcPt(340f),
                new Vector3(50f, 0f, -8f)
            });

        // RedBot: z 符号反転版（出発(50,0,-10)→ θ=340,315,270,225,200→終点(-50,0,-8)）
        PlaceSpawner("Spawner_RedBot",
            new Vector3(50f, 0f, -10f),
            TeamId.Red, matRed, minionPrefab,
            new Vector3[] {
                ArcPt(340f), ArcPt(315f), ArcPt(270f), ArcPt(225f), ArcPt(200f),
                new Vector3(-50f, 0f, -8f)
            });
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

        // ボス撃破で250XP付与
        var bossReward = boss.AddComponent<XpReward>();
        var soBossReward = new SerializedObject(bossReward);
        soBossReward.FindProperty("_amount").floatValue = 250f;
        soBossReward.ApplyModifiedPropertiesWithoutUndo();

        // ボス撃破で300G付与
        var bossGold = boss.AddComponent<GoldReward>();
        var soBossGold = new SerializedObject(bossGold);
        soBossGold.FindProperty("_amount").intValue = 300;
        soBossGold.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- ジャングルパスとキャンプ配置 ----

    /// <summary>
    /// 対角線4本のジャングルパスと、各θ=45/135/225/315°半径30のスライムキャンプを配置する。
    /// </summary>
    private static void PlaceJunglePathsAndCamps(Material matJunglePath)
    {
        float[] campAngles = { 45f, 135f, 225f, 315f };

        foreach (float deg in campAngles)
        {
            float rad = deg * Mathf.Deg2Rad;

            // パス端点: レーンアーク側(R=45)からベイスン縁(r=18)
            var p1 = new Vector3(45f * Mathf.Cos(rad), 0f, 45f * Mathf.Sin(rad));
            var p2 = new Vector3(18f * Mathf.Cos(rad), 0f, 18f * Mathf.Sin(rad));

            // パスを5セグメントの Cube で敷く（y=0.03: レーン0.02と川0.05の中間）
            const int   SegCount = 5;
            float       segLen   = Vector3.Distance(p1, p2) / SegCount;
            var         fwd      = (p2 - p1).normalized;

            for (int si = 0; si < SegCount; si++)
            {
                float  t      = (si + 0.5f) / SegCount;
                var    center = Vector3.Lerp(p1, p2, t);
                center.y = 0.03f;

                var seg = PlaceCube(
                    $"JunglePath_{(int)deg}_{si}",
                    center,
                    new Vector3(6f, 0.1f, segLen + 1f),
                    matJunglePath);
                if (fwd != Vector3.zero)
                    seg.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }

            // キャンプ中心（半径30）
            var campCenter = new Vector3(30f * Mathf.Cos(rad), 0f, 30f * Mathf.Sin(rad));

            // 足元の空き地サークル（コライダーなし）
            var clearing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            clearing.name = $"CampClearing_{(int)deg}";
            clearing.transform.position   = new Vector3(campCenter.x, 0.025f, campCenter.z);
            clearing.transform.localScale = new Vector3(9f, 0.04f, 9f);
            UseFlatMeshCollider(clearing, keepCollider: false);
            SetStatic(clearing);
            SetMat(clearing, matJunglePath);

            // スライムモンスター配置
            CreateSlime($"Slime_{(int)deg}", campCenter);
        }
    }

    /// <summary>
    /// スライムモンスターをプロシージャルに合成してキャンプ中心に配置する。
    /// </summary>
    private static void CreateSlime(string name, Vector3 campCenter)
    {
        // 親 GO（地面 y=0.8 に配置）
        var parent = new GameObject(name);
        parent.transform.position = new Vector3(campCenter.x, 0.8f, campCenter.z);

        // 本体 Sphere
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(parent.transform, false);
        body.transform.localScale = new Vector3(1.6f, 1.1f, 1.6f);
        SetMat(body, GetOrCreateMat("Slime", new Color(0.35f, 0.75f, 0.45f)));
        // 本体の SphereCollider は除去し、親に CapsuleCollider を付与
        Object.DestroyImmediate(body.GetComponent<SphereCollider>());
        var cap = parent.AddComponent<CapsuleCollider>();
        cap.radius = 0.9f;
        cap.height = 1.6f;
        cap.center = Vector3.zero;

        // 目（白）×2
        Vector3[] eyeOffsets = { new Vector3(-0.28f, 0.25f, 0.62f), new Vector3(0.28f, 0.25f, 0.62f) };
        foreach (var eo in eyeOffsets)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(body.transform, false);
            eye.transform.localPosition = eo;
            eye.transform.localScale    = Vector3.one * 0.22f;
            SetMat(eye, GetOrCreateMat("SlimeEye", Color.white));
            Object.DestroyImmediate(eye.GetComponent<SphereCollider>());

            // 瞳（黒）
            var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil";
            pupil.transform.SetParent(eye.transform, false);
            pupil.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            pupil.transform.localScale    = Vector3.one * 0.45f; // 親の 0.22 * 0.45 ≒ 0.1
            SetMat(pupil, GetOrCreateMat("SlimePupil", Color.black));
            Object.DestroyImmediate(pupil.GetComponent<SphereCollider>());
        }

        // HealthComponent (120 HP)
        var hc   = parent.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = 120f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        // TeamTag (Neutral)
        var tt   = parent.AddComponent<TeamTag>();
        var soTt = new SerializedObject(tt);
        soTt.FindProperty("_team").enumValueIndex = (int)TeamId.Neutral;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        // XpReward (50)
        var xp   = parent.AddComponent<XpReward>();
        var soXp = new SerializedObject(xp);
        soXp.FindProperty("_amount").floatValue = 50f;
        soXp.ApplyModifiedPropertiesWithoutUndo();

        // スライム撃破で35G付与
        var slimeGold = parent.AddComponent<GoldReward>();
        var soSlimeGold = new SerializedObject(slimeGold);
        soSlimeGold.FindProperty("_amount").intValue = 35;
        soSlimeGold.ApplyModifiedPropertiesWithoutUndo();

        // 頭上 HP バー（ミニオンと同じ構成）
        var hpBar = new GameObject("HealthBar");
        hpBar.transform.SetParent(parent.transform, false);
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

        // JungleMonster コンポーネント: Initialize で campCenter と barFill を渡す
        var jm = parent.AddComponent<JungleMonster>();
        jm.Initialize(campCenter, fill.transform);
    }

    // ---- ジャングルパス/キャンプ判定ヘルパー ----

    /// <summary>点 p が4本のジャングルパス線分のいずれかから radius 以内か判定する。</summary>
    private static bool IsNearAnyJunglePath(Vector3 p, float radius)
    {
        float[] campAngles = { 45f, 135f, 225f, 315f };
        foreach (float deg in campAngles)
        {
            float rad = deg * Mathf.Deg2Rad;
            var p1 = new Vector3(45f * Mathf.Cos(rad), 0f, 45f * Mathf.Sin(rad));
            var p2 = new Vector3(18f * Mathf.Cos(rad), 0f, 18f * Mathf.Sin(rad));
            if (DistPointToSegment(p, p1, p2) < radius) return true;
        }
        return false;
    }

    /// <summary>点 p が4キャンプ中心（半径30、θ=45/135/225/315°）のいずれかから radius 以内か判定する。</summary>
    private static bool IsNearAnyCamp(Vector3 p, float radius)
    {
        float[] campAngles = { 45f, 135f, 225f, 315f };
        foreach (float deg in campAngles)
        {
            float rad    = deg * Mathf.Deg2Rad;
            var   center = new Vector3(30f * Mathf.Cos(rad), 0f, 30f * Mathf.Sin(rad));
            if (Vector3.Distance(p, center) < radius) return true;
        }
        return false;
    }

    /// <summary>
    /// スケール 3.8〜6.2 のランダムな木を1本配置する。
    /// コライダー半径はスケールに比例（base 0.6 @ scale 4.5）。
    /// </summary>
    private static void PlaceOneTree(
        GameObject[] treeModels, System.Random rng,
        float tx, float tz, int q, int index, Material matJungle)
    {
        float treeScale = (float)(rng.NextDouble() * 2.4 + 3.8); // 3.8〜6.2
        float yaw       = (float)(rng.NextDouble() * 360.0);
        int   modelIdx  = treeModels.Length > 0 ? rng.Next(0, treeModels.Length) : -1;

        GameObject treeGo;
        if (modelIdx >= 0 && treeModels[modelIdx] != null)
        {
            treeGo = (GameObject)PrefabUtility.InstantiatePrefab(treeModels[modelIdx]);
            treeGo.transform.position   = new Vector3(tx, 0f, tz);
            treeGo.transform.localScale = Vector3.one * treeScale;
            treeGo.transform.rotation   = Quaternion.Euler(0f, yaw, 0f);
            // コライダー半径はスケールに比例（base 0.6 @ scale 4.5）
            float capRadius = 0.6f * (treeScale / 4.5f);
            var cap = treeGo.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 2f, 0f);
            cap.radius = capRadius;
            cap.height = 5f;
        }
        else
        {
            treeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            treeGo.transform.position   = new Vector3(tx, 2.5f, tz);
            treeGo.transform.localScale = new Vector3(0.8f * treeScale / 4.5f, 2.5f * treeScale / 4.5f, 0.8f * treeScale / 4.5f);
            SetMat(treeGo, matJungle);
        }
        treeGo.name = $"Tree_Q{q}_{index:D2}";
        SetStatic(treeGo);
    }

    /// <summary>点 p から線分 a-b への最短距離を返す（XZ 平面で評価）。</summary>
    private static float DistPointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var ap = p - a;
        float t = Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f);
        t = Mathf.Clamp01(t);
        var closest = a + ab * t;
        return Vector3.Distance(p, closest);
    }

    private static ItemData GetOrCreateItemData(
        string assetName, string itemName, int price,
        float attackPercent, float maxHpBonus, float moveSpeedPercent,
        string description, Color themeColor)
    {
        var path     = $"{ItemDir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (existing != null) return existing;

        var so               = ScriptableObject.CreateInstance<ItemData>();
        so.ItemName          = itemName;
        so.Price             = price;
        so.AttackPercent     = attackPercent;
        so.MaxHpBonus        = maxHpBonus;
        so.MoveSpeedPercent  = moveSpeedPercent;
        so.Description       = description;
        so.ThemeColor        = themeColor;
        AssetDatabase.CreateAsset(so, path);
        return so;
    }

    private static ItemShopCatalog GetOrCreateItemShopCatalog(System.Collections.Generic.List<ItemData> items)
    {
        const string catalogPath = ItemDir + "/ItemShopCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<ItemShopCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemShopCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }

        // 常に最新の6種に更新
        catalog.Items.Clear();
        catalog.Items.AddRange(items);
        EditorUtility.SetDirty(catalog);
        return catalog;
    }
}
