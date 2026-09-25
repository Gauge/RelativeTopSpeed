using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SEStubs;
using SENetworkAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class SimulationTests : TestSession
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ReportedConfigAppliesDragForBothGridSizes(bool large)
        {
            var s = Unfiltered(); s.SpeedLimit = 600;
            s.LargeGrid.CruiseCurve = new System.Collections.Generic.List<CruisePoint>
            {
                new CruisePoint { Mass = 200000, Speed = 250 }, new CruisePoint { Mass = 8000000, Speed = 140 }
            };
            s.SmallGrid.CruiseCurve = new System.Collections.Generic.List<CruisePoint>
            {
                new CruisePoint { Mass = 10000, Speed = 350 }, new CruisePoint { Mass = 400000, Speed = 140 }
            };
            s.LargeGrid.MaxBoostSpeed = 400; s.SmallGrid.MaxBoostSpeed = 500;
            Start(s); var grid = Grid(550, large ? 1000000 : 100000, large); Frames(1);
            var force = Assert.Single(grid.Physics.Forces);
            Assert.Equal(MyPhysicsForceType.APPLY_WORLD_FORCE, force.Type);
            Assert.True(float.IsFinite(force.Force.X)); Assert.True(force.Force.X < 0);
            Assert.Equal(large ? 400 : 500, force.Limit);
        }
        [Fact]
        public void HardCapSlowsCoastingGridWithoutAcceleration()
        {
            Start(Unfiltered(false)); var grid = Grid(); Frames(1);
            Assert.Equal(Mod.GetCruiseSpeed(grid), grid.Physics.Speed, 3);
            Frames(2); Assert.Single(grid.Physics.Forces);
        }
        [Fact]
        public void BelowCruiseDoesNotApplyForce()
        { Start(Unfiltered()); var grid = Grid(50); Frames(1); Assert.Empty(grid.Physics.Forces); }
        [Fact]
        public void StaticAndInvalidMassGridsDoNotApplyForce()
        {
            Start(Unfiltered()); var grid = Grid(); grid.SetStatic(true); Frames(1); Assert.Empty(grid.Physics.Forces);
            grid.SetStatic(false); grid.Physics.Mass = 0; Frames(1); Assert.Empty(grid.Physics.Forces);
            grid.Physics.Mass = float.PositiveInfinity; Frames(1); Assert.Empty(grid.Physics.Forces);
        }
        [Fact]
        public void ExistingAndDuplicateGridsAreRegisteredExactlyOnce()
        {
            var grid = Grid(); Start(Unfiltered()); Game.Entities.Add(grid); Mod.BeforeStart(); Frames(1);
            Assert.Single(grid.Physics.Forces); Assert.Equal(1, grid.StaticSubscribers);
        }
        [Fact]
        public void DelayedPhysicsIsEventuallyActivatedAndRemovedWithoutPhysics()
        {
            Start(Unfiltered()); var grid = Grid(); var physics = grid.Physics; grid.Physics = null; Frames(1);
            grid.Physics = physics; Frames(180); Assert.NotEmpty(physics.Forces);
            grid.Physics = null; Game.Entities.Remove(grid.EntityId); Frames(180);
            Assert.Equal(0, grid.StaticSubscribers);
        }
        [Fact]
        public void StoppedGridActivatesWhenItStartsMoving()
        {
            Start(Unfiltered()); var grid = Grid(0); Frames(1); Assert.Empty(grid.Physics.Forces);
            grid.Physics.LinearVelocity = new Vector3(200, 0, 0); Frames(180); Assert.NotEmpty(grid.Physics.Forces);
        }
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void ActivationRequirementsCanBeSatisfiedAcrossSubgrids(bool thrust, bool cockpit)
        {
            var s = Unfiltered(); s.IgnoreGridsWithoutThrust = thrust; s.IgnoreGridsWithoutCockpit = cockpit; Start(s);
            var grid = Grid(); Frames(1); Assert.Empty(grid.Physics.Forces);
            var sub = new MyCubeGrid();
            grid.BlocksCounters[MyObjectBuilderType.ParseBackwardsCompatible("Thrust")] = 1;
            sub.BlocksCounters[MyObjectBuilderType.ParseBackwardsCompatible("Cockpit")] = 1;
            MyAPIGateway.GridGroups.Groups[grid] = new System.Collections.Generic.List<IMyCubeGrid> { grid, sub };
            Frames(180); Assert.NotEmpty(grid.Physics.Forces);
        }
        [Fact]
        public void DisabledFiltersSkipGroupQueries()
        { Start(Unfiltered()); for (int i = 0; i < 100; i++) Grid(); Frames(200); Assert.Equal(0, MyAPIGateway.GridGroups.Queries); }
        [Fact]
        public void LocalReloadUpdatesSettingsConsumersAndWorldLimits()
        {
            Start(Unfiltered()); Settings observed = null;
            RelativeTopSpeed.SettingsChanged += (s) => observed = s;
            var changed = Unfiltered(); changed.SpeedLimit = 600;
            Game.Utilities.World[Settings.Filename] = changed.ToString();
            Assert.False(Game.Utilities.SimulateChat("/RTS LOAD"));
            Assert.Same(Mod.cfg.Value, Settings.Instance); Assert.Same(Settings.Instance, observed);
            Assert.Equal(600, Sandbox.Definitions.MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed);
        }
        [Fact]
        public void CleanupAllowsANewSessionWithPropertyIdOne()
        {
            Start(Unfiltered()); var grid = Grid(); var first = Mod.cfg.Id; Mod.SimulateUnload(); new SessionTools().SimulateUnload(); Mod = null;
            Assert.Equal(0, grid.StaticSubscribers); Assert.False(Game.Multiplayer.Secure.ContainsKey(16341));
            Assert.Equal(0, Game.Utilities.MessageEnteredSubscriberCount);
            Start(Unfiltered()); Assert.Equal(first, Mod.cfg.Id); Assert.Equal(1, Mod.cfg.Id);
        }
        [Fact]
        public void HudHandlesMissingPlayerAndThrottlesUpdates()
        {
            Start(Unfiltered()); Game.Utilities.SimulateChat("/rts hud");
            var player = (FakePlayer)Game.Session.LocalHumanPlayer;
            Game.Session.LocalHumanPlayer = null; Frames(1);
            Game.Session.LocalHumanPlayer = player; player.Controller.ControlledEntity = new Cockpit { CubeGrid = Grid() };
            Frames(30); Assert.Equal(3, Game.Utilities.Notifications.Count);
            Game.Utilities.SimulateChat("/rts config"); Assert.Contains("Settings", Game.Utilities.Mission);
            Game.Utilities.SimulateChat("/rts"); Assert.Contains(Game.ShownMessages, m => m.Text.Contains("Relative Top Speed"));
        }
        [Fact]
        public void RemoteControlWaitsForFrameAndFollowsReloadUntilRemoved()
        {
            Start(Unfiltered()); var slider = new Slider(); MyAPIGateway.TerminalControls.Controls.Add(slider);
            var entity = new RemoteEntity(); var logic = new RemoteControl { Entity = entity };
            logic.Init(new VRage.Game.MyObjectBuilder_EntityBase()); Assert.Equal(0, entity.SpeedLimit);
            logic.UpdateOnceBeforeFrame(); Assert.Equal(100, entity.SpeedLimit); Assert.Equal(100, slider.Max);
            var settings = Unfiltered(); settings.RemoteControlSpeedLimit = 120; Mod.cfg.Value = settings;
            Assert.Equal(120, entity.SpeedLimit); Assert.Equal(120, slider.Max);
            logic.OnBeforeRemovedFromContainer(); Mod.cfg.Value = Unfiltered(); Assert.Equal(120, entity.SpeedLimit);
        }
        [Fact]
        public void WarmSimulationLoopDoesNotAllocatePerGridOrFrame()
        {
            Start(Unfiltered());
            for (int i = 0; i < 100; i++) Grid().Physics.RecordForces = false;
            Frames(360);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 360; i++) Mod.Simulate();
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        [Fact]
        public void RemovedRequiredBlockDeactivatesGridAtNextRefresh()
        {
            var s = Unfiltered(); s.IgnoreGridsWithoutThrust = true; Start(s);
            var grid = Grid(); var type = MyObjectBuilderType.ParseBackwardsCompatible("Thrust"); grid.BlocksCounters[type] = 1;
            Frames(180); int forces = grid.Physics.ForceCount;
            grid.BlocksCounters[type] = 0; Frames(180); Assert.Equal(forces, grid.Physics.ForceCount);
        }
        [Fact] public void DebugStatisticsRemainAvailableWithoutShipHud()
        {
            Start(Unfiltered());
            Settings.Debug = true;
            try
            {
                Frames(1);
                Assert.Contains(Game.Utilities.Notifications, text => text.Contains("Tracked:"));
            }
            finally { Settings.Debug = false; }
        }
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MechanicalGroupIsExaminedOncePerRefresh(bool hasThrust)
        {
            var settings = Unfiltered(); settings.IgnoreGridsWithoutThrust = true; Start(settings);
            var members = new System.Collections.Generic.List<IMyCubeGrid>();
            for (int i = 0; i < 10; i++) members.Add(Grid());
            var last = (MyCubeGrid)members[9];
            var thrust = MyObjectBuilderType.ParseBackwardsCompatible("Thrust");
            last.BlocksCounters[thrust] = hasThrust ? 1 : 0;
            foreach (var member in members) MyAPIGateway.GridGroups.Groups[member] = members;
            Frames(1);
            Assert.Equal(1, MyAPIGateway.GridGroups.Queries);
            foreach (MyCubeGrid member in members) Assert.Equal(hasThrust ? 1 : 0, member.Physics.ForceCount);
            // The next refresh must see block removal or addition, including a cached failure.
            last.BlocksCounters[thrust] = hasThrust ? 0 : 1;
            Frames(180);
            Assert.Equal(2, MyAPIGateway.GridGroups.Queries);
            foreach (MyCubeGrid member in members) Assert.Equal(hasThrust ? 180 : 1, member.Physics.ForceCount);
        }

        [Fact]
        public void SelfContainedShipDoesNotQueryMechanicalGroup()
        {
            var settings = Unfiltered(); settings.IgnoreGridsWithoutThrust = true;
            settings.IgnoreGridsWithoutCockpit = true; Start(settings);
            var grid = Grid();
            grid.BlocksCounters[MyObjectBuilderType.ParseBackwardsCompatible("Thrust")] = 1;
            grid.BlocksCounters[MyObjectBuilderType.ParseBackwardsCompatible("Cockpit")] = 1;
            Frames(1);
            Assert.Single(grid.Physics.Forces);
            Assert.Equal(0, MyAPIGateway.GridGroups.Queries);
        }

        [Fact]
        public void ActivationCacheDoesNotSurviveSettingsOrTopologyChanges()
        {
            var settings = Unfiltered(); settings.IgnoreGridsWithoutThrust = true; Start(settings);
            var grid = Grid(); var sub = Grid();
            sub.BlocksCounters[MyObjectBuilderType.ParseBackwardsCompatible("Thrust")] = 1;
            var group = new System.Collections.Generic.List<IMyCubeGrid> { grid, sub };
            MyAPIGateway.GridGroups.Groups[grid] = group;
            MyAPIGateway.GridGroups.Groups[sub] = group;
            Frames(1); Assert.Single(grid.Physics.Forces);
            settings = Unfiltered(); settings.IgnoreGridsWithoutCockpit = true;
            Mod.cfg.Value = settings;
            Frames(1); Assert.Single(grid.Physics.Forces);
            settings = Unfiltered(); settings.IgnoreGridsWithoutThrust = true;
            Mod.cfg.Value = settings;
            MyAPIGateway.GridGroups.Groups[grid] = new System.Collections.Generic.List<IMyCubeGrid> { grid };
            MyAPIGateway.GridGroups.Groups[sub] = new System.Collections.Generic.List<IMyCubeGrid> { sub };
            Frames(1); Assert.Single(grid.Physics.Forces);
            Assert.Equal(2, sub.Physics.ForceCount);
        }

        private class Cockpit : IMyCubeBlock { public IMyCubeGrid CubeGrid { get; set; } }
        private class RemoteEntity : VRage.Game.Entity.MyEntity, IMyRemoteControl { public float SpeedLimit { get; set; } }
        private class Slider : IMyTerminalControlSlider
        { public string Id => "SpeedLimit"; public float Max; public void SetLimits(float min, float max) { Max = max; } }
    }
}
