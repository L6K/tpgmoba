using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Enigma.Combat;
using Enigma.Character;

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

        // マップシーンでのみ自動生成(メニュー/選択シーンでは動かさない)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "AetherRift_Map") return;
            if (FindObjectOfType<FogOfWarDirector>() != null) return;
            var go = new GameObject("FogOfWarDirector");
            go.AddComponent<FogOfWarDirector>();
        }

        /// <summary>ミニマップ等が可視判定を問い合わせるためのアクセサ。</summary>
        public static FogOfWarDirector Instance { get; private set; }

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
            _model = new VisionRevealModel(LingerSeconds);
            Instance = this;
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
                        _sources.Add(new VisionSource(p.x, p.z, radius));
                    }
                    continue;
                }

                // 非味方の動的ユニット(敵/中立のチャンピオン・ミニオン等)のみ霧で隠す対象。
                // 静的構造物(CC 非保持)は常時表示=対象にしない。
                if (!hasCc) continue;

                int id = go.GetInstanceID();
                _seenThisTick.Add(id);
                var pos = go.transform.position;
                _targets.Add(new VisionTarget(id, pos.x, pos.z));

                if (!_foggables.TryGetValue(id, out var fog))
                {
                    fog = new Foggable
                    {
                        Go        = go,
                        Renderers = go.GetComponentsInChildren<Renderer>(true),
                        Canvases  = go.GetComponentsInChildren<Canvas>(true),
                        // CharacterController は Collider を継承しないので含まれない=移動は維持しつつ
                        // ターゲット用 CapsuleCollider 等だけを隠れている間オフにできる(透明な壁の解消)
                        Colliders = go.GetComponentsInChildren<Collider>(true),
                    };
                    _foggables[id] = fog;
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
    }
}
