using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.IO;
using System.Xml.Serialization;
using VRage.Utils;

namespace RelativeTopSpeed
{
    [ProtoContract]
    public class Settings
    {
        public const string Filename = "RelativeTopSpeed.cfg";

        public static readonly Settings Default = new Settings()
        {
            UseLogarithmic = false,
            SpeedLimit = 140,
            LargeGrid_MinCruise = 60,
            LargeGrid_MaxCruise = 110,
            LargeGrid_MaxBoostSpeed = 140,
            LargeGrid_ResistanceMultiplier = 1.5f,
            LargeGrid_MinMass = 200000,
            LargeGrid_MaxMass = 8000000,
            SmallGrid_MinCruise = 90,
            SmallGrid_MaxCruise = 110,
            SmallGrid_MaxBoostSpeed = 140,
            SmallGrid_ResistanceMultiplyer = 1f,
            SmallGrid_MinMass = 10000,
            SmallGrid_MaxMass = 400000
        };

        [ProtoMember]
        public bool UseLogarithmic { get; set; }

        [ProtoMember]
        public float SpeedLimit { get; set; }

        [ProtoMember]
        public float LargeGrid_MinCruise { get; set; }

        [ProtoMember]
        public float LargeGrid_MaxCruise { get; set; }

        [ProtoMember]
        public float LargeGrid_MaxMass { get; set; }

        [ProtoMember]
        public float LargeGrid_MinMass { get; set; }

        [ProtoMember]
        public float LargeGrid_MaxBoostSpeed { get; set; }

        [ProtoMember]
        public float LargeGrid_ResistanceMultiplier { get; set; }

        [ProtoMember]
        public float SmallGrid_MinCruise { get; set; }

        [ProtoMember]
        public float SmallGrid_MaxCruise { get; set; }

        [ProtoMember]
        public float SmallGrid_MaxMass { get; set; }

        [ProtoMember]
        public float SmallGrid_MinMass { get; set; }

        [ProtoMember]
        public float SmallGrid_MaxBoostSpeed { get; set; }

        [ProtoMember]
        public float SmallGrid_ResistanceMultiplyer { get; set; }

        [XmlIgnore]
        public double l_a;
        [XmlIgnore]
        public double s_a;

        public void CalculateCurve()
        {
            if (UseLogarithmic)
            {
                l_a = Math.Pow((LargeGrid_MaxCruise - LargeGrid_MinCruise), (1 / (LargeGrid_MinMass - LargeGrid_MaxMass)));
                s_a = Math.Pow((SmallGrid_MaxCruise - SmallGrid_MinCruise), (1 / (SmallGrid_MinMass - SmallGrid_MaxMass)));
            }
            else
            {
                l_a = ((LargeGrid_MaxCruise - LargeGrid_MinCruise) / Math.Pow((LargeGrid_MinMass - LargeGrid_MaxMass), 2));
                s_a = ((SmallGrid_MaxCruise - SmallGrid_MinCruise) / Math.Pow((SmallGrid_MinMass - SmallGrid_MaxMass), 2));
            }
        }

        public override string ToString()
        {
            return MyAPIGateway.Utilities.SerializeToXML(this);
        }

        public static void Validate(ref Settings s)
        {
            if (s.SpeedLimit <= 0)
            {
                s.SpeedLimit = 100;
            }

            #region Large Grid Validation

            if (s.LargeGrid_MinCruise < 0.01f)
            {
                s.LargeGrid_MinCruise = 0.01f;
            }
            else if (s.LargeGrid_MinCruise > s.SpeedLimit)
            {
                s.LargeGrid_MinCruise = s.SpeedLimit;
            }

            if (s.LargeGrid_MaxCruise < s.LargeGrid_MinCruise)
            {
                s.LargeGrid_MaxCruise = s.LargeGrid_MinCruise;
            }
            else if (s.LargeGrid_MaxCruise > s.SpeedLimit)
            {
                s.LargeGrid_MaxCruise = s.SpeedLimit;
            }

            if (s.LargeGrid_MaxBoostSpeed < s.LargeGrid_MaxCruise)
            {
                s.LargeGrid_MaxBoostSpeed = s.LargeGrid_MaxCruise;
            }
            else if (s.LargeGrid_MaxBoostSpeed > s.SpeedLimit)
            {
                s.LargeGrid_MaxBoostSpeed = s.SpeedLimit;
            }

            if (s.LargeGrid_ResistanceMultiplier <= 0)
            {
                s.LargeGrid_ResistanceMultiplier = 1f;
            }

            if (s.LargeGrid_MinMass < 0)
            {
                s.LargeGrid_MinMass = 0;
            }

            if (s.LargeGrid_MaxMass < s.LargeGrid_MinMass)
            {
                s.LargeGrid_MaxMass = s.LargeGrid_MinMass;
            }
            #endregion

            #region Small Grid Validation

            if (s.SmallGrid_MinCruise < 0.01)
            {
                s.SmallGrid_MinCruise = 0.01f;
            }
            else if (s.SmallGrid_MinCruise > s.SpeedLimit)
            {
                s.SmallGrid_MinCruise = s.SpeedLimit;
            }

            if (s.SmallGrid_MaxCruise < s.SmallGrid_MinCruise)
            {
                s.SmallGrid_MaxCruise = s.SmallGrid_MinCruise;
            }
            else if (s.SmallGrid_MaxCruise > s.SpeedLimit)
            {
                s.SmallGrid_MaxCruise = s.SpeedLimit;
            }

            if (s.SmallGrid_MaxBoostSpeed < s.SmallGrid_MaxCruise)
            {
                s.SmallGrid_MaxBoostSpeed = s.SmallGrid_MaxCruise;
            }
            else if (s.SmallGrid_MaxBoostSpeed > s.SpeedLimit)
            {
                s.SmallGrid_MaxBoostSpeed = s.SpeedLimit;
            }

            if (s.SmallGrid_ResistanceMultiplyer <= 0)
            {
                s.SmallGrid_ResistanceMultiplyer = 1f;
            }

            if (s.SmallGrid_MinMass < 0)
            {
                s.SmallGrid_MinMass = 0;
            }

            if (s.LargeGrid_MaxMass < s.SmallGrid_MinMass)
            {
                s.LargeGrid_MaxMass = s.SmallGrid_MinMass;
            }
            #endregion
        }

        public static Settings Load()
        {
            Settings s = null;
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings)))
                {
                    MyLog.Default.Info("[RelativeTopSpeed] Loading saved settings");
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(Settings));
                    string text = reader.ReadToEnd();
                    reader.Close();

                    s = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);
                    Validate(ref s);
                    Save(s);
                }
                else
                {
                    MyLog.Default.Info("[RelativeTopSpeed] Config file not found. Loading default settings");
                    s = Default;
                    Save(s);
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"[RelativeTopSpeed] Failed to load saved configuration. Loading defaults\n {e.ToString()}");
                s = Default;
                Save(s);
            }

            MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = s.SpeedLimit;
            MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = s.SpeedLimit;
            s.CalculateCurve();
            return s;
        }

        public static void Save(Settings settings)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                try
                {
                    MyLog.Default.Info("[RelativeTopSpeed] Saving Settings");
                    TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(Settings));
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                    writer.Close();
                }
                catch (Exception e)
                {
                    MyLog.Default.Error($"[RelativeTopSpeed] Failed to save settings\n{e.ToString()}");
                }
            }
        }
    }
}
