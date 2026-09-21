using System;

namespace RelativeTopSpeed
{
    public static class SpeedMath
    {
        public static float ExcessSpeedFraction(float speed, float cruise)
        {
            return speed > cruise && speed > 0 ? 1 - cruise / speed : 0;
        }

        public static float Resistance(float mass, float speed, float cruise, float multiplier)
        {
            return multiplier * mass * ExcessSpeedFraction(speed, cruise);
        }

        // Public API order: forward, minimum, average, maximum.
        public static float[] SummarizeAcceleration(float[] directions)
        {
            if (directions == null || directions.Length != 6)
                throw new ArgumentException("Expected six directional accelerations.", "directions");
            float min = directions[0], sum = 0;
            for (int i = 0; i < 6; i++)
            {
                min = Math.Min(min, directions[i]);
                sum += directions[i];
            }
            float max = Math.Max(directions[0], directions[1]) +
                        Math.Max(directions[2], directions[3]) + Math.Max(directions[4], directions[5]);
            return new[] { directions[1], min, sum / 6, max };
        }
    }
}
