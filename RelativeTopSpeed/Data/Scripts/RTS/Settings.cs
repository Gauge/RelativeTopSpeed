using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace RelativeTopSpeed
{
    [ProtoContract]
    public partial class Settings
    {
        public static Settings Instance;
        public static bool Debug = false;
        public const string Filename = "RelativeTopSpeed.cfg";
        // Increment when changing the configuration schema or default behavior.
        public const int CurrentVersion = 1;
        public static readonly Settings Default = CreateDefault();

        // Leave deserialized Version at zero when absent so old files reset.
        [ProtoMember(25)]
        public int Version { get; set; }
        [ProtoMember(1)]
        public bool EnableBoosting { get; set; }
        [ProtoMember(2)]
        public bool IgnoreGridsWithoutThrust { get; set; }
        [ProtoMember(3)]
        public bool IgnoreGridsWithoutCockpit { get; set; }
        [ProtoMember(4)]
        public float ParachuteDeployHeight { get; set; }
        [ProtoMember(5)]
        public float SpeedLimit { get; set; }
        [ProtoMember(6)]
        public float RemoteControlSpeedLimit { get; set; }
        // Tags 7–22 belonged to removed flat settings; do not reuse them.
        [ProtoMember(23)]
        public GridSpeedSettings LargeGrid { get; set; }
        [ProtoMember(24)]
        public GridSpeedSettings SmallGrid { get; set; }

        // Runtime status travels to clients (including late joiners), but is not saved.
        [ProtoMember(26), XmlIgnore]
        public string ConfigurationNotice { get; set; }

        public static Settings CreateDefault()
        {
            var settings = new Settings
            {
                Version = CurrentVersion,
                EnableBoosting = true,
                IgnoreGridsWithoutThrust = true,
                IgnoreGridsWithoutCockpit = false,
                ParachuteDeployHeight = 400,
                SpeedLimit = 140,
                RemoteControlSpeedLimit = 100,
                LargeGrid = new GridSpeedSettings
                {
                    MaxBoostSpeed = 140, ResistanceMultiplier = 1.5f,
                    CruiseCurve = new List<CruisePoint>
                    {
                        new CruisePoint { Mass = 200000, Speed = 110 },
                        new CruisePoint { Mass = 5000000, Speed = 80 },
                        new CruisePoint { Mass = 8000000, Speed = 60 }
                    }
                },
                SmallGrid = new GridSpeedSettings
                {
                    MaxBoostSpeed = 140, ResistanceMultiplier = 1,
                    CruiseCurve = new List<CruisePoint>
                    {
                        new CruisePoint { Mass = 10000, Speed = 110 },
                        new CruisePoint { Mass = 300000, Speed = 95 },
                        new CruisePoint { Mass = 400000, Speed = 90 }
                    }
                }
            };
            Validate(ref settings);
            return settings;
        }

        public float GetCruiseSpeed(float mass, bool isLargeGrid)
        {
            return (isLargeGrid ? LargeGrid : SmallGrid).Evaluate(mass);
        }

        public static void Validate(ref Settings settings)
        {
            if (settings == null || settings.Version != CurrentVersion)
            {
                int foundVersion = settings == null ? 0 : settings.Version;
                settings = CreateDefault();
                settings.ConfigurationNotice = "Detected old or incompatible configuration version " + foundVersion
                    + ". Using defaults until RelativeTopSpeed.cfg is updated to version " + CurrentVersion
                    + " (<Version>" + CurrentVersion + "</Version>). Update the grid settings to the current format, then run /rts load. Your file has been preserved.";
                return;
            }
            settings.SpeedLimit = Positive(settings.SpeedLimit, 100);
            settings.RemoteControlSpeedLimit = Math.Min(Positive(settings.RemoteControlSpeedLimit, 100), settings.SpeedLimit);
            settings.ParachuteDeployHeight = Math.Max(0, FiniteOr(settings.ParachuteDeployHeight, 0));
            if (settings.LargeGrid == null || settings.SmallGrid == null)
                throw new ArgumentException("Both LargeGrid and SmallGrid settings are required.");
            settings.LargeGrid.Validate("LargeGrid", settings.SpeedLimit);
            settings.SmallGrid.Validate("SmallGrid", settings.SpeedLimit);
        }

        internal static float FiniteOr(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        internal static float Positive(float value, float fallback)
        {
            value = FiniteOr(value, fallback);
            return value > 0 ? value : fallback;
        }
    }
}
