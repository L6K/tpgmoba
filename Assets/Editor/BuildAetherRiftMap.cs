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
using Enigma.Map;

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
        // 種の混合: Tree_1 55% / Birch_1 45%（黒葉の Pine/TreeToon は除外）、
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

        // 5c. AaBeam プレハブ（エズリアル風シアンビーム）
        var aaBeamPrefab = CreateAaBeamPrefab();

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

        // 場外レスキュー: 2秒間隔で場外判定し、最寄りレーン地点へ瞬間移動
        player.AddComponent<OutOfBoundsRescue>();

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
        // AA はエズリアル風ビーム（AaBeam）を撃つ。スキル弾・タワーは従来の Projectile のまま
        soAutoAttack.FindProperty("_projectilePrefab").objectReferenceValue = aaBeamPrefab.GetComponent<Projectile>();
        soAutoAttack.FindProperty("_muzzle").objectReferenceValue           = muzzle.transform;
        soAutoAttack.ApplyModifiedPropertiesWithoutUndo();

        // A キー長押しで AA 射程リングを表示するインジケーター
        var rangeIndicator = player.AddComponent<AttackRangeIndicator>();
        var soRangeIndicator = new SerializedObject(rangeIndicator);
        soRangeIndicator.FindProperty("_autoAttack").objectReferenceValue = autoAttack;
        soRangeIndicator.ApplyModifiedPropertiesWithoutUndo();

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

        // MatchBootstrap: ピック済みキャラのスキル・ステータスを Start 時に注入する
        var bootstrap    = player.AddComponent<MatchBootstrap>();
        var soBootstrap  = new SerializedObject(bootstrap);
        soBootstrap.FindProperty("_skillCaster").objectReferenceValue      = skillCaster;
        soBootstrap.FindProperty("_health").objectReferenceValue           = healthComp;
        soBootstrap.FindProperty("_autoAttack").objectReferenceValue       = autoAttack;
        soBootstrap.FindProperty("_playerController").objectReferenceValue  = player.GetComponent<PlayerController>();
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
        // ゼフ(UnityChan)の攻撃アニメ: AttachUnityChanModel が付けた切替機を motor に結線
        var ucSwitcher = player.GetComponentInChildren<Enigma.Character.LocomotionClipSwitcher>();
        if (ucSwitcher != null)
            soMotor.FindProperty("_clipSwitcher").objectReferenceValue = ucSwitcher;
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

        // 11c. 3v3 フルボット編成（敵=Red 3体 / 味方=Blue 2体）。
        // AA はプレイヤー同様 AaBeam ビームを撃つ。各ボットへ BotChampionBootstrap が
        // ピックキャラを適用する。
        var matBarRed   = GetOrCreateBarMat("BarRed",   new Color(0.92f, 0.30f, 0.25f));
        var matBarGreen = GetOrCreateBarMat("BarGreen", new Color(0.30f, 0.85f, 0.35f));
        var aaProj      = aaBeamPrefab.GetComponent<Projectile>();
        var redRing     = new Color(0.9f, 0.15f, 0.15f, 0.5f);
        var blueRing    = new Color(0.15f, 0.35f, 0.9f, 0.5f);

        // 敵チーム（Red）3体: TOP / BOT / Jungle
        var redTop = CreateBotChampion("RedBot_Top", TeamId.Red,
            new Vector3(52f, 1.1f, -6f), BuildTopLaneWaypoints(),
            matRed, matBarRed, redRing, aaProj, telegraphPrefab);
        var redBot = CreateBotChampion("RedBot_Bot", TeamId.Red,
            new Vector3(52f, 1.1f, 6f), BuildBotLaneWaypoints(),
            matRed, matBarRed, redRing, aaProj, telegraphPrefab);
        var redJungle = CreateBotChampion("RedBot_Jungle", TeamId.Red,
            new Vector3(52f, 1.1f, 0f), BuildJungleWaypoints(),
            matRed, matBarRed, redRing, aaProj, telegraphPrefab, farmsNeutralCamps: true);

        // 味方チーム（Blue）2体: TOP / BOT。経路は各レーンの逆順（青ベース開口スタート）。
        var blueTop = CreateBotChampion("BlueBot_Top", TeamId.Blue,
            new Vector3(-52f, 1.1f, -6f), Reverse(BuildTopLaneWaypoints()),
            matBlue, matBarGreen, blueRing, aaProj, telegraphPrefab);
        var blueBot = CreateBotChampion("BlueBot_Bot", TeamId.Blue,
            new Vector3(-52f, 1.1f, 6f), Reverse(BuildBotLaneWaypoints()),
            matBlue, matBarGreen, blueRing, aaProj, telegraphPrefab);

        // BotChampionBootstrap（シーンに1個）: CharacterDatabase と5体を結線する
        WireBotBootstrap(new[] { redTop, redBot, redJungle, blueTop, blueBot });

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

    // 衝突チューブの半径帯・高さ（視覚壁とは独立した「物理的な真の境界」）
    private const float TubeLaneInnerR = 50.0f;
    private const float TubeLaneOuterR = 51.8f;
    private const float TubeHeight     = 2.0f;
    private const float TubePocketInnerR = 11.4f;
    private const float TubePocketOuterR = 12.8f;
    // ポケット弧をレーン弧の壁体内部へ食い込ませる延長角（継ぎ目スリットを構造的に排除）
    private const float PocketEndExtendDeg = 5f;
    // レーン側開口を切り出す閾値: ベース円周上でマップ中心距離がこの値となる角を境界とする
    private const float PocketOpeningDistThreshold = 49.6f;

    /// <summary>
    /// 連続円弧帯メッシュ（CreateWallBandMesh）の衝突チューブを生成し "OuterBoundary" 親 GO に
    /// まとめる。同一メッシュに MeshRenderer を付けて描画も兼ねるため、壁の見た目＝当たり判定が
    /// 完全一致し、箱セグメントの継ぎ目（隙間・飛び出し）が構造的に発生しない。
    /// </summary>
    private static void CreateOuterBoundary()
    {
        var matBoundary = GetOrCreateMat("BoundaryWall", new Color(0.58f, 0.55f, 0.50f));
        ApplyWutheringRamp(matBoundary);
        // 両面三角形の平均法線でフラット気味になり暗くなりやすいため、ランプを緩める
        if (matBoundary.HasProperty("_RampSmoothing")) matBoundary.SetFloat("_RampSmoothing", 0.25f);

        var parent = new GameObject("OuterBoundary");
        SetStatic(parent);

        // ============================================================
        // 衝突チューブ（MeshCollider + MeshRenderer、見た目＝当たり判定一致、static）
        //    名前に "Boundary" を含め VerifyBoundary のヒット判定に乗せる
        // ============================================================

        // --- B1. リング2弧: [50.0, 51.8]・高さ2.0・弧 12°→168° / 192°→348° ---
        // セグメント数 = 弧長 / 3.75°（角度幅 156° → 約 42 セグメント）
        const float RingStepDeg = 3.75f;
        int ringSegs = Mathf.Max(1, Mathf.RoundToInt(156f / RingStepDeg));
        PlaceWallBand(parent, "BoundaryTubeRing_North", TubeLaneInnerR, TubeLaneOuterR, TubeHeight, ringSegs, 12f, 168f, matBoundary);
        PlaceWallBand(parent, "BoundaryTubeRing_South", TubeLaneInnerR, TubeLaneOuterR, TubeHeight, ringSegs, 192f, 348f, matBoundary);

        // --- B2. ベースポケット2つ: [11.4, 12.8]・高さ2.0 ---
        // レーン側開口（マップ中心距離 >= 49.6）を除いた外側区間 + 両端 5° 延長。
        // ベース円（中心±56・半径12）周上で中心距離 = 49.6 となる角を余弦定理で逆算:
        //   d^2 = 56^2 + 12^2 + 2*56*12*cos(φ)  （φ は base-local 角、+x 基準）
        //   Blue: 中心は -56、レーン側(=+x, 原点方向)は φ≈0 → 開口は |φ| < φ0
        //   Red : 中心は +56、レーン側(=-x)は φ≈180 → 開口は |φ-180| < φ0
        float cosPhi0 = (3136f + 144f - PocketOpeningDistThreshold * PocketOpeningDistThreshold) / 1344f;
        cosPhi0 = Mathf.Clamp(cosPhi0, -1f, 1f);
        float phi0Deg = Mathf.Acos(cosPhi0) * Mathf.Rad2Deg;  // ≈ 52.41°（開口の半角）

        // Blue: 壁弧 = [phi0, 360 - phi0]、両端 5° 延長
        float blueStart = phi0Deg - PocketEndExtendDeg;
        float blueEnd   = 360f - phi0Deg + PocketEndExtendDeg;
        // Red: 開口が 180° 中心 → 壁弧 = [180 + phi0, 180 - phi0 + 360]、両端 5° 延長
        float redStart  = 180f + phi0Deg - PocketEndExtendDeg;
        float redEnd    = 180f - phi0Deg + 360f + PocketEndExtendDeg;

        // ポケット弧のセグメント数（弧長を 3.75° 相当で割る）
        int pocketSegs = Mathf.Max(1, Mathf.RoundToInt((blueEnd - blueStart) / RingStepDeg));

        // 描画弧は 5° 延長前の範囲（衝突は延長維持=すり抜け防止、描画は延長なし=リング壁内側への出っ張り解消）
        float blueVisualStart = phi0Deg;
        float blueVisualEnd   = 360f - phi0Deg;
        float redVisualStart  = 180f + phi0Deg;
        float redVisualEnd    = 180f - phi0Deg + 360f;

        PlaceWallBandAt(parent, "BoundaryTubePocket_Blue", new Vector3(-56f, 0f, 0f),
            TubePocketInnerR, TubePocketOuterR, TubeHeight, pocketSegs, blueStart, blueEnd, matBoundary,
            blueVisualStart, blueVisualEnd);
        PlaceWallBandAt(parent, "BoundaryTubePocket_Red", new Vector3(56f, 0f, 0f),
            TubePocketInnerR, TubePocketOuterR, TubeHeight, pocketSegs, redStart, redEnd, matBoundary,
            redVisualStart, redVisualEnd);
    }

    /// <summary>
    /// 衝突チューブ片を原点中心で生成し OuterBoundary 親に追加する（MeshCollider + MeshRenderer）。
    /// </summary>
    private static void PlaceWallBand(GameObject parent, string name,
        float innerR, float outerR, float height, int segments, float startDeg, float endDeg, Material mat)
    {
        PlaceWallBandAt(parent, name, Vector3.zero, innerR, outerR, height, segments, startDeg, endDeg, mat);
    }

    /// <summary>
    /// 衝突チューブ片を指定中心に生成する。
    /// 衝突は両面三角形メッシュ(共有頂点)、描画は面ごとに頂点を分離した片面メッシュを子に持つ。
    /// 両面メッシュは RecalculateNormals が平均化でゼロ化し真っ黒に描画されるため、描画用を分離する。
    /// </summary>
    // visualStartDeg/visualEndDeg を NaN にすると衝突弧（startDeg/endDeg）と同じ範囲で描画する。
    // ベースポケット壁のみ、すり抜け防止の 5° 延長を衝突には残しつつ描画弧だけ延長前範囲を渡すことで
    // リング壁内側への出っ張りを解消する。
    private static void PlaceWallBandAt(GameObject parent, string name, Vector3 center,
        float innerR, float outerR, float height, int segments, float startDeg, float endDeg, Material mat,
        float visualStartDeg = float.NaN, float visualEndDeg = float.NaN)
    {
        if (float.IsNaN(visualStartDeg)) visualStartDeg = startDeg;
        if (float.IsNaN(visualEndDeg))   visualEndDeg   = endDeg;

        var go = new GameObject(name);
        go.transform.position = center;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateWallBandMesh(innerR, outerR, height, segments, startDeg, endDeg);
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        SetStatic(go);
        go.transform.SetParent(parent.transform, true);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var vmf = visual.AddComponent<MeshFilter>();
        vmf.sharedMesh = CreateWallBandRenderMesh(innerR, outerR, height, segments, visualStartDeg, visualEndDeg);
        var vmr = visual.AddComponent<MeshRenderer>();
        vmr.sharedMaterial = mat;
        SetStatic(visual);
    }

    /// <summary>
    /// 境界壁の連続性を検証する。
    /// 中心 (0, 0.75, 0) から 0.5° 刻み 720 本の水平レイ（半径 48 起点、外向き長さ 6）を飛ばし、
    /// "Boundary" を名前に含む壁に当たらず かつ ベース開口（0°/180° ±11°）でもない角度を列挙する。
    /// 素通り角度がなければ "OK" を返す。
    /// </summary>
    public static string VerifyBoundary()
    {
        const float RayOriginR  = 48f;
        const float RayLength   = 6f;
        const float StepDeg     = 0.5f;
        const float BaseOpenHalf = 11f;
        var origin = new Vector3(0f, 0.75f, 0f);
        var gaps = new System.Text.StringBuilder();

        // エディタモードでは生成直後のコライダーが物理ワールド未反映のことがある
        Physics.SyncTransforms();

        for (int i = 0; i < 720; i++)
        {
            float angleDeg = i * StepDeg;

            // ベース開口（0° / 180° ±11°）はスキップ
            float diff0   = Mathf.Abs(Mathf.DeltaAngle(angleDeg, 0f));
            float diff180 = Mathf.Abs(Mathf.DeltaAngle(angleDeg, 180f));
            if (diff0 <= BaseOpenHalf || diff180 <= BaseOpenHalf) continue;

            float rad = angleDeg * Mathf.Deg2Rad;
            var dir   = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            var start = origin + dir * RayOriginR;

            bool hit = false;
            foreach (var h in Physics.RaycastAll(start, dir, RayLength))
            {
                if (h.collider.gameObject.name.Contains("Boundary"))
                {
                    hit = true;
                    break;
                }
            }
            if (!hit)
            {
                if (gaps.Length > 0) gaps.Append(", ");
                gaps.Append(angleDeg.ToString("F1") + "°");
            }
        }

        return gaps.Length == 0 ? "OK" : "GAP at: " + gaps;
    }

    /// <summary>
    /// 場外脱出が不可能であることを検証する（エディタ用）。
    /// VerifyBoundary（放射レイ）に加え、開口端付近で接線スリットを検査する:
    /// 開口端の各角度で半径 50.9 の点から接線方向（両回り）に長さ 4 のレイを飛ばし、
    /// "Boundary" 非ヒットで素通りする角度を列挙する。全て塞がっていれば "OK"。
    /// </summary>
    public static string VerifyEscapeProof()
    {
        // 生成直後のコライダーを物理ワールドへ反映
        Physics.SyncTransforms();

        var radial = VerifyBoundary();
        var slits  = new System.Text.StringBuilder();

        // 開口端付近の検査帯（度）: 各開口の両肩を 0.5° 刻みで走査
        var ranges = new (float from, float to)[]
        {
            (9f, 15f), (165f, 171f), (189f, 195f), (345f, 351f),
        };

        const float ProbeR    = 50.9f;
        const float TangentLen = 4f;

        foreach (var (from, to) in ranges)
        {
            for (float a = from; a <= to + 1e-4f; a += 0.5f)
            {
                float rad = a * Mathf.Deg2Rad;
                var pt = new Vector3(ProbeR * Mathf.Cos(rad), 1.0f, ProbeR * Mathf.Sin(rad));
                // 接線方向（半径方向に直交）。両回りを走査する
                var tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));

                foreach (var dir in new[] { tangent, -tangent })
                {
                    bool hit = false;
                    foreach (var h in Physics.RaycastAll(pt, dir, TangentLen))
                    {
                        if (h.collider.gameObject.name.Contains("Boundary")) { hit = true; break; }
                    }
                    if (!hit)
                    {
                        if (slits.Length > 0) slits.Append(", ");
                        slits.Append(a.ToString("F1") + "°" + (dir == tangent ? "+" : "-"));
                    }
                }
            }
        }

        if (radial == "OK" && slits.Length == 0) return "OK";

        var sb = new System.Text.StringBuilder();
        if (radial != "OK") sb.Append("RADIAL ").Append(radial);
        if (slits.Length > 0)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("TANGENT SLIT at: ").Append(slits);
        }
        return sb.ToString();
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
    /// <summary>
    /// エズリアル風シアンビームの飛翔体プレハブを生成する。
    /// ルート: 空 GO + SphereCollider(trigger) + キネマティック RB + Projectile。
    /// 見た目子 "Beam": Cylinder を +Z 向きに倒して細長くしたシアン発光風メッシュ。
    /// ルートに TrailRenderer で尾を引かせる。発射側が LookRotation で +Z を進行方向へ向ける前提。
    /// </summary>
    private static GameObject CreateAaBeamPrefab()
    {
        var beamPrefabPath = PrefabDir + "/AaBeam.prefab";
        AssetDatabase.DeleteAsset(beamPrefabPath);

        // ルート（空 GO + コリジョン + Projectile）
        var root = new GameObject("AaBeam");
        var col = root.AddComponent<SphereCollider>();
        col.radius    = 0.25f;
        col.isTrigger = true;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
        root.AddComponent<Projectile>();

        // シアン発光風マテリアル（URP/Unlit、_BaseColor をシアン×2 で明るく）
        var beamColor = new Color(0.4f, 0.9f, 1.0f);
        var beamMat   = GetOrCreateMat("AaBeamCyan", beamColor * 2f);
        // GetOrCreateMat は Enigma/Toon を優先するため、ビームは無条件で URP/Unlit へ上書きして発光風にする
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit != null) beamMat.shader = unlit;
        beamMat.SetColor("_BaseColor", beamColor * 2f);

        // 見た目子 "Beam": Cylinder(Y軸向き高さ2)を回転90°で +Z 向きに倒し、細長くする
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Beam";
        var beamCol = beam.GetComponent<Collider>();
        if (beamCol != null) Object.DestroyImmediate(beamCol);
        beam.transform.SetParent(root.transform, false);
        beam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        // 直径0.16(=半径0.08)・長さ0.9(=高さ2×0.45)
        beam.transform.localScale = new Vector3(0.08f, 0.45f, 0.08f);
        SetMat(beam, beamMat);

        // ルートにトレイル（同系シアン、幅 0.12→0、time 0.25）。alpha 減衰で尾を消す
        var trail = root.AddComponent<TrailRenderer>();
        trail.time       = 0.25f;
        trail.startWidth = 0.12f;
        trail.endWidth   = 0f;
        trail.numCapVertices = 2;
        trail.material   = GetOrCreateTransparentMat("AaBeamTrail", new Color(0.4f, 0.9f, 1.0f, 0.8f));
        var trailStart = beamColor; trailStart.a = 0.8f;
        var trailEnd   = beamColor; trailEnd.a   = 0f;
        trail.startColor = trailStart;
        trail.endColor   = trailEnd;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, beamPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

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

        // アニメ切替機: Idle=WAIT00 / 走り=RUN00_F / 攻撃=HANDUP00_R(詠唱風)。
        // プレイヤー・敵チャンピオン双方の UnityChan モデルへ付与する（敵分岐でも歩行/攻撃モーションが必要）。
        // 切替機は Start で runtimeAnimatorController を切り離して Playables 再生に統一する
        var switcher = model.AddComponent<Enigma.Character.LocomotionClipSwitcher>();
        var soSw = new SerializedObject(switcher);
        soSw.FindProperty("_idle").objectReferenceValue   = LoadFirstClip("Assets/UnityChan/Animations/unitychan_WAIT00.fbx");
        soSw.FindProperty("_walk").objectReferenceValue   = LoadFirstClip("Assets/UnityChan/Animations/unitychan_RUN00_F.fbx");
        soSw.FindProperty("_attack").objectReferenceValue = LoadFirstClip("Assets/UnityChan/Animations/unitychan_HANDUP00_R.fbx");
        // _controller はホスト（player 引数の GameObject）の CharacterController。velocity で歩行判定する
        soSw.FindProperty("_controller").objectReferenceValue = player.GetComponent<CharacterController>();
        // 攻撃は上半身レイヤーのみへ適用し、下半身は移動アニメ（Idle/Walk）を継続させる。
        // ユニティちゃんはヒューマノイドなので上半身マスクが効く（Generic リグは結線しない）。
        soSw.FindProperty("_attackMask").objectReferenceValue = GetOrCreateUpperBodyMask();
        soSw.ApplyModifiedPropertiesWithoutUndo();

        // プレイヤー分岐のみ: マズルを右手ボーンへ付け替えてビーム発射点を手元に寄せる。
        // 見つからなければ現状維持（player 直下のまま）
        if (pc != null)
        {
            var hand = FindRightHandBone(model.transform);
            if (hand != null)
            {
                var muzzle = player.transform.Find("Muzzle");
                if (muzzle != null)
                {
                    muzzle.SetParent(hand, false);
                    muzzle.localPosition = Vector3.zero;
                }
            }
        }

        ApplyToonMaterials(model);
    }

    /// <summary>
    /// モデル階層から右手ボーンと思しき Transform を探す（大小無視）。
    /// 名前に "RightHand" を含むものを優先し、無ければ "Hand.R"/"HandR"/"Hand_R" 系を探す。
    /// </summary>
    private static Transform FindRightHandBone(Transform root)
    {
        Transform fallback = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("righthand")) return t;
            if (fallback == null &&
                (n.Contains("hand.r") || n.Contains("handr") || n.Contains("hand_r")))
                fallback = t;
        }
        return fallback;
    }

    /// <summary>FBX サブアセットから最初の AnimationClip(__preview__ 除外)を返す。</summary>
    private static AnimationClip LoadFirstClip(string fbxPath)
    {
        foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath))
            if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        return null;
    }

    /// <summary>
    /// 攻撃レイヤー用の上半身 AvatarMask を取得（無ければ生成）する。
    /// ヒューマノイドのボディパーツを全て有効化したうえで、下半身・ルート・足IKを無効化し、
    /// 上半身（胴/腕/頭/手指）のみが攻撃アニメに置き換わるようにする。
    /// </summary>
    private const string UpperBodyMaskPath = "Assets/_Project/Animations/UpperBodyMask.asset";
    private static AvatarMask GetOrCreateUpperBodyMask()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (existing != null) return existing;

        var mask = new AvatarMask();
        // まず全ボディパーツを有効化
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
        // 下半身・ルート・足IK を無効化（移動レイヤーが担当する）
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);

        var dir = Path.GetDirectoryName(UpperBodyMaskPath);
        if (!AssetDatabase.IsValidFolder(dir))
            Directory.CreateDirectory(dir);
        AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
        AssetDatabase.SaveAssets();
        return mask;
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
    /// 見た目は子 "Visual" 以下にプロシージャル構築（LoL 風の石造多段砲塔＋頂部発光クリスタル）。
    /// 接地 y=0。頂上にチーム色大クリスタル(SlowSpin)を据え、muzzle はクリスタル位置に置く。
    /// </summary>
    private static void PlaceTower(string name, Vector3 pos, Material mat, Projectile projPrefab,
        GameObject towerModel = null, bool isBlue = true)
    {
        // towerModel(FBX)は不使用に。シグネチャ維持のため受け取るだけ
        _ = towerModel;

        // ルート GO（ゲームロジック保持側）。見た目は子 "Visual" に分離する
        var go = new GameObject(name);
        go.transform.position = pos;
        SetStatic(go);

        // 見た目: プロシージャル砲塔（FBX 不使用）
        var crystalTransform = BuildTowerVisual(go.transform, isBlue);

        // ルートのクリック用コライダー（見た目側にはコライダーを付けない）
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

        // TowerAttack: 発射起点はクリスタル位置(y≈6.1)に置き、差し替えでも結線を維持
        var ta      = go.AddComponent<TowerAttack>();
        var muzzleGo = new GameObject("Muzzle");
        muzzleGo.transform.SetParent(go.transform, false);
        muzzleGo.transform.position = crystalTransform.position;

        var soTa = new SerializedObject(ta);
        soTa.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab;
        soTa.FindProperty("_muzzle").objectReferenceValue           = muzzleGo.transform;
        soTa.ApplyModifiedPropertiesWithoutUndo();

        // 頭上 HP バー（クリスタル新位置 y6.1 の上に出すよう yOffset 7.6）。味方=緑/敵=赤 の規約に合わせる
        var matBar = isBlue
            ? GetOrCreateBarMat("BarGreen", new Color(0.30f, 0.85f, 0.35f))
            : GetOrCreateBarMat("BarRed",   new Color(0.92f, 0.30f, 0.25f));
        CreateWorldHealthBar(go.transform, 1.4f, 7.6f, matBar, 500f);

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

        // 死亡演出: 沈下（倒壊後は沈んだまま）。見た目は Visual 子
        AddDeathPresenter(go, mode: 1, destroyWhenDone: false, visualRoot: go.transform.Find("Visual"));
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
    /// LoL 風の石造多段砲塔をプロシージャル構築する（FBX 不使用）。全高 ≈ 6.5m。
    /// 子 "Visual" 以下に台座/基部/柱身/トリム/ヘッド/狭間/爪/大クリスタルを組む。
    /// プリミティブのコライダーは全除去（当たり判定はルートのカプセルに一本化）。
    /// 返り値は頂部の大クリスタル Transform（muzzle/HP バー基準に使う）。
    /// </summary>
    private static Transform BuildTowerVisual(Transform parent, bool isBlue)
    {
        var visual = new GameObject("Visual");
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = Vector3.zero;
        SetStatic(visual);

        var stone = GetOrCreateTowerStoneMat();
        var trim  = GetOrCreateTowerTrimMat(isBlue);

        // 石造の積層（Cylinder メッシュは高2なので scaleY=h/2、半径は r*2）
        AddTowerCylinder(visual.transform, "Plinth",  1.9f, 0.5f, 0.25f, stone); // 台座
        AddTowerCylinder(visual.transform, "Base",    1.5f, 1.4f, 1.2f,  stone); // 基部
        AddTowerCylinder(visual.transform, "Shaft",   1.1f, 2.4f, 3.0f,  stone); // 柱身

        // 柱身トリム（チーム色メタル）。専用トーラスメッシュを1度だけ生成
        var trimMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/TowerTrim.asset");
        if (trimMesh == null)
            trimMesh = ProceduralBossMeshes.CreateTorus("TowerTrim", 1.25f, 0.10f);
        AddTowerTorus(visual.transform, "TrimLow", trimMesh, 1.95f, trim);
        AddTowerTorus(visual.transform, "TrimHigh", trimMesh, 4.1f, trim);

        // ヘッド
        AddTowerCylinder(visual.transform, "Head", 1.4f, 0.9f, 4.65f, stone);

        // 狭間（クラウン）: 小箱 ×4 を半径1.25の円周上(y 5.25)
        var crown = new GameObject("Crown");
        crown.transform.SetParent(visual.transform, false);
        crown.transform.localPosition = new Vector3(0f, 5.25f, 0f);
        SetStatic(crown);
        for (int i = 0; i < 4; i++)
        {
            float ang = (float)i / 4 * Mathf.PI * 2f;
            var merlon = AddTowerBox(crown.transform, $"Merlon_{i}",
                new Vector3(0.5f, 0.5f, 0.35f),
                new Vector3(Mathf.Cos(ang) * 1.25f, 0f, Mathf.Sin(ang) * 1.25f), stone);
            merlon.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
        }

        // クリスタル爪（ホルダー）: 細長い箱 ×3 を半径0.5の円周上で内側に15度傾けて(y≈5.6)
        var holder = new GameObject("Holder");
        holder.transform.SetParent(visual.transform, false);
        holder.transform.localPosition = new Vector3(0f, 5.6f, 0f);
        SetStatic(holder);
        for (int i = 0; i < 3; i++)
        {
            float ang = (float)i / 3 * Mathf.PI * 2f;
            float deg = ang * Mathf.Rad2Deg;
            var claw = AddTowerBox(holder.transform, $"Claw_{i}",
                new Vector3(0.18f, 1.2f, 0.18f),
                new Vector3(Mathf.Cos(ang) * 0.5f, 0f, Mathf.Sin(ang) * 0.5f), trim);
            // 円周方向にヨーを合わせ、内側へ15度傾ける（X 軸前傾を方位へ回す）
            claw.transform.localRotation =
                Quaternion.Euler(0f, -deg, 0f) * Quaternion.Euler(15f, 0f, 0f);
        }

        // 大クリスタル（専用両錐メッシュ）。BossCrystal とは別名で1度だけ生成→使い回す。
        // 既存があればロードして共有（Delete+再生成すると先行タワーの参照が死ぬため）
        var crystalMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/TowerCrystalLarge.asset");
        if (crystalMesh == null)
            crystalMesh = ProceduralBossMeshes.CreateBipyramid("TowerCrystalLarge", 0.62f, 2.2f, 6);

        var color   = isBlue ? new Color(0.35f, 0.65f, 1.0f) : new Color(1.0f, 0.4f, 0.35f);
        var matName = isBlue ? "TowerCrystalBlue" : "TowerCrystalRed";
        var matCrystal = GetOrCreateUnlitEmissiveMat(matName, color * 2f);

        var crystal = CreateMeshGo("Crystal", crystalMesh, matCrystal, visual.transform);
        // ヘッド上端(≈5.1)に「鎮座」する位置。浮かせすぎると塔と分離して見える
        crystal.transform.localPosition = new Vector3(0f, 5.65f, 0f);
        crystal.transform.localScale    = Vector3.one;
        var spin = crystal.AddComponent<Enigma.Map.SlowSpin>();
        ConfigureSlowSpin(spin, 20f, 0.08f);

        return crystal.transform;
    }

    /// <summary>SlowSpin の回転速度/ボブ量を SerializedObject 経由で設定（フィールド名はベストエフォート）。</summary>
    private static void ConfigureSlowSpin(Enigma.Map.SlowSpin spin, float degPerSec, float bob)
    {
        var so = new SerializedObject(spin);
        var sp = so.FindProperty("_degreesPerSecond") ?? so.FindProperty("_spinSpeed")
                 ?? so.FindProperty("_speed");
        if (sp != null) sp.floatValue = degPerSec;
        var bp = so.FindProperty("_bobAmplitude") ?? so.FindProperty("_bobHeight")
                 ?? so.FindProperty("_bob");
        if (bp != null) bp.floatValue = bob;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Cylinder プリミティブを台座部材として追加。CapsuleCollider は除去。</summary>
    private static GameObject AddTowerCylinder(
        Transform parent, string name, float radius, float height, float centerY, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
        go.transform.SetParent(parent, false);
        // Cylinder メッシュは高2 → scaleY=h/2、デフォルト半径0.5 → scaleXZ=r*2
        go.transform.localScale    = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.transform.localPosition = new Vector3(0f, centerY, 0f);
        SetMat(go, mat);
        SetStatic(go);
        return go;
    }

    /// <summary>Cube プリミティブを部材として追加。BoxCollider は除去。</summary>
    private static GameObject AddTowerBox(
        Transform parent, string name, Vector3 size, Vector3 localPos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.transform.SetParent(parent, false);
        go.transform.localScale    = size;
        go.transform.localPosition = localPos;
        SetMat(go, mat);
        SetStatic(go);
        return go;
    }

    /// <summary>トーラスメッシュをチーム色トリムとして追加（コライダーなし、水平に寝かせる）。</summary>
    private static void AddTowerTorus(
        Transform parent, string name, Mesh mesh, float centerY, Material mat)
    {
        var go = CreateMeshGo(name, mesh, mat, parent);
        go.transform.localPosition = new Vector3(0f, centerY, 0f);
        SetStatic(go);
    }

    /// <summary>石材マテリアル "TowerStone" = Enigma/Toon (灰) + ApplyWutheringRamp。</summary>
    private static Material GetOrCreateTowerStoneMat()
    {
        var mat = GetOrCreateMat("TowerStone", new Color(0.62f, 0.62f, 0.66f));
        ApplyWutheringRamp(mat);
        return mat;
    }

    /// <summary>チーム色トリムマテリアル "TowerTrimBlue/Red" = Enigma/Toon。</summary>
    private static Material GetOrCreateTowerTrimMat(bool isBlue)
    {
        var name  = isBlue ? "TowerTrimBlue" : "TowerTrimRed";
        var color = isBlue ? new Color(0.35f, 0.5f, 0.85f) : new Color(0.85f, 0.4f, 0.35f);
        return GetOrCreateMat(name, color);
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

    // 実行時に倒して動かすオブジェクト用。BatchingStatic だと静的バッチ済みメッシュが
    // 実行時の Transform 変更に追従しないため ContributeGI のみ付ける(子も再帰的に)。
    private static void SetStaticContributeGiOnly(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI);
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

    // 共通の死亡演出 DeathPresenter を付与・結線する。
    // mode: 0=Topple(倒れる) / 1=Sink(沈む)。visualRoot が null なら自身を対象にする。
    private static void AddDeathPresenter(GameObject go, int mode, bool destroyWhenDone, Transform visualRoot)
    {
        var dp  = go.AddComponent<DeathPresenter>();
        var so  = new SerializedObject(dp);
        so.FindProperty("_mode").enumValueIndex          = mode;
        so.FindProperty("_destroyWhenDone").boolValue    = destroyWhenDone;
        if (visualRoot != null)
            so.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
        so.ApplyModifiedPropertiesWithoutUndo();
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

        // 死亡演出: 倒れる。リスポーン型なので破棄しない（見た目はカプセル自身）
        AddDeathPresenter(go, mode: 0, destroyWhenDone: false, visualRoot: null);
    }

    // レーナー AI チャンピオン1体を生成する（チーム一般化版）。
    // CharacterController + HealthComponent(500) + TeamTag(team) + EnemyChampionAI +
    // XpReward(100)/GoldReward(300)。UnityChan モデル・足元リング・頭上バーを結線する。
    // 泉中心・リスポーン位置は spawnPos（=自ベース側）に合わせる。戻り値は結線済み AI。
    private static EnemyChampionAI CreateBotChampion(
        string name, TeamId team, Vector3 spawnPos, Vector3[] waypoints,
        Material matBody, Material matBar, Color ringColor, Projectile projPrefab,
        GameObject telegraphPrefab, bool farmsNeutralCamps = false)
    {
        // 経路先頭にスポーン地点(=泉中心)を挿入する。後退(Backward)が index 0 まで
        // 戻ったとき開口部でなく泉の回復圏(半径10)内で止まるようにするため
        var route = new Vector3[waypoints.Length + 1];
        route[0] = new Vector3(spawnPos.x, 0f, spawnPos.z);
        System.Array.Copy(waypoints, 0, route, 1, waypoints.Length);
        waypoints = route;

        // ベースはカプセル（モデルが乗るまでの当たり/フォールバック表示）
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
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
        soTt.FindProperty("_team").enumValueIndex = (int)team;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        // 泉回復(自ベースの泉=リスポーン地点付近で毎秒回復)
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
        var wrapper = CreateWorldHealthBar(go.transform, 1.05f, 0.65f, matBar, 500f);

        // 銃口 Transform（攻撃弾の発射点）。胸高・前方
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(go.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.4f, 0.6f);

        // 識別用の半透明リング（半径1.2 の薄い円柱、コライダーなし）。チーム色で識別。
        var ringMat = GetOrCreateTransparentMat($"BotRing_{name}", ringColor);
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "TeamRing";
        ring.transform.SetParent(go.transform, false);
        ring.transform.localPosition = new Vector3(0f, -0.98f, 0f); // 足元
        ring.transform.localScale    = new Vector3(2.4f, 0.02f, 2.4f); // 直径2.4 = 半径1.2
        SetMat(ring, ringMat);
        Object.DestroyImmediate(ring.GetComponent<CapsuleCollider>());

        // UnityChan モデルを子付け（プレイヤー専用処理は内部で分岐済み）
        AttachUnityChanModel(go);

        var ai = go.AddComponent<EnemyChampionAI>();

        // AttachUnityChanModel がモデルへ付けた切替機を取得して攻撃モーション結線する
        var enemySwitcher = go.GetComponentInChildren<Enigma.Character.LocomotionClipSwitcher>();

        var soAi = new SerializedObject(ai);
        soAi.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab;
        soAi.FindProperty("_muzzle").objectReferenceValue           = muzzle.transform;
        soAi.FindProperty("_barFill").objectReferenceValue          = wrapper;
        soAi.FindProperty("_clipSwitcher").objectReferenceValue     = enemySwitcher;
        soAi.FindProperty("_respawnPos").vector3Value               = spawnPos;
        // スキル地点AoE 用テレグラフ（プレイヤー SkillCaster._telegraphPrefab と同一アセット）
        soAi.FindProperty("_telegraphPrefab").objectReferenceValue  = telegraphPrefab.GetComponent<TelegraphCircle>();
        // ジャングラーのみ中立キャンプ狩りを有効化
        soAi.FindProperty("_farmsNeutralCamps").boolValue           = farmsNeutralCamps;

        var wpProp = soAi.FindProperty("_waypoints");
        wpProp.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
            wpProp.GetArrayElementAtIndex(i).vector3Value = waypoints[i];

        soAi.ApplyModifiedPropertiesWithoutUndo();

        // 死亡演出: 倒れる。リスポーン型なので破棄しない（見た目は UnityChanModel 子）
        var champVisual = go.transform.Find("UnityChanModel");
        AddDeathPresenter(go, mode: 0, destroyWhenDone: false, visualRoot: champVisual);

        return ai;
    }

    // TOPレーン経路を赤ベース→青ベース方向（角度 20°→160°、12°刻み）で構築する。
    // ミニオンの ArcPt と同じ半径45・角度系。z>0 側（北回り）。
    private static Vector3[] BuildTopLaneWaypoints()
    {
        Vector3 ArcPt(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(45f * Mathf.Cos(r), 0f, 45f * Mathf.Sin(r));
        }

        var list = new List<Vector3>();
        // 開口点はポケット壁帯(中心±56, 半径11.4-12.8)の外かつ開口セクター内に置く。
        // (±50,±10) は壁帯内部に埋まりボットが壁をよじ登ってスタックする
        list.Add(new Vector3(45.5f, 0f, 8f)); // 赤ベース開口
        for (float deg = 20f; deg <= 160f + 0.01f; deg += 12f)
            list.Add(ArcPt(deg));
        list.Add(new Vector3(-45.5f, 0f, 8f)); // 青ベース開口
        return list.ToArray();
    }

    // BOTレーン経路を赤ベース→青ベース方向（角度 -20°→-160°、-12°刻み）で構築する。
    // TOP の z>0 ミラー。z<0 側（南回り）。開口は z=-10 側。
    private static Vector3[] BuildBotLaneWaypoints()
    {
        Vector3 ArcPt(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(45f * Mathf.Cos(r), 0f, 45f * Mathf.Sin(r));
        }

        var list = new List<Vector3>();
        // 開口点は壁帯の外かつ開口セクター内(TOP と同様の理由)
        list.Add(new Vector3(45.5f, 0f, -8f)); // 赤ベース開口（南側）
        for (float deg = -20f; deg >= -160f - 0.01f; deg -= 12f)
            list.Add(ArcPt(deg));
        list.Add(new Vector3(-45.5f, 0f, -8f)); // 青ベース開口（南側）
        return list.ToArray();
    }

    // ジャングル巡回ルート（赤サイド周回: 45°キャンプ→ベイスン東縁→315°キャンプ）。
    // 木のない歩行可能コリドーのみを通り、終端到達でピンポン反転して往復する
    // (EnemyChampionAI 側、_farmsNeutralCamps のとき)。敵陣側キャンプ(135/225°)へは
    // 行かない: 旧ルートの終端=敵ベース開口は敵タワーに焼かれるだけのうえ、
    // 経路逸脱時に森でさまよう事故が多発したため自陣周回に限定した。
    private static Vector3[] BuildJungleWaypoints()
    {
        Vector3 Polar(float deg, float radius)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(radius * Mathf.Cos(r), 0f, radius * Mathf.Sin(r));
        }

        return new[]
        {
            new Vector3(45.5f, 0f, 8f),   // 赤ベース開口（TOP側、壁帯の外）
            Polar(32f,  45f),             // レーン帯を45°方向へ
            Polar(45f,  45f),             // 45°パス外端（レーン接続点）
            Polar(45f,  30f),             // 右上キャンプ空き地
            Polar(45f,  18f),             // 45°パス内端（ベイスン縁）
            Polar(0f,   13f),             // ベイスン東縁（ボスピットr8の外・basin r16内）
            Polar(-45f, 18f),             // 315°パス内端
            Polar(-45f, 30f),             // 右下キャンプ空き地
            Polar(-45f, 45f),             // 315°パス外端
            Polar(-32f, 45f),             // レーン帯を赤ベースへ
            new Vector3(45.5f, 0f, -8f),  // 赤ベース開口（BOT側）
        };
    }

    // 経路を逆順にした新配列を返す（味方=青ベース開口スタート用）。元配列は変更しない。
    private static Vector3[] Reverse(Vector3[] src)
    {
        var dst = new Vector3[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = src[src.Length - 1 - i];
        return dst;
    }

    // BotChampionBootstrap GO をシーンに生成し、CharacterDatabase と5体の AI を結線する。
    private static void WireBotBootstrap(EnemyChampionAI[] bots)
    {
        var go = new GameObject("BotBootstrap");
        var bootstrap = go.AddComponent<Enigma.GameMode.BotChampionBootstrap>();

        var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(
            "Assets/_Project/Data/Characters/CharacterDatabase.asset");
        if (db == null)
            Debug.LogWarning("[BuildAetherRiftMap] CharacterDatabase.asset が見つからないため BotBootstrap は未結線");

        var so = new SerializedObject(bootstrap);
        so.FindProperty("_database").objectReferenceValue = db;

        var botsProp = so.FindProperty("_bots");
        botsProp.arraySize = bots.Length;
        for (int i = 0; i < bots.Length; i++)
            botsProp.GetArrayElementAtIndex(i).objectReferenceValue = bots[i];

        so.ApplyModifiedPropertiesWithoutUndo();
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

        // 実体衝突用 CharacterController（MinionAI が Move で移動。タワー・壁をすり抜けない）。
        // ピボットは足元、モデル高 1.6 に合わせて height 1.4 / radius 0.4 / center (0,0.7,0)。
        var minionCc = go.AddComponent<CharacterController>();
        minionCc.height = 1.4f;
        minionCc.radius = 0.4f;
        minionCc.center = new Vector3(0f, 0.7f, 0f);

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

        // 死亡演出: 倒れて消滅（リスポーンしない使い捨てユニット）。見た目は Visual 子
        AddDeathPresenter(go, mode: 0, destroyWhenDone: true, visualRoot: go.transform.Find("Visual"));

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

        // 死亡演出: 沈下。見た目は CoreVisual 子（BuildBossCoreVisual が生成）
        AddDeathPresenter(boss, mode: 1, destroyWhenDone: false, visualRoot: boss.transform.Find("CoreVisual"));

        // 討伐時に森の木を波及的に倒す演出
        boss.AddComponent<ForestToppleDirector>();
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

        // 死亡演出: 倒れる。リスポーン型なので破棄しない（見た目は Visual 子）
        AddDeathPresenter(parent, mode: 0, destroyWhenDone: false, visualRoot: parent.transform.Find("Visual"));
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

        // 出現重み: Tree 55 / Birch 45。DeadTree は専用枠。
        // Pine_1 / TreeToonStylized01 は葉が黒く描画されるため配置から除外（重みを Tree/Birch に再配分）。
        AddSpecies(list, "Tree_1",             "Tree",     weight: 55, isDead: false);
        AddSpecies(list, "Birch_1",            "Birch",    weight: 45, isDead: false);
        // DeadTree は通常の重みテーブルに含めず、確率判定で別扱い（weight 0、ジャングル奥のみ低確率）。
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
            // yaw は後から World-Y 回転として適用するため、ここでは無回転で配置する
            treeGo.transform.rotation = Quaternion.identity;

            // --- 横倒し FBX の直立補正 ---
            // インスタンス化直後に bounds の最長軸を調べ、Y 軸でない場合は立て直す。
            // 最長軸 Z → X 軸周りに -90° / 最長軸 X → Z 軸周りに +90°
            {
                var rawBounds = new Bounds(Vector3.zero, Vector3.zero);
                bool rawInit  = false;
                foreach (var r in treeGo.GetComponentsInChildren<Renderer>())
                {
                    if (!rawInit) { rawBounds = r.bounds; rawInit = true; }
                    else rawBounds.Encapsulate(r.bounds);
                }
                if (rawInit)
                {
                    var sz = rawBounds.size;
                    // 最長軸が Y でない (横幅 or 奥行が高さより大) → 倒れている
                    if (sz.z > sz.y && sz.z >= sz.x)
                    {
                        // 幹が Z 方向 → X 軸 -90° で起こす
                        treeGo.transform.localRotation *= Quaternion.Euler(-90f, 0f, 0f);
                        Physics.SyncTransforms();
                    }
                    else if (sz.x > sz.y && sz.x > sz.z)
                    {
                        // 幹が X 方向 → Z 軸 +90° で起こす
                        treeGo.transform.localRotation *= Quaternion.Euler(0f, 0f, 90f);
                        Physics.SyncTransforms();
                    }
                }
            }

            // --- 原寸差の吸収: ローカル bounds の高さを測り目標樹高へ正規化 ---
            // 目標樹高 4.5〜7m の乱数 + スタイル用に 0.9〜1.4 のランダム倍率を掛ける。
            float targetHeight = (float)(rng.NextDouble() * 2.5 + 4.5);  // 4.5〜7
            float styleMul     = (float)(rng.NextDouble() * 0.5 + 0.9);  // 0.9〜1.4
            float normScale    = ComputeNormalizedScale(treeGo, targetHeight) * styleMul;
            // FBX ルートはファイル単位変換のスケール(例: 100)を持つことがあるため、
            // 上書きでなく現在値への乗算で正規化する(計測 bounds は現在スケール込みのため)
            treeGo.transform.localScale = treeGo.transform.localScale * normScale;

            // --- yaw をワールド Y 軸回転として適用（直立補正・正規化の後） ---
            treeGo.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * treeGo.transform.rotation;

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
        SetStaticContributeGiOnly(treeGo);
        treeGo.AddComponent<TreeTopplePresenter>();
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

    /// <summary>
    /// XZ 平面の円弧帯 [innerR, outerR] を高さ方向 [0, height] へ押し出した「閉じたチューブ片」を生成する。
    /// 面: 内周面・外周面・上面・両端面（startDeg/endDeg を塞ぐ）。底面は地中のため省略。
    /// MeshCollider（非凸=片面判定）専用のため、内周面・外周面は両向きの三角形を張って両面化する。
    /// 角度は度・CCW、円弧は origin 中心の XZ 平面上に置く（GO 位置でベース中心へ移す）。
    /// </summary>
    private static Mesh CreateWallBandMesh(float innerR, float outerR, float height,
        int segments, float startDeg, float endDeg)
    {
        if (segments < 1) segments = 1;

        var mesh  = new Mesh { name = "WallBand" };
        int rings = segments + 1;

        // 各角度ステップで 4 頂点: 0=内下 1=内上 2=外下 3=外上
        var verts = new Vector3[rings * 4];
        for (int i = 0; i < rings; i++)
        {
            float t   = (float)i / segments;
            float deg = Mathf.Lerp(startDeg, endDeg, t);
            float rad = deg * Mathf.Deg2Rad;
            float c   = Mathf.Cos(rad);
            float s   = Mathf.Sin(rad);

            int b = i * 4;
            verts[b + 0] = new Vector3(innerR * c, 0f,     innerR * s); // 内下
            verts[b + 1] = new Vector3(innerR * c, height, innerR * s); // 内上
            verts[b + 2] = new Vector3(outerR * c, 0f,     outerR * s); // 外下
            verts[b + 3] = new Vector3(outerR * c, height, outerR * s); // 外上
        }

        var tris = new List<int>();

        // --- 内周面・外周面（両面化）---
        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 4;       // 当該ステップ
            int b1 = (i + 1) * 4; // 次ステップ

            // 内周面: 内下/内上 の 4 頂点 (b0+0,b0+1,b1+0,b1+1) を両面で張る
            AddQuadDoubleSided(tris, b0 + 0, b0 + 1, b1 + 1, b1 + 0);
            // 外周面: 外下/外上 の 4 頂点 (b0+2,b0+3,b1+2,b1+3) を両面で張る
            AddQuadDoubleSided(tris, b0 + 2, b0 + 3, b1 + 3, b1 + 2);

            // 上面: 内上/外上 を結ぶ（法線が +Y を向くよう CCW 弧に対し内→次内→次外→外の順で張る）
            AddQuad(tris, b0 + 1, b1 + 1, b1 + 3, b0 + 3);
        }

        // --- 端面（startDeg 側 i=0、endDeg 側 i=segments）---
        // 端面は内下/内上/外上/外下 の 4 頂点で塞ぐ。両端で外向きが逆になるよう巻き順を分ける。
        {
            int s = 0;            // 始端
            int e = segments * 4; // 終端
            // 始端: 弧の開始方向を外向きとして張る
            AddQuad(tris, s + 0, s + 2, s + 3, s + 1);
            // 終端: 逆巻き
            AddQuad(tris, e + 0, e + 1, e + 3, e + 2);
        }

        mesh.vertices  = verts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 描画専用の壁帯メッシュ。面ごとに頂点を分離(フラット法線)した片面構成で、
    /// 内周面=中心向き / 外周面=外向き / 上面=+Y / 端面=弧の外向き。
    /// </summary>
    private static Mesh CreateWallBandRenderMesh(float innerR, float outerR, float height,
        int segments, float startDeg, float endDeg)
    {
        if (segments < 1) segments = 1;

        var mesh  = new Mesh { name = "WallBandVisual" };
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        Vector3 P(float deg, float r, float y)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector3(r * Mathf.Cos(rad), y, r * Mathf.Sin(rad));
        }

        // 4 頂点を新規追加して 2 三角形を張る(頂点非共有=フラット法線)
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }

        for (int i = 0; i < segments; i++)
        {
            float d0 = Mathf.Lerp(startDeg, endDeg, (float)i / segments);
            float d1 = Mathf.Lerp(startDeg, endDeg, (float)(i + 1) / segments);

            // 内周面(法線=中心向き): (下0,上0,上1,下1) の逆巻き
            Face(P(d0, innerR, 0f), P(d1, innerR, 0f), P(d1, innerR, height), P(d0, innerR, height));
            // 外周面(法線=外向き)
            Face(P(d0, outerR, 0f), P(d0, outerR, height), P(d1, outerR, height), P(d1, outerR, 0f));
            // 上面(+Y)
            Face(P(d0, innerR, height), P(d1, innerR, height), P(d1, outerR, height), P(d0, outerR, height));
        }

        // 端面(始端=-接線方向、終端=+接線方向が外向き)
        Face(P(startDeg, innerR, 0f), P(startDeg, innerR, height), P(startDeg, outerR, height), P(startDeg, outerR, 0f));
        Face(P(endDeg, innerR, 0f), P(endDeg, outerR, 0f), P(endDeg, outerR, height), P(endDeg, innerR, height));

        mesh.vertices  = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 四角形 (a,b,c,d) を 2 三角形で張る（a→b→c, a→c→d）。
    private static void AddQuad(List<int> tris, int a, int b, int c, int d)
    {
        tris.Add(a); tris.Add(b); tris.Add(c);
        tris.Add(a); tris.Add(c); tris.Add(d);
    }

    // 四角形を表裏両面に張る（非凸 MeshCollider の片面判定対策）。
    private static void AddQuadDoubleSided(List<int> tris, int a, int b, int c, int d)
    {
        AddQuad(tris, a, b, c, d);
        AddQuad(tris, a, d, c, b);
    }
}
