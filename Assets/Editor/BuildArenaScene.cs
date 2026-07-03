using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Enigma.Combat;
using Enigma.Learning;

namespace Enigma.EditorTools
{
    // BuildSandbox.cs の流儀を踏襲した ML-Agents 学習用ミニアリーナのビルダー。
    // 1v1 self-play ミクロ戦闘（B1）の観測/行動契約に沿った Fighter を 2 体配置する。
    public static class BuildArenaScene
    {
        private const string ScenePath = "Assets/Scenes/Arena.unity";
        private const string MatDir = "Assets/_Project/Materials/Arena";

        private static readonly Vector3 ArenaCenter = Vector3.zero;
        private const float ArenaRadius = 20f;

        [MenuItem("Enigma/Build Arena Scene")]
        public static void Execute()
        {
            EnsureDir(MatDir);
            EnsureDir("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildFloor();
            BuildWalls();
            BuildLight();

            // Blue = 遠隔のPPO学習者 / Red = 近接のスクリプト追跡者(HeuristicOnly)
            var blue = BuildFighter("Fighter_Blue", new Vector3(-8f, 1.1f, 0f), TeamId.Blue,
                new Color(0.25f, 0.45f, 0.90f), new Color(0.30f, 0.85f, 1f), isMeleeChaser: false);
            var red = BuildFighter("Fighter_Red", new Vector3(8f, 1.1f, 0f), TeamId.Red,
                new Color(0.90f, 0.30f, 0.25f), new Color(1f, 0.45f, 0.30f), isMeleeChaser: true);

            WireFighters(blue, red);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildArenaScene] Arena.unity を保存しました。");
        }

        private readonly struct FighterHandles
        {
            public readonly GameObject Go;
            public readonly ArenaFighter Fighter;
            public readonly MicroDuelAgent Agent;
            public readonly Vector3 SpawnPos;

            public FighterHandles(GameObject go, ArenaFighter fighter, MicroDuelAgent agent, Vector3 spawnPos)
            {
                Go = go;
                Fighter = fighter;
                Agent = agent;
                SpawnPos = spawnPos;
            }
        }

        private static void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "ArenaFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            // Cylinder メッシュは半径0.5・高さ2。半径20相当にするには scaleXZ=40。
            floor.transform.localScale = new Vector3(40f, 0.5f, 40f);
            // 既知の落とし穴: Cylinder 既定の CapsuleCollider は扁平スケールで球面化し、
            // 床全体が見えないドーム斜面になる(ファイターが外周へ滑り落ちて戻れない)。
            // MeshCollider に差し替えて平坦な床にする。
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<MeshCollider>();
            SetMat(floor, GetOrCreateMat("ArenaFloor", new Color(0.20f, 0.23f, 0.28f)));
        }

        // 外周の低い壁を 16 分割の簡易リングで配置し、はみ出しを防ぐ。
        private static void BuildWalls()
        {
            const int segments = 16;
            var wallMat = GetOrCreateMat("ArenaWall", new Color(0.35f, 0.37f, 0.42f));
            var wallsRoot = new GameObject("ArenaWalls");

            float segmentAngle = Mathf.PI * 2f / segments;
            float chordLength = 2f * ArenaRadius * Mathf.Sin(segmentAngle * 0.5f) * 1.05f; // 隙間防止に少し延長

            for (int i = 0; i < segments; i++)
            {
                float angle = segmentAngle * i;
                Vector3 pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ArenaRadius;

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{i}";
                wall.transform.SetParent(wallsRoot.transform, false);
                wall.transform.position = pos + Vector3.up * 1f;
                wall.transform.rotation = Quaternion.LookRotation(pos.normalized) * Quaternion.Euler(0f, 90f, 0f);
                wall.transform.localScale = new Vector3(chordLength, 2f, 0.5f);
                SetMat(wall, wallMat);
            }
        }

        private static void BuildLight()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.96f, 0.88f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            lightGo.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
        }

        // 非対称デュエル(v2): 対称ステータス+回避不能な自動攻撃では移動が結果に因果を持たず、
        // self-play が「棒立ち相打ち」に収束した(v1で実測)。遠隔=PPO学習者 / 近接=スクリプト追跡者
        // (やや速い)にすることで、カイト=学習すべき技術が明確な因果と報酬を持つ。
        private static FighterHandles BuildFighter(
            string name, Vector3 pos, TeamId team, Color bodyColor, Color beamColor,
            bool isMeleeChaser)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = pos;
            SetMat(go, GetOrCreateMat(name + "_Body", bodyColor));

            // primitive 標準の CapsuleCollider は CharacterController と重複するため除去
            var primitiveCollider = go.GetComponent<Collider>();
            if (primitiveCollider != null)
                Object.DestroyImmediate(primitiveCollider);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;

            var teamTag = go.AddComponent<TeamTag>();
            var soTeam = new SerializedObject(teamTag);
            soTeam.FindProperty("_team").enumValueIndex = (int)team;
            soTeam.ApplyModifiedPropertiesWithoutUndo();

            var health = go.AddComponent<HealthComponent>();
            var soHealth = new SerializedObject(health);
            // 近接追跡者は硬く痛く(接近を許すと大損害)、遠隔学習者は柔らかい(被弾が重い教師信号)
            soHealth.FindProperty("_maxHp").floatValue = isMeleeChaser ? 300f : 200f;
            soHealth.ApplyModifiedPropertiesWithoutUndo();

            var fighter = go.AddComponent<ArenaFighter>();
            var soFighter = new SerializedObject(fighter);
            soFighter.FindProperty("_beamColor").colorValue = beamColor;
            if (isMeleeChaser)
            {
                soFighter.FindProperty("_attackRange").floatValue = 3.5f;
                soFighter.FindProperty("_attackDamage").floatValue = 30f;
                soFighter.FindProperty("_attackCooldown").floatValue = 1.2f;
                // 単純後退では逃げ切れない=空間と角度を使うカイトを学ばせるため、わずかに速くする
                soFighter.FindProperty("_moveSpeed").floatValue = 6.5f;
            }
            soFighter.ApplyModifiedPropertiesWithoutUndo();

            // Agent は [RequireComponent(typeof(BehaviorParameters))] を持つため、
            // AddComponent<MicroDuelAgent> の時点で BehaviorParameters は自動付与済み
            var agent = go.AddComponent<MicroDuelAgent>();
            // 決着が付かないエピソードの打ち切り(約60秒: Academy 50step/s × 60)
            agent.MaxStep = 3000;

            var behaviorParams = go.GetComponent<BehaviorParameters>();
            behaviorParams.BehaviorName = "MicroDuel";
            behaviorParams.BrainParameters.VectorObservationSize = 11;
            behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
            behaviorParams.TeamId = team == TeamId.Blue ? 0 : 1;
            // 近接追跡者は学習せず常にスクリプト(CombatMicroModel)で動く対戦相手
            if (isMeleeChaser)
                behaviorParams.BehaviorType = BehaviorType.HeuristicOnly;

            var decisionRequester = go.AddComponent<DecisionRequester>();
            decisionRequester.DecisionPeriod = 5;

            return new FighterHandles(go, fighter, agent, pos);
        }

        // 相互参照(_enemy/_enemyFighter/_spawnPos 等)を SerializedObject で結線する。
        private static void WireFighters(FighterHandles blue, FighterHandles red)
        {
            WireFighter(blue, red);
            WireFighter(red, blue);
        }

        private static void WireFighter(FighterHandles self, FighterHandles enemy)
        {
            var soFighter = new SerializedObject(self.Fighter);
            soFighter.FindProperty("_enemy").objectReferenceValue = enemy.Fighter;
            soFighter.ApplyModifiedPropertiesWithoutUndo();

            var soAgent = new SerializedObject(self.Agent);
            soAgent.FindProperty("_fighter").objectReferenceValue = self.Fighter;
            soAgent.FindProperty("_enemyFighter").objectReferenceValue = enemy.Fighter;
            soAgent.FindProperty("_spawnPos").vector3Value = self.SpawnPos;
            soAgent.FindProperty("_arenaCenter").vector3Value = ArenaCenter;
            soAgent.FindProperty("_arenaRadius").floatValue = ArenaRadius;
            soAgent.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static Material GetOrCreateMat(string name, Color color)
        {
            var path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.SetColor("_BaseColor", color);
                return existing;
            }

            var shader = Shader.Find("Enigma/Toon") ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void SetMat(GameObject go, Material mat)
        {
            var mr = go.GetComponent<Renderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }
    }
}
