using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class ThermalIntegrationTests : TestSession
    {
        [Fact]
        public void ClientsDoNotApplyPhysicsForces()
        {
            Game.BecomeServer(false);
            Start();
            Mod.cfg.SetValue(Unfiltered());
            var grid = Grid(); Frames(1);
            Assert.Empty(grid.Physics.Forces);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PhysicalGroupUsesCombinedMassOnceAndDistributesByBodyMass(bool boost)
        {
            var settings = Unfiltered(boost); settings.EnableGridGroups = true;
            Start(settings);
            var a = Grid(200, 2000000, true);
            var b = Grid(200, 3000000, false);
            var members = new List<IMyCubeGrid> { b, a };
            Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[a] = members; Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[b] = members;
            Frames(1);
            float cruise = Mod.GetCruiseSpeedForMass(5000000, true);
            Assert.Equal(5000000, Mod.GetEffectiveMass(b));
            Assert.Equal(cruise, Mod.GetEffectiveCruiseSpeed(b));
            var forceA = Assert.Single(a.Physics.Forces);
            var forceB = Assert.Single(b.Physics.Forces);
            Assert.Equal(forceA.Force.X / 2000000, forceB.Force.X / 3000000, 4);
            if (!boost)
            {
                Assert.Equal(cruise, a.Physics.Speed, 3);
                Assert.Equal(cruise, b.Physics.Speed, 3);
            }
        }

        [Fact]
        public void GroupChangesAreObservedWithoutWaitingForActivationRefresh()
        {
            var settings = Unfiltered(); settings.EnableGridGroups = true;
            Start(settings); var a = Grid(200, 2000000); var b = Grid(200, 3000000);
            var members = new List<IMyCubeGrid> { a, b };
            Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[a] = members; Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[b] = members;
            Frames(1);
            float grouped = a.Physics.Forces[0].Force.X;
            Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups.Clear(); Frames(1);
            Assert.NotEqual(grouped, a.Physics.Forces[1].Force.X);
            Assert.Equal(2000000, Mod.GetEffectiveMass(a));
        }

        [Fact]
        public void SharedPhysicsIsCountedAndUpdatedOnceAndStaticGroupsAreSkipped()
        {
            var settings = Unfiltered(); settings.EnableGridGroups = true;
            Start(settings); var a = Grid(); var b = Grid(); b.Physics = a.Physics;
            var members = new List<IMyCubeGrid> { a, b };
            Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[a] = members; Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[b] = members;
            Assert.Equal(a.Physics.Mass, Mod.GetEffectiveMass(a));
            Frames(1); Assert.Single(a.Physics.Forces);
            b.SetStatic(true); Frames(1); Assert.Single(a.Physics.Forces);
        }

        [Fact]
        public void NewApiDelegatesExposeMassCurveAndConfiguredCeiling()
        {
            Start(Unfiltered(false));
            var api = new RtsApi(); api.Load();
            Assert.Equal(140, api.GetWorldSpeedLimit());
            Assert.Equal(80, api.GetCruiseSpeedForMass(5000000, true));
            Assert.Equal(80, api.GetSpeedCeilingForMass(5000000, true));
            Assert.Equal(0, api.GetCruiseSpeedForMass(float.NaN, true));
            var grid = Grid();
            Assert.Equal(grid.Physics.Mass, api.GetEffectiveMass(grid));
            Assert.Equal(api.GetCruiseSpeed(grid), api.GetEffectiveCruiseSpeed(grid));
            api.Unload();
        }

        [Fact]
        public void ApiExposesGroupModeAndMixedGroupsUseLargeGridSettings()
        {
            var settings = Unfiltered(); settings.EnableGridGroups = true;
            Start(settings);
            var api = new RtsApi(); api.Load();
            var ship = Grid(200, 2000000, true);
            var fighter = Grid(200, 30000, false);
            var members = new List<IMyCubeGrid> { ship, fighter };
            Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[ship] = members; Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[fighter] = members;
            Assert.True(api.IsGridGroupsEnabled());
            Assert.Equal(2030000, api.GetEffectiveMass(fighter));
            Assert.Equal(api.GetCruiseSpeedForMass(2030000, true), api.GetEffectiveCruiseSpeed(fighter));
            Assert.Equal(api.GetSpeedCeilingForMass(2030000, true), api.GetEffectiveSpeedCeiling(fighter));
            Assert.Equal(api.GetEffectiveSpeedCeiling(ship), api.GetEffectiveSpeedCeiling(fighter));

            settings = Unfiltered(); settings.EnableGridGroups = false; Mod.cfg.Value = settings;
            Assert.False(api.IsGridGroupsEnabled());
            Assert.Equal(30000, api.GetEffectiveMass(fighter));
            Assert.Equal(api.GetSpeedCeilingForMass(30000, false), api.GetEffectiveSpeedCeiling(fighter));
            api.Unload();
        }

        [Fact]
        public void IsGovernedFollowsStaticGroupsAndFilters()
        {
            var settings = Unfiltered(); settings.EnableGridGroups = true;
            Start(settings);
            var api = new RtsApi(); api.Load();
            var ship = Grid(); var station = Grid();
            Assert.True(api.IsGoverned(ship));
            Assert.False(api.IsGoverned(null));
            var members = new List<IMyCubeGrid> { ship, station };
            Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[ship] = members; Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups[station] = members;
            station.SetStatic(true);
            Assert.False(api.IsGoverned(ship));
            Sandbox.ModAPI.MyAPIGateway.GridGroups.Groups.Clear(); station.SetStatic(false);

            settings = Unfiltered(); settings.IgnoreGridsWithoutThrust = true; Mod.cfg.Value = settings;
            Assert.False(api.IsGoverned(ship));
            api.Unload();
        }
    }
}
