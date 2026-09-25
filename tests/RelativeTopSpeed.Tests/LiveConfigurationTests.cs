using System;
using System.Collections.Generic;
using SENetworkAPI;
using VRage.Game.ModAPI;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class LiveConfigurationTests : TestSession
    {
        private ConfigurationEdit Edit(float limit)
        {
            var s = Settings.CreateDefault(); s.SpeedLimit = limit;
            return new ConfigurationEdit { ExpectedXml = Mod.cfg.Value.ToString(), UpdatedXml = s.ToString() };
        }
        private void Send(ConfigurationEdit edit, ulong actualSender, ulong claimedSender)
        {
            var command = new Command
            {
                SteamId = claimedSender, CommandString = "settings", Timestamp = DateTime.UtcNow.Ticks,
                Data = Game.Utilities.SerializeToBinary(edit)
            };
            Game.Multiplayer.DeliverSecure(16341, Game.Utilities.SerializeToBinary(command), actualSender, false);
        }

        [Fact]
        public void HostEditSavesAppliesAndBroadcastsWithoutRestart()
        {
            Start(Unfiltered()); Game.NextFrame(); Game.ClearTraffic();
            Assert.StartsWith("Settings saved", Mod.ApplySettingsEdit(Edit(600)));
            Assert.Equal(600, Mod.cfg.Value.SpeedLimit);
            Assert.Equal(600, Sandbox.Definitions.MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed);
            Assert.Equal(600, Game.Utilities.SerializeFromXML<Settings>(Game.Utilities.World[Settings.Filename]).SpeedLimit);
            Game.NextFrame();
            Assert.NotEmpty(Game.Sent);
        }

        [Fact]
        public void RemoteEditUsesTransportIdentityAndRejectsForgedAdministrator()
        {
            Start();
            Game.Session.Promotions[200] = MyPromoteLevel.Admin;
            Send(Edit(600), 201, 200);
            Assert.Equal(140, Mod.cfg.Value.SpeedLimit);
            Send(Edit(600), 200, 201);
            Assert.Equal(600, Mod.cfg.Value.SpeedLimit);
        }

        [Fact]
        public void StaleDraftDoesNotOverwriteAnotherAdministratorsEdit()
        {
            Start(); var stale = Edit(700);
            Mod.ApplySettingsEdit(Edit(600));
            Assert.Contains("changed since", Mod.ApplySettingsEdit(stale));
            Assert.Equal(600, Mod.cfg.Value.SpeedLimit);
        }

        [Fact]
        public void InvalidCurveAndSaveFailureLeaveLiveSettingsUntouched()
        {
            Start(); var edit = Edit(600);
            var invalid = Settings.CreateDefault(); invalid.LargeGrid.CruiseCurve.Clear();
            edit.UpdatedXml = invalid.ToString();
            Assert.StartsWith("Settings rejected", Mod.ApplySettingsEdit(edit));
            Assert.Equal(140, Mod.cfg.Value.SpeedLimit);
            Game.Utilities.FailWrite = true;
            Assert.Contains("Could not save", Mod.ApplySettingsEdit(Edit(600)));
            Assert.Equal(140, Mod.cfg.Value.SpeedLimit);
        }

        [Fact]
        public void ClientSubmitsWithoutChangingLocalOrWorldSettings()
        {
            Game.BecomeServer(false); Game.Session.Promotions[100] = MyPromoteLevel.Admin;
            Start(); Game.NextFrame(); Game.ClearTraffic();
            var updated = Settings.CreateDefault(); updated.SpeedLimit = 600;
            Assert.StartsWith("Sent to server", Mod.SubmitSettings(Mod.cfg.Value.ToString(), updated));
            Assert.Equal(140, Mod.cfg.Value.SpeedLimit);
            Assert.Empty(Game.Utilities.World);
            Game.NextFrame(); Assert.NotEmpty(Game.Sent);
        }

        [Fact]
        public void ConfigWithoutAdditiveOptionsRetainsOldBehavior()
        {
            var old = Settings.CreateDefault().ToString().Replace("<EnableGridGroups>false</EnableGridGroups>", "");
            var parsed = Game.Utilities.SerializeFromXML<Settings>(old);
            Settings.Validate(ref parsed);
            Assert.False(parsed.EnableGridGroups);
        }

        [Theory]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-1")]
        [InlineData("1,5")]
        public void EditorRejectsInvalidNumbers(string text)
        {
            Assert.Throws<ArgumentException>(() => SettingsEditor.ParseNumber(text));
        }

        [Fact]
        public void ValidationAcceptsManyPointsAndRejectsTooFewOrDuplicateMasses()
        {
            var s = Settings.CreateDefault();
            s.SmallGrid.CruiseCurve = new List<CruisePoint>();
            for (int i = 0; i < SettingsEditor.MaxCurvePoints; i++) s.SmallGrid.CruiseCurve.Add(new CruisePoint { Mass = i * 100, Speed = 120 - i });
            Settings.Validate(ref s);
            Assert.Equal(SettingsEditor.MaxCurvePoints, s.SmallGrid.CruiseCurve.Count);
            s.SmallGrid.CruiseCurve = new List<CruisePoint> { new CruisePoint { Mass = 0, Speed = 120 }, new CruisePoint { Mass = 0, Speed = 100 } };
            Assert.Throws<ArgumentException>(() => Settings.Validate(ref s));
            s.SmallGrid.CruiseCurve = new List<CruisePoint> { new CruisePoint { Mass = 0, Speed = 120 } };
            Assert.Throws<ArgumentException>(() => Settings.Validate(ref s));
        }
    }
}
