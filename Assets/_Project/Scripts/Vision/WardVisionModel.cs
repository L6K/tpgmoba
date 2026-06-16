using System.Collections.Generic;

namespace Enigma.Vision
{
    /// <summary>設置型偵察ワードの1本。位置は静止前提（XZ）。</summary>
    public readonly struct Ward
    {
        public readonly int Id;
        public readonly int Team;
        public readonly float X;
        public readonly float Z;
        public readonly float VisionRadius;
        public readonly float RemainingSeconds;

        public Ward(int id, int team, float x, float z, float visionRadius, float remainingSeconds)
        {
            Id = id;
            Team = team;
            X = x;
            Z = z;
            VisionRadius = visionRadius;
            RemainingSeconds = remainingSeconds;
        }

        public Ward WithRemaining(float remaining) =>
            new Ward(Id, Team, X, Z, VisionRadius, remaining);
    }

    /// <summary>
    /// ワードの設置・寿命・本数制限・アクティブ視界源の管理（純 C#・Unity 非依存）。
    /// 実際の視界反映は FogOfWarDirector が ActiveWardsForTeam を外部視界源として取り込む。
    /// </summary>
    public sealed class WardVisionModel
    {
        private readonly int _maxPerTeam;
        private readonly float _lifetime;
        private readonly float _visionRadius;
        private readonly List<Ward> _wards = new List<Ward>();
        private int _nextId = 1;

        public WardVisionModel(int maxActivePerTeam = 3, float defaultLifetime = 90f, float defaultVisionRadius = 12f)
        {
            _maxPerTeam = maxActivePerTeam < 1 ? 1 : maxActivePerTeam;
            _lifetime = defaultLifetime <= 0f ? 90f : defaultLifetime;
            _visionRadius = defaultVisionRadius <= 0f ? 12f : defaultVisionRadius;
        }

        /// <summary>ワードを設置。チームのアクティブ数が上限超過なら最古(FIFO)を1本除いてから追加する。</summary>
        public Ward Place(int team, float x, float z, float now)
        {
            if (CountForTeam(team) >= _maxPerTeam)
            {
                for (int i = 0; i < _wards.Count; i++)
                {
                    if (_wards[i].Team == team) { _wards.RemoveAt(i); break; }
                }
            }

            var ward = new Ward(_nextId++, team, x, z, _visionRadius, _lifetime);
            _wards.Add(ward);
            return ward;
        }

        /// <summary>全ワードの残寿命を減らし、0 以下を除去する。</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            for (int i = _wards.Count - 1; i >= 0; i--)
            {
                float rem = _wards[i].RemainingSeconds - dt;
                if (rem <= 0f) _wards.RemoveAt(i);
                else _wards[i] = _wards[i].WithRemaining(rem);
            }
        }

        /// <summary>指定IDのワードを除去（敵ワード破壊=デナイ）。成功で true。</summary>
        public bool Remove(int id)
        {
            for (int i = 0; i < _wards.Count; i++)
            {
                if (_wards[i].Id == id) { _wards.RemoveAt(i); return true; }
            }
            return false;
        }

        public IReadOnlyList<Ward> ActiveWards() => _wards;

        public IReadOnlyList<Ward> ActiveWardsForTeam(int team)
        {
            var list = new List<Ward>();
            for (int i = 0; i < _wards.Count; i++)
                if (_wards[i].Team == team) list.Add(_wards[i]);
            return list;
        }

        public int CountForTeam(int team)
        {
            int c = 0;
            for (int i = 0; i < _wards.Count; i++)
                if (_wards[i].Team == team) c++;
            return c;
        }

        public void Clear() => _wards.Clear();
    }
}
