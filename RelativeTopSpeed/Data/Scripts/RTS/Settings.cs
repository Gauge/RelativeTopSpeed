using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.IO;
using System.Xml.Serialization;
using VRage.Collections;
using VRage.Game;
using VRage.Utils;

namespace RelativeTopSpeed
{
    [ProtoContract]
    public class Settings
    {
        public const string Filename = "RelativeTopSpeed.cfg";

        public static readonly Settings Default = new Settings() {
            EnableBoosting = true,
            IgnoreGridsWithoutThrust = true,
            ParachuteDeployHeight = 400,
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
			SmallGrid_MaxMass = 400000,
		};

        [ProtoMember(1)]
        public bool EnableBoosting { get; set; }

        [ProtoMember(2)]
        public bool IgnoreGridsWithoutThrust { get; set; }

        [ProtoMember(3)]
        public float ParachuteDeployHeight { get; set; }

		[ProtoMember(4)]
        public float SpeedLimit { get; set; }

        [ProtoMember(5)]
        public float LargeGrid_MinCruise { get; set; }

        [ProtoMember(6)]
        public float LargeGrid_MaxCruise { get; set; }

        [ProtoMember(7)]
        public float LargeGrid_MaxMass { get; set; }

        [ProtoMember(8)]
        public float LargeGrid_MinMass { get; set; }

        [ProtoMember(9)]
        public float LargeGrid_MaxBoostSpeed { get; set; }

        [ProtoMember(10)]
        public float LargeGrid_ResistanceMultiplier { get; set; }

        [ProtoMember(11)]
        public float SmallGrid_MinCruise { get; set; }

        [ProtoMember(12)]
        public float SmallGrid_MaxCruise { get; set; }

        [ProtoMember(13)]
        public float SmallGrid_MaxMass { get; set; }

        [ProtoMember(14)]
        public float SmallGrid_MinMass { get; set; }

        [ProtoMember(15)]
        public float SmallGrid_MaxBoostSpeed { get; set; }

        [ProtoMember(16)]
        public float SmallGrid_ResistanceMultiplyer { get; set; }

        [XmlIgnore]
        public double l_a;
        [XmlIgnore]
        public double s_a;

        public void CalculateCurve()
        {
            l_a = ((LargeGrid_MaxCruise - LargeGrid_MinCruise) / Math.Pow((LargeGrid_MinMass - LargeGrid_MaxMass), 2));
            s_a = ((SmallGrid_MaxCruise - SmallGrid_MinCruise) / Math.Pow((SmallGrid_MinMass - SmallGrid_MaxMass), 2));

            MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = SpeedLimit;
            MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = SpeedLimit;


            // parachute deploy hight code is taken directly from midspaces configurable speed mod. All credit goes to them. 
            DictionaryReader<string, MyDropContainerDefinition> dropContainers = MyDefinitionManager.Static.GetDropContainerDefinitions();
            foreach (var kvp in dropContainers)
            {
                foreach (MyObjectBuilder_CubeGrid grid in kvp.Value.Prefab.CubeGrids)
                {
                    foreach (MyObjectBuilder_CubeBlock block in grid.CubeBlocks)
                    {
                        MyObjectBuilder_Parachute chute = block as MyObjectBuilder_Parachute;
                        if (chute != null)
                        {
                            chute.DeployHeight = ParachuteDeployHeight;
                        }
                    }
                }
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

            if (s.ParachuteDeployHeight < 0)
            {
                s.ParachuteDeployHeight = 0;
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

			if (!MyAPIGateway.Multiplayer.IsServer)
			{
				return Default;
			}

            Settings s = null;
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings)))
                {
                    MyLog.Default.Info("[RelativeTopSpeed] Loading settings from world storage");
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(Settings));
                    string text = reader.ReadToEnd();
                    reader.Close();

                    s = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);
                    Validate(ref s);
                    Save(s);
                }
                else
                {
                    MyLog.Default.Info("[RelativeTopSpeed] Config file not found. Loading from local storage");
					if (MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)))
					{
						MyLog.Default.Info("[RelativeTopSpeed] Loading settings from local storage");
						TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(Settings));
						string text = reader.ReadToEnd();
						reader.Close();

						s = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);
						Validate(ref s);
						Save(s);
					}
					else
					{
						MyLog.Default.Info("[RelativeTopSpeed] Config file not found. Loading defaults");
						s = Default;
						Save(s);
					}
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
					TextWriter writer;
					if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)))
					{
						writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(Filename, typeof(Settings));
						writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
						writer.Close();
					}

					writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(Settings));
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
