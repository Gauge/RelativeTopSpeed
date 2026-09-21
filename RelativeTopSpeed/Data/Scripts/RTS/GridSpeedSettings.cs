using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using ProtoBuf;

namespace RelativeTopSpeed
{
    [ProtoContract]
    public class CruisePoint
    {
        [ProtoMember(1), XmlAttribute]
        public float Mass { get; set; }
        [ProtoMember(2), XmlAttribute]
        public float Speed { get; set; }
    }

    [ProtoContract]
    public class GridSpeedSettings
    {
        [ProtoMember(1), XmlArrayItem("Point")]
        public List<CruisePoint> CruiseCurve { get; set; }

        [ProtoMember(2)]
        public float MaxBoostSpeed { get; set; }
        [ProtoMember(3)]
        public float ResistanceMultiplier { get; set; }

        // Cached during validation, never part of the configuration or wire format.
        [XmlIgnore, ProtoIgnore]
        public float MinimumCruiseSpeed { get; private set; }

        public void Validate(string name, float worldLimit)
        {
            if (CruiseCurve == null || CruiseCurve.Count < 2)
                throw new ArgumentException(name + ".CruiseCurve requires at least two points.");
            foreach (var point in CruiseCurve)
            {
                if (point == null || float.IsNaN(point.Mass) || float.IsInfinity(point.Mass) || point.Mass < 0)
                    throw new ArgumentException(name + ".CruiseCurve masses must be finite and nonnegative.");
                if (float.IsNaN(point.Speed) || float.IsInfinity(point.Speed) || point.Speed <= 0)
                    throw new ArgumentException(name + ".CruiseCurve speeds must be finite and positive.");
            }
            CruiseCurve.Sort((a, b) => a.Mass.CompareTo(b.Mass));
            for (int i = 1; i < CruiseCurve.Count; i++)
                if (CruiseCurve[i - 1].Mass == CruiseCurve[i].Mass)
                    throw new ArgumentException(name + ".CruiseCurve has duplicate mass " + CruiseCurve[i].Mass + ".");
            MinimumCruiseSpeed = float.MaxValue;
            float maximumCruiseSpeed = 0;
            foreach (var point in CruiseCurve)
            {
                point.Speed = Math.Min(point.Speed, worldLimit);
                MinimumCruiseSpeed = Math.Min(MinimumCruiseSpeed, point.Speed);
                maximumCruiseSpeed = Math.Max(maximumCruiseSpeed, point.Speed);
            }
            MaxBoostSpeed = Math.Min(worldLimit, Math.Max(maximumCruiseSpeed, Settings.FiniteOr(MaxBoostSpeed, maximumCruiseSpeed)));
            ResistanceMultiplier = Settings.Positive(ResistanceMultiplier, 1);
        }

        // Validation sorts once. Each simulation query is allocation-free O(log n).
        public float Evaluate(float mass)
        {
            var points = CruiseCurve;
            if (mass <= points[0].Mass) return points[0].Speed;
            int last = points.Count - 1;
            if (mass >= points[last].Mass) return points[last].Speed;
            int lower = 0, upper = last;
            while (upper - lower > 1)
            {
                int middle = lower + (upper - lower) / 2;
                if (mass < points[middle].Mass) upper = middle;
                else lower = middle;
            }
            var a = points[lower]; var b = points[upper];
            double fraction = ((double)mass - a.Mass) / ((double)b.Mass - a.Mass);
            return (float)(a.Speed + ((double)b.Speed - a.Speed) * fraction);
        }
    }
}
