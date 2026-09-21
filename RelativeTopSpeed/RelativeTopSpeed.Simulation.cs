using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace RelativeTopSpeed
{
    public partial class RelativeTopSpeed
    {
        private const int RefreshFrames = 180;
        private readonly HashSet<MyCubeGrid> grids = new HashSet<MyCubeGrid>();
        private readonly List<MyCubeGrid> activeGrids = new List<MyCubeGrid>();
        private readonly Dictionary<MyCubeGrid, bool> activationCache = new Dictionary<MyCubeGrid, bool>();
        private readonly List<IMyCubeGrid> groupBuffer = new List<IMyCubeGrid>();
        private readonly MyObjectBuilderType thrustType = MyObjectBuilderType.ParseBackwardsCompatible("Thrust");
        private readonly MyObjectBuilderType cockpitType = MyObjectBuilderType.ParseBackwardsCompatible("Cockpit");
        private int refreshCountdown;
        private int hudCountdown;

        private void AddGrid(IMyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            // Retain grids whose physics has not been initialized yet.
            if (grid == null || !grids.Add(grid)) return;
            grid.OnStaticChanged += OnStaticChanged;
            refreshCountdown = 0;
        }

        private void RemoveGrid(IMyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            if (grid == null || !grids.Remove(grid)) return;
            grid.OnStaticChanged -= OnStaticChanged;
            activeGrids.Remove(grid);
            activationCache.Remove(grid);
        }

        private void OnStaticChanged(MyCubeGrid grid, bool isStatic) { refreshCountdown = 0; }

        private bool HasActivationBlock(MyCubeGrid grid)
        {
            var settings = cfg.Value;
            if (!settings.IgnoreGridsWithoutThrust && !settings.IgnoreGridsWithoutCockpit) return true;
            bool eligible;
            if (activationCache.TryGetValue(grid, out eligible)) return eligible;
            // Most ships satisfy their filters themselves; no group lookup needed.
            int count;
            bool thrust = !settings.IgnoreGridsWithoutThrust ||
                (grid.BlocksCounters.TryGetValue(thrustType, out count) && count > 0);
            bool cockpit = !settings.IgnoreGridsWithoutCockpit ||
                (grid.BlocksCounters.TryGetValue(cockpitType, out count) && count > 0);
            if (thrust && cockpit) return true;

            groupBuffer.Clear();
            MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Mechanical, groupBuffer);
            foreach (var member in groupBuffer)
            {
                var sub = member as MyCubeGrid;
                if (sub == null) continue;
                if (!thrust) thrust = sub.BlocksCounters.TryGetValue(thrustType, out count) && count > 0;
                if (!cockpit) cockpit = sub.BlocksCounters.TryGetValue(cockpitType, out count) && count > 0;
                if (thrust && cockpit) break;
            }
            eligible = thrust && cockpit;
            // Membership and block state are sampled once per refresh, never
            // retained across refreshes or configuration changes.
            foreach (var member in groupBuffer)
            {
                var sub = member as MyCubeGrid;
                if (sub != null) activationCache[sub] = eligible;
            }
            activationCache[grid] = eligible;
            return eligible;
        }

        public override void Simulate()
        {
            if (pendingConfigurationNotice != null && !MyAPIGateway.Utilities.IsDedicated
                && MyAPIGateway.Session?.LocalHumanPlayer != null)
            {
                MyAPIGateway.Utilities.ShowMessage(ModName, pendingConfigurationNotice);
                pendingConfigurationNotice = null;
            }
            if (cfg == null) return;
            if (refreshCountdown-- <= 0)
            {
                activeGrids.Clear();
                activationCache.Clear();
                foreach (var grid in grids)
                {
                    if (grid.Physics == null || grid.IsStatic || grid.Closed) continue;
                    if ((grid.Physics.LinearVelocity.LengthSquared() > 1 ||
                         grid.Physics.LinearAcceleration.LengthSquared() > 1) && HasActivationBlock(grid))
                        activeGrids.Add(grid);
                }
                refreshCountdown = RefreshFrames - 1;
            }
            for (int i = 0; i < activeGrids.Count; i++) UpdateGrid(activeGrids[i]);
            if (hudCountdown-- <= 0)
            {
                UpdateHud();
                hudCountdown = 9;
            }
        }

        private void UpdateGrid(MyCubeGrid grid)
        {
            if (grid.Closed || grid.IsStatic || grid.Physics == null) return;
            var physics = ((IMyCubeGrid)grid).Physics;
            var settings = cfg.Value;
            bool large = grid.GridSizeEnum == MyCubeSize.Large;
            float minimum = large ? settings.LargeGrid.MinimumCruiseSpeed : settings.SmallGrid.MinimumCruiseSpeed;
            if (physics.LinearVelocity.LengthSquared() <= minimum * minimum) return;
            float mass = physics.Mass;
            if (!(mass > 0) || float.IsInfinity(mass)) return;
            float speed = physics.Speed;
            float cruise = settings.GetCruiseSpeed(mass, large);
            if (speed <= cruise) return;
            if (settings.EnableBoosting)
            {
                float resistance = large ? settings.LargeGrid.ResistanceMultiplier : settings.SmallGrid.ResistanceMultiplier;
                float boostLimit = large ? settings.LargeGrid.MaxBoostSpeed : settings.SmallGrid.MaxBoostSpeed;
                float force = SpeedMath.Resistance(mass, speed, cruise, resistance);
                physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE,
                    physics.LinearVelocity * -force, physics.CenterOfMassWorld, null, boostLimit);
            }
            else
            {
                // Remove only excess velocity. This also caps a coasting grid
                // after cargo is added or settings are reloaded.
                Vector3 impulse = physics.LinearVelocity * (-mass * SpeedMath.ExcessSpeedFraction(speed, cruise));
                physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE,
                    impulse, physics.CenterOfMassWorld, null);
            }
        }

        private void UpdateHud()
        {
            if ((!showHud && !Settings.Debug) || MyAPIGateway.Utilities.IsDedicated) return;
            var player = MyAPIGateway.Session?.LocalHumanPlayer;
            if (Settings.Debug && player != null && IsAllowedSpecialOperations(player.SteamUserId))
                MyAPIGateway.Utilities.ShowNotification($"Grids - Tracked: {grids.Count}  Active: {activeGrids.Count}", 200);
            if (!showHud) return;
            var controlled = player?.Controller?.ControlledEntity as IMyCubeBlock;
            var grid = controlled?.CubeGrid;
            if (grid?.Physics == null) return;
            float mass = grid.Physics.Mass;
            float cruise = GetCruiseSpeed(mass, grid.GridSizeEnum == MyCubeSize.Large);
            MyAPIGateway.Utilities.ShowNotification(
                $"Mass: {mass:n0}   Cruise: {cruise:n2}   Max Boost: {GetMaximumBoost(grid):n2}", 200);
        }
    }
}
