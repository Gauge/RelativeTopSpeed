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
        internal const string StartupSettingsKey = "RelativeTopSpeed.StartupSettings";

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
            if (!MyAPIGateway.Multiplayer.IsServer) return LoadStartupSettings();
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

        private static Settings LoadStartupSettings()
        {
            // SE restores Utilities variables from ScriptManagerData before
            // session Init, whereas NetSync.Fetch completes after initialization.
            // Do not seed character physics with 140 m/s while awaiting that fetch.
            string xml;
            if (MyAPIGateway.Utilities.GetVariable(StartupSettingsKey, out xml))
            {
                try
                {
                    var settings = MyAPIGateway.Utilities.SerializeFromXML<Settings>(xml);
                    if (settings != null && settings.Version == CurrentVersion)
                    {
                        Validate(ref settings);
                        return settings;
                    }
                }
                catch (Exception error)
                {
                    MyLog.Default.Warning("[RelativeTopSpeed] Could not read startup settings: " + error.Message);
                }
            }
            MyLog.Default.Warning("[RelativeTopSpeed] Startup settings unavailable; using defaults until server synchronization. A fresh character may be needed after synchronization.");
            return CreateDefault();
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
