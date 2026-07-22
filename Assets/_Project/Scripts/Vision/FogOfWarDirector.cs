using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Enigma.Combat;
using Enigma.Character;
using Enigma.Map;

namespace Enigma.Vision
{
    /// <summary>
    /// Fog of War のランタイム制御。一定間隔で味方視界源と敵動的ユニットを集め、
    /// <see cref="VisionRevealModel"/> で可視判定し、見えない敵の描画(Renderer/Canvas)を隠す。
    /// AI の知覚(ボットの Sense)とは独立で、あくまで「人間プレイヤーの見え方」だけを制御する。
    /// 静的構造物(タワー/タイタン=CharacterController 非保持)は常時表示(対象にしない)。
    /// </summary>
    public sealed class FogOfWarDirector : MonoBehaviour
    {
        private const float UpdateInterval = 0.2f;
        private const float LingerSeconds  = 1.0f;   // 見失い後の猶予(チラつき防止)
        private const float ChampionSight  = 14f;
        private const float MinionSight    = 8f;
        private const float TowerSight     = 12f;
        private const float EyeHeight      = 1.6f;   // 接地yからの目線オフセット(視界2.0: 高低差/LoS 判定用)

        // マップシーンでのみ自動生成(メニュー/選択シーンでは動かさない)。
        // AfterSceneLoad はプロセス開始後1回しか走らず、本体は DontDestroyOnLoad でもないため、
        // BalanceSimRunner 等が2試合目のために AetherRift_Map を再ロードすると破棄されたまま
        // 再生成されない(CentralObjectiveDirector と同じ既知の穴)。sceneLoaded 購読で毎回補充する。
        private static bool _sceneLoadedHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (!_sceneLoadedHooked)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                _sceneLoadedHooked = true;
            }
            TrySpawn();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TrySpawn();

        private static void TrySpawn()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "AetherRift_Map") return;
            if (FindObjectOfType<FogOfWarDirector>() != null) return;
            var go = new GameObject("FogOfWarDirector");
            go.AddComponent<FogOfWarDirector>();
        }

        /// <summary>ミニマップ等が可視判定を問い合わせるためのアクセサ。</summary>
        public static FogOfWarDirector Instance { get; private set; }

        /// <summary>
        /// ユニット以外が与える視界源（ワード/スキャン/リフト等）。キー毎に最新の1点を保持し、
        /// Tick で味方チームのものを視界源へ合流する。設置者が Set/Remove する（位置は静止前提）。
        /// </summary>
        public readonly struct ExternalVisionSource
        {
            public readonly float X;
            public readonly float Z;
            public readonly float Radius;
            public readonly TeamId Team;

            public ExternalVisionSource(float x, float z, float radius, TeamId team)
            {
                X = x; Z = z; Radius = radius; Team = team;
            }
        }

        private static readonly Dictionary<object, ExternalVisionSource> _externalSources =
            new Dictionary<object, ExternalVisionSource>();

        /// <summary>外部視界源を登録/更新する（key は設置物インスタンス等の一意トークン）。</summary>
        public static void SetExternalSource(object key, float x, float z, float radius, TeamId team)
        {
            if (key == null || radius <= 0f) return;
            _externalSources[key] = new ExternalVisionSource(x, z, radius, team);
        }

        /// <summary>外部視界源を取り除く（ワード破壊/寿命切れ時）。</summary>
        public static void RemoveExternalSource(object key)
        {
            if (key == null) return;
            _externalSources.Remove(key);
        }

        private VisionRevealModel _model;
        private TeamId            _playerTeam   = TeamId.Neutral;
        private bool              _teamResolved;
        private float             _timer;

        // 隠す対象(敵動的ユニット)の描画キャッシュ
        private sealed class Foggable
        {
            public GameObject   Go;
            public Renderer[]   Renderers;
            public Canvas[]     Canvases;
            public Collider[]   Colliders; // ターゲット用 CapsuleCollider 等(CharacterController は含まれない)
            public bool         Visible = true;
        }

        private readonly Dictionary<int, Foggable> _foggables = new Dictionary<int, Foggable>();
        private readonly List<VisionSource>        _sources   = new List<VisionSource>();
        private readonly List<VisionTarget>        _targets   = new List<VisionTarget>();
        private readonly HashSet<int>              _seenThisTick = new HashSet<int>();

        private void Awake()
        {
            _model = new VisionRevealModel(LingerSeconds, new RaycastLineOfSightChecker());
            Instance = this;
            // static レジストリは前試合の残りを持ちうるので、マップ初期化時に一掃する
            _externalSources.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 指定 GameObject（GetInstanceID）が「霧で隠すべき敵動的ユニットで、かつ現在不可視」なら true。
        /// 追跡対象外（味方・構造物・未登録）は false（=隠さない）。ミニマップのドット表示判定に使う。
        /// </summary>
        public bool IsHidden(int instanceId)
        {
            return _foggables.TryGetValue(instanceId, out var fog) && !fog.Visible;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = UpdateInterval;

            // プレイヤーチーム未解決のまま Tick すると味方を敵と誤判定して隠す恐れがある。解決まで待つ。
            if (!_teamResolved)
            {
                ResolvePlayerTeam();
                if (!_teamResolved) return;
            }
            Tick();
        }

        private void ResolvePlayerTeam()
        {
            var player = FindObjectOfType<PlayerController>();
            if (player == null) return; // まだ生成前。次の tick で再試行
            var tag = player.GetComponentInParent<TeamTag>();
            _playerTeam   = tag != null ? tag.Team : TeamId.Neutral;
            _teamResolved = true;
        }

        private void Tick()
        {
            _sources.Clear();
            _targets.Clear();
            _seenThisTick.Clear();

            // TeamTag を持つ全ユニットを走査(3v3 規模 + ミニオンで数十体、5Hz なら許容)
            var tags = FindObjectsByType<TeamTag>(FindObjectsSortMode.None);
            foreach (var tag in tags)
            {
                if (tag == null) continue;
                var go = tag.gameObject;

                bool friendly = _teamResolved && tag.Team == _playerTeam;
                bool isChampion = go.GetComponent<PlayerController>() != null
                               || go.GetComponent<EnemyChampionAI>() != null;
                bool hasCc      = go.GetComponent<CharacterController>() != null;
                bool isTower    = go.GetComponent<Enigma.Objective.TowerAttack>() != null;

                if (friendly)
                {
                    // 味方は視界源。静的タワーも視界を与える。
                    float radius = isChampion ? ChampionSight : hasCc ? MinionSight : isTower ? TowerSight : 0f;
                    if (radius > 0f)
                    {
                        var p = go.transform.position;
                        // 目線yは transform.y でなく地形高から求める。CharacterController の中心オフセットが
                        // ユニット種別で異なり(チャンプ1.08/ミニオン0.08)、transform 基準だと差がちょうど
                        // 高低差しきい値1.0になって「ミニオンから敵チャンプが見えない」偽遮蔽が起きた(実測)。
                        float eyeY = MapHeightModel.Height(p.x, p.z) + EyeHeight;
                        int brushId = FindBrushId(p);
                        _sources.Add(new VisionSource(p.x, p.z, radius, eyeY, brushId));
                    }
                    continue;
                }

                // 非味方の動的ユニット(敵/中立のチャンピオン・ミニオン等)のみ霧で隠す対象。
                // 静的構造物(CC 非保持)は常時表示=対象にしない。
                if (!hasCc) continue;

                int id = go.GetInstanceID();
                _seenThisTick.Add(id);
                var pos = go.transform.position;
                float targetEyeY = MapHeightModel.Height(pos.x, pos.z) + EyeHeight;
                int targetBrushId = FindBrushId(pos);
                _targets.Add(new VisionTarget(id, pos.x, pos.z, targetEyeY, targetBrushId));

                if (!_foggables.TryGetValue(id, out var fog))
                {
                    fog = new Foggable
                    {
                        Go        = go,
                        Renderers = go.GetComponentsInChildren<Renderer>(true),
                        Canvases  = go.GetComponentsInChildren<Canvas>(true),
                        // Unity では CharacterController も Collider を継承するため除外する。
                        // 移動用 CC は残し、ターゲット用 CapsuleCollider 等だけを隠れている間オフにする
                        // (これを切ると霧の中で敵が移動できなくなる=透明壁修正で踏んだ罠)。
                        Colliders = System.Array.FindAll(
                            go.GetComponentsInChildren<Collider>(true),
                            c => !(c is CharacterController)),
                    };
                    _foggables[id] = fog;
                }
            }

            // ワード/スキャン等の外部視界源のうち、味方チームのものを合流する。
            // 設置者(ワード等)の接地位置は個々に把握していないため、目線は固定の EyeHeight・茂み判定なしで扱う。
            if (_externalSources.Count > 0)
            {
                foreach (var kv in _externalSources)
                {
                    var src = kv.Value;
                    if (_teamResolved && src.Team == _playerTeam)
                        _sources.Add(new VisionSource(src.X, src.Z, src.Radius, EyeHeight, -1));
                }
            }

            _model.Update(_sources, _targets, UpdateInterval);

            // 可視/不可視を反映
            foreach (var kv in _foggables)
            {
                var fog = kv.Value;
                if (fog.Go == null) continue;
                bool visible = _model.IsVisible(kv.Key);
                if (visible == fog.Visible) continue;
                fog.Visible = visible;
                SetVisible(fog, visible);
            }

            CleanupDestroyed();
        }

        private static void SetVisible(Foggable fog, bool visible)
        {
            if (fog.Renderers != null)
            {
                for (int i = 0; i < fog.Renderers.Length; i++)
                    if (fog.Renderers[i] != null) fog.Renderers[i].enabled = visible;
            }
            if (fog.Colliders != null)
            {
                // 隠れている間は当たり判定も切る(透明な壁＆不可視ターゲットの解消)。CharacterController は対象外なので移動は継続。
                for (int i = 0; i < fog.Colliders.Length; i++)
                    if (fog.Colliders[i] != null) fog.Colliders[i].enabled = visible;
            }
            if (fog.Canvases != null)
            {
                for (int i = 0; i < fog.Canvases.Length; i++)
                    if (fog.Canvases[i] != null) fog.Canvases[i].enabled = visible;
            }
        }

        // 破棄済み or 今回走査で見かけなかった敵をキャッシュから除去。
        // 走査外になった敵は描画を戻してから捨てる(再出現時に隠れたままを防ぐ)。
        private readonly List<int> _toRemove = new List<int>();
        private void CleanupDestroyed()
        {
            _toRemove.Clear();
            foreach (var kv in _foggables)
            {
                if (kv.Value.Go == null) { _toRemove.Add(kv.Key); continue; }
                if (!_seenThisTick.Contains(kv.Key))
                {
                    if (!kv.Value.Visible) { kv.Value.Visible = true; SetVisible(kv.Value, true); }
                    _toRemove.Add(kv.Key);
                }
            }
            for (int i = 0; i < _toRemove.Count; i++)
                _foggables.Remove(_toRemove[i]);
        }

        // BrushZone.Active を線形走査し、pos を含む最初のゾーンの index を返す(非該当は -1)。
        // ゾーン数は現状12個程度で毎tick数十ユニット走査しても軽量なため単純な線形探索でよい。
        private static int FindBrushId(Vector3 pos)
        {
            var zones = BrushZone.Active;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null && zones[i].Contains(pos))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Physics.RaycastAll 相当(NonAlloc)でソース目線→ターゲット目線の間を遮る構造物の有無を判定する。
        /// 遮蔽体は <see cref="VisionBlockerTag"/> を持つコライダー、または名前が "Ground" の地形メッシュのみ。
        /// ユニット本体(CharacterController は Physics.Raycast に無関係、CapsuleCollider 等も無視)は遮蔽対象にしない。
        /// </summary>
        private sealed class RaycastLineOfSightChecker : ILineOfSightChecker
        {
            // 区間長ぴったりのヒット(ソース/ターゲット自身の当たり判定等)を遮蔽と誤認しないための余裕。
            private const float DistanceMargin = 0.1f;
            private const int   MaxHits = 32;

            private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

            public bool HasLineOfSight(in VisionSource source, in VisionTarget target)
            {
                var from = new Vector3(source.X, source.Y, source.Z);
                var to   = new Vector3(target.X, target.Y, target.Z);
                Vector3 delta = to - from;
                float distance = delta.magnitude;
                if (distance <= 0.0001f) return true;
                Vector3 direction = delta / distance;

                int hitCount = Physics.RaycastNonAlloc(from, direction, _hits, distance, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hitCount; i++)
                {
                    var hit = _hits[i];
                    if (hit.distance >= distance - DistanceMargin) continue; // 区間端(ターゲット自身等)は無視
                    if (IsBlocker(hit.collider)) return false;
                }
                return true;
            }

            private static bool IsBlocker(Collider collider)
            {
                if (collider == null) return false;
                if (collider is CharacterController) return false;
                if (collider.GetComponent<VisionBlockerTag>() != null) return true;
                return collider.gameObject.name == "Ground";
            }
        }
    }
}
