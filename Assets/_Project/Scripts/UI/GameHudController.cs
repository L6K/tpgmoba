using UnityEngine;
using UnityEngine.UIElements;
using Enigma.Abilities;
using Enigma.Audio;
using Enigma.Combat;
using Enigma.Ability;
using Enigma.Core;
using Enigma.Data;
using Enigma.Character;
using Enigma.GameModes;

namespace Enigma.UI
{
    /// <summary>
    /// ゲーム内 HUD の毎フレーム更新を担うハンブルオブジェクト。
    /// 要素の取得・バインドは OnEnable で行い、Update は値の書き込みのみ。
    /// </summary>
    public sealed class GameHudController : MonoBehaviour
    {
        [SerializeField] private UIDocument      _uiDocument;
        [SerializeField] private HealthComponent _playerHealth;
        [SerializeField] private SkillCaster     _skillCaster;

        // タイマー
        private Label _timerLabel;
        private Label _objectiveLabel;
        private Label _hintLabel;
        private TeamId _playerTeam = TeamId.Blue;
        private float  _hintTimer;

        // HP バー
        private VisualElement _hpFill;
        private VisualElement _hpDamage;
        private VisualElement _hpShield;
        private VisualElement _hpShieldOver;
        private Label         _hpText;
        private VisualElement _hpBarBg;
        private float         _lastMaxHp = -1f;

        // チームバフ残り時間ラベル
        private Label _buffLabel;

        // 中央コア制圧アナウンスの検知用。CaptureCount の増分で発火する
        private int _lastCaptureCount;

        // レベル・XP 表示
        private Label         _levelLabel;
        private VisualElement _xpFill;
        private PlayerProgression _playerProgression;

        // スキルスロット（4スロット分）
        private readonly VisualElement[] _skillSlots    = new VisualElement[4];
        private readonly Label[]         _skillNames    = new Label[4];
        private readonly Label[]         _skillKeys     = new Label[4];
        private readonly VisualElement[] _skillCdOverlay = new VisualElement[4];
        private readonly Label[]         _skillCdText   = new Label[4];
        private readonly VisualElement[] _skillIcons    = new VisualElement[4];

        // ランクピップ行・レベルアップ + ボタン（スロット 0..2）
        private readonly VisualElement[] _skillPipRows  = new VisualElement[3];
        private readonly Button[]        _skillLevelUp  = new Button[3];

        // スロットごとの最大ランク（Q/E=5, R=3）
        private static readonly int[] _maxRanks = { 5, 5, 3 };

        // スキルランク進行（SkillCaster が公開）。Changed で UI を再構築する
        private SkillProgression _progression;

        // ツールチップパネルとラベル
        private VisualElement _tooltip;
        private Label         _tooltipName;
        private Label         _tooltipRank;
        private Label         _tooltipStats;
        private Label         _tooltipDesc;

        // ピップ生成・クリック/ホバー登録の二重実行を防ぐ（OnEnable 再入対策）
        private bool _skillUiWired;

        // 所持金ラベル
        private Label  _goldLabel;
        private int    _lastGold = -1;

        // アイテムスロット（6枠）
        private readonly VisualElement[] _itemSlots    = new VisualElement[6];
        private readonly Label[]         _itemInitials = new Label[6];

        // Wallet / Items は _playerHealth と同一 GO から取得
        private PlayerWallet _playerWallet;
        private PlayerItems  _playerItems;

        // 戦闘フィードバック用要素（被弾ビネット・キルフィード・センターアナウンス）
        private VisualElement _damageVignette;
        // 低HP時に残す赤ビネットのベース不透明度。被弾フラッシュはこの値へ戻る。
        private float _lowHpVignette;
        private VisualElement _killFeed;
        private Label         _announce;

        // 死亡時の被ダメージ内訳パネル（UXML 非依存でランタイム生成・遅延初期化）
        private VisualElement _deathRecap;
        private Label         _deathRecapTitle;
        private Label         _deathRecapBody;

        // オーバークロックのチャージバー（UXML 非依存でランタイム生成・遅延初期化）
        private VisualElement _overclockBar;
        private VisualElement _overclockFill;
        private Label         _overclockLabel;

        // KillFeedDirector が結線するキルフィードモデル。Changed でフィード行を再構築する
        private KillFeedModel _killFeedModel;

        // チーム色（青=#6FA8FF / 赤=#FF7A6F）。キラーのチームで行色を決める
        private static readonly Color TeamColorBlue = new Color(0x6F / 255f, 0xA8 / 255f, 0xFF / 255f);
        private static readonly Color TeamColorRed  = new Color(0xFF / 255f, 0x7A / 255f, 0x6F / 255f);

        private void OnEnable()
        {
            // GameServices が未初期化の場合の保険（HomeScreenController と同様）
            if (!GameServices.IsInitialized) GameServices.Initialize();

            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

            _timerLabel = root.Q<Label>("hud-timer");
            _objectiveLabel = root.Q<Label>("hud-objective");
            _hintLabel  = root.Q<Label>("hud-hint");
            // 再有効化/再生成時に過去の制圧を新規増分扱いしないよう、現在値で同期する
            _lastCaptureCount = CentralObjectiveDirector.Instance != null
                ? CentralObjectiveDirector.Instance.CaptureCount : 0;
            _hpBarBg    = root.Q<VisualElement>("hud-hp-bar-bg");
            _hpFill     = root.Q<VisualElement>("hud-hp-fill");
            _hpDamage   = root.Q<VisualElement>("hud-hp-damage");
            _hpShield   = root.Q<VisualElement>("hud-hp-shield");
            _hpShieldOver = root.Q<VisualElement>("hud-hp-shield-over");
            _hpText     = root.Q<Label>("hud-hp-text");
            _buffLabel  = root.Q<Label>("hud-buff");
            _levelLabel = root.Q<Label>("hud-level");
            _xpFill     = root.Q<VisualElement>("hud-xp-fill");
            _goldLabel  = root.Q<Label>("hud-gold");

            for (int i = 0; i < 6; i++)
            {
                _itemSlots[i]    = root.Q<VisualElement>($"hud-item-slot-{i}");
                _itemInitials[i] = root.Q<Label>($"hud-item-initial-{i}");
            }

            // SerializedObject を増やさずに playerHealth と同一 GO から取得（null 安全）
            if (_playerHealth != null)
            {
                _playerProgression = _playerHealth.GetComponent<PlayerProgression>();
                _playerWallet      = _playerHealth.GetComponent<PlayerWallet>();
                _playerItems       = _playerHealth.GetComponent<PlayerItems>();
                var tt = _playerHealth.GetComponentInParent<TeamTag>();
                if (tt != null) _playerTeam = tt.Team;
            }

            // スロット 0..2（Q/E/R）のみ。slot3 は HUD に存在しない
            for (int i = 0; i < 3; i++)
            {
                _skillSlots[i]     = root.Q<VisualElement>($"hud-skill-{i}");
                _skillNames[i]     = root.Q<Label>($"hud-skill-name-{i}");
                _skillKeys[i]      = root.Q<Label>($"hud-skill-key-{i}");
                _skillCdOverlay[i] = root.Q<VisualElement>($"hud-skill-cd-{i}");
                _skillCdText[i]    = root.Q<Label>($"hud-skill-cdtext-{i}");
                _skillIcons[i]     = root.Q<VisualElement>($"hud-skill-icon-{i}");
                _skillPipRows[i]   = root.Q<VisualElement>($"hud-skill-pips-{i}");
                _skillLevelUp[i]   = root.Q<Button>($"hud-skill-levelup-{i}");
            }

            // ツールチップパネル
            _tooltip      = root.Q<VisualElement>("hud-skill-tooltip");
            _tooltipName  = root.Q<Label>("hud-tooltip-name");
            _tooltipRank  = root.Q<Label>("hud-tooltip-rank");
            _tooltipStats = root.Q<Label>("hud-tooltip-stats");
            _tooltipDesc  = root.Q<Label>("hud-tooltip-desc");

            // 戦闘フィードバック要素
            _damageVignette = root.Q<VisualElement>("hud-damage-vignette");
            _killFeed       = root.Q<VisualElement>("hud-killfeed");
            _announce       = root.Q<Label>("hud-announce");

            // OnEnable が再入してもモデルが既に結線済みなら再購読・再構築する
            if (_killFeedModel != null)
            {
                _killFeedModel.Changed += RebuildKillFeed;
                RebuildKillFeed();
            }

            SetupSkillProgressionUi();
        }

        private void OnDisable()
        {
            if (_progression != null)
                _progression.Changed -= OnProgressionChanged;
            if (_killFeedModel != null)
                _killFeedModel.Changed -= RebuildKillFeed;
        }

        // KillFeedDirector からキルフィードモデルを結線する。要素取得済みなら即購読・再構築する。
        public void BindKillFeed(KillFeedModel model)
        {
            if (_killFeedModel == model) return;
            if (_killFeedModel != null)
                _killFeedModel.Changed -= RebuildKillFeed;

            _killFeedModel = model;

            if (_killFeedModel != null && _killFeed != null)
            {
                _killFeedModel.Changed += RebuildKillFeed;
                RebuildKillFeed();
            }
        }

        // キルフィードを最新順に再構築する。キラーのチーム色で killer/victim 名を着色する
        private void RebuildKillFeed()
        {
            if (_killFeed == null || _killFeedModel == null) return;

            _killFeed.Clear();
            foreach (var e in _killFeedModel.Entries)
            {
                var row = new Label($"{e.KillerName} ▶ {e.VictimName}");
                row.AddToClassList("hud-killfeed-row");
                row.pickingMode = PickingMode.Ignore;
                row.style.color = new StyleColor(
                    e.KillerTeam == Enigma.Combat.TeamId.Blue ? TeamColorBlue : TeamColorRed);
                _killFeed.Add(row);
            }
        }

        // 被弾フラッシュが効いている終了時刻(Time.time)。この間は低HPビネット更新でopacityを動かさない。
        private float _flashUntilTime;

        // 後方互換: 既定強度のフラッシュ。
        public void FlashDamageVignette() => FlashDamageVignette(0.35f, 0.12f);

        // 被弾時に画面端の赤ビネットを alpha→ベース(低HPビネット)へフラッシュする（PlayerHitFeedback から呼ぶ）。
        // USS の transition(0.4s) に乗せるため、即時ピークをセットし、seconds 保持してからベースへ戻す。
        public void FlashDamageVignette(float alpha, float seconds)
        {
            if (_damageVignette == null) return;

            float hold = Mathf.Clamp(seconds, 0.05f, 1f);
            _flashUntilTime = Time.time + hold + 0.4f; // 0.4 = USS フェード分
            float peak = Mathf.Max(alpha, _lowHpVignette);
            _damageVignette.style.opacity = peak;
            _damageVignette.schedule.Execute(() => _damageVignette.style.opacity = _lowHpVignette)
                                     .StartingIn((long)(hold * 1000f));
        }

        // 低HP時に残す赤ビネットの強さ(0..1)を設定する（HP変化ごとに現在HPから更新される）。
        // フラッシュ中でなければ opacity を即ベースへ追従させる(回復/リスポーンで赤枠が消えるように上下とも追従)。
        public void SetLowHpVignette(float strength)
        {
            _lowHpVignette = Mathf.Clamp01(strength) * 0.45f;
            if (_damageVignette != null && Time.time >= _flashUntilTime)
                _damageVignette.style.opacity = _lowHpVignette;
        }

        // プレイヤーがキルした/された時にセンターアナウンスを 1.5 秒表示する。
        // killed=true で「倒された…」赤、false で「キル!」金色
        public void AnnounceKill(bool killed)
        {
            if (_announce == null) return;

            _announce.text = killed ? "倒された…" : "キル!";
            _announce.style.color = new StyleColor(
                killed ? TeamColorRed : new Color(0xEB / 255f, 0xC8 / 255f, 0x5A / 255f));

            _announce.style.opacity = 1f;
            _announce.schedule.Execute(() => _announce.style.opacity = 0f).StartingIn(1500);
        }

        // マルチキル/ストリーク/シャットダウン等の特別アナウンスを任意テキスト・色で表示する。
        public void AnnounceSpecial(string text, Color color, int holdMs = 1800)
        {
            if (_announce == null || string.IsNullOrEmpty(text)) return;

            _announce.text = text;
            _announce.style.color = new StyleColor(color);
            _announce.style.opacity = 1f;
            _announce.schedule.Execute(() => _announce.style.opacity = 0f).StartingIn(holdMs);
        }

        // 中央コアが制圧された時にセンターアナウンスを 1.5 秒表示する。
        // チーム色(青=水色/赤=赤)でテキストを着色する
        public void AnnounceObjectiveCaptured(TeamId team)
        {
            if (_announce == null) return;

            string teamName = team == TeamId.Red ? "赤" : "青";
            _announce.text = $"{teamName}チームが中央コアを制圧!";
            _announce.style.color = new StyleColor(team == TeamId.Red ? TeamColorRed : TeamColorBlue);

            _announce.style.opacity = 1f;
            _announce.schedule.Execute(() => _announce.style.opacity = 0f).StartingIn(1500);
        }

        // 死亡時の被ダメージ内訳を画面中央上に数秒間表示する。UXML を変更せずランタイム生成する。
        public void ShowDeathRecap(System.Collections.Generic.IReadOnlyList<RecapEntry> entries, int holdMs = 5000)
            => ShowDeathRecap("被ダメージ内訳", entries, holdMs);

        public void ShowDeathRecap(string title,
            System.Collections.Generic.IReadOnlyList<RecapEntry> entries, int holdMs = 5000)
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            EnsureDeathRecapPanel(root);

            _deathRecapTitle.text = title;

            var sb = new System.Text.StringBuilder();
            int shown = 0;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count && shown < 5; i++, shown++)
                {
                    var e = entries[i];
                    sb.Append(e.SourceId).Append("　")
                      .Append(Mathf.RoundToInt(e.TotalDamage)).Append(" (")
                      .Append(e.HitCount).Append("回)\n");
                }
            }
            if (shown == 0) sb.Append("（記録なし）");
            _deathRecapBody.text = sb.ToString().TrimEnd();

            _deathRecap.style.display = DisplayStyle.Flex;
            _deathRecap.style.opacity = 1f;
            _deathRecap.schedule.Execute(() => _deathRecap.style.opacity = 0f).StartingIn(holdMs);
        }

        private void EnsureDeathRecapPanel(VisualElement root)
        {
            if (_deathRecap != null) return;

            // 画面全幅・上から 24% の行に中央寄せのボックスを置く（translate 計算を避ける）。
            _deathRecap = new VisualElement();
            _deathRecap.pickingMode = PickingMode.Ignore;
            _deathRecap.style.position = Position.Absolute;
            _deathRecap.style.top = Length.Percent(24f);
            _deathRecap.style.left = 0;
            _deathRecap.style.right = 0;
            _deathRecap.style.alignItems = Align.Center;
            _deathRecap.style.display = DisplayStyle.None;

            var box = new VisualElement();
            box.pickingMode = PickingMode.Ignore;
            box.style.backgroundColor = new StyleColor(new Color(0.06f, 0.07f, 0.10f, 0.92f));
            box.style.paddingTop = 10;
            box.style.paddingBottom = 12;
            box.style.paddingLeft = 18;
            box.style.paddingRight = 18;
            box.style.borderTopLeftRadius = 8;
            box.style.borderTopRightRadius = 8;
            box.style.borderBottomLeftRadius = 8;
            box.style.borderBottomRightRadius = 8;
            box.style.minWidth = 260;

            _deathRecapTitle = new Label();
            _deathRecapTitle.style.color = new StyleColor(TeamColorRed);
            _deathRecapTitle.style.fontSize = 18;
            _deathRecapTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _deathRecapTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _deathRecapTitle.style.marginBottom = 6;

            _deathRecapBody = new Label();
            _deathRecapBody.style.color = new StyleColor(new Color(0.90f, 0.92f, 0.96f));
            _deathRecapBody.style.fontSize = 15;
            _deathRecapBody.style.whiteSpace = WhiteSpace.Normal;
            _deathRecapBody.style.unityTextAlign = TextAnchor.MiddleCenter;

            box.Add(_deathRecapTitle);
            box.Add(_deathRecapBody);
            _deathRecap.Add(box);
            root.Add(_deathRecap);
        }

        // ピップ生成・ボタンクリック登録・ホバー登録を一度だけ行う
        private void SetupSkillProgressionUi()
        {
            _progression = _skillCaster != null ? _skillCaster.Progression : null;
            if (_progression != null)
                _progression.Changed += OnProgressionChanged;

            // ピップ・クリック・ホバーの登録は一度だけ（OnEnable 再入で重複させない）
            if (_skillUiWired) return;
            _skillUiWired = true;

            for (int i = 0; i < 3; i++)
            {
                int slot = i; // クロージャ用にコピー

                // ピップノッチを最大ランク数だけ生成
                if (_skillPipRows[slot] != null)
                {
                    _skillPipRows[slot].Clear();
                    for (int p = 0; p < _maxRanks[slot]; p++)
                    {
                        var pip = new VisualElement();
                        pip.AddToClassList("hud-skill-pip");
                        pip.pickingMode = PickingMode.Ignore;
                        _skillPipRows[slot].Add(pip);
                    }
                }

                // + ボタン: クリックで TryLevelUp。picking を明示
                if (_skillLevelUp[slot] != null)
                {
                    _skillLevelUp[slot].pickingMode = PickingMode.Position;
                    _skillLevelUp[slot].clicked += () => OnLevelUpClicked(slot);
                }

                // スロットのホバーでツールチップ表示/非表示
                if (_skillSlots[slot] != null)
                {
                    _skillSlots[slot].pickingMode = PickingMode.Position;
                    _skillSlots[slot].RegisterCallback<MouseEnterEvent>(_ => ShowTooltip(slot));
                    _skillSlots[slot].RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());
                }
            }
        }

        private void OnProgressionChanged()
        {
            // ランク変化を即時 UI に反映（毎フレームの RefreshSkillProgressionUi でも追従するが
            // クリック直後の即応性のためここでも更新）
            RefreshSkillProgressionUi();
        }

        private void OnLevelUpClicked(int slot)
        {
            // ボタン押下自体のクリック音。ランクアップ成立時のみ昇格音を重ねる
            GameSfx.PlayUi("ui_click", 0.6f);

            if (_progression == null || _skillCaster == null) return;
            if (_progression.TryLevelUp(slot, _skillCaster.ChampionLevel))
                GameSfx.PlayUi("rank_up", 0.8f);
        }

        private void Update()
        {
            UpdateTimer();
            UpdateObjective();
            UpdateHint();
            UpdateHp();
            UpdateSkills();
            UpdateBuff();
            UpdateLevelXp();
            UpdateGoldAndItems();
            UpdateOverclock();
        }

        // オーバークロックのチャージ率を画面中央下のバーで表示する。未チャージ時は隠す。
        private void UpdateOverclock()
        {
            if (_skillCaster == null || _uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            float charge = _skillCaster.CurrentOverclockCharge01();
            if (charge <= 0f)
            {
                if (_overclockBar != null) _overclockBar.style.display = DisplayStyle.None;
                return;
            }

            EnsureOverclockBar(root);
            _overclockBar.style.display = DisplayStyle.Flex;
            _overclockFill.style.width = Length.Percent(Mathf.Clamp01(charge) * 100f);
            var c = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.25f, 0.2f), charge);
            _overclockFill.style.backgroundColor = new StyleColor(c);
            _overclockLabel.text = $"オーバークロック ⚡ {Mathf.RoundToInt(charge * 100f)}%";
        }

        private void EnsureOverclockBar(VisualElement root)
        {
            if (_overclockBar != null) return;

            _overclockBar = new VisualElement();
            _overclockBar.pickingMode = PickingMode.Ignore;
            _overclockBar.style.position = Position.Absolute;
            _overclockBar.style.bottom = Length.Percent(18f);
            _overclockBar.style.left = 0;
            _overclockBar.style.right = 0;
            _overclockBar.style.alignItems = Align.Center;
            _overclockBar.style.display = DisplayStyle.None;

            _overclockLabel = new Label();
            _overclockLabel.style.color = new StyleColor(new Color(1f, 0.9f, 0.5f));
            _overclockLabel.style.fontSize = 13;
            _overclockLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _overclockLabel.style.marginBottom = 3;

            var track = new VisualElement();
            track.pickingMode = PickingMode.Ignore;
            track.style.width = 220;
            track.style.height = 12;
            track.style.backgroundColor = new StyleColor(new Color(0.08f, 0.09f, 0.12f, 0.9f));
            track.style.borderTopLeftRadius = 6;
            track.style.borderTopRightRadius = 6;
            track.style.borderBottomLeftRadius = 6;
            track.style.borderBottomRightRadius = 6;
            track.style.overflow = Overflow.Hidden;

            _overclockFill = new VisualElement();
            _overclockFill.pickingMode = PickingMode.Ignore;
            _overclockFill.style.height = Length.Percent(100f);
            _overclockFill.style.width = Length.Percent(0f);
            _overclockFill.style.backgroundColor = new StyleColor(new Color(1f, 0.85f, 0.2f));

            track.Add(_overclockFill);
            _overclockBar.Add(_overclockLabel);
            _overclockBar.Add(track);
            root.Add(_overclockBar);
        }

        private void UpdateTimer()
        {
            if (_timerLabel == null) return;
            float elapsed = Time.timeSinceLevelLoad;
            int   minutes = (int)(elapsed / 60f);
            int   seconds = (int)(elapsed % 60f);
            _timerLabel.text = $"{minutes:D2}:{seconds:D2}";
        }

        // 中央オブジェクト(エニグマ・コア)の状態とカウントダウンを表示する
        private void UpdateObjective()
        {
            if (_objectiveLabel == null) return;

            var dir = CentralObjectiveDirector.Instance;

            // 制圧の検知: CaptureCount が増えたらアナウンスを出す
            if (dir != null && dir.CaptureCount > _lastCaptureCount)
            {
                _lastCaptureCount = dir.CaptureCount;
                AnnounceObjectiveCaptured(dir.LastCaptureTeam);
            }

            if (dir == null || !dir.HasObjective)
            {
                _objectiveLabel.style.display = DisplayStyle.None;
                return;
            }

            _objectiveLabel.style.display = DisplayStyle.Flex;
            _objectiveLabel.RemoveFromClassList("hud-objective--warning");
            _objectiveLabel.RemoveFromClassList("hud-objective--active");

            switch (dir.State)
            {
                case ObjectiveState.Active:
                    _objectiveLabel.text = "エニグマ・コア 出現中";
                    _objectiveLabel.AddToClassList("hud-objective--active");
                    break;
                case ObjectiveState.Warning:
                    _objectiveLabel.text = $"エニグマ・コア 出現まもなく ({Mathf.CeilToInt(dir.SecondsUntilSpawn)}s)";
                    _objectiveLabel.AddToClassList("hud-objective--warning");
                    break;
                default:
                    int s = Mathf.Max(0, Mathf.CeilToInt(dir.SecondsUntilSpawn));
                    _objectiveLabel.text = $"エニグマ・コア 出現まで {s / 60}:{s % 60:00}";
                    break;
            }
        }

        // 次の行動ガイド(MatchHint)を 0.5s ごとに更新する。
        private void UpdateHint()
        {
            if (_hintLabel == null) return;
            _hintTimer -= Time.deltaTime;
            if (_hintTimer > 0f) return;
            _hintTimer = 0.5f;

            if (_playerHealth == null || _playerHealth.Model == null)
            {
                _hintLabel.style.display = DisplayStyle.None;
                return;
            }

            float maxHp  = _playerHealth.Model.MaxHp;
            float hpFrac = maxHp > 0f ? Mathf.Clamp01(_playerHealth.Model.CurrentHp / maxHp) : 0f;
            int   gold   = _playerWallet != null ? _playerWallet.Wallet.Gold : 0;

            var dir       = CentralObjectiveDirector.Instance;
            bool objActive = dir != null && dir.State == ObjectiveState.Active;
            bool objWarn   = dir != null && dir.State == ObjectiveState.Warning;

            var ctx  = new MatchHintContext(hpFrac, gold, objActive, objWarn, AlliedMinionsNearPlayer());
            var hint = MatchHintModel.Select(in ctx);

            _hintLabel.style.display = DisplayStyle.Flex;
            _hintLabel.text = HintText(hint);
        }

        private static string HintText(MatchHint h)
        {
            switch (h)
            {
                case MatchHint.Retreat:          return "HPが低い。一度引こう";
                case MatchHint.ContestObjective: return "中央コア出現中! 確保しよう";
                case MatchHint.ObjectiveSoon:    return "まもなく中央コアが出現する";
                case MatchHint.BackToShop:       return "ゴールドが貯まった。帰還して装備更新";
                case MatchHint.PushWithMinions:  return "味方ミニオンと一緒にタワーを攻めよう";
                default:                         return "ファームでレベルとゴールドを稼ごう";
            }
        }

        // プレイヤー周辺(14m)に自チームのミニオンが居るか。
        private bool AlliedMinionsNearPlayer()
        {
            if (_playerHealth == null) return false;
            var hits = Physics.OverlapSphere(_playerHealth.transform.position, 14f);
            for (int i = 0; i < hits.Length; i++)
            {
                var m = hits[i].GetComponentInParent<Enigma.Minion.MinionAI>();
                if (m == null) continue;
                var tt = m.GetComponentInParent<TeamTag>();
                if (tt != null && tt.Team == _playerTeam) return true;
            }
            return false;
        }

        private void UpdateHp()
        {
            if (_playerHealth == null || _playerHealth.Model == null) return;
            var model   = _playerHealth.Model;
            float maxHp = model.MaxHp;
            float ratio = maxHp > 0f ? Mathf.Clamp01(model.CurrentHp / maxHp) : 0f;
            float pct   = ratio * 100f;

            // 最大 HP が変わったとき（初回含む）目盛りを再生成する
            if (!Mathf.Approximately(maxHp, _lastMaxHp))
            {
                _lastMaxHp = maxHp;
                RebuildHpTicks(maxHp);
            }

            if (_hpFill != null)
                _hpFill.style.width = Length.Percent(pct);

            // シールド帯: 現在HPの右端から「追加耐久」として描く（HP+シールドの合計が読み取りやすい）
            if (_hpShield != null)
            {
                float hpFrac     = maxHp > 0f ? Mathf.Clamp01(model.CurrentHp / maxHp) : 0f;
                float shieldFrac = maxHp > 0f ? Mathf.Clamp01(model.Shield / maxHp) : 0f;
                float fitW       = Mathf.Min(shieldFrac, 1f - hpFrac); // 空きHPに収まる分
                _hpShield.style.left  = Length.Percent(hpFrac * 100f);
                _hpShield.style.width = Length.Percent(fitW * 100f);

                // 空きに収まらない余剰分は、HPバー右端から左へ半透明オーバーレイで重ねる。
                // 満HP（hpFrac==1）でもシールドが帯として見えるようになる。
                if (_hpShieldOver != null)
                {
                    float overflow = Mathf.Clamp01(shieldFrac - fitW);
                    _hpShieldOver.style.left  = Length.Percent((1f - overflow) * 100f);
                    _hpShieldOver.style.width = Length.Percent(overflow * 100f);
                }
            }

            // ダメージトレイル: 減少時は delay 付き transition で遅れて縮む。回復時は即追従。
            if (_hpDamage != null)
            {
                bool healing = pct > _hpDamage.resolvedStyle.width / (_hpBarBg?.resolvedStyle.width ?? 1f) * 100f;
                if (healing)
                {
                    // 遅延なしで即追従（インライン transition-delay を 0 に上書き）
                    _hpDamage.style.transitionDelay = new StyleList<TimeValue>(
                        new System.Collections.Generic.List<TimeValue> { new TimeValue(0f, TimeUnit.Second) });
                }
                else
                {
                    // USS の delay (0.25s) に戻す
                    _hpDamage.style.transitionDelay = StyleKeyword.Null;
                }
                _hpDamage.style.width = Length.Percent(pct);
            }

            // 低 HP 警告クラス
            if (_hpFill != null)
                _hpFill.EnableInClassList("hud-hp-fill--low", ratio < 0.3f);

            if (_hpText != null)
                _hpText.text = $"{Mathf.CeilToInt(model.CurrentHp)} / {Mathf.CeilToInt(maxHp)}";
        }

        private void RebuildHpTicks(float maxHp)
        {
            if (_hpBarBg == null) return;

            // 既存 tick を削除
            var existing = _hpBarBg.Query<VisualElement>(className: "hud-hp-tick").ToList();
            foreach (var t in existing)
                t.RemoveFromHierarchy();

            int count = HealthBarTicks.InnerTickCount(maxHp);
            for (int i = 1; i <= count; i++)
            {
                float leftPct = HealthBarTicks.TickRatio(maxHp, i) * 100f;
                var tick = new VisualElement();
                tick.AddToClassList("hud-hp-tick");
                tick.style.position        = Position.Absolute;
                tick.style.left            = Length.Percent(leftPct);
                tick.style.top             = 0;
                tick.style.bottom          = 0;
                tick.style.width           = 1;
                tick.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));

                // fill の直後、hp-text の直前に挿入して描画順を制御
                int insertIndex = _hpBarBg.IndexOf(_hpFill) + 1;
                _hpBarBg.Insert(insertIndex, tick);
            }
        }

        private void UpdateSkills()
        {
            if (_skillCaster == null) return;

            // スロット 0..2（Q/E/R）のみ更新。slot3 は HUD に存在しない
            for (int i = 0; i < 3; i++)
            {
                var def = _skillCaster.GetSkill(i);

                // 未設定スロットは暗く表示して残処理をスキップ
                _skillSlots[i]?.EnableInClassList("hud-skill-slot--empty", def == null);

                if (_skillNames[i] != null)
                    _skillNames[i].text = def != null ? def.SkillName : "";

                // Targeting 種別に応じてアイコンクラスを排他付与（背景画像は USS が持つ）
                UpdateSkillIcon(i, def);

                // キーバインド表示
                if (_skillKeys[i] != null)
                {
                    var key = GameServices.ControlSettings?.GetSkillKey(i)
                              ?? GetFallbackKey(i);
                    _skillKeys[i].text = key.ToString();
                }

                // クールダウンオーバーレイ（上から fraction % 分を覆う）
                float fraction = _skillCaster.GetCooldownFraction(i);
                if (_skillCdOverlay[i] != null)
                    _skillCdOverlay[i].style.height = Length.Percent(fraction * 100f);

                // 残秒テキスト（0.1 秒単位。CD 中のみ表示）
                if (_skillCdText[i] != null)
                {
                    float remaining = _skillCaster.GetCooldownRemaining(i);
                    bool  active    = remaining > 0.05f;
                    _skillCdText[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                    if (active)
                        _skillCdText[i].text = remaining.ToString("F1");
                }
            }

            // ピップ・+ボタン・ロック表示の更新（変更時のみ）
            RefreshSkillProgressionUi();
        }

        // ランク変化やレベル変動に応じてピップ・ボタン・ロックを更新する。
        // ボタン位置はスロット座標が解決済みになってから合わせる
        private void RefreshSkillProgressionUi()
        {
            if (_progression == null) return;

            int championLevel = _skillCaster != null ? _skillCaster.ChampionLevel : 1;

            for (int i = 0; i < 3; i++)
            {
                int rank = _progression.GetRank(i);

                // ピップ充填
                var pipRow = _skillPipRows[i];
                if (pipRow != null)
                {
                    int childCount = pipRow.childCount;
                    for (int p = 0; p < childCount; p++)
                        pipRow[p].EnableInClassList("hud-skill-pip--filled", p < rank);
                }

                // + ボタン: CanLevelUp 時のみ表示し、スロット直上に配置
                var btn = _skillLevelUp[i];
                if (btn != null)
                {
                    bool canLevel = _progression.CanLevelUp(i, championLevel);
                    btn.style.display = canLevel ? DisplayStyle.Flex : DisplayStyle.None;
                    if (canLevel)
                        PositionLevelUpButton(i);
                }
            }
        }

        // + ボタンを対応スロット上端の内側に全幅で重ねる（hud-skills 基準の相対座標）。
        // パネル境界の外（負の top）はピッキング不能になるため内側配置が必須
        private void PositionLevelUpButton(int slot)
        {
            var btn  = _skillLevelUp[slot];
            var slotEl = _skillSlots[slot];
            if (btn == null || slotEl == null) return;

            float slotLeft  = slotEl.layout.x;
            float slotWidth = slotEl.layout.width;
            if (float.IsNaN(slotLeft) || float.IsNaN(slotWidth)) return; // レイアウト未解決
            btn.style.left  = slotLeft;
            btn.style.width = slotWidth;
        }

        // Targeting 種別に対応するアイコンクラスのみを残し、他を外す。
        // rank0（未習得）のスロットはロックアイコンに差し替え、種別クラスは外す
        private void UpdateSkillIcon(int slot, SkillDefinition def)
        {
            var icon = _skillIcons[slot];
            if (icon == null) return;

            bool locked = _progression != null && slot <= 2 && def != null
                          && _progression.GetRank(slot) <= 0;

            _skillSlots[slot]?.EnableInClassList("hud-skill-slot--locked", locked);

            bool directional = !locked && def != null && def.Targeting == SkillTargeting.Directional;
            bool aoe         = !locked && def != null && def.Targeting == SkillTargeting.GroundAoe;
            bool targeted    = !locked && def != null && def.Targeting == SkillTargeting.Targeted;

            icon.EnableInClassList("hud-skill-icon--locked", locked);
            icon.EnableInClassList("hud-skill-icon--directional", directional);
            icon.EnableInClassList("hud-skill-icon--aoe", aoe);
            icon.EnableInClassList("hud-skill-icon--targeted", targeted);
        }

        // ── ツールチップ ──────────────────────────────────────

        private void ShowTooltip(int slot)
        {
            if (_tooltip == null || _skillCaster == null) return;
            var def = _skillCaster.GetSkill(slot);
            if (def == null) { HideTooltip(); return; }

            int rank    = _progression != null ? _progression.GetRank(slot) : 0;
            int maxRank = (slot >= 0 && slot < _maxRanks.Length) ? _maxRanks[slot] : 5;
            float scale = SkillProgression.DamageMultiplier(rank);
            float dmg   = def.Damage * scale;

            if (_tooltipName  != null) _tooltipName.text  = def.SkillName;
            if (_tooltipRank  != null) _tooltipRank.text  = $"ランク {rank}/{maxRank}";
            if (_tooltipStats != null)
            {
                // rank0 はダメージ未発生のため 0 と明示
                string dmgText = rank > 0 ? Mathf.RoundToInt(dmg).ToString() : "0";
                _tooltipStats.text = $"ダメージ {dmgText}    CD {def.CooldownSeconds:0.#}秒";
            }
            if (_tooltipDesc != null) _tooltipDesc.text = def.Description;

            _tooltip.style.display = DisplayStyle.Flex;
        }

        private void HideTooltip()
        {
            if (_tooltip != null)
                _tooltip.style.display = DisplayStyle.None;
        }

        private void UpdateBuff()
        {
            if (_buffLabel == null) return;

            var buffs = GameServices.ObjectiveBuffs;
            if (buffs == null)
            {
                _buffLabel.style.display = DisplayStyle.None;
                return;
            }

            TeamId team = _playerHealth != null
                ? (_playerHealth.GetComponentInParent<TeamTag>()?.Team ?? TeamId.Blue)
                : TeamId.Blue;

            float now    = Time.time;
            var   active = buffs.GetActiveTypes(team, now);
            if (active == null || active.Count == 0)
            {
                _buffLabel.style.display = DisplayStyle.None;
                return;
            }

            float maxRemaining = 0f;
            var   sb = new System.Text.StringBuilder();
            foreach (var type in active)
            {
                if (sb.Length > 0) sb.Append('・');
                sb.Append(BuffTypeLabel(type));
                float r = buffs.GetRemainingSeconds(team, type, now);
                if (r > maxRemaining) maxRemaining = r;
            }

            _buffLabel.text = $"強化: {sb} ({Mathf.CeilToInt(maxRemaining)}s)";
            _buffLabel.style.display = DisplayStyle.Flex;
        }

        private static string BuffTypeLabel(ObjectiveBuffType type) => type switch
        {
            ObjectiveBuffType.Damage      => "ダメージ",
            ObjectiveBuffType.MinionPower => "ミニオン",
            ObjectiveBuffType.MoveSpeed   => "移動速度",
            ObjectiveBuffType.Shield      => "シールド",
            ObjectiveBuffType.TowerWeaken => "タワー弱体",
            _                             => type.ToString(),
        };

        private void UpdateLevelXp()
        {
            if (_playerProgression == null) return;
            var exp = _playerProgression.Experience;

            if (_levelLabel != null)
                _levelLabel.text = $"Lv.{exp.Level}";

            if (_xpFill != null)
            {
                // 最大レベル時は100%固定
                float ratio = exp.Level >= ExperienceModel.MaxLevel
                    ? 1f
                    : (exp.XpToNext > 0f ? Mathf.Clamp01(exp.CurrentXp / exp.XpToNext) : 0f);
                _xpFill.style.width = Length.Percent(ratio * 100f);
            }
        }

        // 所持金ラベルとアイテム6枠を更新する
        private void UpdateGoldAndItems()
        {
            if (_goldLabel != null && _playerWallet != null)
            {
                int gold = _playerWallet.Wallet.Gold;
                if (gold != _lastGold)
                {
                    bool increased = gold > _lastGold && _lastGold >= 0;
                    _lastGold = gold;
                    _goldLabel.text = $"{gold} G";

                    if (increased)
                    {
                        _goldLabel.AddToClassList("hud-gold--pulse");
                        _goldLabel.schedule.Execute(() =>
                            _goldLabel.RemoveFromClassList("hud-gold--pulse"))
                            .StartingIn(300);
                    }
                }
            }

            if (_playerItems == null) return;
            var items = _playerItems.Inventory.Items;

            for (int i = 0; i < 6; i++)
            {
                if (i < items.Count)
                {
                    var item = items[i];

                    // スロット背景色をアイテムのテーマカラーに設定。取得済みは金縁を付与
                    if (_itemSlots[i] != null)
                    {
                        _itemSlots[i].style.backgroundColor = new StyleColor(item.ThemeColor);
                        _itemSlots[i].EnableInClassList("hud-item-slot--filled", true);
                    }

                    // 頭文字1文字を表示
                    if (_itemInitials[i] != null)
                        _itemInitials[i].text = item.ItemName.Length > 0 ? item.ItemName[..1] : "?";
                }
                else
                {
                    // 空枠: 背景色をデフォルトに戻し、金縁を外して文字をクリア
                    if (_itemSlots[i] != null)
                    {
                        _itemSlots[i].style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.35f));
                        _itemSlots[i].EnableInClassList("hud-item-slot--filled", false);
                    }

                    if (_itemInitials[i] != null)
                        _itemInitials[i].text = "";
                }
            }
        }

        private static UnityEngine.InputSystem.Key GetFallbackKey(int slot) =>
            slot switch { 0 => UnityEngine.InputSystem.Key.Q,
                          1 => UnityEngine.InputSystem.Key.E,
                          2 => UnityEngine.InputSystem.Key.R,
                          _ => UnityEngine.InputSystem.Key.None };
    }
}
