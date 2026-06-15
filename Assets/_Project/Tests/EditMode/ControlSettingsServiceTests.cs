using NUnit.Framework;
using UnityEngine.InputSystem;
using Enigma.Data;

namespace Enigma.Tests
{
    public sealed class ControlSettingsServiceTests
    {
        // --- デフォルト値 ---

        [Test]
        public void DefaultSkillKeys_AreQERNone()
        {
            var store   = new FakeSaveStore();
            var service = new ControlSettingsService(store);

            Assert.AreEqual(Key.Q,    service.GetSkillKey(0));
            Assert.AreEqual(Key.E,    service.GetSkillKey(1));
            Assert.AreEqual(Key.R,    service.GetSkillKey(2));
            Assert.AreEqual(Key.None, service.GetSkillKey(3));
        }

        [Test]
        public void DefaultCastMode_IsQuickWithIndicator()
        {
            var store   = new FakeSaveStore();
            var service = new ControlSettingsService(store);
            Assert.AreEqual(CastMode.QuickWithIndicator, service.CastMode);
        }

        // --- 永続化 ---

        [Test]
        public void SetSkillKey_PersistsOnNewInstance()
        {
            var store   = new FakeSaveStore();
            var service = new ControlSettingsService(store);
            service.SetSkillKey(0, Key.Z);

            // 同じ store から新しいインスタンスを生成して読み出す
            var service2 = new ControlSettingsService(store);
            Assert.AreEqual(Key.Z, service2.GetSkillKey(0));
        }

        [Test]
        public void SetSkillKey_CallsSave()
        {
            var store   = new FakeSaveStore();
            var service = new ControlSettingsService(store);
            int beforeCount = store.SaveCallCount;

            service.SetSkillKey(1, Key.X);

            Assert.Greater(store.SaveCallCount, beforeCount);
        }

        [Test]
        public void SetCastMode_PersistsOnNewInstance()
        {
            var store   = new FakeSaveStore();
            var service = new ControlSettingsService(store);
            service.SetCastMode(CastMode.Normal);

            var service2 = new ControlSettingsService(store);
            Assert.AreEqual(CastMode.Normal, service2.CastMode);
        }

        [Test]
        public void SetCastMode_CallsSave()
        {
            var store   = new FakeSaveStore();
            var service = new ControlSettingsService(store);
            int beforeCount = store.SaveCallCount;

            service.SetCastMode(CastMode.Quick);

            Assert.Greater(store.SaveCallCount, beforeCount);
        }

        // --- 境界値 ---

        [Test]
        public void GetSkillKey_OutOfRange_ReturnsNone()
        {
            var store   = new FakeSaveStore();
            var service = new ControlSettingsService(store);

            Assert.AreEqual(Key.None, service.GetSkillKey(-1));
            Assert.AreEqual(Key.None, service.GetSkillKey(4));
        }

        [Test]
        public void SetSkillKey_OutOfRange_DoesNotThrow()
        {
            var store   = new FakeSaveStore();
            var service = new ControlSettingsService(store);
            Assert.DoesNotThrow(() => service.SetSkillKey(-1, Key.A));
            Assert.DoesNotThrow(() => service.SetSkillKey(4, Key.A));
        }
    }
}
