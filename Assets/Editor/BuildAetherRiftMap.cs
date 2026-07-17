using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

public static partial class BuildAetherRiftMap
{
    private const string ScenePath   = "Assets/Scenes/AetherRift_Map.unity";
    private const string MatDir      = "Assets/_Project/Materials/Map";
    private const string PrefabDir   = "Assets/_Project/Prefabs";
    private const string SkillDir    = "Assets/_Project/Data/Skills";
    private const string ItemDir     = "Assets/_Project/Data/Items";

    // ---- マップ座標定数(現行キャンバスの正値を1箇所へ集約)----
    // 旧レイアウトの ±56 / ±68 / ±48 / 泉半径10 / レーン半径45 等は全て無効。以降のコメントは
    // 実値の代わりに下記の定数名を参照する。
    private const float FountainCenterX = 100f; // 泉/基地パッド中心 |x|(視覚リング 4.2〜5、基地パッド r6)
    private const float FountainRadius  = 5f;   // FountainRegen 半径
    private const float TitanCenterX    = 82f;  // タイタン(ネクサス)中心 |x|。影リング半径4 → レーン側端 |x|=78
    // 攻城最終WP: タイタン前で Top(+Z)/Bot(-Z) を分離し、旧 (±72.8, z=0) の一点集約を解消する。
    // x=76 はタイタン索敵(_aggroRange=8 + カプセル半径2.6 = 到達10.6m)が z=±8 でも表面7.4m<8mで
    // 届く前進位置。旧 x=72.8 のまま z=8 にすると表面9.6m>8m でウェーブがタイタンを索敵できず攻城が
    // 成立しないため、分離量 z=±8 を満たす最小の前進 x を採る。
    private const float SiegeWaypointX  = 76f;
    private const float SiegeWaypointZ  = 8f;

    public static void Execute()
    {
        // 壁レジストリ(散布プロップ除去用)をクリア。static のためドメイン内の再実行で蓄積する
        s_wallArcs.Clear();
        s_wallBoxes.Clear();

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
        // 方向インジケーターは地面に寝かせるため両面化（片面だと真上から見て裏面で消える）
        matArrow.SetFloat("_Cull", 0f);
        var matAoeCircle   = GetOrCreateTransparentMat("AoeCircle",    new Color(0.2f, 0.6f, 1f, 0.4f));
        var matStackMarker = GetOrCreateTransparentMat("StackMarker",  new Color(1f, 0.85f, 0f, 0.5f));

        // 3. ジオメトリ配置（円形マップ）
        // ---- レイアウト定数 ----
        // レーンリング帯 r56〜70(中心アーク R=63)、レーン内壁 r54〜55.5。
        // 本拠地/泉 中心(±FountainCenterX,0,0) 半径 FountainRadius、タイタン中心(±TitanCenterX,0,0)。

        // Ground: 立体化(M-A)で矩形グリッドメッシュへ移行。目形境界の内側判定は
        // OutOfBoundsLogic と同式(R=120,B=48)。外側頂点は原点からの radial 二分法で境界へスナップし、
        // 高さは MapHeightModel.Height(x,z) を頂点ごとに評価する。
        {
            var ground = new GameObject("Ground");
            ground.transform.position = new Vector3(0f, 0f, 0f);
            var groundMesh = CreateGridGroundMesh(112f, 74f, 2f, 120f, 48f);
            var gMf = ground.AddComponent<MeshFilter>();
            gMf.sharedMesh = groundMesh;
            var gMr = ground.AddComponent<MeshRenderer>();
            var gMc = ground.AddComponent<MeshCollider>();
            gMc.sharedMesh = groundMesh;
            SetStatic(ground);
            // 草原グリーン + 鳴潮風ランプ + 色むらノイズ
            matGround.SetColor("_BaseColor", new Color(0.40f, 0.58f, 0.32f));
            ApplyWutheringRamp(matGround);
            ApplyNoiseBaseMap(matGround, "GroundNoise", new Vector2(10f, 10f));
            gMr.sharedMaterial = matGround;
        }

        // 川: 縦帯 Cube (両レーンに届く長さ92)
        // 立体化(M-A)で地形自体がトレンチ(底-1.2)になったため、川の視覚帯はトレンチ底+0.03=-1.17へ。
        // レーンが川の上を「橋」として通るため、川はレーンより下に置く
        // 川は楕円境界(z半径≈76)とレーン帯(r56〜70)の内側に収めるため z=±58 まで(scale.z 84→116)。
        // 中央クレーター(縁r22, 底-2.5)の上に1枚板で被さるとコア下半身が水没して見えるため、
        // クレーター帯(|z|<22)を開けて南北2枚に分割する(見た目の板のみ、地形は変更しない)。
        PlaceCube("River_N", new Vector3(0f, -1.17f, 40f), new Vector3(18f, 0.1f, 36f), matRiver);
        PlaceCube("River_S", new Vector3(0f, -1.17f, -40f), new Vector3(18f, 0.1f, 36f), matRiver);

        // レーン色を土色に更新
        matLane.SetColor("_BaseColor", new Color(0.62f, 0.55f, 0.42f));
        // 鳴潮風: 柔らかいランプ + 青みの影、色むらノイズテクスチャ
        ApplyWutheringRamp(matLane);
        ApplyNoiseBaseMap(matLane, "LaneNoise", new Vector2(8f, 8f));

        // レーンアーク: Cube 48個を滑らかなリング帯メッシュ1枚に置換（角のはみ出し解消）
        const float R = 63f;
        {
            var laneRing = new GameObject("LaneRing");
            laneRing.transform.position = new Vector3(0f, 0.06f, 0f);
            var mf = laneRing.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateRingBandMesh(56f, 70f, 96);
            var mr2 = laneRing.AddComponent<MeshRenderer>();
            mr2.sharedMaterial = matLane;
            SetStatic(laneRing);
        }

        // 中央ベイスン（ボスの足場）: 立体化(M-A)でクレーター地形(底-2.5)が実体の足場になったため
        // ベイスン円盤(r22)は撤去。ボスピット目印(r11)のみ残し、クレーター床に追従させる。
        {
            var pit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pit.name = "BossPit";
            pit.transform.position   = new Vector3(0f, -2.46f, 0f);
            pit.transform.localScale = new Vector3(22f, 0.04f, 22f);
            UseFlatMeshCollider(pit, keepCollider: false);
            SetStatic(pit);
            SetMat(pit, matPit);
        }

        // 外周岩壁(旧 RingWall_00〜35) は削除した。境界の見た目と衝突は楕円化した
        // OuterBoundary(z-scale 0.75 で円弧帯を z 方向に潰して楕円にした連続チューブ)に
        // 一本化する。上から見ると楕円の外枠 + 円形レーン帯(LaneRing r40〜50) で「瞳」になる。

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
                        float cr    = (float)(rng.NextDouble() * 25.2 + 28.0);
                        float cang  = (float)(rng.NextDouble() * 90.0 + q * 90.0);
                        float crad  = cang * Mathf.Deg2Rad;
                        float cx    = cr * Mathf.Cos(crad);
                        float cz    = cr * Mathf.Sin(crad);
                        if (Mathf.Abs(cx) < 12.6f) continue;
                        float distFromArcC = Mathf.Abs(Mathf.Sqrt(cx * cx + cz * cz) - R);
                        if (distFromArcC < 9.8f) continue;
                        if (IsNearAnyJunglePath(new Vector3(cx, 0f, cz), 6.3f)) continue;
                        if (IsNearAnyCamp(new Vector3(cx, 0f, cz), 8.4f)) continue;
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

                        if (Mathf.Abs(tx) < 12.6f) continue;
                        float distFromArc = Mathf.Abs(Mathf.Sqrt(tx * tx + tz * tz) - R);
                        if (distFromArc < 9.8f) continue;
                        if (IsNearAnyJunglePath(new Vector3(tx, 0f, tz), 6.3f)) continue;
                        if (IsNearAnyCamp(new Vector3(tx, 0f, tz), 8.4f)) continue;

                        // 木同士の最小間隔 1.68m(旧1.2m×1.4)
                        bool tooClose = false;
                        foreach (var pp in placedPositions)
                        {
                            if (Vector2.Distance(new Vector2(tx, tz), pp) < 1.68f) { tooClose = true; break; }
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
                    float r     = (float)(rng.NextDouble() * 25.2 + 28.0);
                    float angle = (float)(rng.NextDouble() * 90.0 + q * 90.0);
                    float rad2  = angle * Mathf.Deg2Rad;
                    float tx    = r * Mathf.Cos(rad2);
                    float tz    = r * Mathf.Sin(rad2);

                    if (Mathf.Abs(tx) < 12.6f) continue;
                    float distFromArc = Mathf.Abs(Mathf.Sqrt(tx * tx + tz * tz) - R);
                    if (distFromArc < 9.8f) continue;
                    if (IsNearAnyJunglePath(new Vector3(tx, 0f, tz), 6.3f)) continue;
                    if (IsNearAnyCamp(new Vector3(tx, 0f, tz), 8.4f)) continue;

                    bool tooClose = false;
                    foreach (var pp in placedPositions)
                    {
                        if (Vector2.Distance(new Vector2(tx, tz), pp) < 1.68f) { tooClose = true; break; }
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
                    float rr    = (float)(rng.NextDouble() * 25.2 + 28.0);
                    float rang  = (float)(rng.NextDouble() * 90.0 + q * 90.0);
                    float rrad  = rang * Mathf.Deg2Rad;
                    float rx    = rr * Mathf.Cos(rrad);
                    float rz    = rr * Mathf.Sin(rrad);

                    if (Mathf.Abs(rx) < 12.6f) continue;
                    float distFromArcR = Mathf.Abs(Mathf.Sqrt(rx * rx + rz * rz) - R);
                    if (distFromArcR < 9.8f) continue;
                    if (IsNearAnyJunglePath(new Vector3(rx, 0f, rz), 6.3f)) continue;
                    if (IsNearAnyCamp(new Vector3(rx, 0f, rz), 8.4f)) continue;

                    bool tooClose = false;
                    foreach (var pp in placedPositions)
                    {
                        if (Vector2.Distance(new Vector2(rx, rz), pp) < 1.68f) { tooClose = true; break; }
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

        // ---- 回廊奥のジャングルキャンプ6箇所(通路の先にモンスター) ----
        PlaceCorridorCamps(matJunglePath);

        // ---- 森とレーンの間の壁（レーンリング内縁に沿った壁。パス/川口だけ開口）----
        PlaceJungleLaneWalls();

        // ---- 迷路ジャングル壁(スライスM-B)。キャンプ・ガンク口へは干渉しない疎な第一段 ----
        PlaceJungleMaze();
        PlaceOuterLaneWalls();

        // ---- 泉回復圏(FountainRegen 半径 FountainRadius=5)の視覚リング。タイタンと役割を見分けやすくする ----
        PlaceFountainRings();

        // ---- 地表植生の散布（草タフト・小石）----
        ScatterGroundVegetation();

        // ---- 茂みゾーン(スライスM-B)。視界ルール適用は次スライスM-Vで実施 ----
        PlaceBrushZones();

        // ---- 壁と重なった散布プロップの除去(最終ポストパス) ----
        // 散布(木/岩)は壁より先に走るため配置時の物理チェックが効かない。生成順に依存しない
        // よう、全配置が終わった後に壁コライダーと重なるプロップを削除する(ユーザー報告:
        // 迷路壁・キャンプ小部屋に木がめり込む)。
        RemovePropsOverlappingWalls();

        // リスポーンパッド: 各チームの色付き円盤は「リスポーン地点だけ」を示す小さな目印にする
        // (LoL の召喚士の祭壇)。パッドは基地プラトー天面(±FountainCenterX 中心・半径6・薄板、
        // 地面とほぼ同高)。泉回復圏(中心±FountainCenterX・半径 FountainRadius)と同一パッド上に置く。
        {
            var baseBlue = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseBlue.name = "Base_Blue";
            // 基地プラトー天面(+2.5)に追従
            baseBlue.transform.position   = new Vector3(-FountainCenterX, 2.56f, 0f);
            baseBlue.transform.localScale = new Vector3(12f, 0.12f, 12f);
            UseFlatMeshCollider(baseBlue, keepCollider: true);
            SetStatic(baseBlue);
            SetMat(baseBlue, matBlue);

            var baseRed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseRed.name = "Base_Red";
            baseRed.transform.position   = new Vector3(FountainCenterX, 2.56f, 0f);
            baseRed.transform.localScale = new Vector3(12f, 0.12f, 12f);
            UseFlatMeshCollider(baseRed, keepCollider: true);
            SetStatic(baseRed);
            SetMat(baseRed, matRed);
        }

        // タイタン=ネクサス: レーンリング帯(r56〜70)と基地プラトー(±FountainCenterX)の間 (±TitanCenterX) に配置。
        // タイタン台座 最大段 r4.5(=中心 ±TitanCenterX):
        //   基地側 台座外端 |x|=TitanCenterX+4.5=86.5 ⟷ 泉リング レーン側端 |x|=FountainCenterX-FountainRadius=95
        //     = 8.5m クリアランス(泉との必須 8m を満たす。タイタンをこれ以上基地側へ下げると割り込む)。
        //   レーン側 影リング端 |x|=TitanCenterX-4=78 が攻城広場の最終縁になる。
        var blueTitanHc = PlaceTitan("Titan_Blue", new Vector3(-TitanCenterX, 0f, 0f), matBlue);
        var redTitanHc  = PlaceTitan("Titan_Red",  new Vector3( TitanCenterX, 0f, 0f), matRed);

        // タワー: 4本のジャングルパス(45°/135°/225°/315°方向)の両脇 ±10° に対で配置する
        // (物理ゲート導入=M-G 以前のレイアウトへ復帰。ユーザー承認済み画像レイアウト)。
        // パスを挟む Outer(基地から遠い側)/Inner(基地軸に近い側)の対になり、
        // ジャングル出入りを牽制する LoL 風の「ジャングル口タワー」になる。
        // 半径は R=63 (レーンアーク中央)。チームは象限で決定: 右上=Red, 左上=Blue, 左下=Blue, 右下=Red。
        {
            var towerModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/External/Towers/DungeonTowerD.fbx");

            (string name, float theta, Material mat)[] towerDefs =
            {
                // Red パス(右上 45°)の両脇
                ("Tower_RTopOuter", 55f,  matRed),
                ("Tower_RTopInner", 35f,  matRed),
                // Blue パス(左上 135°)の両脇
                ("Tower_BTopOuter", 125f, matBlue),
                ("Tower_BTopInner", 145f, matBlue),
                // Blue パス(左下 225°)の両脇
                ("Tower_BBotOuter", 235f, matBlue),
                ("Tower_BBotInner", 215f, matBlue),
                // Red パス(右下 315°)の両脇
                ("Tower_RBotOuter", 305f, matRed),
                ("Tower_RBotInner", 325f, matRed),
            };

            foreach (var (tname, theta, tmat) in towerDefs)
            {
                float tr  = theta * Mathf.Deg2Rad;
                float tx  = R * Mathf.Cos(tr);
                float tz  = R * Mathf.Sin(tr);
                var tPos  = new Vector3(tx, 0f, tz);

                // 接地位置 y=0。チームはタワー名の B/R プレフィックスで判定
                bool isBlue = tname.StartsWith("Tower_B");
                // 外側タワー(名前に "Outer" を含む)=600で先に折られる(本陣防衛ほど固くする設計)
                float towerHp = tname.Contains("Outer") ? 600f : 800f;
                PlaceTower(tname, tPos, tmat, null, towerModel, isBlue, towerHp);

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

        // 構造物タグ: タワー8基 + タイタン2体へ StructureTag を付与(コア3回目報酬の対構造物バフ判定用)
        TagAllStructures();

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
        // 泉/スポーンは基地最奥のコンパクトな安全パッド(-100)に配置。LoL のフォーメーション:
        // 奥=泉/ショップ/復帰 → 中央=ネクサス(-82) → 前方=防衛広場 → レーン、の並び(report25)。
        var playerSpawnPos = new Vector3(-100f, 3.6f, 0f);
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

        // 泉回復(後方安全パッドの泉=半径5のコンパクト圏で毎秒回復)
        var playerFountain   = player.AddComponent<Enigma.Combat.FountainRegen>();
        var soPlayerFountain = new SerializedObject(playerFountain);
        soPlayerFountain.FindProperty("_fountainCenter").vector3Value = new Vector3(-100f, 3.6f, 0f);
        soPlayerFountain.FindProperty("_radius").floatValue           = 5f;
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

        // 10. Main Camera(URPポスプロ有効化: Bloom等のVolumeオーバーライドはカメラ側フラグが無いと一切効かない)
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var mainCam = camGo.AddComponent<Camera>();
        mainCam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
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

        // (旧: ターゲットダミー2体はフルボット編成の導入で廃止)

        // 11c. 3v3 フルボット編成（敵=Red 3体 / 味方=Blue 2体）。
        // AA はプレイヤー同様 AaBeam ビームを撃つ。各ボットへ BotChampionBootstrap が
        // ピックキャラを適用する。
        var matBarRed   = GetOrCreateBarMat("BarRed",   new Color(0.92f, 0.30f, 0.25f));
        var matBarGreen = GetOrCreateBarMat("BarGreen", new Color(0.30f, 0.85f, 0.35f));
        var aaProj      = aaBeamPrefab.GetComponent<Projectile>();
        var redRing     = new Color(0.9f, 0.15f, 0.15f, 0.5f);
        var blueRing    = new Color(0.15f, 0.35f, 0.9f, 0.5f);

        // 敵チーム（Red）3体: TOP / BOT / Jungle
        // スポーンを後方の安全パッド(±98〜±101)に密集配置し、泉(半径5, 中心±100)内に全員が収まる
        // ようにする(report25: 復帰は小さな泉の中)。前方へ出るときネクサス/タイタン(±82, z=0,
        // capsule半径~2.25)を横切ってスタックしないよう各 Bot はレーン側へ z オフセットを保つ。
        // CreateBotChampion が spawnPos を route[0] に前置するため後退時は泉圏で止まる。
        var redTop = CreateBotChampion("RedBot_Top", TeamId.Red,
            new Vector3(98f, 3.6f, 3.5f), BuildTopLaneWaypoints(),
            matRed, matBarRed, redRing, aaProj, telegraphPrefab);
        var redBot = CreateBotChampion("RedBot_Bot", TeamId.Red,
            new Vector3(98f, 3.6f, -3.5f), BuildBotLaneWaypoints(),
            matRed, matBarRed, redRing, aaProj, telegraphPrefab);
        var redJungle = CreateBotChampion("RedBot_Jungle", TeamId.Red,
            new Vector3(101f, 3.6f, 2f), BuildJungleWaypoints(),
            matRed, matBarRed, redRing, aaProj, telegraphPrefab, farmsNeutralCamps: true);

        // 味方チーム（Blue）2体: TOP / BOT。経路は各レーンの逆順（青ベース開口スタート）。
        var blueTop = CreateBotChampion("BlueBot_Top", TeamId.Blue,
            new Vector3(-98f, 3.6f, 3.5f), Reverse(BuildTopLaneWaypoints()),
            matBlue, matBarGreen, blueRing, aaProj, telegraphPrefab);
        var blueBot = CreateBotChampion("BlueBot_Bot", TeamId.Blue,
            new Vector3(-98f, 3.6f, -3.5f), Reverse(BuildBotLaneWaypoints()),
            matBlue, matBarGreen, blueRing, aaProj, telegraphPrefab);

        // BlueBot_Jungle: 3v3 バランスシム専用(通常プレイは従来どおり5体のまま=非アクティブ化)。
        // マップは180°点対称のため、赤ジャングル経路を MirrorXZ するだけで青側の経路になる。
        var blueJungle = CreateBotChampion("BlueBot_Jungle", TeamId.Blue,
            new Vector3(-101f, 3.6f, -2f), MirrorXZ(BuildJungleWaypoints()),
            matBlue, matBarGreen, blueRing, aaProj, telegraphPrefab, farmsNeutralCamps: true);
        blueJungle.gameObject.SetActive(false);

        // BotChampionBootstrap（シーンに1個）: CharacterDatabase と6体を結線する
        // （通常プレイは BlueBot_Jungle が非アクティブなため実質5体のまま）
        WireBotBootstrap(new[] { redTop, redBot, redJungle, blueTop, blueBot, blueJungle });

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

        // ヒットフィール: プレイヤーのヒットフラッシュ + 被弾フィードバック（シェイク + 赤ビネット）
        player.AddComponent<Enigma.Combat.HitFlash>();
        var hitFeedback = player.AddComponent<Enigma.Character.PlayerHitFeedback>();
        var soHitFeedback = new SerializedObject(hitFeedback);
        soHitFeedback.FindProperty("_camera").objectReferenceValue = orbitCam;
        soHitFeedback.FindProperty("_hud").objectReferenceValue    = hudCtrl;
        soHitFeedback.ApplyModifiedPropertiesWithoutUndo();

        // 死亡時の被ダメージ内訳リキャップ
        var deathRecap   = player.AddComponent<Enigma.Combat.PlayerDeathRecap>();
        var soDeathRecap = new SerializedObject(deathRecap);
        soDeathRecap.FindProperty("_health").objectReferenceValue = healthComp;
        soDeathRecap.FindProperty("_hud").objectReferenceValue    = hudCtrl;
        soDeathRecap.ApplyModifiedPropertiesWithoutUndo();

        // キルフィード司令塔（シーンに1個）。HUD への参照を結線する
        var killFeedGo = new GameObject("KillFeedDirector");
        var killFeedDirector = killFeedGo.AddComponent<Enigma.Combat.KillFeedDirector>();
        var soKillFeed = new SerializedObject(killFeedDirector);
        soKillFeed.FindProperty("_hud").objectReferenceValue = hudCtrl;
        soKillFeed.ApplyModifiedPropertiesWithoutUndo();

        // ShopController: ショップオーバーレイ制御・購入処理（catalog 結線はステップ15の後）
        var shopCtrl   = hudGo.AddComponent<ShopController>();
        var soShopCtrl = new SerializedObject(shopCtrl);
        soShopCtrl.FindProperty("_uiDocument").objectReferenceValue = hudDoc;
        soShopCtrl.FindProperty("_player").objectReferenceValue     = player.transform;
        // _shopCenter は後方安全パッド(-100, 0, 0)。泉と同じパッドに置き、ShopRadius(6)で
        // 後方のみを購入圏にする(report25: ショップをタイタン前広場に広げない)。
        soShopCtrl.FindProperty("_shopCenter").vector3Value         = new Vector3(-100f, 2.5f, 0f);
        soShopCtrl.ApplyModifiedPropertiesWithoutUndo();

        // MinimapController: ミニマップドットを毎フレーム更新する
        var minimapCtrl   = hudGo.AddComponent<MinimapController>();
        var soMinimapCtrl = new SerializedObject(minimapCtrl);
        soMinimapCtrl.FindProperty("_uiDocument").objectReferenceValue = hudDoc;
        soMinimapCtrl.ApplyModifiedPropertiesWithoutUndo();

        // 14. ミニオンプレハブ + スポーナー
        var minionPrefab = CreateMinionPrefab();
        PlaceMinionSpawners(minionPrefab, matBlue, matRed);

        // 14b. オーバータイム進行役(20分経過で構造物が毎秒1%減衰し必ず決着する)
        new GameObject("OvertimeDirector").AddComponent<Enigma.Objective.OvertimeDirector>();

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

        // 16b. クレーターの色アクセント(底ディスク+縁リング、M-C 一部)
        PlaceCraterAccents();

        // 17. シーン保存
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BuildAetherRiftMap] AetherRift_Map.unity を保存しました。");
    }

    // ---- 境界壁 ----

    // 衝突チューブの半径帯・高さ（視覚壁とは独立した「物理的な真の境界」）
    // 注記: 本ファイル内で参照箇所なし（過去設計の名残）。M-0では平面1.4倍に合わせて値のみ更新。
    private const float TubeLaneInnerR = 70.0f;
    private const float TubeLaneOuterR = 72.5f;
    private const float TubeHeight     = 2.0f;
    // LoL 風にリスポーン付近を広く拡張（内 14.4→17.4 / 外 15.8→18.4 の旧値を1.4倍）。基壇 r17 を内包する
    private const float TubePocketInnerR = 24.4f;
    private const float TubePocketOuterR = 25.8f;
    // ポケット弧をレーン弧の壁体内部へ食い込ませる延長角（継ぎ目スリットを構造的に排除）
    private const float PocketEndExtendDeg = 5f;
    // レーン側開口（ベース正面=原点方向）の半角。ポケット半径に依らず固定角で開口を切り出す。
    private const float PocketOpeningHalfDeg = 64f;

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

        // ------------------------------------------------------------
        // 境界 = 目形(アーモンド/vesica piscis)を上下2本の大円弧で構成する。
        //   上まぶた: 中心(0,0,-EyeB), 半径 EyeR、目尻(cornerDeg)〜頂点(+z,90°)〜反対目尻(180-cornerDeg)
        //   下まぶた: 中心(0,0,+EyeB), 半径 EyeR、目尻(180+cornerDeg)〜頂点(-z,270°)〜反対目尻(360-cornerDeg)
        // 2弧は z=0, x=±sqrt(EyeR²-EyeB²) で接続(=目尻)。プレイヤーは目形の内側に閉じ込められる。
        // OutOfBoundsLogic も目形2円の AND 内側で場内判定するように変更済み。
        // ------------------------------------------------------------
        const float EyeR = 120f;
        const float EyeB = 48f;
        const float EyeInnerR = EyeR;          // 内周(プレイヤー側)= 円弧そのもの
        const float EyeOuterR = EyeR + 1.8f;   // 外周(視覚壁厚)
        const float EyeStepDeg = 3.75f;

        // 目尻の角度: 中心(0,0,-EyeB)から目尻(sqrt(R²-B²),0)へ atan2(B, sqrt(R²-B²))
        float cornerDeg = Mathf.Atan2(EyeB, Mathf.Sqrt(EyeR * EyeR - EyeB * EyeB)) * Mathf.Rad2Deg;
        float cornerX   = Mathf.Sqrt(EyeR * EyeR - EyeB * EyeB);

        // 目尻の一点接触は構造的な継ぎ目になる(2026-07-13 実測: 原点→+X の低レイが素通り)ため、
        // 衝突弧だけを目尻の先へ CornerOverlapDeg 延長し、上下弧を目尻で X 字に交差させて封鎖する
        // (ベースポケット壁の PocketEndExtendDeg と同じ食い込み手法)。延長部は目形の外側
        // (もう一方の円の外)へ出るためプレイアブル領域には一切食い込まない。
        // 描画弧は従来の目尻〜目尻範囲のままにして、見た目に延長の角が生えないようにする。
        const float CornerOverlapDeg = 5f;
        float upperStart = cornerDeg        - CornerOverlapDeg;
        float upperEnd   = 180f - cornerDeg + CornerOverlapDeg;
        float lowerStart = 180f + cornerDeg - CornerOverlapDeg;
        float lowerEnd   = 360f - cornerDeg + CornerOverlapDeg;
        int upperSegs = Mathf.Max(1, Mathf.RoundToInt((upperEnd - upperStart) / EyeStepDeg));
        int lowerSegs = Mathf.Max(1, Mathf.RoundToInt((lowerEnd - lowerStart) / EyeStepDeg));

        // floorAtZero: 目尻付近(|x|>=92)は基地プラトー圏で地形追従の底が y=2.5 に浮き、
        // y=0〜2.5 の帯が実在の穴になる(2026-07-13 実測: (100,0.75,0)→+X のレイが 0 ヒット)。
        // 最終防壁は全周で底を絶対 y=0 まで下げたカーテンにする(プラトー外は地形が y=0 なので不変、
        // プラトー圏では地中に沈むだけで見た目への影響もない)。
        PlaceWallBandAt(parent, "BoundaryEye_UpperLid", new Vector3(0f, 0f, -EyeB),
            EyeInnerR, EyeOuterR, TubeHeight, upperSegs, upperStart, upperEnd, matBoundary,
            visualStartDeg: cornerDeg, visualEndDeg: 180f - cornerDeg, floorAtZero: true);
        PlaceWallBandAt(parent, "BoundaryEye_LowerLid", new Vector3(0f, 0f,  EyeB),
            EyeInnerR, EyeOuterR, TubeHeight, lowerSegs, lowerStart, lowerEnd, matBoundary,
            visualStartDeg: 180f + cornerDeg, visualEndDeg: 360f - cornerDeg, floorAtZero: true);

        // 目尻のコーナーシーム・パッチ:
        //   上まぶた弧の右端 外周 = (EyeOuterR·cos(cornerDeg), 0, -EyeB + EyeOuterR·sin(cornerDeg))
        //   下まぶた弧の右端 外周 = (EyeOuterR·cos(cornerDeg), 0, +EyeB - EyeOuterR·sin(cornerDeg))
        //   内側の目尻 = (cornerX, 0, 0)
        // この3点(と高さ方向の対応点)で楔形のパッチを張り、描画弧(目尻止まり)同士の端面の
        // 継ぎ目を視覚的に覆う。衝突面では上の CornerOverlapDeg 延長交差が主封鎖で、パッチは冗長系。
        // パッチの外側端 x = EyeOuterR·cos(cornerDeg) ≈ 111.8 で、Ground の外には出るが
        // Wall band の外周と完全に同じ x なので「壁の外に出た」ようには見えない。
        float cornerRad = cornerDeg * Mathf.Deg2Rad;
        float patchOuterX = EyeOuterR * Mathf.Cos(cornerRad);
        float patchOuterZ = EyeOuterR * Mathf.Sin(cornerRad) - EyeB; // 上まぶた弧の +z 側端
        PlaceCornerSeamPatch(parent, "BoundaryEye_CornerR", +cornerX, +patchOuterX, patchOuterZ, TubeHeight, matBoundary);
        PlaceCornerSeamPatch(parent, "BoundaryEye_CornerL", -cornerX, -patchOuterX, patchOuterZ, TubeHeight, matBoundary);

        // 目尻の y0〜2.5 帯は「プラトー地中で到達不能」と2026-07-12時点で判断していたが、
        // 実測(2026-07-13)で Ground メッシュの量子化欠けから到達可能な実在の穴と判明。
        // 現在はまぶた弧自体の floorAtZero(底 y=0 カーテン)で全周を封鎖している
        // (原点中心の扇形パッチは基地を飲み込む中実くさびになるため使わない)。
    }


    /// <summary>
    /// クレーターの色アクセント(M-C 一部)。
    /// ・CraterFloorAccent: 底ディスク(半透明の暗紫、r14、y=-2.44=床-2.5の少し上、collider無し)。
    /// ・CraterRimRing: 縁リング帯(r21.5〜22.5、y=0.06=レーンリングと同高、暗色アクセント)。
    /// </summary>
    private static void PlaceCraterAccents()
    {
        var parent = new GameObject("CraterAccents");
        SetStatic(parent);

        // 底ディスク: 半透明の暗紫（URP/Unlit 半透明・collider除去）
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "CraterFloorAccent";
            floor.transform.SetParent(parent.transform, false);
            floor.transform.position   = new Vector3(0f, -2.44f, 0f);
            floor.transform.localScale = new Vector3(14f, 0.02f, 14f);
            UseFlatMeshCollider(floor, keepCollider: false);
            SetStatic(floor);
            var fmr = floor.GetComponent<MeshRenderer>();
            fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fmr.receiveShadows    = false;
            fmr.sharedMaterial    = GetOrCreateTransparentMat("CraterFloorAccent", new Color(0.22f, 0.08f, 0.30f, 0.55f));
        }

        // 縁リング: CreateRingBandMesh パターン(r21.5〜22.5、y0.06、暗色アクセント)
        {
            var rim = new GameObject("CraterRimRing");
            rim.transform.SetParent(parent.transform, false);
            rim.transform.position = new Vector3(0f, 0.06f, 0f);
            var mf = rim.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateRingBandMesh(21.5f, 22.5f, 96);
            var mr = rim.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            mr.sharedMaterial    = GetOrCreateMat("CraterRimRing", new Color(0.16f, 0.14f, 0.20f));
            ApplyWutheringRamp(mr.sharedMaterial);
            SetStatic(rim);
        }
    }

    // 目尻パッチ。内側の目尻(cornerX,0,0) と 外側2点(outerX, ±zOuter) でできる楔を高さ方向に押し出す。
    // 6頂点(底3 + 天3)、天面1枚 + 上下側面2枚で表面を張る。外周面は Wall band の端面と接して継ぎ目を覆う。
    // 頂点は OuterBoundary(原点・オフセット無し)の子としてワールド座標そのままなので、
    // 地形追従は MapHeightModel.Height(x,z) をそのまま各点の底/天に加算すればよい
    // (コーナーは |x|≈110 でプラトー圏 +2.5 に入るため、そのぶん持ち上がる)。
    private static void PlaceCornerSeamPatch(GameObject parent, string name, float cornerX, float outerX, float zOuter, float height, Material mat)
    {
        // 右側(cornerX>0)は z>0 が上まぶた外端、z<0 が下まぶた外端。左側(cornerX<0)は対称。
        float sgn = Mathf.Sign(cornerX);
        float zUp   = +Mathf.Abs(zOuter);
        float zDown = -Mathf.Abs(zOuter);

        float groundInner = MapHeightModel.Height(cornerX, 0f);
        float groundUp    = MapHeightModel.Height(outerX,  zUp);
        float groundDown  = MapHeightModel.Height(outerX,  zDown);

        // 実測(RaycastAll)で判明: 目尻付近は基地プラトー(y=2.5)圏に入るため、底を地形高さに
        // 合わせると y=0〜2.5 の帯にコライダーが存在しない「実在の穴」になる
        // (Ground メッシュ自体もこの鋭角コーナーではグリッドセルが量子化で欠落し、
        // y=0 側を塞ぐものが何もない)。パッチの底は常に絶対 y=0 まで下げて隙間なく塞ぐ。
        const float baseY = 0f;

        var verts = new Vector3[]
        {
            new Vector3(cornerX,        baseY,                0f),     // 0 内側目尻 底
            new Vector3(outerX,         baseY,                zUp),    // 1 上まぶた外端 底
            new Vector3(outerX,         baseY,                zDown),  // 2 下まぶた外端 底
            new Vector3(cornerX,        groundInner + height, 0f),     // 3 内側目尻 天
            new Vector3(outerX,         groundUp + height,    zUp),    // 4 上まぶた外端 天
            new Vector3(outerX,         groundDown + height,  zDown),  // 5 下まぶた外端 天
        };

        // 天面: +Y を向く CCW を sgn で切り替える(右側と左側で巻き向きが反転)
        var tris = new System.Collections.Generic.List<int>();
        if (sgn > 0f)
        {
            tris.AddRange(new[] { 3, 4, 5 });             // 天面
            tris.AddRange(new[] { 0, 1, 4, 0, 4, 3 });    // 上側面 (z>0)
            tris.AddRange(new[] { 0, 3, 5, 0, 5, 2 });    // 下側面 (z<0)
        }
        else
        {
            tris.AddRange(new[] { 3, 5, 4 });
            tris.AddRange(new[] { 0, 4, 1, 0, 3, 4 });
            tris.AddRange(new[] { 0, 5, 3, 0, 2, 5 });
        }

        var mesh = new Mesh { name = name + "Mesh" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh;
        SetStatic(go);
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
    // 散布プロップ除去(RemovePropsOverlappingWalls)用の壁レジストリ。
    // OverlapBox 等の物理クエリは凹型 MeshCollider(壁バンド)との重なりを検出できないため、
    // 壁の定義を配置時に記録して数学的に判定する。Execute の冒頭でクリアする。
    private static readonly System.Collections.Generic.List<(Vector3 center, float innerR, float outerR, float startDeg, float endDeg)>
        s_wallArcs = new();
    private static readonly System.Collections.Generic.List<(Vector3 center, float halfLen, float halfThick, float yawDeg)>
        s_wallBoxes = new();

    // floorAtZero: true にすると帯の底を地形追従でなく絶対 y=0 まで下げる(壁天面は地形+height のまま)。
    // 地形が持ち上がる区間(基地プラトー等)で「壁の下の空洞」が生じない最終防壁用。
    private static GameObject PlaceWallBandAt(GameObject parent, string name, Vector3 center,
        float innerR, float outerR, float height, int segments, float startDeg, float endDeg, Material mat,
        float visualStartDeg = float.NaN, float visualEndDeg = float.NaN, bool floorAtZero = false)
    {
        if (float.IsNaN(visualStartDeg)) visualStartDeg = startDeg;
        if (float.IsNaN(visualEndDeg))   visualEndDeg   = endDeg;

        s_wallArcs.Add((center, innerR, outerR, startDeg, endDeg));

        var go = new GameObject(name);
        go.transform.position = center;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateWallBandMesh(innerR, outerR, height, segments, startDeg, endDeg, center, floorAtZero);
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        SetStatic(go);
        go.transform.SetParent(parent.transform, true);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var vmf = visual.AddComponent<MeshFilter>();
        vmf.sharedMesh = CreateWallBandRenderMesh(innerR, outerR, height, segments, visualStartDeg, visualEndDeg, center, floorAtZero);
        var vmr = visual.AddComponent<MeshRenderer>();
        vmr.sharedMaterial = mat;
        SetStatic(visual);

        return go;
    }

    // 意図された開口（現行仕様、2026-07-12 更新）。レイヤーごとに担当する開口が異なる:
    //   - JungleLaneWalls（ジャングル⇔レーン、r54〜55.5）: パス口・川口・ガンク口が開口、基地正面は封鎖済み
    //   - OuterLaneWalls（レーン⇔外周、r70.5〜72）: 基地正面±30°のみ開口、他は閉
    //   - OuterBoundary（最終防壁、目形）: 開口なし（全周閉）
    // 旧 BaseOpenHalf=11f（基地正面±11°のみ開口、単一レイヤー想定）はこの多層構成と食い違い、
    // パス口/川口/ガンク口を軒並み偽 GAP 扱いしていた。
    // レイヤーを問わず「他層に頼らずそのレイヤー単体で継ぎ目が塞がっているか」を検証するため、
    // 各レイヤーは自分が名前に含む壁だけを対象にレイキャストし、自分の意図開口だけをスキップする
    // （全層合成で判定すると、他層の壁が偶然その角度をカバーしているだけで単体レイヤーの
    // 継ぎ目破損を見逃す偽陰性が生じるため）。
    private static readonly (string label, float centerDeg, float halfDeg)[] s_jungleLaneOpenings =
    {
        ("JunglePath_045", 45f,  8f),
        ("JunglePath_135", 135f, 8f),
        ("JunglePath_225", 225f, 8f),
        ("JunglePath_315", 315f, 8f),
        ("RiverGap_090",   90f,  13f),
        ("RiverGap_270",   270f, 13f),
        ("GankGap_026",    26f,  3f),
        ("GankGap_154",    154f, 3f),
        ("GankGap_206",    206f, 3f),
        ("GankGap_334",    334f, 3f),
    };

    private static readonly (string label, float centerDeg, float halfDeg)[] s_outerLaneOpenings =
    {
        ("BaseFront_000", 0f,   30f),
        ("BaseFront_180", 180f, 30f),
    };

    private static readonly (string label, float centerDeg, float halfDeg)[] s_boundaryOpenings =
    {
        // OuterBoundary（目形の最終防壁）は開口なし（全周閉）
    };

    /// <summary>
    /// 境界壁の連続性を検証する。レイヤー（JungleLaneWalls / OuterLaneWalls / OuterBoundary）ごとに、
    /// 中心 (0, 0.75, 0) から 0.5° 刻み 720 本の水平レイ（長さ 200）を飛ばし、
    /// そのレイヤー自身の名前を含む壁に当たらず、かつそのレイヤーの意図された開口でもない
    /// 角度（＝そのレイヤー単体の継ぎ目 GAP）を列挙する。他レイヤーによる偶然の補完に
    /// 紛れないよう、レイヤーごとに独立して判定する。素通り角度がなければ "OK" を返す。
    /// </summary>
    public static string VerifyBoundary()
    {
        var sb = new System.Text.StringBuilder();

        AppendLayerGaps(sb, "JungleLaneWalls", "JungleLaneWall", s_jungleLaneOpenings);
        AppendLayerGaps(sb, "OuterLaneWalls",  "OuterLaneWall",  s_outerLaneOpenings);
        // 最終防壁は底を絶対 y=0 まで下げたカーテン設計(floorAtZero)のため、低レイ(y=0.75)単独で
        // 全周ヒットを要求する。3高度 OR だと「プラトー上に浮いた壁+壁下の実在空洞」を高レイの
        // ヒットで塞がっている扱いにする偽陰性が生じる(2026-07-13 の目尻脱出穴を見逃した実績)。
        AppendLayerGaps(sb, "OuterBoundary",   "Boundary",       s_boundaryOpenings, requireGroundLevelHit: true);

        return sb.Length == 0 ? "OK" : sb.ToString();
    }

    private static void AppendLayerGaps(System.Text.StringBuilder sb, string layerLabel,
        string nameFilter, (string label, float centerDeg, float halfDeg)[] intendedOpenings,
        bool requireGroundLevelHit = false)
    {
        const float RayLength = 200f;
        const float StepDeg   = 0.5f;
        var origin = new Vector3(0f, 0.75f, 0f);

        // エディタモードでは生成直後のコライダーが物理ワールド未反映のことがある
        Physics.SyncTransforms();

        var gaps = new System.Text.StringBuilder();

        for (int i = 0; i < 720; i++)
        {
            float angleDeg = i * StepDeg;

            // このレイヤーの意図開口はスキップ
            bool inIntendedOpening = false;
            foreach (var (_, centerDeg, halfDeg) in intendedOpenings)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(angleDeg, centerDeg)) <= halfDeg) { inIntendedOpening = true; break; }
            }
            if (inIntendedOpening) continue;

            float rad = angleDeg * Mathf.Deg2Rad;
            var dir   = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

            // 3高度方式: 平地(壁y0〜)は低レイ、ランプ帯(壁y0.75〜2.0〜)は中レイ、プラトー圏(壁y2.5〜)は高レイが捉える。
            // どちらかがヒットすれば遮蔽扱い。y0.75固定だと基地プラトーの地中(到達不能空間)を
            // 「穴」と誤報告する(2026-07-12 の扇形パッチ事故の教訓)。
            // requireGroundLevelHit(最終防壁): 壁が底 y=0 カーテン設計のため低レイ単独ヒットを要求する。
            bool hit = requireGroundLevelHit
                ? LayerHitAtHeight(origin, dir, RayLength, nameFilter)
                : LayerHitAtHeight(origin, dir, RayLength, nameFilter)
                    || LayerHitAtHeight(new Vector3(0f, 2.0f, 0f), dir, RayLength, nameFilter)
                    || LayerHitAtHeight(new Vector3(0f, 3.25f, 0f), dir, RayLength, nameFilter);
            if (!hit)
            {
                if (gaps.Length > 0) gaps.Append(", ");
                gaps.Append(angleDeg.ToString("F1") + "°");
            }
        }

        if (gaps.Length > 0)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append(layerLabel).Append(" GAP at: ").Append(gaps);
        }
    }

    private static bool LayerHitAtHeight(Vector3 origin, Vector3 dir, float rayLength, string nameFilter)
    {
        foreach (var h in Physics.RaycastAll(origin, dir, rayLength))
        {
            if (h.collider.gameObject.name.Contains(nameFilter)) return true;
        }
        return false;
    }

    /// <summary>
    /// 場外脱出が不可能であることを検証する（エディタ用）。
    /// VerifyBoundary（レイヤー別放射レイ、0.5°刻み）に加え、各レイヤーの意図開口の境界線（肩）
    /// ぴったりの角度を 0.05°刻みの高解像度で再検査する。壁弧は開口の肩で隣の壁と隙間なく
    /// 接続している設計のため、肩の内側（開口側）ではヒットせず、外側（壁側）では
    /// そのレイヤー自身の壁に必ずヒットするはずである。肩の外側 0.3° 以内でヒットしない
    /// 角度があれば、壁同士の継ぎ目に「すり抜け可能な薄いスリット」が生じていることを意味する。
    /// 全て塞がっていれば "OK"。
    /// </summary>
    public static string VerifyEscapeProof()
    {
        // 生成直後のコライダーを物理ワールドへ反映
        Physics.SyncTransforms();

        var radial = VerifyBoundary();
        var slits  = new System.Text.StringBuilder();

        AppendLayerShoulderSlits(slits, "JungleLaneWalls", "JungleLaneWall", s_jungleLaneOpenings);
        AppendLayerShoulderSlits(slits, "OuterLaneWalls",  "OuterLaneWall",  s_outerLaneOpenings);
        AppendLayerShoulderSlits(slits, "OuterBoundary",   "Boundary",       s_boundaryOpenings);

        if (radial == "OK" && slits.Length == 0) return "OK";

        var sb = new System.Text.StringBuilder();
        if (radial != "OK") sb.Append("RADIAL ").Append(radial);
        if (slits.Length > 0)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("SHOULDER SLIT at: ").Append(slits);
        }
        return sb.ToString();
    }

    // レイヤー単体の意図開口の肩（境界線）ぴったりの角度を高解像度で再検査し、
    // 肩のすぐ外側（壁があるべき側）でそのレイヤー自身の壁にヒットしない角度を slits に追記する。
    private static void AppendLayerShoulderSlits(System.Text.StringBuilder slits, string layerLabel,
        string nameFilter, (string label, float centerDeg, float halfDeg)[] intendedOpenings)
    {
        var origin = new Vector3(0f, 0.75f, 0f);
        const float RayLength = 200f;
        const float FineStepDeg = 0.05f;
        const float ShoulderBand = 0.3f; // 肩の外側だけを検査する帯幅

        foreach (var (label, centerDeg, halfDeg) in intendedOpenings)
        {
            foreach (float shoulderDeg in new[] { centerDeg - halfDeg, centerDeg + halfDeg })
            {
                // 肩の外側（壁があるべき側）へ向かう符号: 開口中心から離れる方向
                float outwardSign = Mathf.Sign(shoulderDeg - centerDeg);
                if (outwardSign == 0f) outwardSign = 1f;

                for (float d = FineStepDeg; d <= ShoulderBand + 1e-4f; d += FineStepDeg)
                {
                    float angleDeg = shoulderDeg + outwardSign * d;
                    float rad = angleDeg * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                    bool hit = LayerHitAtHeight(origin, dir, RayLength, nameFilter)
                            || LayerHitAtHeight(new Vector3(0f, 2.0f, 0f), dir, RayLength, nameFilter)
                            || LayerHitAtHeight(new Vector3(0f, 3.25f, 0f), dir, RayLength, nameFilter);
                    if (!hit)
                    {
                        if (slits.Length > 0) slits.Append(", ");
                        slits.Append(angleDeg.ToString("F2") + "°[" + layerLabel + "/" + label + " shoulder]");
                    }
                }
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
    /// <summary>
    /// エズリアル風シアンビームの飛翔体プレハブを生成する。
    /// ルート: 空 GO + SphereCollider(trigger) + キネマティック RB + Projectile。
    /// 見た目子 "Beam": Cylinder を +Z 向きに倒して細長くしたシアン発光風メッシュ。
    /// ルートに TrailRenderer で尾を引かせる。発射側が LookRotation で +Z を進行方向へ向ける前提。
    /// </summary>
    // AaBeam だけを再生成する。マップ全体を作り直さずビーム見た目を更新したいとき用（VFX 反復）。
    [MenuItem("Enigma/VFX/Rebuild AaBeam Prefab")]
    public static void RebuildAaBeamPrefab()
    {
        CreateAaBeamPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildAetherRiftMap] AaBeam.prefab を再生成しました");
    }

    // 現在のシーンへ「マップネオン」(基地床の発光リム + 中央コアのハロー)を付与する。
    // マップ全体を再生成せず的を絞って付与し、再実行時は既存 MapNeon_* を作り直す（VFX 反復・churn 最小）。
    [MenuItem("Enigma/VFX/Apply Map Neon")]
    public static void ApplyMapNeon()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("AetherRift_Map.unity"))
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // 既存の MapNeon_* を掃除（再実行で重複しないように）
        foreach (var root in scene.GetRootGameObjects())
        {
            var stale = new System.Collections.Generic.List<GameObject>();
            CollectByNamePrefix(root.transform, "MapNeon_", stale);
            foreach (var go in stale) Object.DestroyImmediate(go);
        }

        // ネオン親（整理用）
        var neonParent = new GameObject("MapNeon_Root");

        // 青/赤の床にコントラストが出る明色の半透明リム（GetOrCreateTransparentMat=描画実績ありを使用）。
        var blueHdr = new Color(0.30f, 1.00f, 1.00f, 0.90f); // 鮮シアン（青床に映える）
        var redHdr  = new Color(1.00f, 0.55f, 0.10f, 0.90f); // 橙（赤床に映える）
        // 基地床の外周に発光リム（水平アニュラス・加算・単色）。CreateRingBandMesh は UV 無しのため
        // テクスチャは使わず単色加算にする（テクスチャを使うと (0,0) サンプルで暗くなる）。
        CreateBaseNeonRim(neonParent.transform, "MapNeon_RimBlue", new Vector3(-TitanCenterX, 1.3f, 0f), blueHdr);
        CreateBaseNeonRim(neonParent.transform, "MapNeon_RimRed",  new Vector3( TitanCenterX, 1.3f, 0f), redHdr);

        // 中央コア（NeutralBoss）の発光ハロー
        var boss = FindInSceneByName(scene, "NeutralBoss");
        if (boss != null)
        {
            // 加算スフィアの発光オーラ（どの角度でも光球に見えるためビルボード不要）
            var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "MapNeon_CoreHalo";
            var hc = halo.GetComponent<Collider>(); if (hc != null) Object.DestroyImmediate(hc);
            halo.transform.SetParent(boss.transform, false);
            halo.transform.localPosition = Vector3.zero;
            halo.transform.localScale    = Vector3.one * 7f;
            var hmr = halo.GetComponent<MeshRenderer>();
            hmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hmr.receiveShadows    = false;
            hmr.sharedMaterial    = GetOrCreateTransparentMat("CoreHalo", new Color(0.70f, 0.45f, 1.0f, 0.45f));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BuildAetherRiftMap] マップネオンを付与しました（基地リム×2 + 中央コアハロー）");
    }

    // 泉回復圏(FountainRegen 半径 FountainRadius=5)を床リングで可視化する。Blue=(-FountainCenterX,0)、Red=(FountainCenterX,0)。
    // ショップ範囲(中心±FountainCenterX r6)やタイタン(±TitanCenterX)と役割を視覚的に区別できるようにする。
    private static void PlaceFountainRings()
    {
        var parent = new GameObject("FountainRings");
        SetStatic(parent);
        CreateFountainRing(parent.transform, "FountainRing_Blue", new Vector3(-FountainCenterX, 3.56f, 0f), new Color(0.35f, 0.75f, 1.00f, 0.32f));
        CreateFountainRing(parent.transform, "FountainRing_Red",  new Vector3( FountainCenterX, 3.56f, 0f), new Color(1.00f, 0.55f, 0.30f, 0.32f));
    }

    // 泉回復半径(10)の内縁に薄い半透明リングを敷く（床面のすぐ上）。
    private static void CreateFountainRing(Transform parent, string name, Vector3 center, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateRingBandMesh(4.2f, 5f, 64);
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        mr.sharedMaterial    = GetOrCreateTransparentMat(name, color);
    }

    // 基地床の外周に薄い発光リム（水平アニュラス・加算・両面不要＝上向き法線）を敷く。
    private static void CreateBaseNeonRim(Transform parent, string name, Vector3 center, Color hdrColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateRingBandMesh(15.3f, 17.2f, 96);
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        mr.sharedMaterial    = GetOrCreateTransparentMat("NeonRim_" + name, hdrColor);
    }

    private static void CollectByNamePrefix(Transform t, string prefix, System.Collections.Generic.List<GameObject> outList)
    {
        if (t.name.StartsWith(prefix)) { outList.Add(t.gameObject); return; }
        foreach (Transform c in t) CollectByNamePrefix(c, prefix, outList);
    }

    private static GameObject FindInSceneByName(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            var found = FindChildByName(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindChildByName(Transform t, string name)
    {
        foreach (Transform c in t)
        {
            if (c.name == name) return c.gameObject;
            var f = FindChildByName(c, name);
            if (f != null) return f;
        }
        return null;
    }

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

        // ネオン加算ビーム: Vfx_Beam(URP/Unlit One/One・beam_core_gradient)を共用。
        // 発射時に AutoAttack/SkillVfx が MPB で champion 別 HDR 色を per-instance 上書きするため、
        // ベース色は白(=テクスチャそのまま)に保つ。Vfx_Beam が無ければ従来のシアン発光へフォールバック。
        var beamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/VFX/Vfx_Beam.mat");
        if (beamMat == null)
        {
            var beamColor = new Color(0.4f, 0.9f, 1.0f);
            beamMat = GetOrCreateMat("AaBeamCyan", beamColor * 2f);
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null) beamMat.shader = unlit;
            beamMat.SetColor("_BaseColor", beamColor * 2f);
        }

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

        // ルートにトレイル（Vfx_Beam 共用・幅 0.18→0、time 0.35）。
        // 頂点色は白→透明にし、発射時に per-instance で Primary 着色する
        var trail = root.AddComponent<TrailRenderer>();
        trail.time       = 0.35f;
        trail.startWidth = 0.18f;
        trail.endWidth   = 0f;
        trail.numCapVertices = 2;
        trail.material   = beamMat;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;

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

        // アニメ切替機: Idle=WAIT00 / 歩行=WALK00_F / 走り=RUN00_F / 攻撃=HANDUP00_R(詠唱風)。
        // プレイヤー・敵チャンピオン双方の UnityChan モデルへ付与する（敵分岐でも歩行/攻撃モーションが必要）。
        // 切替機は Start で runtimeAnimatorController を切り離して Playables 再生に統一する
        var switcher = model.AddComponent<Enigma.Character.LocomotionClipSwitcher>();
        var soSw = new SerializedObject(switcher);
        // なめらか切替（クロスフェード）+ アイドルバリエーション用に Idle/Walk/Run/IdleVariants を結線。
        // Idle=WAIT00、歩行=WALK00_F、走り=RUN00_F、アイドルバリアント=WAIT01/02/03、攻撃=HANDUP00_R(詠唱風・単発)。
        soSw.FindProperty("_idle").objectReferenceValue   = LoadFirstClip("Assets/UnityChan/Animations/unitychan_WAIT00.fbx");
        soSw.FindProperty("_walk").objectReferenceValue   = LoadFirstClip("Assets/UnityChan/Animations/unitychan_WALK00_F.fbx");
        soSw.FindProperty("_run").objectReferenceValue    = LoadFirstClip("Assets/UnityChan/Animations/unitychan_RUN00_F.fbx");
        soSw.FindProperty("_attack").objectReferenceValue = LoadFirstClip("Assets/UnityChan/Animations/unitychan_HANDUP00_R.fbx");

        // アイドルバリアント配列（棒立ち回避）。存在するクリップのみ詰める。
        var idleVariantClips = new System.Collections.Generic.List<AnimationClip>();
        foreach (var p in new[] { "WAIT01", "WAIT02", "WAIT03" })
        {
            var c = LoadFirstClip($"Assets/UnityChan/Animations/unitychan_{p}.fbx");
            if (c != null) idleVariantClips.Add(c);
        }
        var ivProp = soSw.FindProperty("_idleVariants");
        ivProp.arraySize = idleVariantClips.Count;
        for (int i = 0; i < idleVariantClips.Count; i++)
            ivProp.GetArrayElementAtIndex(i).objectReferenceValue = idleVariantClips[i];

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
    private static GameObject PlaceTower(string name, Vector3 pos, Material mat, Projectile projPrefab,
        GameObject towerModel = null, bool isBlue = true, float maxHp = 500f)
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

        // HP（外側=600 / 内側=800 を呼び出し元から渡す。既定 500 は後方互換）
        var hc = go.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = maxHp;
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
        // チャージ予兆用に頂部クリスタルを結線（ランタイムの名前検索フォールバックより確実）
        var crystalProp = soTa.FindProperty("_crystal");
        if (crystalProp != null) crystalProp.objectReferenceValue = crystalTransform;
        soTa.ApplyModifiedPropertiesWithoutUndo();

        // 頭上 HP バー（クリスタル新位置 y6.1 の上に出すよう yOffset 7.6）。味方=緑/敵=赤 の規約に合わせる
        var matBar = isBlue
            ? GetOrCreateBarMat("BarGreen", new Color(0.30f, 0.85f, 0.35f))
            : GetOrCreateBarMat("BarRed",   new Color(0.92f, 0.30f, 0.25f));
        CreateWorldHealthBar(go.transform, 1.4f, 7.6f, matBar, maxHp);

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

        return go;
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
        // 親 GO: 当たり判定(CapsuleCollider)/HealthComponent/TeamTag を持つ「ネクサス本体」。
        // 見た目は子オブジェクトの多段台座+石柱+発光クリスタルでプロシージャルに組む。
        var root = new GameObject(name);
        root.transform.position = new Vector3(pos.x, 0f, pos.z);
        SetStatic(root);

        // ダメージ受け取り用のコライダー(クリック・スキル弾の命中判定)
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 5.5f, 0f);
        col.height = 11f;
        col.radius = 2.6f;

        // 足元の影リング(チーム色の薄リング)。前方広場を広く取るため小さめに(半径4)。
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = $"{name}_Ring";
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        ring.transform.localScale    = new Vector3(8f, 0.05f, 8f);
        UseFlatMeshCollider(ring, keepCollider: false);
        SetStatic(ring);
        SetMat(ring, mat);

        // 多段台座: 3段重ね。下が広く上が狭い、いかにも祭壇な見た目。チーム色 + 暗色トリム。
        Material trim = GetOrCreateMat($"{name}_Trim", new Color(0.18f, 0.18f, 0.22f));
        AddTitanTier(root.transform, $"{name}_Tier1", new Vector3(0f, 0.30f, 0f), new Vector3(9.0f, 0.50f, 9.0f), mat);
        AddTitanTier(root.transform, $"{name}_Tier1Trim", new Vector3(0f, 0.62f, 0f), new Vector3(9.1f, 0.10f, 9.1f), trim);
        AddTitanTier(root.transform, $"{name}_Tier2", new Vector3(0f, 0.95f, 0f), new Vector3(7.0f, 0.50f, 7.0f), mat);
        AddTitanTier(root.transform, $"{name}_Tier2Trim", new Vector3(0f, 1.27f, 0f), new Vector3(7.1f, 0.10f, 7.1f), trim);
        AddTitanTier(root.transform, $"{name}_Tier3", new Vector3(0f, 1.60f, 0f), new Vector3(5.0f, 0.50f, 5.0f), mat);

        // 中央の石柱(細く高い)。クリスタルを支える礎。
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = $"{name}_Pillar";
        pillar.transform.SetParent(root.transform, false);
        pillar.transform.localPosition = new Vector3(0f, 4.0f, 0f);
        pillar.transform.localScale    = new Vector3(2.4f, 2.6f, 2.4f);
        SetStatic(pillar);
        SetMat(pillar, trim);

        // 石柱の途中にチーム色のトリム輪(2本)。
        AddTitanTier(root.transform, $"{name}_PillarRing1", new Vector3(0f, 2.55f, 0f), new Vector3(2.7f, 0.15f, 2.7f), mat);
        AddTitanTier(root.transform, $"{name}_PillarRing2", new Vector3(0f, 5.45f, 0f), new Vector3(2.7f, 0.15f, 2.7f), mat);

        // 頂上クリスタル: 半透明発光のチーム色。立方体を 45°回転させて八面体っぽく見せる。
        // チーム色を暗くしたものに少しの透過を乗せた専用マテリアル。
        Color crystalCol = pos.x < 0f
            ? new Color(0.35f, 0.65f, 1.00f, 0.85f)
            : new Color(1.00f, 0.45f, 0.30f, 0.85f);
        Material crystalMat = GetOrCreateTransparentMat($"{name}_Crystal", crystalCol);
        var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.name = $"{name}_Crystal";
        crystal.transform.SetParent(root.transform, false);
        crystal.transform.localPosition = new Vector3(0f, 8.2f, 0f);
        crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 35f);
        crystal.transform.localScale    = new Vector3(2.3f, 3.6f, 2.3f);
        Object.DestroyImmediate(crystal.GetComponent<Collider>());
        SetStatic(crystal);
        SetMat(crystal, crystalMat);

        // クリスタル直下の冠リング(チーム色トリム)。
        AddTitanTier(root.transform, $"{name}_CrystalBase", new Vector3(0f, 6.6f, 0f), new Vector3(3.2f, 0.15f, 3.2f), mat);

        var hc = root.AddComponent<HealthComponent>();
        var soHc = new SerializedObject(hc);
        soHc.FindProperty("_maxHp").floatValue = 2500f;
        soHc.ApplyModifiedPropertiesWithoutUndo();

        var tt   = root.AddComponent<TeamTag>();
        var soTt = new SerializedObject(tt);
        soTt.FindProperty("_team").enumValueIndex = pos.x < 0f ? (int)TeamId.Blue : (int)TeamId.Red;
        soTt.ApplyModifiedPropertiesWithoutUndo();

        // 露出ゲート: 自チームの1レーン分のタワー全滅まで、全攻撃者のダメージを 0 化する。
        root.AddComponent<Enigma.Character.TitanGuard>();

        return hc;
    }

    // ネクサスの台座1段(=チーム色 or トリム色の薄い円柱)を子として追加する。
    private static void AddTitanTier(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
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
        // 経路先頭に「チーム泉中心(±100, 0)」を挿入する。後退(Backward)が index 0 まで戻ったとき、
        // WaypointReach(3m) 手前で停止しても泉の回復圏(中心±100・半径5)内に必ず収まるようにする。
        // spawnPos(±98〜101, ±3.5)を先頭にすると停止位置が泉圏から最大7m外れ、低HPの Retreat が
        // 回復できず永久に解除されないデッドロックになる(泉を r10→5 に縮めた際の回帰)。
        var route = new Vector3[waypoints.Length + 1];
        route[0] = new Vector3(Mathf.Sign(spawnPos.x) * 100f, 0f, 0f);
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

        // 泉回復: チーム共通の後方安全パッド中心(±100, 0)・半径5のコンパクト圏。視覚リング
        // (PlaceFountainRings)と一致させる。各 Bot のスポーンは z オフセットで散らすが泉中心は1点。
        var botFountain   = go.AddComponent<Enigma.Combat.FountainRegen>();
        var soBotFountain = new SerializedObject(botFountain);
        soBotFountain.FindProperty("_fountainCenter").vector3Value = new Vector3(Mathf.Sign(spawnPos.x) * FountainCenterX, 3.6f, 0f);
        soBotFountain.FindProperty("_radius").floatValue           = FountainRadius;
        soBotFountain.ApplyModifiedPropertiesWithoutUndo();

        var xp = go.AddComponent<XpReward>();
        var soXp = new SerializedObject(xp);
        soXp.FindProperty("_amount").floatValue = 100f;
        soXp.ApplyModifiedPropertiesWithoutUndo();

        var gold = go.AddComponent<GoldReward>();
        var soGold = new SerializedObject(gold);
        soGold.FindProperty("_amount").intValue = 300;
        soGold.ApplyModifiedPropertiesWithoutUndo();

        // 頭上 HPバー（レベル表示なし）。cc.center=(0,0,0) のため capsule top は local y=+1.0。
        // yOffset=1.3 で world y≈2.4 に出してカプセル天面(2.1)より上に表示する。
        var wrapper = CreateWorldHealthBar(go.transform, 1.05f, 1.3f, matBar, 500f);

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

        // 被弾ヒットフラッシュ（チャンピオンのみ。ミニオンには付けない）
        go.AddComponent<Enigma.Combat.HitFlash>();

        return ai;
    }

    // TOPレーン経路を赤ベース→青ベース方向（角度 20°→160°、12°刻み）で構築する。
    // ミニオンの ArcPt と同じ半径63・角度系。z>0 側（北回り）。
    private static Vector3[] BuildTopLaneWaypoints()
    {
        Vector3 ArcPt(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(63f * Mathf.Cos(r), 0f, 63f * Mathf.Sin(r));
        }

        var list = new List<Vector3>();
        // 開口点はポケット壁帯の外かつ開口セクター内に置く(M-0で半径1.4倍・z オフセットも同比率)。
        // 壁帯内部に埋まりボットが壁をよじ登ってスタックしないよう外側に置く
        list.Add(new Vector3(63.5f, 0f, 11.2f)); // 赤ベース開口
        for (float deg = 20f; deg <= 160f + 0.01f; deg += 12f)
            list.Add(ArcPt(deg));
        list.Add(new Vector3(-63.5f, 0f, 11.2f)); // 青ベース開口
        return list.ToArray();
    }

    // BOTレーン経路を赤ベース→青ベース方向（角度 -20°→-160°、-12°刻み）で構築する。
    // TOP の z>0 ミラー。z<0 側（南回り）。開口は z=-14 側。
    private static Vector3[] BuildBotLaneWaypoints()
    {
        Vector3 ArcPt(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(63f * Mathf.Cos(r), 0f, 63f * Mathf.Sin(r));
        }

        var list = new List<Vector3>();
        // 開口点は壁帯の外かつ開口セクター内(TOP と同様の理由)
        list.Add(new Vector3(63.5f, 0f, -11.2f)); // 赤ベース開口（南側）
        for (float deg = -20f; deg >= -160f - 0.01f; deg -= 12f)
            list.Add(ArcPt(deg));
        list.Add(new Vector3(-63.5f, 0f, -11.2f)); // 青ベース開口（南側）
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
            new Vector3(63.5f, 0f, 11.2f),  // 赤ベース開口（TOP側、壁帯の外）
            Polar(32f,  63f),              // レーン帯を45°方向へ
            Polar(45f,  63f),              // 45°パス外端（レーン接続点）
            Polar(45f,  42f),              // 右上キャンプ空き地
            Polar(26f,  48f),              // 26°ガンク口内側の茂みを経由(スライスM-B)
            Polar(45f,  25f),              // 45°パス内端（ベイスン縁）
            // 基地軸ポケット(Ax0 キャンプ)への往復スイープ。チョークは r42〜54 のため
            // r37 では 15°帯が開通しており (45°,25)→(37,0) は直行できる(基地正面壁は通らない)。
            // 出口は同じ道を戻る(0°直進はr30-31.5の内周弧に塞がれる)
            new Vector3(37f, 0f, 0f),
            Polar(45f,  25f),              // ポケットから復帰
            Polar(0f,   18f),              // ベイスン東縁（ボスピットr11の外・basin r22内）
            Polar(-45f, 25f),              // 315°パス内端
            Polar(-26f, 48f),              // 334°ガンク口内側の茂みを経由(スライスM-B)
            Polar(-45f, 42f),              // 右下キャンプ空き地
            Polar(-45f, 63f),              // 315°パス外端
            Polar(-32f, 63f),              // レーン帯を赤ベースへ
            new Vector3(63.5f, 0f, -11.2f), // 赤ベース開口（BOT側）
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

    // 各点の x,z を反転した新配列を返す（マップは180°点対称なので、赤側ジャングル経路を
    // そのまま青側ジャングル経路に変換できる）。元配列は変更しない。
    private static Vector3[] MirrorXZ(Vector3[] src)
    {
        var dst = new Vector3[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = new Vector3(-src[i].x, src[i].y, -src[i].z);
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

        // アーク半径 R=63 上のウェイポイント計算ヘルパー
        static Vector3 ArcPt(float deg) {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(63f * Mathf.Cos(r), 0f, 63f * Mathf.Sin(r));
        }

        // 各ルートの終端はレーン開口(±70, ±11.2)から敵タイタン前の分離WP(±SiegeWaypointX, ±SiegeWaypointZ)まで
        // 延伸する。旧レイアウトは Top/Bot とも (±72.8, z=0) の一点に畳まれ攻城が短いチョークに集中したため、
        // Top 由来を +Z / Bot 由来を -Z へ分離する(横方向の有効戦闘面を確保)。最終WP(±76, ±8)は敵タイタン
        // (±TitanCenterX, 0)のカプセル(r2.6)表面まで約7.4m<_aggroRange(8) で届くため、ウェーブは到達直前に
        // タイタンを標的化し攻城へ移る(=タイタン撃破で決着。索敵成立の x=76 は分離量 z=±8 を満たす最小前進)。
        // ※x を旧 72.8 のままにすると z=8 で表面9.6m>8m となり索敵が成立しないため SiegeWaypointX へ前進させた。

        // 立体化(M-A)で出発点を基地内(±76,±6)へ移設(旧±70,±14は囲い壁帯r70.5〜72に埋まっていた)。
        // 出発点→最初のアーク WP への直線が壁帯(r70.5〜72)を横切る角度は各ゲート開口(±8°)内に
        // 収まることを事前計算済み(RedTop/RedBot/BlueTop/BlueBotいずれも開口内)。後続WPは不変。

        // BlueTop(Top由来→+Z): 出発(-76,0,6)→ θ=160,135,90,45,20 のアーク→敵開口→Redタイタン前(+X,+Z)
        PlaceSpawner("Spawner_BlueTop",
            new Vector3(-76f, 0f, 6f),
            TeamId.Blue, matBlue, minionPrefab,
            new Vector3[] {
                ArcPt(160f), ArcPt(135f), ArcPt(90f), ArcPt(45f), ArcPt(20f),
                new Vector3(70f, 0f, 11.2f), new Vector3(73f, 0f, 9f), new Vector3(SiegeWaypointX, 0f, SiegeWaypointZ)
            });

        // RedTop(Top由来→+Z): 出発(76,0,6)→ θ=20,45,90,135,160 のアーク→敵開口→Blueタイタン前(-X,+Z)
        PlaceSpawner("Spawner_RedTop",
            new Vector3(76f, 0f, 6f),
            TeamId.Red, matRed, minionPrefab,
            new Vector3[] {
                ArcPt(20f), ArcPt(45f), ArcPt(90f), ArcPt(135f), ArcPt(160f),
                new Vector3(-70f, 0f, 11.2f), new Vector3(-73f, 0f, 9f), new Vector3(-SiegeWaypointX, 0f, SiegeWaypointZ)
            });

        // BlueBot(Bot由来→-Z): z 符号反転版。敵タイタン前(+X,-Z)
        PlaceSpawner("Spawner_BlueBot",
            new Vector3(-76f, 0f, -6f),
            TeamId.Blue, matBlue, minionPrefab,
            new Vector3[] {
                ArcPt(200f), ArcPt(225f), ArcPt(270f), ArcPt(315f), ArcPt(340f),
                new Vector3(70f, 0f, -11.2f), new Vector3(73f, 0f, -9f), new Vector3(SiegeWaypointX, 0f, -SiegeWaypointZ)
            });

        // RedBot: z 符号反転版
        // RedBot(Bot由来→-Z): z 符号反転版。敵タイタン前(-X,-Z)
        PlaceSpawner("Spawner_RedBot",
            new Vector3(76f, 0f, -6f),
            TeamId.Red, matRed, minionPrefab,
            new Vector3[] {
                ArcPt(340f), ArcPt(315f), ArcPt(270f), ArcPt(225f), ArcPt(200f),
                new Vector3(-70f, 0f, -11.2f), new Vector3(-73f, 0f, -9f), new Vector3(-SiegeWaypointX, 0f, -SiegeWaypointZ)
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
        // 赤側はウェーブを半周期(12.5s)遅らせ、前線を振動させて自然な押し込みを生む
        // (完全対称だとレーン中央で永久均衡しタワーまで届かない)
        soSpawner.FindProperty("_initialDelayOffset").floatValue =
            team == TeamId.Red ? 12.5f : 0f;

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
        // 立体化(M-A)でクレーター底(-2.5)が実体の足場になったため、旧オフセット(+0.18)はそのまま底面基準に加算
        boss.transform.position = new Vector3(0f, -2.32f, 0f); // クレーター床(-2.5)+ピット足場オフセット(0.18)

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

    // ---- 森とレーンの壁 ----

    /// <summary>
    /// レーンリング内縁（半径 38.5〜39.8）に沿って壁弧を立て、ジャングル（中央側）とレーン（外周リング）を
    /// 分離する。LoL のように決められた入口でのみ行き来できるよう、以下にだけ開口を残す:
    ///   - ジャングルパス入口 θ=45/135/225/315°（各 ±8°）
    ///   - 川がリングと交わる口 θ=90/270°（各 ±13°）
    /// 衝突＝見た目一致の円弧帯メッシュ(PlaceWallBandAt)を使い、継ぎ目の透明壁/すり抜けを防ぐ。
    /// </summary>
    private static void PlaceJungleLaneWalls()
    {
        var mat = GetOrCreateMat("JungleLaneWall", new Color(0.46f, 0.50f, 0.40f));
        ApplyWutheringRamp(mat);

        var parent = new GameObject("JungleLaneWalls");
        SetStatic(parent);

        const float innerR = 54.0f;
        const float outerR = 55.5f;
        const float height = 2.5f;
        const float stepDeg = 3.75f;

        // 開口(start,end)を除いた壁弧。[0,360) を一周し、各開口の隙間を空ける。
        // 基地正面(0°/180°)の開口は 2026-07-05 に封鎖(ユーザー指示)。物理ゲート廃止後、
        // ジャングル→基地前広場→タイタンがタワーを1本も経由しない抜け道になっていたため、
        // 壁弧を連結してタイタンへの全導線を「レーン経由=インナータワー射程圏」に限定する。
        // 開口: パス口±8°(45°系)/川口±13°(90°系)/ガンク口±3°(±26°系)のみ。
        (float start, float end)[] wallArcs =
        {
            ( 29f,  37f),   // 26°ガンク口 と 45°パス口 の間
            ( 53f,  77f),   // 45°パス口 と 90°川口 の間
            (103f, 127f),   // 90°川口 と 135°パス口 の間
            (143f, 151f),   // 135°パス口 と 154°ガンク口 の間
            (157f, 203f),   // 154°ガンク口 〜 青基地正面(180°、封鎖済)〜 206°ガンク口
            (209f, 217f),   // 206°ガンク口 と 225°パス口 の間
            (233f, 257f),   // 225°パス口 と 270°川口 の間
            (283f, 307f),   // 270°川口 と 315°パス口 の間
            (323f, 331f),   // 315°パス口 と 334°ガンク口 の間
            (337f, 383f),   // 334°ガンク口 〜 赤基地正面(0°/360°、封鎖済)〜 26°ガンク口
        };

        int i = 0;
        foreach (var (start, end) in wallArcs)
        {
            int segs = Mathf.Max(1, Mathf.RoundToInt((end - start) / stepDeg));
            var wallGo = PlaceWallBandAt(parent, $"JungleLaneWall_{i:D2}", Vector3.zero,
                innerR, outerR, height, segs, start, end, mat);
            // 視界2.0(M-V): レーン/ジャングル分離壁は地形遮蔽の対象(茂み演出ではなく構造物の視界ブロック)
            wallGo.AddComponent<Enigma.Vision.VisionBlockerTag>();
            i++;
        }
    }

    /// <summary>
    /// 迷路ジャングル壁(スライスM-B)の疎な第一段。基地側象限(赤=0°/青=180°)と川側象限(90°/270°)に
    /// 円弧帯を配置し、回廊幅≥12mを保ったままジャングル内の見通しを分断する。
    /// キャンプ(対角パス、開口±8°)とガンク口(±26°等、開口±3°)へは干渉しない半径・角度で設計済み。
    /// (旧第一段の軸上放射壁2本は基地軸キャンプ貫通のため2026-07-06撤去、下記コメント参照)
    /// </summary>
    private static void PlaceJungleMaze()
    {
        var mat = GetOrCreateMat("JungleLaneWall", new Color(0.46f, 0.50f, 0.40f));
        ApplyWutheringRamp(mat);

        var parent = new GameObject("JungleMaze");
        SetStatic(parent);

        const float arcInnerR = 40.0f;
        const float arcOuterR = 41.5f;
        const float riverArcInnerR = 38.0f;
        const float riverArcOuterR = 39.5f;
        const float height = 2.5f;
        const float stepDeg = 3.75f;

        // 弧壁8本: 基地側象限(0°/180°軸)r=40〜41.5、川側象限(90°/270°軸)r=38〜39.5
        (float start, float end, float innerR, float outerR)[] arcDefs =
        {
            (  8f,  22f, arcInnerR, arcOuterR),       // 赤軸(0°)寄り
            (338f, 352f, arcInnerR, arcOuterR),       // 赤軸(0°)寄り(対称)
            (158f, 172f, arcInnerR, arcOuterR),       // 青軸(180°)寄り
            (188f, 202f, arcInnerR, arcOuterR),       // 青軸(180°)寄り(対称)
            ( 58f,  72f, riverArcInnerR, riverArcOuterR), // 川北(90°)寄り
            (108f, 122f, riverArcInnerR, riverArcOuterR), // 川北(90°)寄り(対称)
            (238f, 252f, riverArcInnerR, riverArcOuterR), // 川南(270°)寄り
            (288f, 302f, riverArcInnerR, riverArcOuterR), // 川南(270°)寄り(対称)
        };

        int ai = 0;
        foreach (var (start, end, innerR, outerR) in arcDefs)
        {
            int segs = Mathf.Max(1, Mathf.RoundToInt((end - start) / stepDeg));
            var arcGo = PlaceWallBandAt(parent, $"JungleMazeArc_{ai:D2}", Vector3.zero,
                innerR, outerR, height, segs, start, end, mat);
            // 視界2.0(M-V): 迷路壁も地形遮蔽の対象
            arcGo.AddComponent<Enigma.Vision.VisionBlockerTag>();
            ai++;
        }

        // 旧M-Bの軸上放射壁(JungleMazeRadial_Red/Blue、x±30〜40)は2026-07-06に撤去。
        // 基地軸キャンプ(Polar(0°/180°,37.5))のポケットを貫通しスライムが壁にめり込んで
        // いたため(ユーザー報告「狩り場に壁が突っ込んでいる」)。内側アプローチ分断の役割は
        // 第2段(内周弧r30〜31.5+放射チョークr42〜54)が代替済み。

        // ---- 迷路ジャングル第2段(回廊化、追加のみ) ----
        // 弧6本: 基地軸(0°/180°)寄りの内周弧(r30〜31.5)+川4象限寄りの内周弧(r28〜29.5)。
        // 既存弧(r38〜41.5系)より内側に配置し、ジャングラー13点経路(r18〜48)を回廊状に分断する。
        (float start, float end, float innerR, float outerR)[] arc2Defs =
        {
            (350f, 370f, 30.0f, 31.5f),   // 赤基地軸(0°)±10°の内周弧
            (170f, 190f, 30.0f, 31.5f),   // 青側対称
            ( 56f,  70f, 28.0f, 29.5f),   // 川北東の内周弧
            (110f, 124f, 28.0f, 29.5f),   // 川北西
            (236f, 250f, 28.0f, 29.5f),   // 川南西
            (290f, 304f, 28.0f, 29.5f),   // 川南東
        };

        int ai2 = 0;
        foreach (var (start, end, innerR, outerR) in arc2Defs)
        {
            int segs = Mathf.Max(1, Mathf.RoundToInt((end - start) / stepDeg));
            var arcGo = PlaceWallBandAt(parent, $"JungleMaze2Arc_{ai2:D2}", Vector3.zero,
                innerR, outerR, height, segs, start, end, mat);
            arcGo.AddComponent<Enigma.Vision.VisionBlockerTag>();
            ai2++;
        }

        // 放射チョーク4本: 基地側象限(15°/345°/165°/195°)のみ、r42〜54を塞いで回廊を折り曲げる。
        foreach (float deg in new[] { 15f, 345f, 165f, 195f })
        {
            var radGo = PlaceRadialWallAt(parent, $"JungleMaze2Radial_{deg:F0}", deg, 42f, 54f, mat);
            radGo.AddComponent<Enigma.Vision.VisionBlockerTag>();
        }
    }

    // 放射壁1本(角度指定、Cubeを θ 方向へ回転して配置)。innerR〜outerR を θ 方向に塞ぐ。
    private static GameObject PlaceRadialWallAt(GameObject parent, string name, float deg,
        float innerR, float outerR, Material mat)
    {
        float rad  = deg * Mathf.Deg2Rad;
        float midR = (innerR + outerR) * 0.5f;
        var center = new Vector3(midR * Mathf.Cos(rad), 1.25f, midR * Mathf.Sin(rad));
        var go = PlaceCube(name, center, new Vector3(outerR - innerR, 2.5f, 1.5f), mat);
        // ローカルX(長軸)を θ 方向へ: Unity の +Y 回転は +X を -Z に倒すため yaw=-θ
        go.transform.rotation = Quaternion.Euler(0f, -deg, 0f);
        go.transform.SetParent(parent.transform, true);
        s_wallBoxes.Add((center, (outerR - innerR) * 0.5f, 0.75f, deg));
        return go;
    }

    /// <summary>
    /// レーン外周壁(物理ゲート廃止後のユーザー指定)。レーン(r56〜70)の外側 r70.5〜72 に
    /// 弧壁を張り、基地正面(基地軸±30°)は開けたままにする=タイタン前に壁は置かない。
    /// 弧の端には放射キャップを立て、レーン外の「縁の帯」(境界までの隙間)がバイパス回廊に
    /// ならないよう塞ぐ(境界は θ=90° で r72 まで迫るため弧はちょうど帯内に収まる)。
    /// </summary>
    private static void PlaceOuterLaneWalls()
    {
        var mat = GetOrCreateMat("JungleLaneWall", new Color(0.46f, 0.50f, 0.40f));
        ApplyWutheringRamp(mat);

        var parent = new GameObject("OuterLaneWalls");
        SetStatic(parent);

        const float innerR  = 70.5f;
        const float outerR  = 72.0f;
        const float height  = 2.5f;
        const float stepDeg = 3.75f;

        (float start, float end)[] arcs = { (30f, 150f), (210f, 330f) };
        int i = 0;
        foreach (var (start, end) in arcs)
        {
            int segs = Mathf.Max(1, Mathf.RoundToInt((end - start) / stepDeg));
            var arcGo = PlaceWallBandAt(parent, $"OuterLaneWall_{i:D2}", Vector3.zero,
                innerR, outerR, height, segs, start, end, mat);
            arcGo.AddComponent<Enigma.Vision.VisionBlockerTag>();
            i++;
        }

        // 放射キャップ4本: 弧端(θ=30/150/210/330)から境界(r≈88.6)の外まで塞ぐ。
        // 端点は境界の先(r90.5)に埋めて回り込みを封じる。ルートは r>70 を通らないため無干渉。
        foreach (float deg in new[] { 30f, 150f, 210f, 330f })
        {
            float rad    = deg * Mathf.Deg2Rad;
            float midR   = (innerR + 90.5f) * 0.5f;
            float length = 90.5f - innerR;
            var center   = new Vector3(midR * Mathf.Cos(rad), 1.25f, midR * Mathf.Sin(rad));
            var go = PlaceCube($"OuterLaneCap_{deg:F0}", center, new Vector3(length, height, 1.5f), mat);
            // ローカルX(長軸)を θ 方向へ: Unity の +Y 回転は +X を -Z に倒すため yaw=-θ
            go.transform.rotation = Quaternion.Euler(0f, -deg, 0f);
            go.transform.SetParent(parent.transform, true);
            go.AddComponent<Enigma.Vision.VisionBlockerTag>();
            s_wallBoxes.Add((center, length * 0.5f, 0.75f, deg));
        }
    }

    /// <summary>
    /// コア3回目報酬(StructureDamage)の対構造物バフ判定用に、タワー8基とタイタン2体へ
    /// StructureTag を付与する(合計10)。
    /// </summary>
    private static void TagAllStructures()
    {
        foreach (var ta in Object.FindObjectsByType<TowerAttack>(FindObjectsSortMode.None))
            if (ta.GetComponent<StructureTag>() == null) ta.gameObject.AddComponent<StructureTag>();

        foreach (var name in new[] { "Titan_Blue", "Titan_Red" })
        {
            var go = GameObject.Find(name);
            if (go != null && go.GetComponent<StructureTag>() == null)
                go.AddComponent<StructureTag>();
        }
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

            // パス端点: レーンアーク側(R=63)からベイスン縁(r=25)
            var p1 = new Vector3(63f * Mathf.Cos(rad), 0f, 63f * Mathf.Sin(rad));
            var p2 = new Vector3(25f * Mathf.Cos(rad), 0f, 25f * Mathf.Sin(rad));

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

            // キャンプ中心（半径42）
            var campCenter = new Vector3(42f * Mathf.Cos(rad), 0f, 42f * Mathf.Sin(rad));

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
    /// 回廊奥のジャングルキャンプ6箇所(ユーザー要望「通路の先にモンスター」)。
    /// 既存4キャンプ(対角パス上 r42、θ=45/135/225/315°)とは独立に、
    /// 迷路壁(PlaceJungleMaze)の袋小路・ポケットへ配置する。
    /// ・基地軸の奥ポケット×2: 原点 Polar(0°,36)/(180°,36) から半径方向に+1.5m
    ///   外側へシフトした Polar(0°,37.5)/(180°,37.5)。内周弧(r30〜31.5)と
    ///   放射チョーク(r42〜54)の間の袋小路。各2体(通常キャンプより濃い報酬感)。
    /// ・川岸ポケット×4: 原点 Polar(63/117/243/297°,25) から半径方向に-2.0m
    ///   内側(クレーター側)へシフトした同角度 r=23。クレーター縁(r22)と
    ///   川岸内周弧(r28〜29.5)の間のポケット。各1体。
    /// 追加の囲い壁(旧 PlaceCampRoom による C字リング)は不評だったため2026-07-06に撤去。
    /// キャンプは既存迷路壁が自然に作る袋小路の中にそのまま置く方針(ユーザー指示)。
    /// 地形はいずれも平坦 y0(クレーター r&gt;=22・川 |x|&gt;9・プラトー |x|&lt;86 のため
    /// MapHeightModel.Height の全分岐が0を返す設計検証済み)。
    /// </summary>
    private static void PlaceCorridorCamps(Material matJunglePath)
    {
        // 基地軸の奥ポケット×2(各2体)。原点から半径+1.5mシフトして既存迷路壁
        // (内周弧r30〜31.5・放射チョークr42〜54)が形成する袋小路に収める。
        // 追加の囲い壁(旧C字リング)は2026-07-06にユーザー指示で撤去済み。
        (string name, float deg, float shiftR)[] axisPockets =
        {
            ("Ax0",   0f,   1.5f),
            ("Ax180", 180f, 1.5f),
        };

        foreach (var (name, deg, shiftR) in axisPockets)
        {
            float rad    = deg * Mathf.Deg2Rad;
            float r      = 36f + shiftR;
            var   center = new Vector3(r * Mathf.Cos(rad), 0f, r * Mathf.Sin(rad));

            PlaceCorridorClearing($"CampClearing_{name}", center, 4.4f, matJunglePath);
            CreateSlime($"Slime_{name}_a", center + new Vector3(1.1f, 0f, 0.6f));
            CreateSlime($"Slime_{name}_b", center + new Vector3(-1.1f, 0f, -0.6f));
        }

        // 川岸ポケット×4(各1体)。原点から半径-2.0m(クレーター側)シフトして
        // 既存迷路壁(内周弧r28〜29.5)が形成するポケットに収める。
        // 追加の囲い壁(旧C字リング)は2026-07-06にユーザー指示で撤去済み。
        (string name, float deg)[] riverPockets =
        {
            ("Rv63",  63f),
            ("Rv117", 117f),
            ("Rv243", 243f),
            ("Rv297", 297f),
        };

        foreach (var (name, deg) in riverPockets)
        {
            float rad    = deg * Mathf.Deg2Rad;
            const float r = 25f - 2.0f;
            var   center  = new Vector3(r * Mathf.Cos(rad), 0f, r * Mathf.Sin(rad));

            PlaceCorridorClearing($"CampClearing_{name}", center, 4.4f, matJunglePath);
            CreateSlime($"Slime_{name}", center);
        }
    }

    /// <summary>
    /// 回廊奥キャンプの足元空き地(コライダーなし・小径版)。既存4キャンプのクリアリング
    /// (半径4.5)より狭いポケットへ収めるため半径をパラメータ化した版。
    /// </summary>
    private static void PlaceCorridorClearing(string name, Vector3 center, float radius, Material mat)
    {
        var clearing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        clearing.name = name;
        clearing.transform.position   = new Vector3(center.x, 0.025f, center.z);
        clearing.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);
        UseFlatMeshCollider(clearing, keepCollider: false);
        SetStatic(clearing);
        SetMat(clearing, mat);
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

                // 接地補正: スケール後に再計測し、Visual の最下端をワールド地面(y=0、
                // MapHeightModel 上は campCenter.y = 常に0)へ一致させる。
                // 旧実装は親の位置(y=0.8、クリック用コライダーの中心オフセット)を
                // 地面基準と誤認しており、その結果スライムが地面に0.8m埋まって見えていた。
                var b2 = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b2.Encapsulate(rends[i].bounds);
                float groundY    = campCenter.y;
                float footOffset = b2.min.y - groundY;
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

        // JungleMonster コンポーネント: エディタ時の結線は SerializedObject 経由でないと
        // シーン保存に乗らない(Initialize の非シリアライズ書き込みは実行時に消えていた実測バグ)
        var jm = parent.AddComponent<JungleMonster>();
        var soJm = new SerializedObject(jm);
        soJm.FindProperty("_campCenter").vector3Value = campCenter;
        soJm.FindProperty("_barFill").objectReferenceValue = wrapper;
        soJm.ApplyModifiedPropertiesWithoutUndo();

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
            var p1 = new Vector3(63f * Mathf.Cos(rad), 0f, 63f * Mathf.Sin(rad));
            var p2 = new Vector3(25f * Mathf.Cos(rad), 0f, 25f * Mathf.Sin(rad));
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
            var   center = new Vector3(42f * Mathf.Cos(rad), 0f, 42f * Mathf.Sin(rad));
            if (Vector3.Distance(p, center) < radius) return true;
        }
        return false;
    }

    /// <summary>
    /// 地表散布物（草タフト・小石）の配置除外判定。木/草で共通に使う。
    /// 除外: レーン帯（半径40〜50）・川（中央 |x|<8 の帯）・ベイスン（半径<18）・
    /// ジャングルパス近傍・ベース周辺（±56 付近半径12）。
    /// </summary>
    /// <summary>
    /// 壁(迷路/レーン壁/外周壁)と重なる散布プロップ(木/岩/草/小石)を削除する。
    /// 幹周辺の箱(±1.0, 高さ3)で判定し、樹冠が壁上に少し掛かる程度は許容する。
    /// あわせてキャンプ空き地(CampClearing)周辺7.0m以内に生えたプロップも除去する
    /// (2026-07-06: ユーザー報告「空き地の上と周辺の木・草が残っている」を受け 5.0→7.0 に拡大)。
    /// </summary>
    private static void RemovePropsOverlappingWalls()
    {
        // 幹まわりの許容マージン。樹冠が壁上に少し掛かる程度は許容する
        const float Margin = 1.2f;

        var clearings = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("CampClearing"))
            .Select(t => t.position)
            .ToArray();

        string[] propPrefixes = { "Tree_Q", "Rock_Q", "GrassTuft_", "Pebble_" };
        int removed = 0;
        foreach (var root in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                     .Where(t => t == t.root && propPrefixes.Any(p => t.name.StartsWith(p)))
                     .ToArray())
        {
            var pos = root.position;
            bool overlaps = OverlapsAnyWall(pos, Margin)
                || clearings.Any(c => Vector2.Distance(
                       new Vector2(pos.x, pos.z), new Vector2(c.x, c.z)) < 7.0f);

            if (overlaps)
            {
                Object.DestroyImmediate(root.gameObject);
                removed++;
            }
        }

        Debug.Log($"[BuildAetherRiftMap] 壁/空き地と重なる散布プロップを {removed} 個除去しました。");
    }

    private static bool OverlapsAnyWall(Vector3 pos, float margin)
    {
        foreach (var (center, innerR, outerR, startDeg, endDeg) in s_wallArcs)
        {
            float dx = pos.x - center.x;
            float dz = pos.z - center.z;
            float r  = Mathf.Sqrt(dx * dx + dz * dz);
            if (r < innerR - margin || r > outerR + margin) continue;

            // 角度は壁定義が 360 を跨ぐ場合(例 337〜383)があるため ±360 で照合する
            float theta     = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            float angMargin = margin / Mathf.Max(r, 1f) * Mathf.Rad2Deg;
            for (int k = -1; k <= 1; k++)
            {
                float t = theta + 360f * k;
                if (t >= startDeg - angMargin && t <= endDeg + angMargin) return true;
            }
        }

        foreach (var (center, halfLen, halfThick, yawDeg) in s_wallBoxes)
        {
            // Cube は Euler(0,-yaw,0) で回転済み → ワールド→ローカルは Euler(0,-yaw,0) の逆
            var local = Quaternion.Euler(0f, yawDeg, 0f) * (pos - center);
            if (Mathf.Abs(local.x) <= halfLen + margin && Mathf.Abs(local.z) <= halfThick + margin)
                return true;
        }

        return false;
    }

    private static bool IsExcludedFromScatter(Vector3 p)
    {
        // 川（中央の縦帯）(8f×1.4)
        if (Mathf.Abs(p.x) < 11.2f) return true;

        // ベイスン（中央オブジェクティブ）
        float distFromCenter = Mathf.Sqrt(p.x * p.x + p.z * p.z);
        if (distFromCenter < 25f) return true;

        // レーンアーク帯（半径 56〜70）
        if (distFromCenter > 56f && distFromCenter < 70f) return true;

        // ジャングルパス近傍
        if (IsNearAnyJunglePath(p, 6.3f)) return true;

        // ベース周辺（±100, 半径14）
        if (Vector3.Distance(p, new Vector3(-100f, 0f, 0f)) < 14f) return true;
        if (Vector3.Distance(p, new Vector3( 100f, 0f, 0f)) < 14f) return true;

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
            // 草地: 半径 25〜95 のリング内（外周岩壁の内側、旧18〜68×1.4）に一様散布。
            float r   = 25f + Random.value * 70f;
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
            float r   = 25f + Random.value * 70f;
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
    /// 茂みゾーン12個(スライスM-B)を配置する。y は MapHeightModel.Height に追従させ、地形に埋まらないようにする。
    /// 視界ルール(茂み内から見えにくくする等)は次スライスM-Vで実装するため、ここでは
    /// BrushZone コンポーネント(器)と可視化(半透明ディスク+草タフト)のみを行う。
    /// </summary>
    private static void PlaceBrushZones()
    {
        Vector3 Polar(float deg, float radius)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(radius * Mathf.Cos(r), 0f, radius * Mathf.Sin(r));
        }

        var matBrush = GetOrCreateTransparentMat("BrushZoneDisc", new Color(0.08f, 0.28f, 0.10f, 0.55f));
        var matTuft  = GetOrCreateMat("BrushZoneTuft", new Color(0.10f, 0.32f, 0.12f));
        ApplyWutheringRamp(matTuft);

        var parent = new GameObject("BrushZones");

        (string name, Vector3 pos, float radius)[] zoneDefs =
        {
            // ガンク口内側×4
            ("Brush_Gank_026", Polar(26f,  50f), 3.5f),
            ("Brush_Gank_154", Polar(154f, 50f), 3.5f),
            ("Brush_Gank_206", Polar(206f, 50f), 3.5f),
            ("Brush_Gank_334", Polar(334f, 50f), 3.5f),
            // 川岸×4
            ("Brush_River_00", new Vector3(13f,  0f, 31f),  3f),
            ("Brush_River_01", new Vector3(-13f, 0f, 31f),  3f),
            ("Brush_River_02", new Vector3(-13f, 0f, -31f), 3f),
            ("Brush_River_03", new Vector3(13f,  0f, -31f), 3f),
            // レーン中腹×4
            ("Brush_Lane_058", Polar(58f,  67f), 3.5f),
            ("Brush_Lane_122", Polar(122f, 67f), 3.5f),
            ("Brush_Lane_238", Polar(238f, 67f), 3.5f),
            ("Brush_Lane_302", Polar(302f, 67f), 3.5f),
        };

        int zoneIndex = 0;
        foreach (var (name, pos, radius) in zoneDefs)
        {
            float y = MapHeightModel.Height(pos.x, pos.z);
            var zonePos = new Vector3(pos.x, y, pos.z);

            var zoneGo = new GameObject(name);
            zoneGo.transform.position = zonePos;
            var brushZone = zoneGo.AddComponent<BrushZone>();
            var soZone = new SerializedObject(brushZone);
            soZone.FindProperty("_radius").floatValue = radius;
            soZone.ApplyModifiedPropertiesWithoutUndo();
            zoneGo.transform.SetParent(parent.transform, true);

            // 可視化: 半透明の濃緑ディスク(コライダー除去)
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            disc.transform.SetParent(zoneGo.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            disc.transform.localScale    = new Vector3(radius * 2f, 0.02f, radius * 2f);
            UseFlatMeshCollider(disc, keepCollider: false);
            SetMat(disc, matBrush);
            SetStatic(disc);

            // 可視化: 草タフト8本をインデックスベースの決定的な式で半径内に風配置
            for (int t = 0; t < 8; t++)
            {
                // 黄金角(137.5°)で角度をずらし、インデックスの小数余りで半径・高さ・スケールを分散させる
                float angleDeg = zoneIndex * 41f + t * 137.5f;
                float frac     = ((zoneIndex * 7 + t * 3) % 11) / 10f;
                float tuftR    = frac * radius * 0.8f;
                float ang      = angleDeg * Mathf.Deg2Rad;
                float tx       = tuftR * Mathf.Cos(ang);
                float tz       = tuftR * Mathf.Sin(ang);

                var tuft = PlaceCube($"Tuft_{t:D2}",
                    new Vector3(tx, 0.4f, tz),
                    new Vector3(0.12f, 0.8f, 0.12f),
                    matTuft);
                tuft.transform.SetParent(zoneGo.transform, false);
                tuft.transform.localRotation = Quaternion.Euler(0f, angleDeg * 2f, 8f + frac * 10f);
                Object.DestroyImmediate(tuft.GetComponent<BoxCollider>());
            }

            SetStatic(zoneGo);
            zoneIndex++;
        }
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

            // コライダー: 幹相当の細い鉛直カプセル。
            // FBX ごとに直立補正の有無・ローカル軸が異なるため、固定 direction は横倒し壁の原因になる
            // （Y-up モデルは補正されず、direction=2 だと長軸が世界水平に寝て巨大な透明壁になっていた）。
            // 最終姿勢から「世界鉛直に最も近いローカル軸」を判定し、その軸に合わせて collider を組む。
            float capHeight = targetHeight * styleMul;
            Physics.SyncTransforms();
            var tr = treeGo.transform;
            // 世界の上方向を木ローカルへ変換し、最大成分の軸を縦軸とする
            Vector3 localUp = tr.InverseTransformDirection(Vector3.up);
            int dir = 0; float maxAbs = Mathf.Abs(localUp.x);
            if (Mathf.Abs(localUp.y) > maxAbs) { dir = 1; maxAbs = Mathf.Abs(localUp.y); }
            if (Mathf.Abs(localUp.z) > maxAbs) { dir = 2; }
            var cap = treeGo.AddComponent<CapsuleCollider>();
            cap.direction = dir;
            // 中心: 幹の中点 = 世界 (tx, capHeight/2, tz) をローカルへ変換
            cap.center = tr.InverseTransformPoint(new Vector3(tx, capHeight * 0.5f, tz));
            // height/radius はローカル値。CapsuleCollider は direction 軸を lossyScale[dir]、
            // 半径を他2軸の最大 lossyScale で拡大するため、世界寸法へ割り戻す。
            Vector3 ls = tr.lossyScale;
            float axisScale   = dir == 0 ? Mathf.Abs(ls.x) : dir == 1 ? Mathf.Abs(ls.y) : Mathf.Abs(ls.z);
            float radialScale = dir == 0 ? Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z))
                              : dir == 1 ? Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z))
                                         : Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y));
            cap.height = capHeight / Mathf.Max(1e-4f, axisScale);
            cap.radius = Mathf.Max(0.25f, capHeight * 0.06f) / Mathf.Max(1e-4f, radialScale);
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
    /// 目形(アーモンド/vesica piscis)境界の矩形グリッド地面メッシュを生成する(M-A 立体化)。
    /// halfX/halfZ の矩形を cellSize 間隔でグリッド分割し、各頂点を目形の内外判定にかける。
    /// 外側頂点は原点からの radial 二分法(15回)で境界へスナップする(アーモンドは原点から見て
    /// 星形凸なので、放射方向の射影で必ず正しい境界点に乗る)。セルの4頂点が全て外側なら
    /// 三角形を生成しない。頂点の y は MapHeightModel.Height(x,z) をスナップ後座標で評価する。
    /// </summary>
    private static Mesh CreateGridGroundMesh(float halfX, float halfZ, float cellSize, float eyeR, float eyeB)
    {
        int cols = Mathf.CeilToInt((halfX * 2f) / cellSize);
        int rows = Mathf.CeilToInt((halfZ * 2f) / cellSize);
        int vertsPerRow = cols + 1;

        var rawPos = new Vector3[vertsPerRow * (rows + 1)];
        var inside = new bool[rawPos.Length];

        for (int j = 0; j <= rows; j++)
        {
            for (int i = 0; i <= cols; i++)
            {
                float x = -halfX + i * cellSize;
                float z = -halfZ + j * cellSize;
                int idx = j * vertsPerRow + i;

                bool isInside = IsInsideAlmond(x, z, eyeR, eyeB);
                inside[idx] = isInside;

                if (!isInside)
                {
                    (x, z) = SnapToAlmondBoundary(x, z, eyeR, eyeB);
                }

                rawPos[idx] = new Vector3(x, MapHeightModel.Height(x, z), z);
            }
        }

        var verts = new List<Vector3>(rawPos.Length);
        var tris = new List<int>();

        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < cols; i++)
            {
                int i00 = j * vertsPerRow + i;
                int i10 = j * vertsPerRow + (i + 1);
                int i01 = (j + 1) * vertsPerRow + i;
                int i11 = (j + 1) * vertsPerRow + (i + 1);

                // 4頂点すべてが外側(境界スナップ済み)のセルは面を張らない
                if (!inside[i00] && !inside[i10] && !inside[i01] && !inside[i11]) continue;

                int base0 = verts.Count;
                verts.Add(rawPos[i00]);
                verts.Add(rawPos[i10]);
                verts.Add(rawPos[i01]);
                verts.Add(rawPos[i11]);

                // CCW from +Y: (00,01,11) と (00,11,10)
                tris.Add(base0 + 0); tris.Add(base0 + 2); tris.Add(base0 + 3);
                tris.Add(base0 + 0); tris.Add(base0 + 3); tris.Add(base0 + 1);
            }
        }

        var mesh = new Mesh { name = "GridGround" };
        mesh.indexFormat = verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 目形(アーモンド)内側判定。OutOfBoundsLogic.IsOutOfBounds と同式(上下2大円の AND 内側)。
    private static bool IsInsideAlmond(float x, float z, float eyeR, float eyeB)
    {
        float r2 = eyeR * eyeR;
        float dUpper = x * x + (z + eyeB) * (z + eyeB);
        float dLower = x * x + (z - eyeB) * (z - eyeB);
        return dUpper <= r2 && dLower <= r2;
    }

    // 原点から (x,z) 方向への radial 二分法で目形境界へスナップする。アーモンドは原点から見て
    // 星形凸(どの方向にも境界との交点が唯一)なので、この射影で必ず正しい境界点に乗る。
    private static (float x, float z) SnapToAlmondBoundary(float x, float z, float eyeR, float eyeB)
    {
        float dist = Mathf.Sqrt(x * x + z * z);
        if (dist < 1e-5f) return (0f, 0f); // 原点は常に内側なのでこの分岐には到達しない想定

        float nx = x / dist;
        float nz = z / dist;

        float lo = 0f;      // 内側(原点)
        float hi = dist;    // 外側(元の座標)
        for (int iter = 0; iter < 15; iter++)
        {
            float mid = (lo + hi) * 0.5f;
            if (IsInsideAlmond(nx * mid, nz * mid, eyeR, eyeB)) lo = mid;
            else hi = mid;
        }
        float snapped = (lo + hi) * 0.5f;
        return (nx * snapped, nz * snapped);
    }

    /// <summary>
    /// XZ 平面の円弧帯 [innerR, outerR] を高さ方向 [0, height] へ押し出した「閉じたチューブ片」を生成する。
    /// 面: 内周面・外周面・上面・両端面（startDeg/endDeg を塞ぐ）。底面は地中のため省略。
    /// MeshCollider（非凸=片面判定）専用のため、内周面・外周面は両向きの三角形を張って両面化する。
    /// 角度は度・CCW、円弧は origin 中心の XZ 平面上に置く（GO 位置でベース中心へ移す）。
    /// </summary>
    private static Mesh CreateWallBandMesh(float innerR, float outerR, float height,
        int segments, float startDeg, float endDeg, Vector3 center = default, bool floorAtZero = false)
    {
        if (segments < 1) segments = 1;

        var mesh  = new Mesh { name = "WallBand" };
        int rings = segments + 1;

        // 各角度ステップで 4 頂点: 0=内下 1=内上 2=外下 3=外上
        // 地形追従(境界チューブのプラトー区間対策): y はワールド座標(center+ローカル)で
        // MapHeightModel.Height を評価し、従来の 0/height オフセットへ加算する。
        // クレーター/川/ジャングル高台ブロブ帯では Height=0 のため見た目は不変。
        // floorAtZero 指定時は底のみ絶対 y=0 まで下げる(地形が持ち上がる区間の壁下空洞を封鎖)。
        var verts = new Vector3[rings * 4];
        for (int i = 0; i < rings; i++)
        {
            float t   = (float)i / segments;
            float deg = Mathf.Lerp(startDeg, endDeg, t);
            float rad = deg * Mathf.Deg2Rad;
            float c   = Mathf.Cos(rad);
            float s   = Mathf.Sin(rad);

            float innerX = innerR * c, innerZ = innerR * s;
            float outerX = outerR * c, outerZ = outerR * s;
            float innerGroundY = MapHeightModel.Height(center.x + innerX, center.z + innerZ);
            float outerGroundY = MapHeightModel.Height(center.x + outerX, center.z + outerZ);
            float innerBaseY   = floorAtZero ? Mathf.Min(0f, innerGroundY) : innerGroundY;
            float outerBaseY   = floorAtZero ? Mathf.Min(0f, outerGroundY) : outerGroundY;

            int b = i * 4;
            verts[b + 0] = new Vector3(innerX, innerBaseY,            innerZ); // 内下
            verts[b + 1] = new Vector3(innerX, innerGroundY + height, innerZ); // 内上
            verts[b + 2] = new Vector3(outerX, outerBaseY,            outerZ); // 外下
            verts[b + 3] = new Vector3(outerX, outerGroundY + height, outerZ); // 外上
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
        int segments, float startDeg, float endDeg, Vector3 center = default, bool floorAtZero = false)
    {
        if (segments < 1) segments = 1;

        var mesh  = new Mesh { name = "WallBandVisual" };
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        // 地形追従: y はローカルオフセット(0/height)に加え、ワールド座標での
        // MapHeightModel.Height を足し込む(CreateWallBandMesh と同じ方式)。
        // floorAtZero 指定時は底(y=0 オフセット)のみ絶対 y=0 まで下げる(衝突メッシュと一致させ、
        // 地形メッシュの量子化欠けから壁下の空洞が覗いても壁面で塞がって見えるようにする)。
        Vector3 P(float deg, float r, float y)
        {
            float rad = deg * Mathf.Deg2Rad;
            float x = r * Mathf.Cos(rad);
            float z = r * Mathf.Sin(rad);
            float groundY = MapHeightModel.Height(center.x + x, center.z + z);
            float baseY   = floorAtZero ? Mathf.Min(0f, groundY) : groundY;
            return new Vector3(x, y <= 0f ? baseY : groundY + y, z);
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
