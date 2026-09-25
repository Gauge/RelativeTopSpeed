using System.Collections.Generic;
using RichHudFramework.Client;
using RichHudFramework.UI.Client;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class RichHudTests : TestSession
    {
        private FakeSettingsView View { get { return FakeSettingsView.Last; } }

        [Fact]
        public void MissingFrameworkIsOptionalAndLateRegistrationBuildsTheWindowOnce()
        {
            FakeSettingsView.Last = null;
            Start();
            Assert.Null(View);
            Game.Utilities.SimulateChat("/rts menu");
            Assert.Contains(Game.ShownMessages, m => m.Text.Contains("optional Rich HUD"));
            RichHudClient.Register();
            var first = View;
            Game.Utilities.SimulateChat("/rts menu");
            Assert.True(first.Opened);
            RichHudClient.Register(); Assert.Same(first, View);
        }

        [Fact]
        public void WindowAppliesAllScalarSettingsAndPersistsOnlyOnApply()
        {
            RichHudClient.Available = true; Start();
            View.Text["Limits/World speed limit (m/s)"] = "600";
            View.Text["Limits/Remote control limit (m/s)"] = "300";
            View.Text["Limits/Parachute height (m)"] = "700";
            View.Text["Large grids/Resistance"] = "2.5";
            View.Flags["World/Enable grid groups"] = true;
            View.Flags["World/Allow boosting"] = false;
            Assert.Equal(140, Mod.cfg.Value.SpeedLimit);
            View.Apply();
            Assert.Equal(600, Mod.cfg.Value.SpeedLimit);
            Assert.Equal(300, Mod.cfg.Value.RemoteControlSpeedLimit);
            Assert.Equal(700, Mod.cfg.Value.ParachuteDeployHeight);
            Assert.Equal(2.5f, Mod.cfg.Value.LargeGrid.ResistanceMultiplier);
            Assert.True(Mod.cfg.Value.EnableGridGroups);
            Assert.False(Mod.cfg.Value.EnableBoosting);
            Assert.Equal("600", View.Text["Limits/World speed limit (m/s)"]);
            Assert.Contains("<SpeedLimit>600</SpeedLimit>", Game.Utilities.World[Settings.Filename]);
        }

        [Fact]
        public void CurvePointsCanBeAddedAndRemovedAndAreSortedOnApply()
        {
            RichHudClient.Available = true; Start();
            var small = View.Curves["Small grids/Cruise curve"];
            Assert.Equal(3, small.Count);
            small.Add(SettingsEditor.NextPoint(small));
            small.Insert(0, new CruisePoint { Mass = 5000, Speed = 120 });
            View.Apply();
            Assert.Equal(5, Mod.cfg.Value.SmallGrid.CruiseCurve.Count);
            Assert.Equal(5000, Mod.cfg.Value.SmallGrid.CruiseCurve[0].Mass);
            Assert.Equal(800000, Mod.cfg.Value.SmallGrid.CruiseCurve[4].Mass);

            var large = View.Curves["Large grids/Cruise curve"];
            large.RemoveRange(1, large.Count - 1);
            View.Apply();
            Assert.StartsWith("Not applied", View.Status);
            Assert.Equal(3, Mod.cfg.Value.LargeGrid.CruiseCurve.Count);
        }

        [Fact]
        public void NextPointDoublesTheHeaviestMassAndKeepsItsSpeed()
        {
            var next = SettingsEditor.NextPoint(new List<CruisePoint> { new CruisePoint { Mass = 0, Speed = 90 } });
            Assert.Equal(1000, next.Mass); Assert.Equal(90, next.Speed);
            next = SettingsEditor.NextPoint(new List<CruisePoint> { new CruisePoint { Mass = 300, Speed = 70 } });
            Assert.Equal(600, next.Mass); Assert.Equal(70, next.Speed);
        }

        [Fact]
        public void WindowProtectsDirtyDraftAgainstOtherAdministratorAndCanReloadOrReset()
        {
            RichHudClient.Available = true; Start();
            View.Text["Limits/World speed limit (m/s)"] = "600";
            var other = Settings.CreateDefault(); other.SpeedLimit = 500; Mod.cfg.Value = other;
            View.Apply();
            Assert.Equal(500, Mod.cfg.Value.SpeedLimit);
            Assert.Equal("600", View.Text["Limits/World speed limit (m/s)"]);
            View.Reload();
            Assert.Equal("500", View.Text["Limits/World speed limit (m/s)"]);
            View.ResetDraft();
            Assert.Equal("140", View.Text["Limits/World speed limit (m/s)"]);
            Assert.Equal(500, Mod.cfg.Value.SpeedLimit);
        }

        [Fact]
        public void NonAdministratorCannotApplyButCanToggleOwnHud()
        {
            RichHudClient.Available = true; Game.BecomeServer(false); Start();
            View.Text["Limits/World speed limit (m/s)"] = "600";
            View.Apply();
            Assert.Equal(140, Mod.cfg.Value.SpeedLimit);
            View.SetHud(true);
            Assert.True(Mod.ShowHud);
            Mod.SimulateUnload(); Assert.False(RichHudTerminal.Root.Enabled);
        }

        [Fact]
        public void DedicatedServerNeverRegistersMenu()
        {
            FakeSettingsView.Last = null;
            Game.Utilities.IsDedicated = true; RichHudClient.Available = true; Start();
            Assert.False(RichHudClient.Registered); Assert.Null(View);
        }
    }
}
