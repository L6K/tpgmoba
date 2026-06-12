using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Audio
{
    /// <summary>
    /// 効果音再生用の軽量 static ヘルパー。SkillVfx と同じ「static + キャッシュ」の流儀で、
    /// Resources からのクリップ読み込みを使い回し、3D/2D/バリアント再生をワンライナーで提供する。
    /// </summary>
    public static class GameSfx
    {
        // 全体音量の調整係数（後でバランス調整できるよう一箇所に集約）
        private const float MasterVolume = 1f;

        // 3D 減衰の基準距離。近距離は等倍、遠距離で線形に落とす
        private const float MinDistance = 6f;
        private const float MaxDistance = 45f;

        // 同名クリップの連射スパムを抑える最小間隔（AA 連射やヒット音の多重発火対策）
        private const float SpamGuardSeconds = 0.05f;

        // クリップ破棄までの余白（再生終了を確実に待つ）
        private const float DestroyPadding = 0.1f;

        // name → AudioClip。見つからなかった null もキャッシュして毎フレームの再ロードを避ける
        private static readonly Dictionary<string, AudioClip> _clipCache = new();

        // name → 最終再生時刻（Time.time 基準）。スパムガード用
        private static readonly Dictionary<string, float> _lastPlayedAt = new();

        // ピッチ揺らぎ用。Unity の Random を避け演出スレッドに依存しない static 乱数
        private static readonly System.Random _rng = new();

        /// <summary>指定クリップを 3D 空間再生する。ピッチを微妙に揺らして連射の単調さを避ける。</summary>
        public static void Play(string name, Vector3 pos, float volume = 1f)
        {
            if (IsSpam(name)) return;

            var clip = Load(name);
            if (clip == null) return;

            var go = new GameObject("Sfx_" + name);
            go.transform.position = pos;

            var src = go.AddComponent<AudioSource>();
            src.clip          = clip;
            src.volume        = volume * MasterVolume;
            src.spatialBlend  = 1f; // 完全な 3D
            src.minDistance   = MinDistance;
            src.maxDistance   = MaxDistance;
            src.rolloffMode   = AudioRolloffMode.Linear;
            src.pitch         = RandomPitch();
            src.Play();

            Object.Destroy(go, clip.length + DestroyPadding);
        }

        /// <summary>prefix_0..count-1 からランダムに 1 つ選んで 3D 再生する（AA など同種多バリアント用）。</summary>
        public static void PlayVariant(string prefix, int count, Vector3 pos, float volume = 1f)
        {
            if (count <= 0) return;
            int index = _rng.Next(count);
            Play(prefix + "_" + index, pos, volume);
        }

        /// <summary>UI 等の 2D 再生。位置と減衰を持たず、ピッチも固定する。</summary>
        public static void PlayUi(string name, float volume = 1f)
        {
            if (IsSpam(name)) return;

            var clip = Load(name);
            if (clip == null) return;

            var go = new GameObject("SfxUi_" + name);
            var src = go.AddComponent<AudioSource>();
            src.clip         = clip;
            src.volume       = volume * MasterVolume;
            src.spatialBlend = 0f; // 完全な 2D
            src.pitch        = 1f;
            src.Play();

            Object.Destroy(go, clip.length + DestroyPadding);
        }

        // 同名クリップが直近 SpamGuardSeconds 以内に鳴っていれば true（無視させる）。
        // 真と判定した場合でも最終再生時刻は更新しない（連続呼び出しで時刻が伸び続けるのを防ぐ）
        private static bool IsSpam(string name)
        {
            float now = Time.time;
            if (_lastPlayedAt.TryGetValue(name, out var last) && now - last < SpamGuardSeconds)
                return true;
            _lastPlayedAt[name] = now;
            return false;
        }

        // 0.92〜1.08 のランダムピッチ
        private static float RandomPitch()
        {
            return 0.92f + (float)_rng.NextDouble() * 0.16f;
        }

        private static AudioClip Load(string name)
        {
            if (_clipCache.TryGetValue(name, out var cached))
                return cached;

            var clip = Resources.Load<AudioClip>("Sfx/" + name);
            _clipCache[name] = clip; // null もキャッシュして再ロードを避ける
            return clip;
        }
    }
}
