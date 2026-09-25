using System;
using System.Collections.Generic;
using System.Globalization;

namespace RelativeTopSpeed
{
    // Parsing is independent of RHF so the exact same validation is testable.
    public static class SettingsEditor
    {
        public const int MinCurvePoints = 2, MaxCurvePoints = 64;

        public static float ParseNumber(string text)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                throw new ArgumentException("Enter a finite, nonnegative number using '.' as the decimal separator.");
            return value;
        }

        public static string FormatNumber(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        // The point a curve grows by: heavier than the last point, at the same speed.
        public static CruisePoint NextPoint(List<CruisePoint> points)
        {
            var last = points[points.Count - 1];
            return new CruisePoint { Mass = last.Mass > 0 ? last.Mass * 2 : 1000, Speed = last.Speed };
        }
    }
}
