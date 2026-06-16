using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Enigma.Ability;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Core;
using Enigma.Data;

namespace Enigma.Sandbox
{
    /// <summary>
    /// キャラ試用シーン（Sandbox）の司令塔。
    /// M キーでキャラ一覧オーバーレイを開き、選択したキャラの見た目・スキル・ステータスを
    /// 即座にプレイヤーへ適用する。スキルは全スロット習得済み状態にしてすべて使えるようにする。
    ///
    /// MatchBootstrap（GameServices.Match.PickedCharacter 依存）の代わりに本クラスが
    /// キャラ適用を担うため、Sandbox のプレイヤーには MatchBootstrap を付けない。
    /// </summary>
    public sealed class CharacterSandbox : MonoBehaviour
    {
        [SerializeField] private CharacterDatabase _database;
        [SerializeField] private GameObject _player;

        // プレイヤーから解決する各サブシステム参照。
        private SkillCaster _skillCaster;
        private HealthComponent _health;
        private AutoAttack _autoAttack;
        private PlayerController _controller;

        private bool _menuOpen;
        private bool _relicMenuOpen;
        private int _currentIndex = -1;

        // サンドボックスで選択中のレリック ID（最大 3）。キャラ適用時にまとめて反映する。
        private const int MaxRelics = 3;
        private readonly List<string> _selectedRelics = new List<string>();

        // OnGUI スタイル（OnGUI 内で遅延生成する）。
        private bool _stylesReady;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _itemCurrentStyle;
        private Texture2D _panelTex;
        private Texture2D _itemTex;
        private Texture2D _itemCurrentTex;

        private void Awake()
        {
            // HUD など他コンポーネントの Start より前に初期化しておく。
            if (!GameServices.IsInitialized)
                GameServices.Initialize();
        }

        private void Start()
        {
            ResolveRefs();

            // 既定キャラ（先頭）を適用しておく。
            if (_database != null && _database.Characters != null && _database.Characters.Count > 0)
                ApplyCharacter(0);
        }

        private void ResolveRefs()
        {
            if (_player == null) return;
            _skillCaster = _player.GetComponent<SkillCaster>();
            _health      = _player.GetComponent<HealthComponent>();
            _autoAttack  = _player.GetComponent<AutoAttack>();
            _controller  = _player.GetComponent<PlayerController>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.mKey.wasPressedThisFrame)
                ToggleCharMenu();
            else if (kb.rKey.wasPressedThisFrame)
                ToggleRelicMenu();
            else if (kb.escapeKey.wasPressedThisFrame)
                CloseMenus();
        }

        private void ToggleCharMenu()
        {
            _menuOpen = !_menuOpen;
            if (_menuOpen) _relicMenuOpen = false;
            SyncPause();
        }

        private void ToggleRelicMenu()
        {
            _relicMenuOpen = !_relicMenuOpen;
            if (_relicMenuOpen) _menuOpen = false;
            SyncPause();
        }

        private void CloseMenus()
        {
            _menuOpen = false;
            _relicMenuOpen = false;
            SyncPause();
        }

        // いずれかのメニューが開いている間はゲームを一時停止してモーダルにする。
        private void SyncPause()
        {
            bool open = _menuOpen || _relicMenuOpen;
            Time.timeScale = open ? 0f : 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // 選択キャラを見た目・スキル・ステータスに反映する（MatchBootstrap のミラー）。
        private void ApplyCharacter(int index)
        {
            if (_database == null || _database.Characters == null) return;
            if (index < 0 || index >= _database.Characters.Count) return;

            var data = _database.Characters[index];
            if (data == null) return;
            _currentIndex = index;

            // 見た目（3D モデル）。連続切替に対応した ChampionModelSwapper を使う。
            ChampionModelSwapper.Apply(_player, data);

            // スキルセット差し替え + 全スロット習得（試用なので全スキル即発動可能にする）。
            if (_skillCaster != null)
            {
                if (data.Skills != null)
                    _skillCaster.SetSkills(data.Skills);
                _skillCaster.Progression?.GrantAllRanks(1);
            }

            // ステータス（HP/AA/移動速度）。HP は差分加算で寄せて全回復する。
            if (_health != null && data.BaseHp > 0f)
            {
                float delta = data.BaseHp - _health.Model.MaxHp;
                if (Mathf.Abs(delta) > 0.001f)
                    _health.Model.AddMaxHp(delta);
                _health.Model.Heal(_health.Model.MaxHp);
            }

            if (_autoAttack != null)
            {
                _autoAttack.Configure(data.AttackDamage, data.AttackRange, data.AttackCooldown);
                _autoAttack.SetChampion(data.CharId);
            }

            if (_controller != null && data.MoveSpeed > 0f)
                _controller.SetMoveSpeed(data.MoveSpeed);

            // レリックの集約効果を最後に重ねる（ステータスを基準値へ戻した直後なので二重適用にならない）。
            RelicApplier.ApplyIds(_selectedRelics, _health != null ? _health.Model : null, _skillCaster, _player);
        }

        // レリック選択を更新し、現在キャラを再適用してステータスを基準から組み直す。
        private void ToggleRelic(string id)
        {
            if (_selectedRelics.Contains(id))
                _selectedRelics.Remove(id);
            else if (_selectedRelics.Count < MaxRelics)
                _selectedRelics.Add(id);

            // 試合フローと同じ保存先にも反映しておく。
            if (GameServices.Match != null)
                GameServices.Match.SelectedRelicIds = new List<string>(_selectedRelics);

            if (_currentIndex >= 0)
                ApplyCharacter(_currentIndex);
        }

        // ── IMGUI（キャラ一覧 + 常時ヒント） ───────────────────────

        private void OnGUI()
        {
            EnsureStyles();

            // 常時ヒント（左上）。
            string current = (_currentIndex >= 0 && _database != null
                              && _currentIndex < _database.Characters.Count
                              && _database.Characters[_currentIndex] != null)
                ? _database.Characters[_currentIndex].DisplayName
                : "-";
            GUI.Label(new Rect(12f, 10f, 700f, 24f),
                $"[M] キャラ変更   [R] レリック({_selectedRelics.Count}/{MaxRelics})   現在: {current}", _hintStyle);

            if (!_menuOpen && !_relicMenuOpen) return;

            // 半透明の暗幕。
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (_menuOpen) DrawCharMenu();
            else DrawRelicMenu();
        }

        private void DrawCharMenu()
        {
            int count = _database != null && _database.Characters != null ? _database.Characters.Count : 0;

            const float panelW = 420f;
            float rowH = 52f;
            float headH = 64f;
            float footH = 36f;
            float panelH = headH + footH + rowH * Mathf.Max(count, 1) + 24f;
            float px = (Screen.width - panelW) * 0.5f;
            float py = (Screen.height - panelH) * 0.5f;

            GUI.Box(new Rect(px, py, panelW, panelH), GUIContent.none, _panelStyle);
            GUI.Label(new Rect(px, py + 14f, panelW, 32f), "キャラクター選択", _titleStyle);

            float y = py + headH;
            for (int i = 0; i < count; i++)
            {
                var data = _database.Characters[i];
                if (data == null) continue;

                var rect = new Rect(px + 16f, y, panelW - 32f, rowH - 8f);
                bool isCurrent = i == _currentIndex;

                var swatch = new Rect(rect.x, rect.y, 6f, rect.height);
                GUI.color = data.ThemeColor.a > 0f ? data.ThemeColor : Color.gray;
                GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                GUI.color = Color.white;

                string role = string.IsNullOrEmpty(data.RoleLabelRaw) ? data.Role.ToString() : data.RoleLabelRaw;
                string label = $"  {data.DisplayName}\n  <size=11><color=#9fb0c8>{role}</color></size>";

                if (GUI.Button(rect, label, isCurrent ? _itemCurrentStyle : _itemStyle))
                {
                    ApplyCharacter(i);
                    CloseMenus();
                }
                y += rowH;
            }

            GUI.Label(new Rect(px, py + panelH - footH, panelW, 24f),
                "クリックで切替 / [M] または [Esc] で閉じる", _hintStyle);
        }

        private void DrawRelicMenu()
        {
            var all = RelicCatalog.All;

            const float panelW = 460f;
            float rowH = 56f;
            float headH = 64f;
            float footH = 36f;
            float panelH = headH + footH + rowH * all.Count + 24f;
            float px = (Screen.width - panelW) * 0.5f;
            float py = (Screen.height - panelH) * 0.5f;

            GUI.Box(new Rect(px, py, panelW, panelH), GUIContent.none, _panelStyle);
            GUI.Label(new Rect(px, py + 14f, panelW, 32f),
                $"レリック選択（{_selectedRelics.Count}/{MaxRelics}）", _titleStyle);

            float y = py + headH;
            for (int i = 0; i < all.Count; i++)
            {
                var info = all[i];
                bool selected = _selectedRelics.Contains(info.Id);
                bool atLimit = !selected && _selectedRelics.Count >= MaxRelics;

                var rect = new Rect(px + 16f, y, panelW - 32f, rowH - 8f);
                string mark = selected ? "✓ " : (atLimit ? "× " : "＋ ");
                string label = $"  {mark}{info.DisplayName}\n  <size=11><color=#9fb0c8>{info.Description}</color></size>";

                GUI.enabled = !atLimit;
                if (GUI.Button(rect, label, selected ? _itemCurrentStyle : _itemStyle))
                    ToggleRelic(info.Id);
                GUI.enabled = true;

                y += rowH;
            }

            GUI.Label(new Rect(px, py + panelH - footH, panelW, 24f),
                "クリックで付け外し（最大3） / [R] または [Esc] で閉じる", _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _panelTex       = SolidTex(new Color(0.10f, 0.12f, 0.17f, 0.98f));
            _itemTex        = SolidTex(new Color(0.16f, 0.19f, 0.26f, 1f));
            _itemCurrentTex = SolidTex(new Color(0.20f, 0.34f, 0.52f, 1f));

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = _panelTex;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
            };
            _titleStyle.normal.textColor = Color.white;

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                richText = true,
            };
            _hintStyle.normal.textColor = new Color(0.82f, 0.86f, 0.92f);

            _itemStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 16,
                richText = true,
            };
            _itemStyle.normal.background = _itemTex;
            _itemStyle.hover.background  = _itemCurrentTex;
            _itemStyle.normal.textColor  = Color.white;
            _itemStyle.hover.textColor   = Color.white;

            _itemCurrentStyle = new GUIStyle(_itemStyle);
            _itemCurrentStyle.normal.background = _itemCurrentTex;
        }

        private static Texture2D SolidTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
