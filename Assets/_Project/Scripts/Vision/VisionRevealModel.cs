using System;
using System.Collections.Generic;

namespace Enigma.Vision
{
    /// <summary>
    /// 地形遮蔽の判定を担う抽象。呼び側(FogOfWarDirector)が Physics 依存の実装を注入する。
    /// null を渡した場合は常に true(=遮蔽なし、旧挙動)として扱う。
    /// </summary>
    public interface ILineOfSightChecker
    {
        bool HasLineOfSight(in VisionSource source, in VisionTarget target);
    }

    public readonly struct VisionSource
    {
        public readonly float X;
        public readonly float Z;
        public readonly float Radius;
        public readonly float Y;
        public readonly int BrushId;

        public VisionSource(float x, float z, float radius)
            : this(x, z, radius, 0f, -1)
        {
        }

        public VisionSource(float x, float z, float radius, float y, int brushId)
        {
            X = x;
            Z = z;
            Radius = radius;
            Y = y;
            BrushId = brushId;
        }
    }

    public readonly struct VisionTarget
    {
        public readonly int Id;
        public readonly float X;
        public readonly float Z;
        public readonly float Y;
        public readonly int BrushId;

        public VisionTarget(int id, float x, float z)
            : this(id, x, z, 0f, -1)
        {
        }

        public VisionTarget(int id, float x, float z, float y, int brushId)
        {
            Id = id;
            X = x;
            Z = z;
            Y = y;
            BrushId = brushId;
        }
    }

    public sealed class VisionRevealModel
    {
        // ターゲットより 1.0m を超えて高い位置にいるソースからは見えない(高低差ルール)。
        private const float HeightAdvantageLimit = 1.0f;

        private readonly float _lingerSeconds;
        private readonly ILineOfSightChecker _lineOfSightChecker;
        private readonly Dictionary<int, float> _lingerRemainingByTarget = new Dictionary<int, float>();
        private readonly HashSet<int> _visibleTargetIds = new HashSet<int>();
        private readonly HashSet<int> _currentTargetIds = new HashSet<int>();

        public VisionRevealModel(float lingerSeconds = 0f, ILineOfSightChecker lineOfSightChecker = null)
        {
            _lingerSeconds = Math.Max(0f, lingerSeconds);
            _lineOfSightChecker = lineOfSightChecker;
        }

        public IReadOnlyCollection<int> Update(
            IReadOnlyList<VisionSource> sources,
            IReadOnlyList<VisionTarget> targets,
            float deltaTime)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            _visibleTargetIds.Clear();
            _currentTargetIds.Clear();

            int targetCount = targets?.Count ?? 0;
            for (int i = 0; i < targetCount; i++)
            {
                VisionTarget target = targets[i];
                _currentTargetIds.Add(target.Id);

                if (IsDirectlyVisible(sources, target, _lineOfSightChecker))
                {
                    _lingerRemainingByTarget[target.Id] = _lingerSeconds;
                    _visibleTargetIds.Add(target.Id);
                    continue;
                }

                if (_lingerRemainingByTarget.TryGetValue(target.Id, out float remaining))
                {
                    remaining -= deltaTime;
                    if (remaining > 0f)
                    {
                        _lingerRemainingByTarget[target.Id] = remaining;
                        _visibleTargetIds.Add(target.Id);
                    }
                    else
                    {
                        _lingerRemainingByTarget.Remove(target.Id);
                    }
                }
            }

            RemoveMissingTargets();
            return _visibleTargetIds;
        }

        public bool IsVisible(int targetId)
        {
            return _visibleTargetIds.Contains(targetId);
        }

        public void Clear()
        {
            _lingerRemainingByTarget.Clear();
            _visibleTargetIds.Clear();
            _currentTargetIds.Clear();
        }

        private static bool IsDirectlyVisible(
            IReadOnlyList<VisionSource> sources,
            VisionTarget target,
            ILineOfSightChecker lineOfSightChecker)
        {
            int sourceCount = sources?.Count ?? 0;
            for (int i = 0; i < sourceCount; i++)
            {
                VisionSource source = sources[i];
                if (source.Radius <= 0f)
                    continue;

                float dx = target.X - source.X;
                float dz = target.Z - source.Z;
                float radius = source.Radius;
                if (dx * dx + dz * dz > radius * radius)
                    continue;

                // 茂みルール: ターゲットが茂み内なら、同じ茂み内のソースからしか見えない。
                if (target.BrushId >= 0 && source.BrushId != target.BrushId)
                    continue;

                // 高低差ルール: ターゲットより 1.0m を超えて高い位置のソースからは見えない。
                if (target.Y - source.Y > HeightAdvantageLimit)
                    continue;

                // 地形遮蔽: チェッカー未注入時は常に可視(既存挙動維持)。
                if (lineOfSightChecker != null && !lineOfSightChecker.HasLineOfSight(in source, in target))
                    continue;

                return true;
            }

            return false;
        }

        private void RemoveMissingTargets()
        {
            if (_lingerRemainingByTarget.Count == 0)
                return;

            var keysToRemove = new List<int>();
            foreach (int targetId in _lingerRemainingByTarget.Keys)
            {
                if (!_currentTargetIds.Contains(targetId))
                    keysToRemove.Add(targetId);
            }

            for (int i = 0; i < keysToRemove.Count; i++)
                _lingerRemainingByTarget.Remove(keysToRemove[i]);
        }
    }
}
