using UnityEngine;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Core;

namespace Enigma.GameMode
{
    /// <summary>
    /// 3v3 フルボット編成の composition root（Humble Object）。
    /// 試合開始時、各ボットへピックキャラ（プレイヤーピックを除外して決定的に割当）を適用する。
    /// モデルスワップ・ステータス反映は ChampionModelSwapper / EnemyChampionAI に委譲し、
    /// 本クラスは Unity の組み立てのみを担う。
    /// </summary>
    public sealed class BotChampionBootstrap : MonoBehaviour
    {
        [SerializeField] private CharacterDatabase _database;
        [SerializeField] private EnemyChampionAI[] _bots;

        // プレイヤー未ピック時のフォールバック。除外対象に使う。
        private const string DefaultPlayerPick = "zeph";

        // 編成を毎回同じにするための固定シード（Date 等の非決定的入力は使わない）。
        // BalanceSimRunner がバランスシム実行時のみ Overrides で上書きする。
        private const int AssignSeed = 20260612;

        // BalanceSimRunner がシム開始前にセットする override。null なら通常プレイの固定シードを使う。
        // Start より前にシーンロード直後の一瞬で設定する必要があるため static（Runner が単一起動前提）。
        private static int? s_seedOverride;

        /// <summary>
        /// バランスシム用: 次回 Start 時に使うシードを指定する。指定するとチーム別シャッフル
        /// （AssignPerTeam: 同チーム内重複なし、青赤間の重複は許可）になり、Player 除外は行わない
        /// （シムには Player が参加しないため）。1試合限りの override（Runner が毎試合明示的に設定し直す）。
        /// </summary>
        public static void SetSimSeed(int seed)
        {
            s_seedOverride = seed;
        }

        /// <summary>バランスシム終了後、通常プレイの固定シードへ戻す。</summary>
        public static void ClearSimOverride()
        {
            s_seedOverride = null;
        }

        // 直近の Start で確定したロースター（bots[i] に対応する CharId）。BalanceSimRunner が読み取る。
        public string[] LastAssignment { get; private set; }

        private void Start()
        {
            if (_database == null || _bots == null || _bots.Length == 0) return;

            // シーン単体起動など未初期化パスへの保険（MatchBootstrap 同様）
            if (!GameServices.IsInitialized)
                GameServices.Initialize();

            var allIds = CollectIds();
            string[] assignment;

            if (s_seedOverride.HasValue)
            {
                // シムモード: チームごとに独立シャッフル（TeamTag で青/赤を判定）。
                // Player は参加しないため除外なし。チームサイズは半々前提。
                int teamSize = _bots.Length / 2;
                var perTeam = BotRosterAssignment.AssignPerTeam(allIds, s_seedOverride.Value, teamSize);
                assignment = new string[_bots.Length];

                int blueIdx = 0, redIdx = 0;
                for (int i = 0; i < _bots.Length; i++)
                {
                    var bot = _bots[i];
                    var tag = bot != null ? bot.GetComponent<TeamTag>() : null;
                    if (tag != null && tag.Team == TeamId.Red)
                        assignment[i] = redIdx < teamSize ? perTeam[teamSize + redIdx++] : "";
                    else
                        assignment[i] = blueIdx < teamSize ? perTeam[blueIdx++] : "";
                }
            }
            else
            {
                int seed = AssignSeed;
                var picked = GameServices.Match?.PickedCharacter;
                string playerPick = picked != null ? picked.CharId : DefaultPlayerPick;
                assignment = BotRosterAssignment.Assign(allIds, playerPick, seed, _bots.Length);
            }

            LastAssignment = assignment;

            for (int i = 0; i < _bots.Length; i++)
            {
                var bot = _bots[i];
                if (bot == null) continue;

                var data = FindById(assignment[i]);
                if (data == null) continue;

                // 見た目（3Dモデル）を差し替える。UnityChan/未結線は既存モデル維持（戻り値 null）。
                var model = ChampionModelSwapper.Apply(bot.gameObject, data);
                if (model != null)
                {
                    var switcher = model.GetComponentInChildren<LocomotionClipSwitcher>();
                    bot.SetClipSwitcher(switcher);
                }

                bot.ApplyCharacter(data);
            }
        }

        private string[] CollectIds()
        {
            var list = _database.Characters;
            var ids = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                ids[i] = list[i] != null ? list[i].CharId : null;
            return ids;
        }

        private CharacterData FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in _database.Characters)
                if (c != null && c.CharId == id) return c;
            return null;
        }
    }
}
