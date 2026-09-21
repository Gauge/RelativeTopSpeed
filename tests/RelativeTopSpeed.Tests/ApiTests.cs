using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class ApiTests : TestSession
    {
        [Fact]
        public void ApiCanLoadBeforeBackendAndUnloadCleanly()
        {
            var api = new RtsApi(); int ready = 0; api.Load(() => ready++); Assert.False(api.IsReady);
            Assert.Throws<InvalidOperationException>(() => api.GetCruiseSpeed(null));
            Start(Unfiltered()); Assert.True(api.IsReady); Assert.Equal(1, ready);
            var grid = Grid(); Assert.Equal(Mod.GetCruiseSpeed(grid), api.GetCruiseSpeed(grid));
            Assert.Equal(Mod.GetMaxSpeed(grid), api.GetMaxSpeed(grid));
            Assert.Equal(Mod.GetBoost(grid), api.GetBoost(grid));
            Assert.Equal(Mod.GetAcceleration(grid), api.GetAcceleration(grid));
            Assert.Equal(Mod.GetAccelerationsByDirection(grid), api.GetAccelerationByDirection(grid));
            Assert.Throws<Exception>(() => api.Load());
            api.Unload(); api.Unload(); Assert.False(api.IsReady);
            Assert.Throws<InvalidOperationException>(() => api.GetMaxSpeed(grid));
        }
        [Fact]
        public void ApiRequestsExistingBackendAndIgnoresUnrelatedMessages()
        {
            Start(Unfiltered()); var api = new RtsApi(); int calls = 0; api.Load(() => calls++);
            Assert.True(api.IsReady); Game.Utilities.SendModMessage(2772681332, new object());
            Game.Utilities.SendModMessage(2772681332, "ApiEndpointRequest"); Assert.Equal(1, calls); api.Unload();
        }
        [Fact]
        public void MalformedEndpointDoesNotMarkApiReady()
        {
            var api = new RtsApi(); api.Load();
            Assert.Throws<Exception>(() => Game.Utilities.SendModMessage(2772681332, new Dictionary<string, Delegate>()));
            Assert.False(api.IsReady); Assert.Throws<InvalidOperationException>(() => api.GetBoost(null)); api.Unload();
        }
        [Fact]
        public void BackendInitializationIsIdempotent()
        {
            Start(Unfiltered()); RtsApiBackend.Init(Mod);
            Assert.Single(Game.Utilities.ModHandlers[2772681332]);
            Assert.Throws<ArgumentNullException>(() => RtsApiBackend.Init(null));
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InvalidLastMethodCanRecoverWithoutPartialBinding(bool wrongSignature)
        {
            var api = new RtsApi();
            int ready = 0;
            api.Load(() => ready++);
            var endpoint = new Dictionary<string, Delegate>
            {
                ["GetCruiseSpeed"] = new Func<IMyCubeGrid, float>(grid => -123),
                ["GetMaxSpeed"] = new Func<IMyCubeGrid, float>(grid => -456),
                ["GetBoost"] = new Func<IMyCubeGrid, float[]>(grid => new float[4]),
                ["GetAcceleration"] = new Func<IMyCubeGrid, float[]>(grid => new float[4]),
            };
            if (wrongSignature)
                endpoint["GetAccelerationByDirection"] = new Func<IMyCubeGrid, float>(grid => 0);
            Assert.Throws<Exception>(() => Game.Utilities.SendModMessage(2772681332, endpoint));
            Assert.False(api.IsReady);
            Assert.Equal(0, ready);
            Assert.Throws<InvalidOperationException>(() => api.GetCruiseSpeed(Grid()));
            Start(Unfiltered());
            Assert.True(api.IsReady);
            Assert.Equal(1, ready);
            Assert.Equal(Mod.GetCruiseSpeed(Grid()), api.GetCruiseSpeed(Grid()));
            api.Unload();
        }

        [Fact]
        public void BoundCallsDoNotUseDiscoveryAndConsumerCanReload()
        {
            Start(Unfiltered());
            var api = new RtsApi();
            api.Load();
            int messages = 0;
            Action<object> observer = message => messages++;
            Game.Utilities.RegisterMessageHandler(2772681332, observer);
            try
            {
                var grid = Grid();
                for (int i = 0; i < 10; i++)
                {
                    Assert.Equal(Mod.GetCruiseSpeed(grid), api.GetCruiseSpeed(grid));
                    Assert.Equal(Mod.GetMaxSpeed(grid), api.GetMaxSpeed(grid));
                    Assert.Equal(Mod.GetBoost(grid), api.GetBoost(grid));
                    Assert.Equal(Mod.GetAcceleration(grid), api.GetAcceleration(grid));
                    Assert.Equal(Mod.GetAccelerationsByDirection(grid), api.GetAccelerationByDirection(grid));
                }
                Assert.Equal(0, messages);
                api.Unload();
                Assert.Throws<InvalidOperationException>(() => api.GetAccelerationByDirection(grid));
                api.Load();
                Assert.True(api.IsReady);
                Assert.Equal(Mod.GetMaxSpeed(grid), api.GetMaxSpeed(grid));
            }
            finally
            {
                api.Unload();
                Game.Utilities.UnregisterMessageHandler(2772681332, observer);
            }
        }
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApiMaximumRespectsBoostAndWorldCaps(bool boost)
        {
            var s = Unfiltered(boost); s.SpeedLimit = 600; s.LargeGrid.MaxBoostSpeed = 400; Start(s);
            var grid = Grid(); grid.CubeBlocks.Add(new Block { FatBlock = new Thruster { MaxThrust = 100000000, GridThrustDirection = new Vector3(0, 0, 1) } });
            Assert.Equal(boost ? 400 : Mod.GetCruiseSpeed(grid), Mod.GetMaxSpeed(grid));
            Assert.Equal(Mod.GetAcceleration(grid)[3] / s.LargeGrid.ResistanceMultiplier, Mod.GetBoost(grid)[3]);
        }
        [Fact]
        public void AllSixDirectionsAndPhysicsMassAreUsed()
        {
            Start(Unfiltered()); var grid = Grid(200, 10);
            var directions = new[] { new Vector3(0, 0, -1), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, -1, 0) };
            for (int i = 0; i < 6; i++) grid.CubeBlocks.Add(new Block { FatBlock = new Thruster { MaxThrust = 10 * (i + 1), GridThrustDirection = directions[i] } });
            grid.CubeBlocks.Add(new Block()); Assert.Equal(new[] { 1f, 2f, 3f, 4f, 5f, 6f }, Mod.GetAccelerationsByDirection(grid));
            Assert.Equal(new[] { 2f, 1f, 3.5f, 12f }, Mod.GetAcceleration(grid));
        }
        [Fact]
        public void MissingPhysicsAndZeroMassReturnSafeResults()
        {
            Start(); Assert.Equal(0, Mod.GetMaxSpeed(null)); Assert.Equal(new float[4], Mod.GetBoost(null));
            Assert.Equal(0, Mod.GetCruiseSpeed(null)); Assert.Equal(new float[6], Mod.GetAccelerationsByDirection(null));
            var grid = Grid(); grid.Physics.Mass = 0; Assert.Equal(new float[6], Mod.GetAccelerationsByDirection(grid));
            grid.Physics = null; Assert.Equal(0, Mod.GetMaxSpeed(grid));
        }
        [Fact]
        public void ScalarSpeedQueryDoesNotAllocateAndReadsChangingThrust()
        {
            var settings = Unfiltered(); settings.SpeedLimit = 600;
            settings.LargeGrid.MaxBoostSpeed = 500; Start(settings);
            var grid = Grid(200, 10000);
            var thruster = new Thruster { MaxThrust = 10000, GridThrustDirection = new Vector3(0, 0, 1) };
            grid.CubeBlocks.Add(new Block { FatBlock = thruster });
            for (int i = 0; i < 100; i++) Mod.GetMaxSpeed(grid);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) Mod.GetMaxSpeed(grid);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            float original = Mod.GetMaxSpeed(grid);
            thruster.MaxThrust *= 2;
            Assert.True(Mod.GetMaxSpeed(grid) > original);
            grid.Physics.Mass *= 2;
            Assert.Equal(original, Mod.GetMaxSpeed(grid));
        }

        [Fact]
        public void SummaryMatchesDirectionalApiAndReturnedArraysAreIndependent()
        {
            Start(Unfiltered()); var grid = Grid(200, 12345.67f);
            var vectors = new[] { new Vector3(0, 0, -1), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, -1, 0) };
            for (int i = 0; i < 60; i++) grid.CubeBlocks.Add(new Block
            {
                FatBlock = new Thruster { MaxThrust = 100.123f * (i + 1), GridThrustDirection = vectors[i % 6] }
            });
            var directions = Mod.GetAccelerationsByDirection(grid);
            var expected = SpeedMath.SummarizeAcceleration(directions);
            Assert.Equal(expected, Mod.GetAcceleration(grid));
            var boost = Mod.GetBoost(grid);
            for (int i = 0; i < 4; i++) Assert.Equal(expected[i] / Mod.cfg.Value.LargeGrid.ResistanceMultiplier, boost[i]);
            directions[0] = -999; boost[0] = -999;
            Assert.Equal(expected, Mod.GetAcceleration(grid));
            Assert.NotEqual(-999, Mod.GetAccelerationsByDirection(grid)[0]);
            Assert.NotEqual(-999, Mod.GetBoost(grid)[0]);
        }

        private class Block : IMySlimBlock { public object FatBlock { get; set; } }
        private class Thruster : IMyThrust { public float MaxThrust { get; set; } public Vector3 GridThrustDirection { get; set; } }
    }
}
