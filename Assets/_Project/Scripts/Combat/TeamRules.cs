namespace Enigma.Combat
{
    // フレンドリーファイア判定の単一ソース。MonoBehaviour に依存しない純ロジック。
    public static class TeamRules
    {
        // 同チーム同士のダメージのみ禁止する。Neutral は中立物のため、
        // 攻撃側・被弾側いずれかが Neutral なら常に許可する（誰でも/誰にでも攻撃可）。
        public static bool CanDamage(TeamId attacker, TeamId target)
        {
            if (attacker == TeamId.Neutral || target == TeamId.Neutral) return true;
            return attacker != target;
        }
    }
}
