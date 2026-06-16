using System;

namespace Enigma.Map
{
    public readonly struct LaunchVelocity
    {
        public readonly float Vx;
        public readonly float Vy;
        public readonly float Vz;
        public readonly float TravelSeconds;

        public LaunchVelocity(float vx, float vy, float vz, float travelSeconds)
        {
            Vx = vx;
            Vy = vy;
            Vz = vz;
            TravelSeconds = travelSeconds;
        }
    }

    public static class GimmickPhysicsModel
    {
        private const float DefaultGravity = 9.8f;
        private const float DefaultArcHeight = 1f;
        private const float MinGravityWellDistance = 1e-4f;

        public static LaunchVelocity LaunchToTarget(
            float fromX,
            float fromY,
            float fromZ,
            float toX,
            float toY,
            float toZ,
            float gravity,
            float arcHeight)
        {
            float safeGravity = gravity > 0f ? gravity : DefaultGravity;
            float safeArcHeight = arcHeight > 0f ? arcHeight : DefaultArcHeight;
            float peakY = Math.Max(fromY, toY) + safeArcHeight;

            float vy = Sqrt(2f * safeGravity * (peakY - fromY));
            float tUp = vy / safeGravity;
            float tDown = Sqrt(2f * (peakY - toY) / safeGravity);
            float travelSeconds = tUp + tDown;

            float vx = (toX - fromX) / travelSeconds;
            float vz = (toZ - fromZ) / travelSeconds;
            return new LaunchVelocity(vx, vy, vz, travelSeconds);
        }

        public static void GravityWellAccel(
            float unitX,
            float unitZ,
            float centerX,
            float centerZ,
            float radius,
            float strength,
            out float ax,
            out float az)
        {
            float dx = centerX - unitX;
            float dz = centerZ - unitZ;
            float distance = Sqrt(dx * dx + dz * dz);

            if (distance >= radius || distance < MinGravityWellDistance)
            {
                ax = 0f;
                az = 0f;
                return;
            }

            float magnitude = strength * (1f - distance / radius);
            ax = dx / distance * magnitude;
            az = dz / distance * magnitude;
        }

        public static float GateSlowMultiplier(bool inside, float slowStrength)
        {
            if (!inside)
                return 1f;

            return Clamp01(1f - slowStrength);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }

        private static float Sqrt(float value)
        {
            return (float)Math.Sqrt(value);
        }
    }
}
