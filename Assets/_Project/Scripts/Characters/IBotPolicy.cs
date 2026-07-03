namespace Enigma.Character
{
    /// <summary>
    /// Bot のミクロ戦闘判断の差し替え境界。スクリプト実装(ScriptedBotPolicy)が既定で、
    /// 将来 ML-Agents の学習済みポリシーをここに差し込む(マクロ判断はスクリプトのまま)。
    /// </summary>
    public interface IBotPolicy
    {
        MicroDecision DecideMicro(in MicroContext ctx);
    }

    public sealed class ScriptedBotPolicy : IBotPolicy
    {
        public MicroDecision DecideMicro(in MicroContext ctx) => CombatMicroModel.Decide(in ctx);
    }
}
