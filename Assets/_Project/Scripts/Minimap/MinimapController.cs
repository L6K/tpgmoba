using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Enigma.Combat;
using Enigma.Vision;

namespace Enigma.Minimap
{
    /// <summary>
    /// ミニマップパネルに TeamTag 持ちオブジェクトのドットを描画する Humble Object。
    /// 変換ロジックは MinimapMath に切り出しており、このクラスは Unity API とのグルー層のみ担う。
    /// </summary>
    public sealed class MinimapController : MonoBehaviour
    {
        // ---- シリアライズ ----

        [SerializeField] private UIDocument _uiDocument;

        // コード側から背景・矢印アイコンを差し込む（USS の project:// URL が解決しない環境への保険）。
        [SerializeField] private Texture2D _mapBackground;
        [SerializeField] private Texture2D _arrowIcon;

        // ---- 定数 ----

        // マップの世界座標範囲（円形マップ: 半径75 の正方形に内包）
        private static readonly Rect WorldBounds = new Rect(-75f, -75f, 150f, 150f);

        // ミニマップパネルのピクセルサイズ（GameHud.uss の hud-minimap に合わせる）
        private static readonly Vector2 PanelSize = new Vector2(160f, 160f);

        // 各ドットの一辺ピクセル
        private const float SizeMinion    = 4f;   // ミニオン（小ドット）
        private const float SizeChampion  = 7f;   // 一般チャンピオン/中立小物
        private const float SizeArrow     = 14f;  // 自プレイヤー矢印
        private const float SizeStructure = 8f;   // タワー（菱形）
        private const float SizeTitan     = 11f;  // タイタン（大菱形）

        // ---- ランタイム ----

        private VisualElement _mapPanel;
        private Transform _player;

        // ドットプール: TeamTag インスタンス → VisualElement
        private readonly Dictionary<TeamTag, VisualElement> _dotPool
            = new Dictionary<TeamTag, VisualElement>();

        // 各ドットの一辺サイズ（中心合わせ用に保持）
        private readonly Dictionary<TeamTag, float> _dotSizes
            = new Dictionary<TeamTag, float>();

        // 収集済み対象リスト（0.5 秒ごとに再収集）
        private TeamTag[] _targets = System.Array.Empty<TeamTag>();

        private void OnEnable()
        {
            if (_uiDocument == null) return;

            _mapPanel = _uiDocument.rootVisualElement.Q<VisualElement>("hud-minimap");
            if (_mapPanel == null) return;

            // 背景テクスチャをコードから設定（円形 border-radius は USS 側で付与）。
            if (_mapBackground != null)
                _mapPanel.style.backgroundImage = new StyleBackground(_mapBackground);

            StartCoroutine(RefreshTargetsPeriodically());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void Update()
        {
            if (_mapPanel == null) return;

            foreach (var target in _targets)
            {
                if (target == null) continue;

                if (!_dotPool.TryGetValue(target, out var dot))
                {
                    dot = CreateDot(target, out float size);
                    _dotPool[target] = dot;
                    _dotSizes[target] = size;
                    _mapPanel.Add(dot);
                }

                bool alive = IsAlive(target);
                // Fog of War: 視界外の敵動的ユニットはミニマップからも隠す（味方・構造物は対象外）
                bool fogHidden = FogOfWarDirector.Instance != null
                              && FogOfWarDirector.Instance.IsHidden(target.gameObject.GetInstanceID());
                dot.style.display = (alive && !fogHidden) ? DisplayStyle.Flex : DisplayStyle.None;
                if (!alive || fogHidden) continue;

                float dotSize = _dotSizes.TryGetValue(target, out var s) ? s : SizeChampion;

                var mapPos = MinimapMath.WorldToMap(target.transform.position, WorldBounds, PanelSize);

                // left/top はドット中心合わせ（半サイズ引く）
                dot.style.left = mapPos.x - dotSize * 0.5f;
                dot.style.top  = mapPos.y - dotSize * 0.5f;

                // 自プレイヤー矢印は進行方向に回す。
                // ミニマップ上方向 = +Z = world forward(0,0,1) の向き。
                // Unity の Y オイラー角は +Z(北) を 0、時計回り（東向き）で増加。
                // UI Toolkit の rotate も時計回り正なので、Y 角をそのまま適用すると整合する。
                if (target.CompareTag("Player"))
                {
                    float yaw = target.transform.eulerAngles.y;
                    dot.style.rotate = new StyleRotate(new Rotate(new Angle(yaw, AngleUnit.Degree)));
                }
            }
        }

        // ---- コルーチン ----

        private IEnumerator RefreshTargetsPeriodically()
        {
            var interval = new WaitForSeconds(0.5f);
            while (true)
            {
                _targets = Object.FindObjectsByType<TeamTag>(FindObjectsSortMode.None);
                CleanStaleDotsFromPool();
                yield return interval;
            }
        }

        // ---- ヘルパー ----

        private VisualElement CreateDot(TeamTag target, out float size)
        {
            var dot = new VisualElement();
            dot.AddToClassList("minimap-dot");

            bool isSelf      = target.CompareTag("Player");
            bool isStructure = IsStructure(target.gameObject);
            bool isTitan     = IsTitan(target.gameObject);
            bool isMinion    = IsMinion(target.gameObject);

            // チーム色クラス
            switch (target.Team)
            {
                case TeamId.Blue:    dot.AddToClassList("minimap-dot--blue");    break;
                case TeamId.Red:     dot.AddToClassList("minimap-dot--red");     break;
                default:             dot.AddToClassList("minimap-dot--neutral"); break;
            }

            if (isSelf)
            {
                // 矢印アイコン（向き表示）。背景画像はコードから設定。
                dot.AddToClassList("minimap-dot--self");
                if (_arrowIcon != null)
                    dot.style.backgroundImage = new StyleBackground(_arrowIcon);
                size = SizeArrow;
            }
            else if (isTitan)
            {
                dot.AddToClassList("minimap-dot--diamond");
                dot.AddToClassList("minimap-dot--titan");
                size = SizeTitan;
            }
            else if (isStructure)
            {
                dot.AddToClassList("minimap-dot--diamond");
                size = SizeStructure;
            }
            else if (isMinion)
            {
                dot.AddToClassList("minimap-dot--minion");
                size = SizeMinion;
            }
            else
            {
                size = SizeChampion;
            }

            return dot;
        }

        private static bool IsAlive(TeamTag target)
        {
            var hc = target.GetComponent<HealthComponent>();
            if (hc != null)
                return hc.Model != null && !hc.Model.IsDead;

            return target.gameObject.activeInHierarchy;
        }

        // GameObject 名で種別を判定（名前規約は BuildAetherRiftMap 準拠）。
        private static bool IsStructure(GameObject go)
        {
            string n = go.name;
            return n.Contains("Tower") || n.Contains("Titan");
        }

        private static bool IsTitan(GameObject go) => go.name.Contains("Titan");

        private static bool IsMinion(GameObject go) => go.name.Contains("Minion");

        private void CleanStaleDotsFromPool()
        {
            // シーンから消えた TeamTag のドットを除去してメモリリークを防ぐ
            var toRemove = new List<TeamTag>();
            foreach (var kv in _dotPool)
            {
                if (kv.Key == null)
                    toRemove.Add(kv.Key);
            }
            foreach (var key in toRemove)
            {
                _dotPool[key]?.RemoveFromHierarchy();
                _dotPool.Remove(key);
                _dotSizes.Remove(key);
            }
        }
    }
}
