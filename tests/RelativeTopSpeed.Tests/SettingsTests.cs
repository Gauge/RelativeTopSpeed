using System;
using System.Linq;
using System.Reflection;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class SettingsTests : TestSession
    {
        [Fact]
        public void DefaultsAreIndependentAndValid()
        {
            var a = Settings.CreateDefault(); var b = Settings.CreateDefault();
            var original = Game.Utilities.SerializeToXML(a);
            Settings.Validate(ref a);
            Assert.Equal(original, Game.Utilities.SerializeToXML(a));
            a.SpeedLimit = 600;
            Assert.Equal(140, b.SpeedLimit);
            Assert.Equal(140, Settings.Default.SpeedLimit);
        }
        [Fact]
        public void NullSettingsUseDefaults()
        { Settings s = null; Settings.Validate(ref s); Assert.Equal(140, s.SpeedLimit); }

        public static object[][] InvalidValues => new[] { -100f, 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue }.Select(x => new object[] { x }).ToArray();
        [Theory, MemberData(nameof(InvalidValues))]
        public void EveryNumericFieldIsValidated(float value)
        {
            foreach (var property in typeof(Settings).GetProperties().Where(x => x.PropertyType == typeof(float)))
            {
                var s = Settings.CreateDefault(); property.SetValue(s, value); Settings.Validate(ref s);
                AssertInvariants(s);
                var once = Game.Utilities.SerializeToXML(s);
                Settings.Validate(ref s);
                Assert.Equal(once, Game.Utilities.SerializeToXML(s));
            }
        }
        private static void AssertInvariants(Settings s)
        {
            foreach (var p in typeof(Settings).GetProperties().Where(x => x.PropertyType == typeof(float)))
                Assert.True(float.IsFinite((float)p.GetValue(s)), p.Name);
            Assert.True(s.SpeedLimit > 0);
            Assert.InRange(s.RemoteControlSpeedLimit, 0, s.SpeedLimit);
            Assert.True(s.ParachuteDeployHeight >= 0);
            foreach (var grid in new[] { s.LargeGrid, s.SmallGrid })
            {
                Assert.True(float.IsFinite(grid.MaxBoostSpeed));
                Assert.True(float.IsFinite(grid.ResistanceMultiplier));
                Assert.InRange(grid.MaxBoostSpeed, grid.CruiseCurve.Max(p => p.Speed), s.SpeedLimit);
                Assert.True(grid.ResistanceMultiplier > 0);
                Assert.Equal(grid.CruiseCurve.Min(p => p.Speed), grid.MinimumCruiseSpeed);
            }
        }
        [Fact]
        public void RandomConfigurationsNormalizeIdempotently()
        {
            var random = new Random(42);
            for (int i = 0; i < 500; i++)
            {
                var s = Settings.CreateDefault();
                foreach (var p in typeof(Settings).GetProperties().Where(x => x.PropertyType == typeof(float)))
                    p.SetValue(s, (float)(random.NextDouble() * 2000000 - 1000000));
                Settings.Validate(ref s); AssertInvariants(s);
            }
        }
        [Fact]
        public void XmlAndProtobufPreserveEverySettingAndWireTag()
        {
            var s = Settings.CreateDefault();
            var xml = s.ToString();
            Assert.Contains("<Version>" + Settings.CurrentVersion + "</Version>", xml);
            Assert.DoesNotContain("SmallGrid_", xml);
            Assert.Equal(xml, Game.Utilities.SerializeToXML(Game.Utilities.SerializeFromXML<Settings>(xml)));
            var binary = Game.Utilities.SerializeToBinary(s);
            Assert.Equal(xml, Game.Utilities.SerializeToXML(Game.Utilities.SerializeFromBinary<Settings>(binary)));
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 23, 24, 25, 26, 27, 28 }, typeof(Settings).GetProperties().Select(p => p.GetCustomAttribute<ProtoMemberAttribute>().Tag).OrderBy(x => x));
        }
        [Theory]
        [InlineData(null, false)] [InlineData("0", false)]
        [InlineData("99", false)] [InlineData("-1", false)]
        [InlineData(null, true)] [InlineData("99", true)]
        public void IncompatibleVersionsUseDefaultsAndPreserveOriginalFile(string version, bool local)
        {
            string xml = "<Settings>" + (version == null ? "" : "<Version>" + version + "</Version>")
                + "<SpeedLimit>600</SpeedLimit><LargeGrid_MaxBoostSpeed>400</LargeGrid_MaxBoostSpeed></Settings>";
            var storage = local ? Game.Utilities.Local : Game.Utilities.World;
            storage[Settings.Filename] = xml;
            var fallback = Settings.CreateDefault(); fallback.SpeedLimit = 900;
            var result = Settings.Load(fallback);
            Assert.Equal(Settings.CurrentVersion, result.Version);
            Assert.Equal(140, result.SpeedLimit);
            Assert.Equal(xml, storage[Settings.Filename]);
            if (local) Assert.False(Game.Utilities.World.ContainsKey(Settings.Filename));
            Assert.Contains("Using defaults until", result.ConfigurationNotice);
            Assert.True(Game.Log.Contains(VRage.Utils.LogSeverity.Warning, "version"));
            Assert.DoesNotContain("ConfigurationNotice", result.ToString());
            var received = Game.Utilities.SerializeFromBinary<Settings>(Game.Utilities.SerializeToBinary(result));
            Assert.Equal(result.ConfigurationNotice, received.ConfigurationNotice);
        }

        [Fact]
        public void VersionWarningAppearsOnceInChatAndUpdatingConfigRestoresCustomSettings()
        {
            var settings = Settings.CreateDefault(); settings.Version = 0; settings.SpeedLimit = 600;
            Start(settings); Frames(2);
            Assert.Single(Game.ShownMessages, m => m.Text.Contains("Using defaults until"));
            Assert.Equal(140, Mod.cfg.Value.SpeedLimit);
            settings.Version = Settings.CurrentVersion;
            Game.Utilities.World[Settings.Filename] = settings.ToString();
            Game.Utilities.SimulateChat("/rts load"); Frames(2);
            Assert.Equal(600, Mod.cfg.Value.SpeedLimit);
            Assert.Null(Mod.cfg.Value.ConfigurationNotice);
            Assert.Single(Game.ShownMessages, m => m.Text.Contains("Using defaults until"));
        }

        [Theory]
        [InlineData(true)] [InlineData(false)]
        public void CurrentVersionRequiresBothGridGroups(bool large)
        {
            var settings = Settings.CreateDefault();
            if (large) settings.LargeGrid = null; else settings.SmallGrid = null;
            Assert.Throws<ArgumentException>(() => Settings.Validate(ref settings));
        }

        [Fact]
        public void WorldStorageWinsAndDoesNotOverwriteLocalTemplate()
        {
            var world = Settings.CreateDefault(); world.SpeedLimit = 600;
            Game.Utilities.World[Settings.Filename] = world.ToString();
            Game.Utilities.Local[Settings.Filename] = Settings.CreateDefault().ToString();
            Assert.Equal(600, Settings.Load().SpeedLimit);
            Assert.Equal(140, Game.Utilities.SerializeFromXML<Settings>(Game.Utilities.Local[Settings.Filename]).SpeedLimit);
        }
        [Fact]
        public void LocalStorageMigratesToWorld()
        {
            var s = Settings.CreateDefault(); s.SpeedLimit = 500;
            Game.Utilities.Local[Settings.Filename] = s.ToString();
            Assert.Equal(500, Settings.Load().SpeedLimit);
            Assert.True(Game.Utilities.World.ContainsKey(Settings.Filename));
        }
        [Fact]
        public void MissingStorageCreatesDefaults()
        {
            Assert.Equal(140, Settings.Load().SpeedLimit);
            Assert.True(Game.Utilities.World.ContainsKey(Settings.Filename));
            Assert.True(Game.Utilities.Local.ContainsKey(Settings.Filename));
        }
        [Fact]
        public void InvalidXmlIsPreservedForRepair()
        {
            Game.Utilities.World[Settings.Filename] = "broken xml";
            Assert.Equal(140, Settings.Load().SpeedLimit);
            Assert.Equal("broken xml", Game.Utilities.World[Settings.Filename]);
        }
        [Fact]
        public void ClientDoesNotReadOrWriteServerConfiguration()
        {
            Game.BecomeServer(false);
            Game.Utilities.World[Settings.Filename] = "broken";
            Assert.Equal(140, Settings.Load().SpeedLimit);
            Settings.Save(Settings.CreateDefault());
            Assert.Equal("broken", Game.Utilities.World[Settings.Filename]);
        }
        [Fact]
        public void WriteFailureIsLoggedWithoutLosingLoadedSettings()
        {
            Game.Utilities.FailWrite = true;
            Assert.Equal(140, Settings.Load().SpeedLimit);
            Assert.True(Game.Log.Contains(VRage.Utils.LogSeverity.Error, "save"));
        }
        [Fact]
        public void WorldLimitsAndParachutesAreApplied()
        {
            var chute = new MyObjectBuilder_Parachute();
            var grid = new MyObjectBuilder_CubeGrid(); grid.CubeBlocks.Add(chute); grid.CubeBlocks.Add(new MyObjectBuilder_CubeBlock());
            var manager = MyDefinitionManager.Static;
            manager.Drops["drop"] = new MyDropContainerDefinition { Prefab = new Prefab { CubeGrids = new[] { grid } } };
            manager.Drops["empty"] = new MyDropContainerDefinition();
            var s = Settings.CreateDefault(); s.SpeedLimit = 600; s.ParachuteDeployHeight = 123; s.CalculateCurve();
            Assert.Equal(600, manager.EnvironmentDefinition.LargeShipMaxSpeed);
            Assert.Equal(600, manager.EnvironmentDefinition.SmallShipMaxSpeed);
            Assert.Equal(123, chute.DeployHeight);
        }
    }
}
