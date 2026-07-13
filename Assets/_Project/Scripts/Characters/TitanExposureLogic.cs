using System.Collections.Generic;

namespace Enigma.Character
{
    /// <summary>
    /// 「レーン開通=タイタン露出」判定の純粋ロジック。UnityEngine 非依存。
    /// LoL のインヒビター相当ルール: 1レーンの両タワー(外+内)が破壊されたら、
    /// そのレーンを通してタイタンが攻撃対象になる。
    /// </summary>
    public static class TitanExposureLogic
    {
        /// <summary>
        /// 敵タワー一覧(生死・所属レーンID)から、タイタンが露出しているか判定する。
        /// いずれかの laneId で、そのレーンに属する全タワーが死んでいれば true。
        /// 該当レーンにタワーが1基も含まれていない場合はそのレーンを判定対象にしない(安全側=false)。
        /// </summary>
        public static bool IsTitanExposed(IReadOnlyList<(bool isAlive, int laneId)> enemyTowers)
        {
            if (enemyTowers == null || enemyTowers.Count == 0) return false;

            // レーンIDごとに「生存タワーが1基でもあるか」を集計する。
            var laneHasAlive = new Dictionary<int, bool>();
            for (int i = 0; i < enemyTowers.Count; i++)
            {
                var (isAlive, laneId) = enemyTowers[i];
                if (!laneHasAlive.ContainsKey(laneId))
                    laneHasAlive[laneId] = false;
                if (isAlive)
                    laneHasAlive[laneId] = true;
            }

            foreach (var kvp in laneHasAlive)
            {
                if (!kvp.Value) return true; // このレーンは全滅=開通
            }
            return false;
        }
    }
}
