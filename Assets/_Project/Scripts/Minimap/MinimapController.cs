using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Enigma.Combat;

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

        // ---- 定数 ----

        // マップの世界座標範囲（円形マップ: 半径75 の正方形に内包）
        private static readonly Rect WorldBounds = new Rect(-75f, -75f, 150f, 150f);

        // ミニマップパネルのピクセルサイズ（GameHud.uss の hud-minimap に合わせる）
        private static readonly Vector2 PanelSize = new Vector2(160f, 160f);

        // ドットの基準サイズ（通常ユニット 6px、自分・タワー等 8px）
        private const float DotSizeNormal = 6f;
        private const float DotSizeLarge  = 8f;

        // ---- ランタイム ----

        private VisualElement _mapPanel;

        // ドットプール: TeamTag インスタンス → VisualElement
        private readonly Dictionary<TeamTag, VisualElement> _dotPool
            = new Dictionary<TeamTag, VisualElement>();

        // 収集済み対象リスト（0.5 秒ごとに再収集）
        private TeamTag[] _targets = System.Array.Empty<TeamTag>();

        private void OnEnable()
        {
            if (_uiDocument == null) return;

            _mapPanel = _uiDocument.rootVisualElement.Q<VisualElement>("hud-minimap");
            if (_mapPanel == null) return;

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
                    dot = CreateDot(target);
                    _dotPool[target] = dot;
                    _mapPanel.Add(dot);
                }

                // 生存判定: HealthComponent があれば IsDead、なければ activeInHierarchy で判断
                bool alive = IsAlive(target);
                dot.style.display = alive ? DisplayStyle.Flex : DisplayStyle.None;

                if (!alive) continue;

                // ドットサイズ（タワー・タイタンは 8px 角）
                float dotSize = IsStructure(target.gameObject) ? DotSizeLarge : DotSizeNormal;

                var mapPos = MinimapMath.WorldToMap(target.transform.position, WorldBounds, PanelSize);

                // left/top はドット中心合わせ（半サイズ引く）
                dot.style.left = mapPos.x - dotSize * 0.5f;
                dot.style.top  = mapPos.y - dotSize * 0.5f;
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

        private VisualElement CreateDot(TeamTag target)
        {
            var dot = new VisualElement();
            dot.AddToClassList("minimap-dot");

            bool isStructure = IsStructure(target.gameObject);
            bool isSelf      = target.CompareTag("Player");

            // チーム色クラス
            switch (target.Team)
            {
                case TeamId.Blue:    dot.AddToClassList("minimap-dot--blue");    break;
                case TeamId.Red:     dot.AddToClassList("minimap-dot--red");     break;
                default:             dot.AddToClassList("minimap-dot--neutral"); break;
            }

            // 自分: 白・枠付き
            if (isSelf)
                dot.AddToClassList("minimap-dot--self");

            // タワー・タイタン: 正方形
            if (isStructure)
                dot.AddToClassList("minimap-dot--square");

            return dot;
        }

        private static bool IsAlive(TeamTag target)
        {
            var hc = target.GetComponent<HealthComponent>();
            if (hc != null)
                return hc.Model != null && !hc.Model.IsDead;

            return target.gameObject.activeInHierarchy;
        }

        // GameObject 名に "Tower" または "Titan" を含む場合を構造物と判定する。
        // Inspector での tag 設定を要求しない簡易判定（名前規約は BuildAetherRiftMap 準拠）。
        private static bool IsStructure(GameObject go)
        {
            string n = go.name;
            return n.Contains("Tower") || n.Contains("Titan");
        }

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
            }
        }
    }
}
