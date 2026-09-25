using System;
using Sandbox.Game.Entities;
using SEStubs;
using SENetworkAPI;
using VRage.Game;
using VRageMath;
using Xunit;
using Mod = RelativeTopSpeed.RelativeTopSpeed;

// The upstream fixture disables parallel tests for the shared game gateway.

namespace RelativeTopSpeed.Tests
{
    public abstract class TestSession : IDisposable
    {
        protected readonly FakeGame Game = FakeGame.StartServer();
        protected Mod Mod;
        protected TestSession() { RichHudFramework.Client.RichHudClient.Clear(); }
        protected Mod Start(Settings settings = null)
        {
            if (settings != null) Game.Utilities.World[Settings.Filename] = Game.Utilities.SerializeToXML(settings);
            Mod = new Mod();
            Mod.Init(new MyObjectBuilder_SessionComponent());
            Mod.BeforeStart();
            return Mod;
        }
        protected MyCubeGrid Grid(float speed = 200, float mass = 10000, bool large = true)
        {
            var grid = new MyCubeGrid { GridSizeEnum = large ? MyCubeSize.Large : MyCubeSize.Small };
            grid.Physics.LinearVelocity = new Vector3(speed, 0, 0);
            grid.Physics.Mass = mass;
            return Game.Entities.Add(grid);
        }
        protected Settings Unfiltered(bool boost = true)
        {
            var settings = Settings.CreateDefault();
            settings.IgnoreGridsWithoutThrust = settings.IgnoreGridsWithoutCockpit = false;
            settings.EnableBoosting = boost;
            return settings;
        }
        protected void Frames(int count) { for (int i = 0; i < count; i++) { Mod.Simulate(); Game.NextFrame(); } }
        public void Dispose()
        {
            Mod?.SimulateUnload();
            // The current network dependency owns cleanup in its session component.
            new SessionTools().SimulateUnload();
            RichHudFramework.Client.RichHudClient.Clear();
            RtsApiBackend.Close();
            Settings.Instance = null;
            Game.Dispose();
        }
    }
}
