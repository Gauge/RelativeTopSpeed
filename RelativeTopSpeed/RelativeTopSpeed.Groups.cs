using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace RelativeTopSpeed
{
    public partial class RelativeTopSpeed
    {
        private readonly List<IMyCubeGrid> physicalGroup = new List<IMyCubeGrid>();
        private readonly HashSet<IMyCubeGrid> handledGroupGrids = new HashSet<IMyCubeGrid>();
        private readonly HashSet<object> handledBodies = new HashSet<object>();

        private void ReadPhysicalGroup(IMyCubeGrid grid, List<IMyCubeGrid> result)
        {
            result.Clear();
            MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical, result);
            if (!result.Contains(grid)) result.Add(grid);
        }

        private float GroupMass(List<IMyCubeGrid> members, out bool large, out bool anchored)
        {
            handledBodies.Clear();
            double mass = 0;
            large = false;
            anchored = false;
            foreach (var member in members)
            {
                if (member == null || member.Closed) continue;
                large |= member.GridSizeEnum == MyCubeSize.Large;
                anchored |= member.IsStatic;
                if (member.Physics == null || !handledBodies.Add(member.Physics)) continue;
                float bodyMass = member.Physics.Mass;
                if (bodyMass > 0 && !float.IsInfinity(bodyMass)) mass += bodyMass;
            }
            return mass > float.MaxValue ? 0 : (float)mass;
        }

        private void UpdatePhysicalGroup(MyCubeGrid grid)
        {
            if (grid.Closed || grid.IsStatic || grid.Physics == null) return;
            if (handledGroupGrids.Contains(grid)) return;
            ReadPhysicalGroup(grid, physicalGroup);
            foreach (var member in physicalGroup) if (member != null) handledGroupGrids.Add(member);
            bool large, anchored;
            float mass = GroupMass(physicalGroup, out large, out anchored);
            if (anchored || !(mass > 0)) return;
            Vector3 velocity = Vector3.Zero;
            handledBodies.Clear();
            foreach (var member in physicalGroup)
            {
                if (member == null || member.Closed || member.Physics == null || !handledBodies.Add(member.Physics)) continue;
                float bodyMass = member.Physics.Mass;
                if (bodyMass > 0 && !float.IsInfinity(bodyMass))
                    velocity += member.Physics.LinearVelocity * (bodyMass / mass);
            }
            float speed = velocity.Length();
            if (float.IsNaN(speed) || float.IsInfinity(speed)) return;
            float cruise = GetCruiseSpeedForMass(mass, large);
            if (speed <= cruise) return;
            float fraction = SpeedMath.ExcessSpeedFraction(speed, cruise);
            var gridSettings = large ? cfg.Value.LargeGrid : cfg.Value.SmallGrid;
            handledBodies.Clear();
            foreach (var member in physicalGroup)
            {
                if (member == null || member.Closed || member.Physics == null || !handledBodies.Add(member.Physics)) continue;
                var physics = member.Physics;
                float bodyMass = physics.Mass;
                if (!(bodyMass > 0) || float.IsInfinity(bodyMass)) continue;
                // Equal translational acceleration, distributed by mass at each body's centre.
                // This sums to a force through the group COM without adding a net torque.
                Vector3 force = velocity * (-bodyMass * fraction);
                if (cfg.Value.EnableBoosting)
                    physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force * gridSettings.ResistanceMultiplier,
                        physics.CenterOfMassWorld, null, GetSpeedCeilingForMass(mass, large));
                else
                    physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE,
                        force, physics.CenterOfMassWorld, null);
            }
        }
        public float GetWorldSpeedLimit() { return cfg.Value.SpeedLimit; }
        public float GetCruiseSpeedForMass(float mass, bool largeGrid)
        {
            if (!(mass >= 0) || float.IsInfinity(mass)) return 0;
            return cfg.Value.GetCruiseSpeed(mass, largeGrid);
        }
        public float GetSpeedCeilingForMass(float mass, bool largeGrid)
        {
            var settings = cfg.Value;
            float cruise = GetCruiseSpeedForMass(mass, largeGrid);
            var gridSettings = largeGrid ? settings.LargeGrid : settings.SmallGrid;
            return Math.Min(settings.SpeedLimit, settings.EnableBoosting ? Math.Max(cruise, gridSettings.MaxBoostSpeed) : cruise);
        }
        public bool IsGridGroupsEnabled() { return cfg.Value.EnableGridGroups; }

        // The mass and grid class enforcement uses: the grid alone, or its physical group
        // (any large member makes the group large). Zero when the grid has no physics.
        private float EffectiveMass(IMyCubeGrid grid, out bool large, out bool anchored)
        {
            large = grid != null && grid.GridSizeEnum == MyCubeSize.Large;
            anchored = grid != null && grid.IsStatic;
            if (grid == null || grid.Physics == null) return 0;
            if (!cfg.Value.EnableGridGroups) return grid.Physics.Mass;
            ReadPhysicalGroup(grid, physicalGroup);
            return GroupMass(physicalGroup, out large, out anchored);
        }
        public float GetEffectiveMass(IMyCubeGrid grid)
        {
            bool large, anchored;
            return EffectiveMass(grid, out large, out anchored);
        }
        public float GetEffectiveCruiseSpeed(IMyCubeGrid grid)
        {
            bool large, anchored;
            float mass = EffectiveMass(grid, out large, out anchored);
            return grid?.Physics == null ? 0 : GetCruiseSpeedForMass(mass, large);
        }
        public float GetEffectiveSpeedCeiling(IMyCubeGrid grid)
        {
            bool large, anchored;
            float mass = EffectiveMass(grid, out large, out anchored);
            return grid?.Physics == null ? 0 : GetSpeedCeilingForMass(mass, large);
        }
        // Whether RTS applies its force to this grid when it moves faster than cruise:
        // not static or anchored through its group, and passing the thrust/cockpit filters.
        public bool IsGoverned(IMyCubeGrid grid)
        {
            var cube = grid as MyCubeGrid;
            if (cube == null || cube.Closed || cube.Physics == null || cube.IsStatic) return false;
            bool large, anchored;
            float mass = EffectiveMass(grid, out large, out anchored);
            return !anchored && mass > 0 && !float.IsInfinity(mass) && HasActivationBlock(cube);
        }
    }
}
