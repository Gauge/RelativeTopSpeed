using Sandbox.Definitions;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class StartupSettingsTests : TestSession
    {
        private Settings SettingsAt(float limit)
        {
            var settings = Settings.CreateDefault();
            settings.SpeedLimit = limit;
            return settings;
        }

        [Fact]
        public void ClientAppliesCheckpointSettingsDuringInitBeforeNetworkFrames()
        {
            Game.BecomeServer(false);
            var settings = SettingsAt(600);
            settings.SmallGrid.MaxBoostSpeed = 500;
            Game.Utilities.SetVariable(Settings.StartupSettingsKey, settings.ToString());
            Mod = new RelativeTopSpeed();

            Mod.Init(new VRage.Game.MyObjectBuilder_SessionComponent());

            Assert.Equal(600, Mod.cfg.Value.SpeedLimit);
            Assert.Equal(500, Mod.cfg.Value.SmallGrid.MaxBoostSpeed);
            Assert.Equal(600, MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed);
            Assert.Equal(600, MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed);
            Assert.Empty(Game.Utilities.World);
        }

        [Fact]
        public void ServerUsesConfigFileAndReplacesStaleCheckpointSettings()
        {
            Game.Utilities.SetVariable(Settings.StartupSettingsKey, SettingsAt(300).ToString());
            Start(SettingsAt(600));

            Assert.Equal(600, Mod.cfg.Value.SpeedLimit);
            Assert.True(Game.Utilities.GetVariable<string>(Settings.StartupSettingsKey, out var xml));
            Assert.Equal(600, Game.Utilities.SerializeFromXML<Settings>(xml).SpeedLimit);
        }

        [Fact]
        public void AcceptedEditsUpdateStartupSettingsForSubsequentJoiners()
        {
            Start();
            Assert.StartsWith("Settings saved", Mod.ApplySettingsEdit(new ConfigurationEdit
            {
                ExpectedXml = Mod.cfg.Value.ToString(), UpdatedXml = SettingsAt(600).ToString()
            }));
            Assert.True(Game.Utilities.GetVariable<string>(Settings.StartupSettingsKey, out var xml));
            Assert.Equal(600, Game.Utilities.SerializeFromXML<Settings>(xml).SpeedLimit);

            Game.Utilities.FailWrite = true;
            Mod.ApplySettingsEdit(new ConfigurationEdit
            {
                ExpectedXml = Mod.cfg.Value.ToString(), UpdatedXml = SettingsAt(800).ToString()
            });
            Assert.True(Game.Utilities.GetVariable<string>(Settings.StartupSettingsKey, out var unchanged));
            Assert.Equal(xml, unchanged);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("broken xml")]
        [InlineData("<Settings><Version>999</Version></Settings>")]
        [InlineData("<Settings><Version>2</Version><LargeGrid><CruiseCurve /></LargeGrid></Settings>")]
        public void MissingOrInvalidStartupSettingsFallBackWithoutBreakingLoad(string xml)
        {
            Game.BecomeServer(false);
            if (xml != null) Game.Utilities.SetVariable(Settings.StartupSettingsKey, xml);
            Start();
            Assert.Equal(140, Mod.cfg.Value.SpeedLimit);
        }

        [Fact]
        public void UnloadRemovesStartupSettingsFromSharedProcessState()
        {
            Start(SettingsAt(600));
            Mod.SimulateUnload();
            Mod = null;
            Assert.False(Game.Utilities.GetVariable<string>(Settings.StartupSettingsKey, out var unused));
        }
    }
}
