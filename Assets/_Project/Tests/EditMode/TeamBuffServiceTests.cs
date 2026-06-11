using NUnit.Framework;
using Enigma.Combat;
using Enigma.Data;

namespace Enigma.Tests.EditMode
{
    public sealed class TeamBuffServiceTests
    {
        private TeamBuffService _svc;

        [SetUp]
        public void SetUp()
        {
            _svc = new TeamBuffService();
        }

        [Test]
        public void 未付与チームは倍率1を返す()
        {
            Assert.AreEqual(1f, _svc.GetDamageMultiplier(TeamId.Blue, 0f));
        }

        [Test]
        public void 付与中は指定倍率を返す()
        {
            _svc.GrantDamageBuff(TeamId.Blue, 1.15f, 300f, 0f);
            Assert.AreEqual(1.15f, _svc.GetDamageMultiplier(TeamId.Blue, 150f), 0.001f);
        }

        [Test]
        public void 期限切れ後は倍率1に戻る()
        {
            _svc.GrantDamageBuff(TeamId.Blue, 1.15f, 300f, 0f);
            Assert.AreEqual(1f, _svc.GetDamageMultiplier(TeamId.Blue, 300f));
        }

        [Test]
        public void 残り秒数が正しく計算される()
        {
            _svc.GrantDamageBuff(TeamId.Blue, 1.15f, 300f, 0f);
            Assert.AreEqual(200f, _svc.GetRemainingSeconds(TeamId.Blue, 100f), 0.001f);
        }

        [Test]
        public void 期限切れ後の残り秒数は0()
        {
            _svc.GrantDamageBuff(TeamId.Blue, 1.15f, 300f, 0f);
            Assert.AreEqual(0f, _svc.GetRemainingSeconds(TeamId.Blue, 300f));
        }

        [Test]
        public void チームは独立して管理される()
        {
            _svc.GrantDamageBuff(TeamId.Blue, 1.15f, 300f, 0f);
            Assert.AreEqual(1f,    _svc.GetDamageMultiplier(TeamId.Red,  100f));
            Assert.AreEqual(1.15f, _svc.GetDamageMultiplier(TeamId.Blue, 100f), 0.001f);
        }

        [Test]
        public void Neutralチームにも付与すれば倍率を返す()
        {
            _svc.GrantDamageBuff(TeamId.Neutral, 1.2f, 60f, 0f);
            Assert.AreEqual(1.2f, _svc.GetDamageMultiplier(TeamId.Neutral, 30f), 0.001f);
        }
    }
}
