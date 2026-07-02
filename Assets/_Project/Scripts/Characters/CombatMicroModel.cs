namespace Enigma.Character
{
    /// 戦闘マイクロ判断のコンテキスト（全て平面 XZ）。UnityEngine 非依存。
    public readonly struct MicroContext
    {
        public readonly float MyX, MyZ;
        public readonly float MyHpRatio;     // 0〜1
        public readonly float AttackRange;
        public readonly bool  AttackReady;   // AA クールダウン明けか
        public readonly bool  IsMelee;       // 射程<=7 の近接キャラか
        public readonly float TargetX, TargetZ;
        public readonly float TargetHpRatio;
        public readonly bool  HasThreat;     // 最寄りの敵チャンピオンがいるか（攻撃対象と同一でも可）
        public readonly float ThreatX, ThreatZ;

        public MicroContext(
            float myX, float myZ, float myHpRatio, float attackRange, bool attackReady, bool isMelee,
            float targetX, float targetZ, float targetHpRatio,
            bool hasThreat, float threatX, float threatZ)
        {
            MyX           = myX;
            MyZ           = myZ;
            MyHpRatio     = myHpRatio;
            AttackRange   = attackRange;
            AttackReady   = attackReady;
            IsMelee       = isMelee;
            TargetX       = targetX;
            TargetZ       = targetZ;
            TargetHpRatio = targetHpRatio;
            HasThreat     = hasThreat;
            ThreatX       = threatX;
            ThreatZ       = threatZ;
        }
    }

    public readonly struct MicroDecision
    {
        public readonly float MoveX, MoveZ; // 移動方向（正規化済 or 零ベクトル）
        public readonly bool  Attack;       // このフレーム AA してよいか

        public MicroDecision(float moveX, float moveZ, bool attack)
        {
            MoveX  = moveX;
            MoveZ  = moveZ;
            Attack = attack;
        }
    }

    public readonly struct FocusCandidate
    {
        public readonly float X, Z, HpRatio;
        public readonly bool  IsChampion;

        public FocusCandidate(float x, float z, float hpRatio, bool isChampion)
        {
            X          = x;
            Z          = z;
            HpRatio    = hpRatio;
            IsChampion = isChampion;
        }
    }

    public static class CombatMicroModel
    {
        // ヒステリシス: 現対象より一定以上 HP が低い候補でなければ乗り換えない（頻繁な振替防止）
        private const float FocusSwitchHpMargin = 0.15f;
        // 低HP離脱を発動する自HP閾値と、脅威が近いとみなす射程倍率
        private const float LowHpRatio       = 0.35f;
        private const float ThreatNearFactor = 1.2f;
        // 低HPオーバーレイのブレンド比（下がりつつ戦う: 既定移動0.7 / 脅威回避0.3）
        private const float OverlayKeepWeight   = 0.7f;
        private const float OverlayAvoidWeight  = 0.3f;

        public static MicroDecision Decide(in MicroContext ctx)
        {
            float dx = ctx.TargetX - ctx.MyX;
            float dz = ctx.TargetZ - ctx.MyZ;
            float dist = Distance(dx, dz);

            float moveX, moveZ;
            bool attack;

            if (dist > ctx.AttackRange)
            {
                // 射程外: 対象へ接近
                Normalize(dx, dz, out moveX, out moveZ);
                attack = false;
            }
            else
            {
                attack = ctx.AttackReady;
                float ideal = ctx.IsMelee ? ctx.AttackRange * 0.6f : ctx.AttackRange * 0.85f;

                if (ctx.IsMelee)
                {
                    if (dist > ideal)
                    {
                        Normalize(dx, dz, out moveX, out moveZ);
                    }
                    else if (!ctx.AttackReady)
                    {
                        Strafe(ctx.MyX, ctx.MyZ, dx, dz, out moveX, out moveZ);
                    }
                    else
                    {
                        moveX = 0f;
                        moveZ = 0f;
                    }
                }
                else
                {
                    float kiteThreshold = ideal * 0.75f;
                    if (dist < kiteThreshold)
                    {
                        // カイトアウト: 対象から離れる
                        Normalize(-dx, -dz, out moveX, out moveZ);
                    }
                    else if (ctx.AttackReady && dist <= ctx.AttackRange)
                    {
                        moveX = 0f;
                        moveZ = 0f;
                    }
                    else if (!ctx.AttackReady)
                    {
                        Strafe(ctx.MyX, ctx.MyZ, dx, dz, out moveX, out moveZ);
                    }
                    else
                    {
                        moveX = 0f;
                        moveZ = 0f;
                    }
                }
            }

            // 低HPオーバーレイ: 至近の脅威から下がりながら戦う
            if (ctx.MyHpRatio < LowHpRatio && ctx.HasThreat)
            {
                float tdx = ctx.ThreatX - ctx.MyX;
                float tdz = ctx.ThreatZ - ctx.MyZ;
                float threatDist = Distance(tdx, tdz);
                if (threatDist < ctx.AttackRange * ThreatNearFactor)
                {
                    Normalize(-tdx, -tdz, out float awayX, out float awayZ);
                    float blendX = moveX * OverlayKeepWeight + awayX * OverlayAvoidWeight;
                    float blendZ = moveZ * OverlayKeepWeight + awayZ * OverlayAvoidWeight;
                    Normalize(blendX, blendZ, out moveX, out moveZ);
                }
            }

            return new MicroDecision(moveX, moveZ, attack);
        }

        public static int ChooseFocusTarget(
            System.Collections.Generic.IReadOnlyList<FocusCandidate> candidates, int currentIndex, float myX, float myZ)
        {
            if (candidates == null || candidates.Count == 0) return -1;

            int bestIndex = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (bestIndex < 0 || IsBetterCandidate(candidates[i], candidates[bestIndex], myX, myZ))
                    bestIndex = i;
            }

            bool currentValid = currentIndex >= 0 && currentIndex < candidates.Count;
            if (!currentValid) return bestIndex;

            var current = candidates[currentIndex];
            var best    = candidates[bestIndex];

            // クラス格上げ（ミニオン→チャンピオン）はヒステリシス無視で即乗り換え
            if (best.IsChampion && !current.IsChampion) return bestIndex;

            // 同クラス以下では、十分に HP が低い候補でなければ現対象を維持する
            if (best.HpRatio < current.HpRatio - FocusSwitchHpMargin) return bestIndex;

            return currentIndex;
        }

        // 優先度: ①チャンピオン>非チャンピオン ②HpRatio最小 ③距離が近い方
        private static bool IsBetterCandidate(in FocusCandidate a, in FocusCandidate b, float myX, float myZ)
        {
            if (a.IsChampion != b.IsChampion) return a.IsChampion;
            if (a.HpRatio != b.HpRatio) return a.HpRatio < b.HpRatio;

            float distA = Distance(a.X - myX, a.Z - myZ);
            float distB = Distance(b.X - myX, b.Z - myZ);
            return distA < distB;
        }

        private static float Distance(float dx, float dz)
        {
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }

        private static void Normalize(float x, float z, out float nx, out float nz)
        {
            float mag = Distance(x, z);
            if (mag < 0.0001f)
            {
                nx = 0f;
                nz = 0f;
                return;
            }
            nx = x / mag;
            nz = z / mag;
        }

        // 対象方向と直交する垂直ストレイフ方向。左右は (MyX + MyZ) >= 0 なら左、そうでなければ右で決定的に選ぶ。
        private static void Strafe(float myX, float myZ, float towardX, float towardZ, out float nx, out float nz)
        {
            Normalize(towardX, towardZ, out float dirX, out float dirZ);
            if (dirX == 0f && dirZ == 0f)
            {
                nx = 0f;
                nz = 0f;
                return;
            }

            // 左90度回転 (dirX, dirZ) -> (-dirZ, dirX)、右90度回転 -> (dirZ, -dirX)
            bool left = (myX + myZ) >= 0f;
            nx = left ? -dirZ : dirZ;
            nz = left ? dirX  : -dirX;
        }
    }
}
