using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        ApplyWutheringRamp(matJungle);
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
            // 鳴潮風: 柔らかいランプ + 青みの影、色むらノイズテクスチャ
            ApplyWutheringRamp(matGround);
            ApplyNoiseBaseMap(matGround, "GroundNoise", new Vector2(10f, 10f));
            SetMat(ground, matGround);
        }

        // 川: 縦帯 Cube (両レーンに届く長さ92)
        // 階段順: 地面(0) < 川上面(0.03) < パス(0.045) < レーン(0.06) < ベイスン(0.12) < ピット(0.18)
        // レーンが川の上を「橋」として通るため、川はレーンより下に置く
        PlaceCube("River", new Vector3(0f, -0.02f, 0f), new Vector3(14f, 0.1f, 92f), matRiver);

        // レーン色を土色に更新
        matLane.SetColor("_BaseColor", new Color(0.62f, 0.55f, 0.42f));
        // 鳴潮風: 柔らかいランプ + 青みの影、色むらノイズテクスチャ
        ApplyWutheringRamp(matLane);
        ApplyNoiseBaseMap(matLane, "LaneNoise", new Vector2(8f, 8f));

        // レーンアーク: Cube 48個を滑らかなリング帯メッシュ1枚に置換（角のはみ出し解消）
        const float R = 45f;
        {
            var laneRing = new GameObject("LaneRing");
            laneRing.transform.position = new Vector3(0f, 0.06f, 0f);
            var mf = laneRing.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateRingBandMesh(40f, 50f, 96);
            var mr2 = laneRing.AddComponent<MeshRenderer>();
            mr2.sharedMaterial = matLane;
            SetStatic(laneRing);
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
        // 高品質 FBX（Assets/External/Nature）へ置換。ロード後に bounds を測って
        // 目標樹高 4.5〜7m へ正規化（モデルごとの原寸差を吸収）。
        // 種の混合: Tree_1 40% / Birch_1 25% / Pine_1 25% / TreeToonStylized01 10%、
        // まれに DeadTree_1（5%以下、ジャングル奥のみ）。葉は 3 トーンからシード固定で割当。
        {
            var natureSpecies = LoadNatureSpecies();

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

                        // クラスタ中心はジャングル奥扱い → まれに枯木を許可
                        PlaceOneTree(natureSpecies, rng, tx, tz, q, placed, matJungle, allowDeadTree: true);
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

                    PlaceOneTree(natureSpecies, rng, tx, tz, q, placed, matJungle, allowDeadTree: false);
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
        ApplyWutheringRamp(matJunglePath);
        PlaceJunglePathsAndCamps(matJunglePath);

        // ---- 地表植生の散布（草タフト・小石）----
        ScatterGroundVegetation();

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
                "Assets/External/Towers/DungeonTowerD.fbx");

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

                // 接地位置 y=0。チームはタワー名の B/R プレフィックスで判定
                bool isBlue = tname.StartsWith("Tower_B");
                PlaceTower(tname, tPos, tmat, null, towerModel, isBlue);

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
        light.color     = new Color(1.0f, 0.96f, 0.88f);
        light.intensity = 1.25f;
        // 鳴潮風の柔らかい実時間影（shadowBias/normalBias はデフォルトのまま）
        light.shadows        = LightShadows.Soft;
        light.shadowStrength = 0.85f;
        dirLight.transform.rotation = Quaternion.Euler(48f, -38f, 0f);

        // アニメ調スカイボックス + 環境光（URP）
        var skyMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/AnimeSky.mat");
        if (skyMat != null) RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.55f, 0.65f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.50f, 0.58f);
        RenderSettings.ambientGroundColor  = new Color(0.30f, 0.28f, 0.30f);

        // 大気フォグ（遠景を薄く沈める）
        RenderSettings.fog           = true;
        RenderSettings.fogMode       = FogMode.ExponentialSquared;
        RenderSettings.fogColor      = new Color(0.62f, 0.70f, 0.85f);
        RenderSettings.fogDensity    = 0.0045f;

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
        // トリガーイベントはペアのどちらかに Rigidbody が必須。
        // 弾側に持たせることで RB なしの静的コライダー（ミニオン等）にも OnTriggerEnter が発火する
        var projRb = projGo.AddComponent<Rigidbody>();
        projRb.isKinematic = true;
        projRb.useGravity  = false;
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
        // Directional=0.2/0.3、GroundAoe=0.35/0.45、Targeted=0.15/0.3
        var skillSlash = GetOrCreateSkillDefinition("Skill_MagicSlash",
            "魔導斬撃", SkillTargeting.Directional, 25f, 25f, 0f, 4f, 30f, 0.2f, 0.3f,
            "指定方向へ魔力の斬撃を飛ばし、直線上の敵にダメージを与える。");
        var skillAoe = GetOrCreateSkillDefinition("Skill_ExplosionCircle",
            "爆裂魔法陣", SkillTargeting.GroundAoe, 40f, 20f, 4f, 8f, 0f, 0.35f, 0.45f,
            "指定地点に魔法陣を展開し、少し遅れて爆発させ範囲内の敵にダメージを与える。");
        var skillChase = GetOrCreateSkillDefinition("Skill_Chase",
            "追撃", SkillTargeting.Targeted, 30f, 15f, 0f, 6f, 0f, 0.15f, 0.3f,
            "対象の敵単体へ瞬時に追撃を加え、確定ダメージを与える。");

        // garon
        var garonQ = GetOrCreateSkillDefinition("Skill_garon_Q",
            "シールドバッシュ", SkillTargeting.Directional, 15f, 15f, 0f, 5f, 25f, 0.2f, 0.3f,
            "盾を構えて前方へ突進し、直線上の敵を打ち据えてダメージを与える。");
        var garonW = GetOrCreateSkillDefinition("Skill_garon_W",
            "グランドスラム", SkillTargeting.GroundAoe, 30f, 12f, 5f, 9f, 0f, 0.35f, 0.45f,
            "指定地点の地面を叩きつけ、範囲内の敵にダメージを与える。");
        var garonE = GetOrCreateSkillDefinition("Skill_garon_E",
            "チェーンフック", SkillTargeting.Targeted, 20f, 12f, 0f, 7f, 0f, 0.15f, 0.3f,
            "対象の敵単体へ鎖の鉤を打ち込み、ダメージを与える。");

        // veil
        var veilQ = GetOrCreateSkillDefinition("Skill_veil_Q",
            "アーケインボルト", SkillTargeting.Directional, 30f, 30f, 0f, 4f, 35f, 0.2f, 0.3f,
            "指定方向へ魔力弾を放ち、直線上の最初に当たった敵にダメージを与える。");
        var veilW = GetOrCreateSkillDefinition("Skill_veil_W",
            "量子爆発", SkillTargeting.GroundAoe, 50f, 22f, 5f, 10f, 0f, 0.35f, 0.45f,
            "指定地点で量子エネルギーを暴走させ、範囲内の敵に大ダメージを与える。");
        var veilE = GetOrCreateSkillDefinition("Skill_veil_E",
            "ヘックス", SkillTargeting.Targeted, 35f, 18f, 0f, 8f, 0f, 0.15f, 0.3f,
            "対象の敵単体に呪詛をかけ、確定ダメージを与える。");

        // rin
        var rinQ = GetOrCreateSkillDefinition("Skill_rin_Q",
            "貫通矢", SkillTargeting.Directional, 28f, 35f, 0f, 3.5f, 45f, 0.2f, 0.3f,
            "指定方向へ高速の矢を放ち、直線上の敵を貫いてダメージを与える。");
        var rinW = GetOrCreateSkillDefinition("Skill_rin_W",
            "矢の雨", SkillTargeting.GroundAoe, 35f, 25f, 4.5f, 9f, 0f, 0.35f, 0.45f,
            "指定地点へ無数の矢を降らせ、範囲内の敵にダメージを与える。");
        var rinE = GetOrCreateSkillDefinition("Skill_rin_E",
            "狙撃", SkillTargeting.Targeted, 40f, 20f, 0f, 9f, 0f, 0.15f, 0.3f,
            "対象の敵単体を遠距離から狙撃し、高い確定ダメージを与える。");

        // nova
        var novaQ = GetOrCreateSkillDefinition("Skill_nova_Q",
            "パルスウェーブ", SkillTargeting.Directional, 18f, 20f, 0f, 4f, 28f, 0.2f, 0.3f,
            "指定方向へエネルギー波を放ち、直線上の敵にダメージを与える。");
        var novaW = GetOrCreateSkillDefinition("Skill_nova_W",
            "リペアフィールド", SkillTargeting.GroundAoe, 20f, 18f, 5f, 8f, 0f, 0.35f, 0.45f,
            "指定地点に力場を展開し、範囲内の敵にダメージを与える。");
        var novaE = GetOrCreateSkillDefinition("Skill_nova_E",
            "スタンボルト", SkillTargeting.Targeted, 15f, 15f, 0f, 6f, 0f, 0.15f, 0.3f,
            "対象の敵単体へ電撃を撃ち込み、ダメージを与える。");

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
        var playerProgression = player.AddComponent<PlayerProgression>();

        // 頭上 HP バー（緑）＋ レベル表示テキスト
        var matBarGreenPlayer = GetOrCreateBarMat("BarGreen", new Color(0.30f, 0.85f, 0.35f));
        // プレイヤーのピボットはカプセル中心（地上+1.05m）。UnityChan の頭頂はローカル+0.45 付近
        var playerBarFill     = CreateWorldHealthBar(player.transform, 1.05f, 0.65f, matBarGreenPlayer, 200f);

        // LevelText: HealthBar GO の子に配置し、バー左側に添える
        var healthBarGo = player.transform.Find("HealthBar");
        if (healthBarGo != null)
        {
            var lvTextGo = new GameObject("LevelText");
            lvTextGo.transform.SetParent(healthBarGo, false);
            // 親の HealthBar が既に頭上 (y+1.75) にあるためローカル Y は 0
            lvTextGo.transform.localPosition = new Vector3(-0.68f, 0f, 0f);
            var lvTm             = lvTextGo.AddComponent<TextMesh>();
            lvTm.text            = "1";
            lvTm.color           = Color.white;
            lvTm.fontSize        = 36;
            lvTm.characterSize   = 0.05f;
            lvTm.anchor          = TextAnchor.MiddleCenter;
            lvTm.alignment       = TextAlignment.Center;
        }

        // PlayerOverheadUI: HP バー比率とレベル数字を更新するコンポーネント
        var overheadUi    = player.AddComponent<Enigma.Character.PlayerOverheadUI>();
        var soOverheadUi  = new SerializedObject(overheadUi);
        soOverheadUi.FindProperty("_barFill").objectReferenceValue          = playerBarFill;
        soOverheadUi.FindProperty("_healthComponent").objectReferenceValue  = healthComp;
        soOverheadUi.FindProperty("_progression").objectReferenceValue      = playerProgression;
        soOverheadUi.ApplyModifiedPropertiesWithoutUndo();

        // ゴールドとアイテム管理
        player.AddComponent<PlayerWallet>();
        player.AddComponent<PlayerItems>();

        // 泉回復(青ベースの泉付近で毎秒回復)
        var playerFountain   = player.AddComponent<Enigma.Combat.FountainRegen>();
        var soPlayerFountain = new SerializedObject(playerFountain);
        soPlayerFountain.FindProperty("_fountainCenter").vector3Value = new Vector3(-52f, 1.1f, 10f);
        soPlayerFountain.ApplyModifiedPropertiesWithoutUndo();

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

        // ダメージポップアップ管理（シーン常駐、1秒スキャンで自動購読）
        matchFlowGo.AddComponent<Enigma.UI.DamagePopupManager>();

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

        // PlayerAttackMotor を追加し、UnityChanModel の Transform を _modelRoot に結線
        var attackMotor = player.AddComponent<PlayerAttackMotor>();
        var soMotor = new SerializedObject(attackMotor);
        var unityChanModel = player.transform.Find("UnityChanModel");
        if (unityChanModel != null)
            soMotor.FindProperty("_modelRoot").objectReferenceValue = unityChanModel;
        soMotor.ApplyModifiedPropertiesWithoutUndo();

        // SkillCaster/_motor 結線
        var soSkillCasterMotor = new SerializedObject(skillCaster);
        soSkillCasterMotor.FindProperty("_motor").objectReferenceValue = attackMotor;
        soSkillCasterMotor.ApplyModifiedPropertiesWithoutUndo();

        // AutoAttack/_motor 結線
        var soAutoAttackMotor = new SerializedObject(autoAttack);
        soAutoAttackMotor.FindProperty("_motor").objectReferenceValue = attackMotor;
        soAutoAttackMotor.ApplyModifiedPropertiesWithoutUndo();

        // PlayerController/_motor 結線
        var soPlayerMotor = new SerializedObject(player.GetComponent<PlayerController>());
        soPlayerMotor.FindProperty("_motor").objectReferenceValue = attackMotor;
        soPlayerMotor.ApplyModifiedPropertiesWithoutUndo();

        // OrbitCamera
        var soCam = new SerializedObject(orbitCam);
        soCam.FindProperty("_target").objectReferenceValue = player.transform;
        soCam.ApplyModifiedPropertiesWithoutUndo();

        // 11. ターゲットダミー 2体
        CreateDummy("Dummy_A", new Vector3(-32f, 1f, 30f), matDummy);
        CreateDummy("Dummy_B", new Vector3(-26f, 1f, 36f), matDummy);

        // 11b. デバッグ用ダミー敵プレイヤー 2体（Red チーム、HP500、リスポーン付き）
        var matBarRed = GetOrCreateBarMat("BarRed", new Color(0.92f, 0.30f, 0.25f));
        CreateEnemyDummy("EnemyDummy_Lane",  new Vector3(-31.8f, 1.1f, 31.8f), matRed, matBarRed);
        CreateEnemyDummy("EnemyDummy_River", new Vector3(3.5f,   1.1f, -28f),  matRed, matBarRed);

        // 11c. 赤チーム レーナー AI チャンピオン（TOPレーンを北回りに進軍）
        CreateEnemyChampion(matRed, matBarRed, projPrefab.GetComponent<Projectile>());

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

        // 16. 境界壁
        CreateOuterBoundary();

        // 17. シーン保存
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BuildAetherRiftMap] AetherRift_Map.unity を保存しました。");
    }

    // ---- 境界壁 ----

    /// <summary>
    /// レーン外縁リング壁 + 各ベースポケット壁を生成し "OuterBoundary" 親 GO にまとめる。
    /// BoxCollider はプリミティブ既定のものを使用（追加設定不要）。
    /// </summary>
    private static void CreateOuterBoundary()
    {
        var matBoundary = GetOrCreateMat("BoundaryWall", new Color(0.58f, 0.55f, 0.50f));
        ApplyWutheringRamp(matBoundary);

        var parent = new GameObject("OuterBoundary");
        SetStatic(parent);

        // --- 1. レーン外縁リング壁 (半径 51、96 セグメント) ---
        const int   LaneSegments   = 96;
        const float LaneRadius     = 51.0f;
        const float LaneWallH      = 1.5f;
        const float LaneWallDepth  = 1.2f;
        // 開口: ベース接続部 0°±12° および 180°±12°
        const float GapHalfAngle   = 12f;

        float laneStepDeg  = 360f / LaneSegments;
        float laneChordLen = 2f * LaneRadius * Mathf.Sin(laneStepDeg * 0.5f * Mathf.Deg2Rad);
        float laneSegW     = laneChordLen + 0.15f;

        for (int i = 0; i < LaneSegments; i++)
        {
            float angleDeg = i * laneStepDeg;

            // ベース接続開口をスキップ
            float normAngle = ((angleDeg % 360f) + 360f) % 360f;
            float diff0     = Mathf.Abs(Mathf.DeltaAngle(normAngle, 0f));
            float diff180   = Mathf.Abs(Mathf.DeltaAngle(normAngle, 180f));
            if (diff0 <= GapHalfAngle || diff180 <= GapHalfAngle) continue;

            float rad = angleDeg * Mathf.Deg2Rad;
            var   pos = new Vector3(LaneRadius * Mathf.Cos(rad), LaneWallH * 0.5f, LaneRadius * Mathf.Sin(rad));

            var seg = PlaceCube($"LaneWall_{i:D3}", pos, new Vector3(laneSegW, LaneWallH, LaneWallDepth), matBoundary);
            // 円周接線方向に回転（法線が中心を向くように +90°）
            seg.transform.rotation = Quaternion.Euler(0f, -(angleDeg + 90f), 0f);
            seg.transform.SetParent(parent.transform, true);
        }

        // --- 2. ベースポケット壁 (各ベース中心 ±56、半径 12、32 セグメント) ---
        const int   BaseSegments  = 32;
        const float BaseRadius    = 12.0f;
        const float BaseWallH     = 1.5f;
        const float BaseWallDepth = 1.0f;

        float baseStepDeg  = 360f / BaseSegments;
        float baseChordLen = 2f * BaseRadius * Mathf.Sin(baseStepDeg * 0.5f * Mathf.Deg2Rad);
        float baseSegW     = baseChordLen + 0.15f;

        var baseCenters = new (Vector3 center, string label)[]
        {
            (new Vector3(-56f, 0f, 0f), "Blue"),
            (new Vector3( 56f, 0f, 0f), "Red"),
        };

        foreach (var (center, label) in baseCenters)
        {
            for (int i = 0; i < BaseSegments; i++)
            {
                float angleDeg = i * baseStepDeg;
                float rad      = angleDeg * Mathf.Deg2Rad;
                var   segWorld = center + new Vector3(BaseRadius * Mathf.Cos(rad), 0f, BaseRadius * Mathf.Sin(rad));

                // マップ中心からの距離 < 51 はレーン側開口 → スキップ
                if (segWorld.magnitude < LaneRadius) continue;

                var pos = new Vector3(segWorld.x, BaseWallH * 0.5f, segWorld.z);
                var seg = PlaceCube($"BaseWall_{label}_{i:D2}", pos,
                    new Vector3(baseSegW, BaseWallH, BaseWallDepth), matBoundary);
                seg.transform.rotation = Quaternion.Euler(0f, -(angleDeg + 90f), 0f);
                seg.transform.SetParent(parent.transform, true);
            }
        }
    }

    // ---- ヘルパー ----

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    /// <summary>
    /// 鳴潮風の柔らかいトゥーンランプを設定する。
    /// バンド境界をぼかし（_RampSmoothing）、暗部に青みを与える（_ShadeColor）。
    /// シェーダー本体は変更せずマテリアルプロパティのみ調整する。
    /// </summary>
    private static void ApplyWutheringRamp(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty("_RampSmoothing")) mat.SetFloat("_RampSmoothing", 0.18f);
        if (mat.HasProperty("_ShadeColor"))    mat.SetColor("_ShadeColor", new Color(0.58f, 0.62f, 0.80f, 1f));
        EditorUtility.SetDirty(mat);
    }

    /// <summary>
    /// Assets/_Project/UI/Textures 配下のノイズ PNG を _BaseMap に設定し、タイリングを与える。
    /// 既存マテリアルを GetOrCreateMat が返した場合でも上書き設定する。
    /// </summary>
    private static void ApplyNoiseBaseMap(Material mat, string textureName, Vector2 tiling)
    {
        if (mat == null) return;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"Assets/_Project/UI/Textures/{textureName}.png");
        if (tex == null) return;
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", tiling);
        EditorUtility.SetDirty(mat);
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

    /// <summary>
    /// 不透明 URP/Unlit バー用マテリアルを GetOrCreate する。
    /// 既存アセットがあれば色を更新して返す。
    /// </summary>
    private static Material GetOrCreateBarMat(string name, Color color)
    {
        var path     = $"{MatDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.SetColor("_BaseColor", color);
            return existing;
        }

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat    = new Material(shader);
        mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>
    /// 左端アンカー型ワールド HP バーを生成し、FillWrapper の Transform を返す。
    /// 呼び出し側は FillWrapper を _barFill に結線する。
    /// FillWrapper.localScale.x = ratio (0〜1) で左詰め表示になる。
    /// </summary>
    /// <param name="parent">バーを親付けする Transform（エンティティ本体）</param>
    /// <param name="width">バーの全幅</param>
    /// <param name="yOffset">頭上オフセット（localPosition.y）</param>
    /// <param name="fillMat">Fill Quad に設定するマテリアル</param>
    /// <param name="maxHp">目盛り間隔の計算に使う最大 HP</param>
    private static Transform CreateWorldHealthBar(
        Transform parent, float width, float yOffset, Material fillMat, float maxHp)
    {
        var matBack = GetOrCreateBarMat("BarBack", new Color(0.08f, 0.08f, 0.10f));

        // HealthBar GO（Billboard 付き）
        var hpBar = new GameObject("HealthBar");
        hpBar.transform.SetParent(parent, false);
        hpBar.transform.localPosition = new Vector3(0f, yOffset, 0f);
        hpBar.AddComponent<HealthBarBillboard>();

        // Background Quad（中央ピボット）
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "Background";
        bg.transform.SetParent(hpBar.transform, false);
        bg.transform.localScale = new Vector3(width, 0.18f, 1f);
        bg.GetComponent<Renderer>().sharedMaterial = matBack;
        Object.DestroyImmediate(bg.GetComponent<MeshCollider>());

        // FillWrapper: 左端をアンカーとして配置
        // localPosition.x = -width/2 なので scale.x=1 で右端が中央まで伸び、
        // scale.x=ratio で左端から ratio 分の幅になる
        var fillWrapper = new GameObject("FillWrapper");
        fillWrapper.transform.SetParent(hpBar.transform, false);
        fillWrapper.transform.localPosition = new Vector3(-width / 2f, 0f, -0.001f);
        fillWrapper.transform.localScale    = Vector3.one;

        // Fill Quad: FillWrapper 内で右端中心（localPosition.x = width/2）に配置
        // FillWrapper.scale.x が変化しても Fill 自体の localPosition.x は変わらないため
        // 常に左端から伸長する外見になる
        var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "Fill";
        fill.transform.SetParent(fillWrapper.transform, false);
        fill.transform.localPosition = new Vector3(width / 2f, 0f, 0f);
        fill.transform.localScale    = new Vector3(width, 0.14f, 1f);
        fill.GetComponent<Renderer>().sharedMaterial = fillMat;
        Object.DestroyImmediate(fill.GetComponent<MeshCollider>());

        // 目盛り Quad: Background の子として各目盛り位置に配置。
        // Background は 1x1 Quad（localScale = (width, 0.18, 1)）なので、
        // 子の localPosition.x は [-0.5, +0.5] が全幅に対応。
        // FillWrapper は hpBar ローカル z=-0.001 なので、z=-0.002 で Fill より手前に描画される。
        var matTick   = GetOrCreateBarMat("BarTick", new Color(0.05f, 0.05f, 0.05f));
        int tickCount = HealthBarTicks.InnerTickCount(maxHp);
        for (int i = 1; i <= tickCount; i++)
        {
            float ratio = HealthBarTicks.TickRatio(maxHp, i);
            var tick = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tick.name = $"Tick_{i}";
            tick.transform.SetParent(bg.transform, false);
            tick.transform.localPosition = new Vector3(ratio - 0.5f, 0f, -0.002f);
            tick.transform.localScale    = new Vector3(0.02f / width, 1f, 1f);
            tick.GetComponent<Renderer>().sharedMaterial = matTick;
            Object.DestroyImmediate(tick.GetComponent<MeshCollider>());
        }

        return fillWrapper.transform;
    }

    private static SkillDefinition GetOrCreateSkillDefinition(
        string assetName, string skillName, SkillTargeting targeting,
        float damage, float range, float radius, float cd, float projSpeed,
        float windup = 0.2f, float recovery = 0.35f, string description = "")
    {
        var path     = $"{SkillDir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (existing != null)
        {
            // 既存アセットも windup/recovery/説明文を上書き更新
            existing.WindupSeconds   = windup;
            existing.RecoverySeconds = recovery;
            existing.Description     = description;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var so = ScriptableObject.CreateInstance<SkillDefinition>();
        so.SkillName       = skillName;
        so.Description     = description;
        so.Targeting       = targeting;
        so.Damage          = damage;
        so.Range           = range;
        so.Radius          = radius;
        so.CooldownSeconds = cd;
        so.ProjectileSpeed = projSpeed;
        so.WindupSeconds   = windup;
        so.RecoverySeconds = recovery;
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

        // PlayerController がある場合のみ Animator を結線する（敵 AI には存在しない）
        var pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            var soPc = new SerializedObject(pc);
            soPc.FindProperty("_animator").objectReferenceValue = animator;
            soPc.ApplyModifiedPropertiesWithoutUndo();
        }

        ApplyToonMaterials(model);
    }

    // 元マテリアルのメインテクスチャを引き継いだ Enigma/Toon マテリアルに差し替える
    private static void ApplyToonMaterials(GameObject model)
    {
        const string dir = "Assets/_Project/Materials/UnityChan";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "UnityChan");
        var toon     = Shader.Find("Enigma/Toon");
        var toonFace = Shader.Find("Enigma/ToonFace");
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
                    // 顔・目はソフトシャドウが汚く落ちるため顔専用シェーダーで面受けさせる
                    bool useFace = toonFace != null && IsFaceLikeMaterial(src.name);
                    dst = new Material(useFace ? toonFace : toon);
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
                // マテリアル名で部位別の見た目を調整（_Cutoff は維持）。既存アセットも毎回上書き（冪等）
                TuneUnityChanMaterial(dst, src.name);
                mats[i] = dst;
            }
            r.sharedMaterials = mats;
        }
    }

    // 顔系・目系は Enigma/ToonFace を使う（小文字部分一致）
    private static bool IsFaceLikeMaterial(string materialName)
    {
        string n = materialName.ToLowerInvariant();
        return n.Contains("face") || n.Contains("cheek")
            || n.Contains("eye") || n.Contains("eyebrow")
            || n.Contains("eyelash") || n.Contains("eyeline")
            || n.Contains("mat_eye");
    }

    // マテリアル名（小文字部分一致）で部位ごとのトゥーン設定を上書きする。
    // _Cutoff / _BaseMap には触れない（スペキュラマスク・カットアウトの既存対処を保護）
    private static void TuneUnityChanMaterial(Material m, string materialName)
    {
        string n = materialName.ToLowerInvariant();

        // 目・眉・まつ毛・アイライン・まつ毛左右: 常時フルライト（影・ランプ・輪郭を排除）
        bool isEye = n.Contains("eye") || n.Contains("eyebrow")
                  || n.Contains("eyelash") || n.Contains("eyeline")
                  || n.Contains("mat_eye")
                  || n == "left" || n == "right";
        if (isEye)
        {
            m.SetFloat("_RampThreshold", 0f);
            m.SetFloat("_RampSmoothing", 0.001f);
            m.SetFloat("_SelfShadowStrength", 0f);
            m.SetFloat("_OutlineWidth", 0f);
            return;
        }

        // 顔・頬: 暖ピンクの陰、細い肌色アウトライン
        if (n.Contains("face") || n.Contains("cheek"))
        {
            m.SetColor("_ShadeColor", new Color(0.96f, 0.80f, 0.78f));
            m.SetFloat("_OutlineWidth", 0.0015f);
            m.SetColor("_OutlineColor", new Color(0.55f, 0.36f, 0.32f));
            return;
        }

        // 肌: 暖色寄りの陰
        if (n.Contains("skin"))
        {
            m.SetColor("_ShadeColor", new Color(0.95f, 0.74f, 0.72f));
            return;
        }

        // 髪: 紫寄りの陰 + 明るいリム
        if (n.Contains("hair"))
        {
            m.SetColor("_ShadeColor", new Color(0.62f, 0.60f, 0.82f));
            m.SetColor("_RimColor", new Color(1.0f, 0.95f, 0.85f, 0.55f));
            m.SetFloat("_RimPower", 2.8f);
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

    private const float TowerHeight = 6.5f;

    /// <summary>
    /// タワーを配置する。ルート GO にゲームロジック（HP/Team/TowerAttack/コライダー/HPバー/報酬）を持たせ、
    /// 見た目は DungeonTowerD.fbx を子 "Visual" として分離。高さは bounds 計測→相対乗算で TowerHeight に正規化、
    /// 接地 y=0。頂上にチーム色クリスタル(SlowSpin)を浮かべ、muzzle はクリスタル位置に置く。
    /// </summary>
    private static void PlaceTower(string name, Vector3 pos, Material mat, Projectile projPrefab,
        GameObject towerModel = null, bool isBlue = true)
    {
        // ルート GO（ゲームロジック保持側）。見た目は子の FBX に分離する
        var go = new GameObject(name);
        go.transform.position = pos;
        SetStatic(go);

        if (towerModel != null)
        {
            // 見た目: DungeonTowerD.fbx を子としてインスタンス化
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(towerModel);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // FBX 側のコライダーは除去（ルートのクリック用コライダーに一本化）
            foreach (var c in visual.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            // 高さ正規化: bounds 計測 → 相対乗算（FBX ルートの単位変換スケールを壊さない）
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float measuredH = b.size.y;
                if (measuredH > 0.0001f)
                    visual.transform.localScale =
                        visual.transform.localScale * (TowerHeight / measuredH);

                // スケール変更を即時反映させてから再計測する
                Physics.SyncTransforms();

                // 接地補正: スケール後に再計測して最下端を y=0 に合わせる
                var b2 = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b2.Encapsulate(rends[i].bounds);
                float footOffset = b2.min.y - go.transform.position.y;
                visual.transform.localPosition -= new Vector3(0f, footOffset, 0f);
            }

            // チーム色マテリアル（Enigma/Toon + パレット PNG）を全レンダラーに適用
            var towerMat = GetOrCreateTowerMat(isBlue);
            foreach (var r in rends) r.sharedMaterial = towerMat;

            // 見た目サブツリーを静的化（クリスタルは別ツリーなので動的のまま）
            foreach (var t in visual.GetComponentsInChildren<Transform>())
                SetStatic(t.gameObject);
        }

        // ルートのクリック用コライダー（FBX 由来は上で除去済み）
        var cap = go.AddComponent<CapsuleCollider>();
        cap.radius = 1.2f;
        cap.height = TowerHeight;
        cap.center = new Vector3(0f, TowerHeight * 0.5f, 0f);

        // HP
        var hc = go.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = 500f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        // チーム
        var tt   = go.AddComponent<TeamTag>();
        var soTt = new SerializedObject(tt);
        soTt.FindProperty("_team").enumValueIndex = isBlue ? (int)TeamId.Blue : (int)TeamId.Red;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        // 頂上チーム色クリスタル（BossCrystal メッシュ）+ SlowSpin。コライダーなし
        var crystalTransform = CreateTowerCrystal(go.transform, isBlue);

        // TowerAttack: 発射起点はクリスタル位置に置き、差し替えでも結線を維持
        var ta      = go.AddComponent<TowerAttack>();
        var muzzleGo = new GameObject("Muzzle");
        muzzleGo.transform.SetParent(go.transform, false);
        muzzleGo.transform.position = crystalTransform.position;

        var soTa = new SerializedObject(ta);
        soTa.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab;
        soTa.FindProperty("_muzzle").objectReferenceValue           = muzzleGo.transform;
        soTa.ApplyModifiedPropertiesWithoutUndo();

        // 頭上 HP バー（新高 6.5m に合わせて yOffset +7.2）。味方=緑/敵=赤 の規約に合わせる
        var matBar = isBlue
            ? GetOrCreateBarMat("BarGreen", new Color(0.30f, 0.85f, 0.35f))
            : GetOrCreateBarMat("BarRed",   new Color(0.92f, 0.30f, 0.25f));
        CreateWorldHealthBar(go.transform, 1.4f, 7.2f, matBar, 500f);

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

    /// <summary>
    /// タワー外壁用 Enigma/Toon マテリアルを GetOrCreate する。
    /// UV パレット方式のため、青/赤パレット PNG を _BaseMap に貼るだけで色違いになる。
    /// </summary>
    private static Material GetOrCreateTowerMat(bool isBlue)
    {
        var name = isBlue ? "TowerBlue" : "TowerRed";
        var palettePng = isBlue
            ? "Assets/External/Towers/DungeonArena_ColorPaletteBLUE.png"
            : "Assets/External/Towers/DungeonArena_ColorPaletteRED.png";

        var path = $"{MatDir}/{name}.mat";
        var mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Enigma/Toon") ?? Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        // このモデルの UV はパレットの黒スウォッチ帯を指す「ダーク調」前提のため、
        // パレットは使わずフラットな石色+チーム色味で塗る(トゥーン+輪郭線で形状を立てる)
        _ = palettePng;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", isBlue
                ? new Color(0.55f, 0.61f, 0.76f)
                : new Color(0.76f, 0.55f, 0.53f));
        if (mat.HasProperty("_RampSmoothing")) mat.SetFloat("_RampSmoothing", 0.18f);
        if (mat.HasProperty("_ShadeColor")) mat.SetColor("_ShadeColor", new Color(0.58f, 0.62f, 0.80f, 1f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// タワー頂上(y≈7.0)に浮かべるチーム色クリスタル(BossCrystal メッシュ)を生成する。
    /// URP/Unlit 発光マテリアル + SlowSpin。コライダーは付けない。
    /// </summary>
    private static Transform CreateTowerCrystal(Transform parent, bool isBlue)
    {
        // ボスの BossCrystal.asset はボス生成時に Delete+再生成されるため共有しない
        // (先に配置したタワーの参照が死ぬ)。タワー専用メッシュを実寸で1度だけ作る
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/TowerCrystal.asset");
        if (mesh == null)
            mesh = ProceduralBossMeshes.CreateBipyramid("TowerCrystal", 0.45f, 1.4f, 6);

        var color = isBlue ? new Color(0.35f, 0.65f, 1.0f) : new Color(1.0f, 0.4f, 0.35f);
        var matName = isBlue ? "TowerCrystalBlue" : "TowerCrystalRed";
        var mat = GetOrCreateUnlitEmissiveMat(matName, color * 2f);

        var crystal = CreateMeshGo("Crystal", mesh, mat, parent);
        crystal.transform.localPosition = new Vector3(0f, 7.0f, 0f);
        crystal.transform.localScale = Vector3.one;

        crystal.AddComponent<Enigma.Map.SlowSpin>();
        return crystal.transform;
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

        var matBarRed = GetOrCreateBarMat("BarRed", new Color(0.92f, 0.30f, 0.25f));
        var wrapper   = CreateWorldHealthBar(dummy.transform, 1.2f, 1.6f, matBarRed, 200f);

        var soTd = new SerializedObject(td);
        soTd.FindProperty("_barFill").objectReferenceValue = wrapper;
        soTd.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateEnemyDummy(string name, Vector3 pos, Material matCapsule, Material matBarRed)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.position = pos;
        SetMat(go, matCapsule);

        // クリックしやすいコライダー調整（カプセルのピボットは中心）
        var cap = go.GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            cap.radius = 0.6f;
            cap.height = 2.4f;
            cap.center = new Vector3(0f, 0f, 0f);
        }

        var hc = go.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = 500f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        var tt = go.AddComponent<TeamTag>();
        var soTt = new SerializedObject(tt);
        soTt.FindProperty("_team").enumValueIndex = (int)TeamId.Red;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        var wrapper = CreateWorldHealthBar(go.transform, 1.05f, 1.0f, matBarRed, 500f);

        var dc = go.AddComponent<DummyChampion>();
        var soDc = new SerializedObject(dc);
        soDc.FindProperty("_barFill").objectReferenceValue = wrapper;
        soDc.ApplyModifiedPropertiesWithoutUndo();

        // 撃破で50XP付与
        var reward = go.AddComponent<XpReward>();
        var soReward = new SerializedObject(reward);
        soReward.FindProperty("_amount").floatValue = 50f;
        soReward.ApplyModifiedPropertiesWithoutUndo();
    }

    // 赤チームのレーナー AI チャンピオン1体を生成する。
    // CharacterController + HealthComponent(500) + TeamTag(Red) + EnemyChampionAI +
    // XpReward(100)/GoldReward(300)。UnityChan モデル・足元リング・頭上バーを結線する。
    private static void CreateEnemyChampion(Material matBody, Material matBarRed, Projectile projPrefab)
    {
        var spawnPos = new Vector3(52f, 1.1f, -6f);

        // ベースはカプセル（モデルが乗るまでの当たり/フォールバック表示）
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "EnemyChampion";
        go.transform.position = spawnPos;
        SetMat(go, matBody);

        // 既定のカプセルコライダーは消し、CharacterController + クリック用カプセルを付ける
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());

        var cc = go.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = new Vector3(0f, 0f, 0f);

        // クリック判定用の追加カプセル（プレイヤー/ミニオン同様）
        var clickCap = go.AddComponent<CapsuleCollider>();
        clickCap.radius = 0.6f;
        clickCap.height = 2.4f;
        clickCap.center = new Vector3(0f, 0f, 0f);

        var hc = go.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = 500f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        var tt = go.AddComponent<TeamTag>();
        var soTt = new SerializedObject(tt);
        soTt.FindProperty("_team").enumValueIndex = (int)TeamId.Red;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        // 泉回復(赤ベースの泉=リスポーン地点付近で毎秒回復)
        var botFountain   = go.AddComponent<Enigma.Combat.FountainRegen>();
        var soBotFountain = new SerializedObject(botFountain);
        soBotFountain.FindProperty("_fountainCenter").vector3Value = spawnPos;
        soBotFountain.ApplyModifiedPropertiesWithoutUndo();

        var xp = go.AddComponent<XpReward>();
        var soXp = new SerializedObject(xp);
        soXp.FindProperty("_amount").floatValue = 100f;
        soXp.ApplyModifiedPropertiesWithoutUndo();

        var gold = go.AddComponent<GoldReward>();
        var soGold = new SerializedObject(gold);
        soGold.FindProperty("_amount").intValue = 300;
        soGold.ApplyModifiedPropertiesWithoutUndo();

        // 頭上 HPバー（レベル表示なし）
        var wrapper = CreateWorldHealthBar(go.transform, 1.05f, 0.65f, matBarRed, 500f);

        // 銃口 Transform（攻撃弾の発射点）。胸高・前方
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(go.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.4f, 0.6f);

        // 識別用の赤い半透明リング（半径1.2 の薄い円柱、コライダーなし）
        var ringMat = GetOrCreateTransparentMat("EnemyChampionRing", new Color(0.9f, 0.15f, 0.15f, 0.5f));
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "EnemyRing";
        ring.transform.SetParent(go.transform, false);
        ring.transform.localPosition = new Vector3(0f, -0.98f, 0f); // 足元
        ring.transform.localScale    = new Vector3(2.4f, 0.02f, 2.4f); // 直径2.4 = 半径1.2
        SetMat(ring, ringMat);
        Object.DestroyImmediate(ring.GetComponent<CapsuleCollider>());

        // UnityChan モデルを子付け（プレイヤー専用処理は内部で分岐済み）
        AttachUnityChanModel(go);

        var ai = go.AddComponent<EnemyChampionAI>();

        // TOPレーン（z>0 側）の経路: 赤ベース開口(50,0,10) → 半径45 を北回りに
        // 12°刻みでアーク → 青ベース開口(-50,0,10)。
        var waypoints = BuildTopLaneWaypoints();

        var soAi = new SerializedObject(ai);
        soAi.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab;
        soAi.FindProperty("_muzzle").objectReferenceValue           = muzzle.transform;
        soAi.FindProperty("_barFill").objectReferenceValue          = wrapper;

        var wpProp = soAi.FindProperty("_waypoints");
        wpProp.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
            wpProp.GetArrayElementAtIndex(i).vector3Value = waypoints[i];

        soAi.ApplyModifiedPropertiesWithoutUndo();
    }

    // TOPレーン経路を赤ベース→青ベース方向（角度 20°→160°、12°刻み）で構築する。
    // ミニオンの ArcPt と同じ半径45・角度系。
    private static Vector3[] BuildTopLaneWaypoints()
    {
        Vector3 ArcPt(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(45f * Mathf.Cos(r), 0f, 45f * Mathf.Sin(r));
        }

        var list = new List<Vector3>();
        list.Add(new Vector3(50f, 0f, 10f)); // 赤ベース開口
        for (float deg = 20f; deg <= 160f + 0.01f; deg += 12f)
            list.Add(ArcPt(deg));
        list.Add(new Vector3(-50f, 0f, 10f)); // 青ベース開口
        return list.ToArray();
    }

    private const float MinionHeight = 1.6f;

    /// <summary>
    /// Enigma/Toon のミニオン/中立用フラットカラーマテリアルを GetOrCreate する。
    /// テクスチャ無しの Quaternius モデルをチーム色で塗るため _BaseColor/_ShadeColor/_RampSmoothing を設定。
    /// </summary>
    private static Material GetOrCreateToonUnitMat(string name, Color baseColor)
    {
        var path     = $"{MatDir}/{name}.mat";
        var shadeCol = new Color(0.58f, 0.62f, 0.80f);

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.SetColor("_BaseColor", baseColor);
            existing.SetColor("_ShadeColor", shadeCol);
            existing.SetFloat("_RampSmoothing", 0.18f);
            return existing;
        }

        var shader = Shader.Find("Enigma/Toon") ?? Shader.Find("Universal Render Pipeline/Lit");
        var mat    = new Material(shader);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_ShadeColor", shadeCol);
        mat.SetFloat("_RampSmoothing", 0.18f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static MinionAI CreateMinionPrefab()
    {
        // 既存プレハブの再利用は旧構造・一時マテリアル参照を残すため毎回作り直す
        var prefabPath = PrefabDir + "/Minion.prefab";
        AssetDatabase.DeleteAsset(prefabPath);

        // ルート GO（ゲームロジック保持側）。見た目は子 "Visual"(Skeleton.fbx) に分離する
        var go = new GameObject("Minion");

        // 見た目より大きめのコライダーでクリック判定を取りやすくする（旧カプセル踏襲）
        var minionCap = go.AddComponent<CapsuleCollider>();
        minionCap.radius = 0.9f;
        minionCap.height = 2.4f;
        minionCap.center = new Vector3(0f, 0.8f, 0f);

        // 見た目: Skeleton.fbx を子 "Visual" としてインスタンス化
        var skeletonModel = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/External/Units/Skeleton.fbx");
        var matMinionRed  = GetOrCreateToonUnitMat("MinionRed",  new Color(0.95f, 0.50f, 0.45f));
        var matMinionBlue = GetOrCreateToonUnitMat("MinionBlue", new Color(0.55f, 0.66f, 0.95f));

        if (skeletonModel != null)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(skeletonModel);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // FBX 由来のコライダーは除去（ルートのクリック用コライダーに一本化）
            foreach (var c in visual.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            // 高さ正規化: bounds 計測 → 相対乗算（FBX ルートの単位変換スケールを壊さない）
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float measuredH = b.size.y;
                if (measuredH > 0.0001f)
                    visual.transform.localScale =
                        visual.transform.localScale * (MinionHeight / measuredH);

                Physics.SyncTransforms();

                // 接地補正: スケール後に再計測して最下端を y=0 に合わせる
                var b2 = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b2.Encapsulate(rends[i].bounds);
                float footOffset = b2.min.y - go.transform.position.y;
                visual.transform.localPosition -= new Vector3(0f, footOffset, 0f);
            }

            // プレハブ既定は Red（敵）。Blue は MinionAI.Initialize で全 Renderer を差し替える
            foreach (var r in rends) r.sharedMaterial = matMinionRed;

            // Animator を1つだけ残す（重複は除去）。無ければ Visual ルートに付与
            var animators = visual.GetComponentsInChildren<Animator>(true);
            Animator animator = animators.Length > 0 ? animators[0] : null;
            for (int i = 1; i < animators.Length; i++) Object.DestroyImmediate(animators[i]);
            if (animator == null) animator = visual.AddComponent<Animator>();

            // 歩行アニメ: FBX サブアセットから "Walk"（無ければ先頭）を選び AutoPlayClip に結線
            var clips = AssetDatabase
                .LoadAllAssetsAtPath("Assets/External/Units/Skeleton.fbx")
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview__"))
                .ToArray();

            var apc   = visual.AddComponent<AutoPlayClip>();
            var soApc = new SerializedObject(apc);
            soApc.FindProperty("_clipNameContains").stringValue = "Walk";
            var clipArr = soApc.FindProperty("_clips");
            clipArr.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                clipArr.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            soApc.ApplyModifiedPropertiesWithoutUndo();
        }

        go.AddComponent<HealthComponent>();
        go.AddComponent<TeamTag>();
        var ai = go.AddComponent<MinionAI>();

        // 頭上 HP バー。デフォルト色は BarRed（敵チーム）。Blue のときは Initialize で BarGreen に差し替える
        var matBarRed   = GetOrCreateBarMat("BarRed",   new Color(0.92f, 0.30f, 0.25f));
        var matBarGreen = GetOrCreateBarMat("BarGreen",  new Color(0.30f, 0.85f, 0.35f));
        var wrapper     = CreateWorldHealthBar(go.transform, 1.2f, 1.6f, matBarRed, 50f);

        // HealthComponent の maxHp を 50 に設定
        var soHc = new SerializedObject(go.GetComponent<HealthComponent>());
        soHc.FindProperty("_maxHp").floatValue = 50f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        // MinionAI に FillWrapper と ally マテリアルを結線
        var soAi = new SerializedObject(ai);
        soAi.FindProperty("_barFill").objectReferenceValue    = wrapper;
        soAi.FindProperty("_allyBarMat").objectReferenceValue = matBarGreen;
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

    private static void PlaceMinionSpawners(MinionAI minionPrefab, Material matBlueUnused, Material matRedUnused)
    {
        // ミニオン専用のトゥーンチーム色（プレハブ既定=Red、Initialize で Blue を全 Visual Renderer に適用）。
        // タワー等の TeamBlue/TeamRed ではなく MinionBlue/MinionRed を使う。
        var matBlue = GetOrCreateToonUnitMat("MinionBlue", new Color(0.55f, 0.66f, 0.95f));
        var matRed  = GetOrCreateToonUnitMat("MinionRed",  new Color(0.95f, 0.50f, 0.45f));

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
        // ルートは見た目を持たない空 GO。クリック用コライダーのみ持つ
        var boss = new GameObject("NeutralBoss");
        boss.transform.position = new Vector3(0f, 0.18f, 0f); // ボスピット足場の上

        // クリック判定用コライダー（旧プリミティブの代替）。全高 4〜5m を覆う
        var bossCol    = boss.AddComponent<CapsuleCollider>();
        bossCol.center = new Vector3(0f, 2.2f, 0f);
        bossCol.radius = 1.6f;
        bossCol.height = 5f;

        // 見た目: プロシージャル浮遊クリスタルコア
        BuildBossCoreVisual(boss.transform, matBoss);

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

        // ボスの頭上 HP バー（中立ボスは BarRed、幅は大きめ 2.4）
        // ルートがピット足場(y=0.18)に下がったため、クリスタル頂部(全高≈4.2m)の上に出すよう yOffset を上げる
        var matBarRed = GetOrCreateBarMat("BarRed", new Color(0.92f, 0.30f, 0.25f));
        var wrapper   = CreateWorldHealthBar(boss.transform, 2.4f, 4.6f, matBarRed, 1000f);

        // TargetDummy を流用してボスのバー更新（リスポーンなし）
        var td = boss.AddComponent<TargetDummy>();
        var soTd = new SerializedObject(td);
        soTd.FindProperty("_barFill").objectReferenceValue = wrapper;
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

    /// <summary>
    /// エニグマ・コアの見た目（浮遊クリスタル＋3リング＋破片）を構築し、
    /// BossCoreVisual を結線する。コライダーは付けず（クリックはルート）。
    /// </summary>
    private static void BuildBossCoreVisual(Transform bossRoot, Material legacyMatBoss)
    {
        // legacyMatBoss は不要になったため未使用（参照シグネチャ維持のため受け取るだけ）
        _ = legacyMatBoss;

        // メッシュ生成（既存があれば作り直す）
        var crystalMesh = ProceduralBossMeshes.CreateBipyramid("BossCrystal", 1.1f, 3.6f, 6);
        var shardMesh   = ProceduralBossMeshes.CreateBipyramid("BossShard",   0.18f, 0.6f, 6);
        var ringMeshA   = ProceduralBossMeshes.CreateTorus("BossRingA", 2.2f, 0.10f);
        var ringMeshB   = ProceduralBossMeshes.CreateTorus("BossRingB", 2.9f, 0.10f);
        var ringMeshC   = ProceduralBossMeshes.CreateTorus("BossRingC", 3.6f, 0.10f);

        // マテリアル: 発光クリスタル(URP/Unlit, HDR風) と 暗金属リング(Enigma/Toon)
        var crystalColor = new Color(0.55f, 0.35f, 1.0f);
        var matCrystal   = GetOrCreateUnlitEmissiveMat("BossCrystal", crystalColor * 2f);
        var matRing      = GetOrCreateMat("BossRing", new Color(0.16f, 0.17f, 0.22f));

        // CoreVisual ルート
        var visualRoot = new GameObject("CoreVisual");
        visualRoot.transform.SetParent(bossRoot, false);
        visualRoot.transform.localPosition = Vector3.zero;

        // 中央クリスタル（y≈2.2）
        var crystal = CreateMeshGo("Crystal", crystalMesh, matCrystal, visualRoot.transform);
        crystal.transform.localPosition = new Vector3(0f, 2.2f, 0f);

        // リング3本（傾き euler を基準に）
        var ringA = CreateMeshGo("RingA", ringMeshA, matRing, visualRoot.transform);
        ringA.transform.localPosition    = new Vector3(0f, 2.2f, 0f);
        ringA.transform.localEulerAngles = new Vector3(90f, 0f, 0f);

        var ringB = CreateMeshGo("RingB", ringMeshB, matRing, visualRoot.transform);
        ringB.transform.localPosition    = new Vector3(0f, 2.2f, 0f);
        ringB.transform.localEulerAngles = new Vector3(60f, 0f, 20f);

        var ringC = CreateMeshGo("RingC", ringMeshC, matRing, visualRoot.transform);
        ringC.transform.localPosition    = new Vector3(0f, 2.2f, 0f);
        ringC.transform.localEulerAngles = new Vector3(110f, 0f, -15f);

        // 小クリスタル破片 ×6 を半径2.6の円周上(y 2.2)に配置
        var shardRoot = new GameObject("Shards");
        shardRoot.transform.SetParent(visualRoot.transform, false);
        shardRoot.transform.localPosition = new Vector3(0f, 2.2f, 0f);

        const int shardCount = 6;
        const float shardRadius = 2.6f;
        for (int i = 0; i < shardCount; i++)
        {
            float ang = (float)i / shardCount * Mathf.PI * 2f;
            var shard = CreateMeshGo($"Shard_{i}", shardMesh, matCrystal, shardRoot.transform);
            shard.transform.localPosition = new Vector3(
                Mathf.Cos(ang) * shardRadius, 0f, Mathf.Sin(ang) * shardRadius);
        }

        // BossCoreVisual を結線
        var coreVisual = bossRoot.gameObject.AddComponent<Enigma.Map.BossCoreVisual>();
        var soCv = new SerializedObject(coreVisual);
        soCv.FindProperty("_crystal").objectReferenceValue   = crystal.transform;
        soCv.FindProperty("_ringA").objectReferenceValue     = ringA.transform;
        soCv.FindProperty("_ringB").objectReferenceValue     = ringB.transform;
        soCv.FindProperty("_ringC").objectReferenceValue     = ringC.transform;
        soCv.FindProperty("_shardRoot").objectReferenceValue = shardRoot.transform;
        soCv.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// MeshFilter+MeshRenderer のみを持つ見た目 GO を作る（コライダーなし）。
    /// </summary>
    private static GameObject CreateMeshGo(string name, Mesh mesh, Material mat, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh         = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial   = mat;
        return go;
    }

    /// <summary>
    /// 発光風の不透明 URP/Unlit マテリアルを GetOrCreate する。
    /// _BaseColor が無いシェーダ向けに _Color へフォールバックする。
    /// </summary>
    private static Material GetOrCreateUnlitEmissiveMat(string name, Color color)
    {
        var path     = $"{MatDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            ApplyUnlitColor(existing, color);
            return existing;
        }

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat    = new Material(shader);
        ApplyUnlitColor(mat, color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void ApplyUnlitColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
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

            // パスを5セグメントの Cube で敷く（y=0.045: 川上面0.03とレーン0.06の中間）
            const int   SegCount = 5;
            float       segLen   = Vector3.Distance(p1, p2) / SegCount;
            var         fwd      = (p2 - p1).normalized;

            for (int si = 0; si < SegCount; si++)
            {
                float  t      = (si + 0.5f) / SegCount;
                var    center = Vector3.Lerp(p1, p2, t);
                center.y = 0.045f;

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
        // 親 GO（地面 y=0.8 に配置）。クリック判定は親の CapsuleCollider に一本化（不変）
        var parent = new GameObject(name);
        parent.transform.position = new Vector3(campCenter.x, 0.8f, campCenter.z);

        var cap = parent.AddComponent<CapsuleCollider>();
        cap.radius = 1.3f;
        cap.height = 2.0f;
        cap.center = Vector3.zero;

        // 見た目: Slime.fbx を子 "Visual" としてインスタンス化（高さ正規化 1.4m・接地）
        var slimeModel = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/External/Units/Slime.fbx");
        var matSlime = GetOrCreateToonUnitMat("JungleSlime", new Color(0.40f, 0.72f, 0.42f));

        if (slimeModel != null)
        {
            const float slimeHeight = 1.4f;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(slimeModel);
            visual.name = "Visual";
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // FBX 由来のコライダーは除去（親のクリック用コライダーに一本化）
            foreach (var c in visual.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            // 高さ正規化: bounds 計測 → 相対乗算（FBX ルートの単位変換スケールを壊さない）
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float measuredH = b.size.y;
                if (measuredH > 0.0001f)
                    visual.transform.localScale =
                        visual.transform.localScale * (slimeHeight / measuredH);

                Physics.SyncTransforms();

                // 接地補正: スケール後に再計測して最下端を親の足元(y=0)に合わせる
                var b2 = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b2.Encapsulate(rends[i].bounds);
                float footOffset = b2.min.y - parent.transform.position.y;
                visual.transform.localPosition -= new Vector3(0f, footOffset, 0f);
            }

            // 緑トゥーン色を全 Renderer に適用
            foreach (var r in rends) r.sharedMaterial = matSlime;

            // Animator を1つだけ残す。無ければ Visual ルートに付与
            var animators = visual.GetComponentsInChildren<Animator>(true);
            Animator animator = animators.Length > 0 ? animators[0] : null;
            for (int i = 1; i < animators.Length; i++) Object.DestroyImmediate(animators[i]);
            if (animator == null) animator = visual.AddComponent<Animator>();

            // アニメ: "Idle"（無ければ先頭）を AutoPlayClip に結線
            var clips = AssetDatabase
                .LoadAllAssetsAtPath("Assets/External/Units/Slime.fbx")
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview__"))
                .ToArray();

            var apc   = visual.AddComponent<AutoPlayClip>();
            var soApc = new SerializedObject(apc);
            soApc.FindProperty("_clipNameContains").stringValue = "Idle";
            var clipArr = soApc.FindProperty("_clips");
            clipArr.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                clipArr.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            soApc.ApplyModifiedPropertiesWithoutUndo();
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

        // 頭上 HP バー（中立モンスターは BarRed）
        var matBarRed = GetOrCreateBarMat("BarRed", new Color(0.92f, 0.30f, 0.25f));
        var wrapper   = CreateWorldHealthBar(parent.transform, 1.2f, 1.6f, matBarRed, 120f);

        // JungleMonster コンポーネント: Initialize で campCenter と barFill（FillWrapper）を渡す
        var jm = parent.AddComponent<JungleMonster>();
        jm.Initialize(campCenter, wrapper);
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
    /// 地表散布物（草タフト・小石）の配置除外判定。木/草で共通に使う。
    /// 除外: レーン帯（半径40〜50）・川（中央 |x|<8 の帯）・ベイスン（半径<18）・
    /// ジャングルパス近傍・ベース周辺（±56 付近半径12）。
    /// </summary>
    private static bool IsExcludedFromScatter(Vector3 p)
    {
        // 川（中央の縦帯）
        if (Mathf.Abs(p.x) < 8f) return true;

        // ベイスン（中央オブジェクティブ）
        float distFromCenter = Mathf.Sqrt(p.x * p.x + p.z * p.z);
        if (distFromCenter < 18f) return true;

        // レーンアーク帯（半径 40〜50）
        if (distFromCenter > 40f && distFromCenter < 50f) return true;

        // ジャングルパス近傍
        if (IsNearAnyJunglePath(p, 4.5f)) return true;

        // ベース周辺（±56, 半径12）
        if (Vector3.Distance(p, new Vector3(-56f, 0f, 0f)) < 12f) return true;
        if (Vector3.Distance(p, new Vector3( 56f, 0f, 0f)) < 12f) return true;

        return false;
    }

    /// <summary>
    /// 草むらタフト（約350個）と小石（60個）を草地に決定論的に散布する。
    /// シードは Random.InitState(20260612) で固定。すべて static フラグ・コライダー無し。
    /// 草タフトは ShadowCastingMode.Off（数が多くシャドウマップを汚すため）。
    /// </summary>
    private static void ScatterGroundVegetation()
    {
        Random.InitState(20260612);

        var grassParent = new GameObject("GroundVegetation");
        SetStatic(grassParent);

        // --- 草タフト用マテリアル: Enigma/ToonLeaf + GrassBlade.png、_Cutoff 0.45 ---
        // 交差 Quad は単面のため、両面・輪郭線なしの葉専用シェーダーを使う
        var grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/_Project/UI/Textures/GrassBlade.png");
        var matGrass = GetOrCreateMat("GrassTuft", new Color(0.55f, 0.78f, 0.40f));
        var leafShader = Shader.Find("Enigma/ToonLeaf");
        if (leafShader != null) matGrass.shader = leafShader;
        ApplyWutheringRamp(matGrass);
        if (grassTex != null) matGrass.SetTexture("_BaseMap", grassTex);
        if (matGrass.HasProperty("_Cutoff")) matGrass.SetFloat("_Cutoff", 0.45f);
        matGrass.EnableKeyword("_ALPHATEST_ON");
        matGrass.SetOverrideTag("RenderType", "TransparentCutout");
        matGrass.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(matGrass);

        // 交差 Quad タフトの共有メッシュ（1m 四方の Quad を3枚 0/60/120°で交差）
        var tuftMesh = CreateCrossedQuadMesh();

        // --- 草タフト 約350個 ---
        const int grassGoal = 350;
        int grassPlaced = 0;
        int grassAttempts = 0;
        while (grassPlaced < grassGoal && grassAttempts < grassGoal * 12)
        {
            grassAttempts++;
            // 草地: 半径 18〜68 のリング内（外周岩壁 72 の内側）に一様散布。
            float r   = 18f + Random.value * 50f;
            float ang = Random.value * 360f * Mathf.Deg2Rad;
            float gx  = r * Mathf.Cos(ang);
            float gz  = r * Mathf.Sin(ang);
            var pos   = new Vector3(gx, 0f, gz);
            if (IsExcludedFromScatter(pos)) continue;

            float size = 0.7f + Random.value * 0.4f; // 0.7〜1.1m
            float yaw  = Random.value * 360f;

            var tuft = new GameObject($"GrassTuft_{grassPlaced:D3}");
            tuft.transform.SetParent(grassParent.transform, false);
            tuft.transform.position   = pos;
            tuft.transform.localScale = new Vector3(size, size, size);
            tuft.transform.rotation   = Quaternion.Euler(0f, yaw, 0f);
            var mf = tuft.AddComponent<MeshFilter>();
            mf.sharedMesh = tuftMesh;
            var mr = tuft.AddComponent<MeshRenderer>();
            mr.sharedMaterial = matGrass;
            // 草は影を落とさない（数が多くシャドウマップを汚す）
            mr.shadowCastingMode = ShadowCastingMode.Off;
            SetStatic(tuft);
            grassPlaced++;
        }

        // --- 小石 60個（灰系2色の扁平 Sphere、コライダー無し）---
        var matPebbleA = GetOrCreateMat("PebbleA", new Color(0.55f, 0.55f, 0.57f));
        var matPebbleB = GetOrCreateMat("PebbleB", new Color(0.42f, 0.43f, 0.40f));
        ApplyWutheringRamp(matPebbleA);
        ApplyWutheringRamp(matPebbleB);

        const int pebbleGoal = 60;
        int pebblePlaced = 0;
        int pebbleAttempts = 0;
        while (pebblePlaced < pebbleGoal && pebbleAttempts < pebbleGoal * 12)
        {
            pebbleAttempts++;
            float r   = 18f + Random.value * 50f;
            float ang = Random.value * 360f * Mathf.Deg2Rad;
            float px  = r * Mathf.Cos(ang);
            float pz  = r * Mathf.Sin(ang);
            var pos   = new Vector3(px, 0f, pz);
            if (IsExcludedFromScatter(pos)) continue;

            float s = 0.25f + Random.value * 0.4f; // 0.25〜0.65m
            var pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // 当たり判定不要 → 自動付与された Collider を除去
            var col = pebble.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            pebble.name = $"Pebble_{pebblePlaced:D2}";
            pebble.transform.SetParent(grassParent.transform, false);
            // 扁平（地面に半埋まり）
            pebble.transform.position   = new Vector3(px, s * 0.18f, pz);
            pebble.transform.localScale = new Vector3(s, s * 0.5f, s);
            pebble.transform.rotation   = Quaternion.Euler(0f, Random.value * 360f, 0f);
            SetMat(pebble, (Random.value < 0.5f) ? matPebbleA : matPebbleB);
            SetStatic(pebble);
            pebblePlaced++;
        }

        Debug.Log($"[BuildAetherRiftMap] 植生散布: 草タフト {grassPlaced}個 / 小石 {pebblePlaced}個");
    }

    /// <summary>
    /// 1m 四方の Quad を Y 軸 0°/60°/120° で交差させた単一メッシュを生成する（草タフト用）。
    /// 各 Quad は原点を底辺中央とし、上方向（+Y）へ立つ。両面描画のため法線は上向き固定。
    /// </summary>
    private static Mesh CreateCrossedQuadMesh()
    {
        var mesh = new Mesh { name = "GrassCrossedQuad" };
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs   = new System.Collections.Generic.List<Vector2>();
        var tris  = new System.Collections.Generic.List<int>();

        float[] degs = { 0f, 60f, 120f };
        foreach (float deg in degs)
        {
            float rad = deg * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            float half = 0.5f;
            // 底辺は y=0、上辺は y=1。
            var bl = -dir * half;                       // 底辺左
            var br =  dir * half;                        // 底辺右
            var tl = -dir * half + Vector3.up;           // 上辺左
            var tr =  dir * half + Vector3.up;           // 上辺右
            int baseIdx = verts.Count;
            verts.Add(bl); verts.Add(br); verts.Add(tr); verts.Add(tl);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
            // 表裏両面（背面カリングでも見えるよう2組）
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 1 樹種の FBX プレハブと、そのトゥーンマテリアル群（幹 + 葉3トーン）を束ねる。
    /// マテリアル名規約は ConvertNatureMaterials が生成するものに一致させる。
    /// </summary>
    private sealed class NatureSpecies
    {
        public GameObject Prefab;
        public Material   Bark;
        public Material[] LeafTones; // 3 トーン（標準/黄緑/深緑）。フラット樹種でも 3 本。
        public bool       IsDead;    // 枯木フラグ（葉なし扱い）
        public int        Weight;    // 出現重み（合計から正規化）
    }

    /// <summary>
    /// Nature 樹種テーブルをロードする。FBX とマテリアルを Assets/External/Nature から読む。
    /// マテリアルは ConvertNatureMaterials が事前生成している前提。欠けていれば null のまま
    /// （PlaceOneNatureTree 側でフォールバック）。
    /// </summary>
    private static System.Collections.Generic.List<NatureSpecies> LoadNatureSpecies()
    {
        var list = new System.Collections.Generic.List<NatureSpecies>();

        // 出現重み: Tree 40 / Birch 25 / Pine 25 / TreeToon 10。DeadTree は専用枠。
        AddSpecies(list, "Tree_1",             "Tree",     weight: 40, isDead: false);
        AddSpecies(list, "Birch_1",            "Birch",    weight: 25, isDead: false);
        AddSpecies(list, "Pine_1",             "Pine",     weight: 25, isDead: false);
        AddSpecies(list, "TreeToonStylized01", "TreeToon", weight: 10, isDead: false);
        // DeadTree は通常の重みテーブルに含めず、確率判定で別扱い（weight 0）。
        AddSpecies(list, "DeadTree_1",         "DeadTree", weight: 0,  isDead: true);

        return list;
    }

    private static void AddSpecies(
        System.Collections.Generic.List<NatureSpecies> list,
        string fbxName, string matSpecies, int weight, bool isDead)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/External/Nature/{fbxName}.fbx");
        if (prefab == null) return; // FBX が無ければスキップ

        const string matDir = "Assets/External/Nature/Materials";
        var bark = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Nature_{matSpecies}_Bark.mat");
        var leaves = new Material[3];
        for (int i = 0; i < 3; i++)
            leaves[i] = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Nature_{matSpecies}_Leaf_{i}.mat");

        list.Add(new NatureSpecies
        {
            Prefab = prefab, Bark = bark, LeafTones = leaves,
            IsDead = isDead, Weight = weight,
        });
    }

    /// <summary>重み付き抽選で通常樹種を1つ選ぶ。allowDeadTree かつ5%判定が当たれば枯木を返す。</summary>
    private static NatureSpecies PickSpecies(
        System.Collections.Generic.List<NatureSpecies> species,
        System.Random rng, bool allowDeadTree)
    {
        if (species.Count == 0) return null;

        // ジャングル奥のみ、5%未満の確率で枯木を選ぶ。
        if (allowDeadTree && rng.NextDouble() < 0.04)
        {
            foreach (var s in species) if (s.IsDead) return s;
        }

        int totalWeight = 0;
        foreach (var s in species) totalWeight += s.Weight;
        if (totalWeight <= 0)
        {
            // 重み無し（マテリアル未変換等）→ 非枯木を等確率で。
            foreach (var s in species) if (!s.IsDead) return s;
            return species[0];
        }

        int roll = rng.Next(0, totalWeight);
        foreach (var s in species)
        {
            if (s.Weight <= 0) continue;
            roll -= s.Weight;
            if (roll < 0) return s;
        }
        return species[0];
    }

    /// <summary>
    /// 高品質 FBX の木を1本配置する。ロード後に bounds を測って目標樹高 4.5〜7m へ
    /// 正規化スケールし、モデルごとの原寸差を吸収する。葉は 3 トーンからシード固定で割当。
    /// コライダーは幹相当の CapsuleCollider。Renderer の shadowCastingMode は既定（On）。
    /// </summary>
    private static void PlaceOneTree(
        System.Collections.Generic.List<NatureSpecies> species, System.Random rng,
        float tx, float tz, int q, int index, Material matJungle, bool allowDeadTree)
    {
        float yaw = (float)(rng.NextDouble() * 360.0);
        var sp   = PickSpecies(species, rng, allowDeadTree);

        GameObject treeGo;
        if (sp != null && sp.Prefab != null)
        {
            treeGo = (GameObject)PrefabUtility.InstantiatePrefab(sp.Prefab);
            treeGo.transform.position = new Vector3(tx, 0f, tz);
            treeGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // --- 原寸差の吸収: ローカル bounds の高さを測り目標樹高へ正規化 ---
            // 目標樹高 4.5〜7m の乱数 + スタイル用に 0.9〜1.4 のランダム倍率を掛ける。
            float targetHeight = (float)(rng.NextDouble() * 2.5 + 4.5);  // 4.5〜7
            float styleMul     = (float)(rng.NextDouble() * 0.5 + 0.9);  // 0.9〜1.4
            float normScale    = ComputeNormalizedScale(treeGo, targetHeight) * styleMul;
            // FBX ルートはファイル単位変換のスケール(例: 100)を持つことがあるため、
            // 上書きでなく現在値への乗算で正規化する(計測 bounds は現在スケール込みのため)
            treeGo.transform.localScale = treeGo.transform.localScale * normScale;

            // --- マテリアル差し替え: 葉トーンをシード固定で抽選し、幹/葉を判定して割当 ---
            int leafTone = rng.Next(0, 3);
            ApplyNatureMaterials(treeGo, sp, leafTone);

            // コライダー: 幹相当の半径（正規化後の樹高に比例）。
            float capHeight = targetHeight * styleMul;
            var cap = treeGo.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, capHeight * 0.5f, 0f);
            cap.radius = Mathf.Max(0.25f, capHeight * 0.06f);
            cap.height = capHeight;
        }
        else
        {
            // フォールバック（FBX 欠落時）: 旧来の円柱ダミー。
            float treeScale = (float)(rng.NextDouble() * 2.4 + 3.8);
            treeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            treeGo.transform.position   = new Vector3(tx, 2.5f, tz);
            treeGo.transform.localScale = new Vector3(0.8f * treeScale / 4.5f, 2.5f * treeScale / 4.5f, 0.8f * treeScale / 4.5f);
            SetMat(treeGo, matJungle);
        }
        treeGo.name = $"Tree_Q{q}_{index:D2}";
        SetStatic(treeGo);
    }

    /// <summary>
    /// GameObject の全 Renderer のローカル bounds 高さから、目標樹高へ収めるための一様スケールを返す。
    /// bounds はワールド（現状 scale=1）で測れるため、目標高 / 現高 がそのまま正規化倍率になる。
    /// </summary>
    private static float ComputeNormalizedScale(GameObject go, float targetHeight)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool init = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!init) { bounds = r.bounds; init = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!init || bounds.size.y < 1e-4f) return 1f;
        return targetHeight / bounds.size.y;
    }

    /// <summary>
    /// 木の各 Renderer のサブメッシュを「幹」か「葉」か推定して Toon マテリアルを差し替える。
    /// 元マテリアル名/レンダラー名に "leaf/leaves/foliage/needle" を含むものを葉とみなす。
    /// 枯木（IsDead）は全て幹マテリアルで塗る。判定不能時は幹マテリアルを既定とする。
    /// </summary>
    private static void ApplyNatureMaterials(GameObject go, NatureSpecies sp, int leafTone)
    {
        var leafMat = (sp.LeafTones != null && sp.LeafTones[leafTone] != null)
            ? sp.LeafTones[leafTone] : null;
        var barkMat = sp.Bark;

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            var next = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                bool isLeaf = !sp.IsDead && IsLeafSlot(r, mats[i]);
                var chosen  = isLeaf ? leafMat : barkMat;
                // マテリアル未変換時は元のマテリアルを温存（null 差し替えを避ける）。
                next[i] = chosen != null ? chosen : mats[i];
            }
            r.sharedMaterials = next;
        }
    }

    /// <summary>レンダラー名 / 元マテリアル名から葉スロットかを推定する。</summary>
    private static bool IsLeafSlot(Renderer r, Material srcMat)
    {
        string name = ((srcMat != null ? srcMat.name : "") + " " + r.gameObject.name).ToLowerInvariant();
        return name.Contains("leaf") || name.Contains("leaves")
            || name.Contains("foliage") || name.Contains("needle")
            || name.Contains("canopy");
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

    /// <summary>
    /// XZ 平面上の環状帯メッシュを生成する。
    /// Cube セグメントの角はみ出しを解消するためにレーンリングに使用する。
    /// UV は不要（単色マテリアル前提）、法線は +Y 固定。
    /// </summary>
    private static Mesh CreateRingBandMesh(float innerR, float outerR, int segments)
    {
        var mesh      = new Mesh { name = "RingBand" };
        int vertCount = (segments + 1) * 2;
        var vertices  = new Vector3[vertCount];
        var normals   = new Vector3[vertCount];
        var triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * (2f * Mathf.PI / segments);
            float cos   = Mathf.Cos(angle);
            float sin   = Mathf.Sin(angle);

            vertices[i * 2]     = new Vector3(innerR * cos, 0f, innerR * sin);
            vertices[i * 2 + 1] = new Vector3(outerR * cos, 0f, outerR * sin);
            normals[i * 2]      = Vector3.up;
            normals[i * 2 + 1]  = Vector3.up;
        }

        for (int i = 0; i < segments; i++)
        {
            int b         = i * 2;
            int ti        = i * 6;
            triangles[ti]     = b;
            triangles[ti + 1] = b + 2;
            triangles[ti + 2] = b + 1;
            triangles[ti + 3] = b + 1;
            triangles[ti + 4] = b + 2;
            triangles[ti + 5] = b + 3;
        }

        mesh.vertices  = vertices;
        mesh.normals   = normals;
        mesh.triangles = triangles;
        return mesh;
    }
}
