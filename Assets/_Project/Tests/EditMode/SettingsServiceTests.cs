using NUnit.Framework;
using Enigma.Data;

namespace Enigma.Tests
{
    public class SettingsServiceTests
    {
        // ── フェイク applier ───────────────────────────────

        private sealed class FakeApplier : ISystemSettingsApplier
        {
            public float LastBgm   { get; private set; }
            public float LastSe    { get; private set; }
            public float LastVoice { get; private set; }
            public int   LastQuality    { get; private set; }
            public int   LastWindowMode { get; private set; }
            public int   ApplyVolumeCallCount  { get; private set; }
            public int   ApplyQualityCallCount { get; private set; }
            public int   ApplyWindowCallCount  { get; private set; }

            public void ApplyVolume(float bgm, float se, float voice)
            {
                LastBgm   = bgm;
                LastSe    = se;
                LastVoice = voice;
                ApplyVolumeCallCount++;
            }

            public void ApplyQuality(int level)
            {
                LastQuality = level;
                ApplyQualityCallCount++;
            }

            public void ApplyWindowMode(int mode)
            {
                LastWindowMode = mode;
                ApplyWindowCallCount++;
            }
        }

        // ── Apply → ストア保存 & Save 呼び出し ────────────

        [Test]
        public void Apply_SavesValuesToStoreAndCallsSave()
        {
            var store   = new FakeSaveStore();
            var applier = new FakeApplier();
            var service = new SettingsService(store, applier);

            service.Apply(0.5f, 0.6f, 0.7f, 1, 2);

            Assert.AreEqual(0.5f, store.GetFloat("settings_bgm",     0f));
            Assert.AreEqual(0.6f, store.GetFloat("settings_se",      0f));
            Assert.AreEqual(0.7f, store.GetFloat("settings_voice",   0f));
            Assert.AreEqual(1,    store.GetInt("settings_quality",   0));
            Assert.AreEqual(2,    store.GetInt("settings_window",    0));
            Assert.Greater(store.SaveCallCount, 0, "Save() が呼ばれること");
        }

        // ── Apply → applier に正しい値が渡る ──────────────

        [Test]
        public void Apply_PassesCorrectValuesToApplier()
        {
            var store   = new FakeSaveStore();
            var applier = new FakeApplier();
            var service = new SettingsService(store, applier);

            service.Apply(0.3f, 0.8f, 1.0f, 3, 0);

            Assert.AreEqual(0.3f, applier.LastBgm);
            Assert.AreEqual(0.8f, applier.LastSe);
            Assert.AreEqual(1.0f, applier.LastVoice);
            Assert.AreEqual(3,    applier.LastQuality);
            Assert.AreEqual(0,    applier.LastWindowMode);
            Assert.AreEqual(1,    applier.ApplyVolumeCallCount);
            Assert.AreEqual(1,    applier.ApplyQualityCallCount);
            Assert.AreEqual(1,    applier.ApplyWindowCallCount);
        }

        // ── Load → ストアの値がプロパティに反映される ─────

        [Test]
        public void Load_ReflectsStoredValues()
        {
            var store = new FakeSaveStore();
            store.SetFloat("settings_bgm",   0.4f);
            store.SetFloat("settings_se",    0.9f);
            store.SetFloat("settings_voice", 0.2f);
            store.SetInt("settings_quality", 0);
            store.SetInt("settings_window",  2);

            var applier = new FakeApplier();
            var service = new SettingsService(store, applier);

            service.Load();

            Assert.AreEqual(0.4f, service.BgmVolume,    1e-5f);
            Assert.AreEqual(0.9f, service.SeVolume,     1e-5f);
            Assert.AreEqual(0.2f, service.VoiceVolume,  1e-5f);
            Assert.AreEqual(0,    service.QualityLevel);
            Assert.AreEqual(2,    service.WindowMode);
        }
    }
}
