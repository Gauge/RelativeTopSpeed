using System;
using System.IO;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Utils;

namespace RelativeTopSpeed
{
    public partial class Settings
    {
        public void CalculateCurve()
        {
            MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = SpeedLimit;
            MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = SpeedLimit;

            // parachute deploy hight code is taken directly from midspaces configurable speed mod. All credit goes to them.
            DictionaryReader<string, MyDropContainerDefinition> dropContainers = MyDefinitionManager.Static.GetDropContainerDefinitions();
            foreach (var kvp in dropContainers)
            {
                if (kvp.Value?.Prefab?.CubeGrids == null) continue;
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

        public static Settings Load() { return Load(null); }

        public static Settings Load(Settings fallback)
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return CreateDefault();
            Settings settings;
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(Settings)))
                        settings = MyAPIGateway.Utilities.SerializeFromXML<Settings>(reader.ReadToEnd());
                }
                else if (MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(Settings)))
                        settings = MyAPIGateway.Utilities.SerializeFromXML<Settings>(reader.ReadToEnd());
                }
                else settings = CreateDefault();
                bool incompatible = settings == null || settings.Version != CurrentVersion;
                Validate(ref settings);
                if (incompatible)
                    MyLog.Default.Warning("[RelativeTopSpeed] " + settings.ConfigurationNotice);
                else Save(settings);
            }
            catch (Exception error)
            {
                // Preserve the invalid file so an administrator can repair it.
                MyLog.Default.Warning("[RelativeTopSpeed] Failed to load configuration; keeping the previous settings or startup defaults. " + error);
                settings = fallback ?? CreateDefault();
            }
            return settings;
        }

        public static void Save(Settings settings)
        {
            TrySave(settings);
        }

        public static bool TrySave(Settings settings)
        {
            if (!MyAPIGateway.Session.IsServer) return false;
            try
            {
                string xml = MyAPIGateway.Utilities.SerializeToXML(settings);
                if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)))
                {
                    using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(Filename, typeof(Settings)))
                        writer.Write(xml);
                }
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(Settings)))
                    writer.Write(xml);
                return true;
            }
            catch (Exception error)
            {
                MyLog.Default.Error("[RelativeTopSpeed] Failed to save settings. " + error);
                return false;
            }
        }
    }
}
