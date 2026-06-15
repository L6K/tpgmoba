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
        private const int AssignSeed = 20260612;

        private void Start()
        {
            if (_database == null || _bots == null || _bots.Length == 0) return;

            // シーン単体起動など未初期化パスへの保険（MatchBootstrap 同様）
            if (!GameServices.IsInitialized)
                GameServices.Initialize();

            var picked = GameServices.Match?.PickedCharacter;
            string playerPick = picked != null ? picked.CharId : DefaultPlayerPick;

            var allIds = CollectIds();
            var assignment = BotRosterAssignment.Assign(allIds, playerPick, AssignSeed, _bots.Length);

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
