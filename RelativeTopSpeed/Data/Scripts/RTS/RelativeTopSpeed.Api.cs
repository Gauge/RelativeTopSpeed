using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using VRage.Game;
using VRage.Game.ModAPI;
using static VRageMath.Base6Directions;

namespace RelativeTopSpeed
{
    public partial class RelativeTopSpeed
    {
        private struct DirectionalAcceleration
        {
            public float Forward, Backward, Left, Right, Up, Down;
            public float Maximum { get { return Math.Max(Forward, Backward) + Math.Max(Left, Right) + Math.Max(Up, Down); } }

            public float[] Summary()
            {
                float minimum = Math.Min(Math.Min(Math.Min(Math.Min(Math.Min(Forward, Backward), Left), Right), Up), Down);
                float average = (Forward + Backward + Left + Right + Up + Down) / 6;
                return new[] { Backward, minimum, average, Maximum };
            }
        }

        public float[] GetAcceleration(IMyCubeGrid grid)
        {
            return ReadAcceleration(grid).Summary();
        }

        private float ResistanceFor(IMyCubeGrid grid)
        {
            return grid.GridSizeEnum == MyCubeSize.Large
                ? cfg.Value.LargeGrid.ResistanceMultiplier : cfg.Value.SmallGrid.ResistanceMultiplier;
        }

        private float GetMaximumBoost(IMyCubeGrid grid)
        {
            return ReadAcceleration(grid).Maximum / ResistanceFor(grid);
        }

        public float[] GetBoost(IMyCubeGrid grid)
        {
            if (grid?.Physics == null) return new float[4];
            float[] acceleration = GetAcceleration(grid);
            float resistance = ResistanceFor(grid);
            for (int i = 0; i < acceleration.Length; i++) acceleration[i] /= resistance;
            return acceleration;
        }

        public float[] GetAccelerationsByDirection(IMyCubeGrid grid)
        {
            var value = ReadAcceleration(grid);
            return new[] { value.Forward, value.Backward, value.Left, value.Right, value.Up, value.Down };
        }

        private DirectionalAcceleration ReadAcceleration(IMyCubeGrid grid)
        {
            var result = new DirectionalAcceleration();
            var cube = grid as MyCubeGrid;
            if (cube?.Physics == null) return result;
            float mass = cube.Physics.Mass;
            if (!(mass > 0) || float.IsInfinity(mass)) return result;
            foreach (IMySlimBlock block in cube.CubeBlocks)
            {
                var thruster = block.FatBlock as IMyThrust;
                if (thruster == null) continue;
                float thrust = thruster.MaxThrust;
                switch (GetDirection(thruster.GridThrustDirection))
                {
                    case Direction.Forward: result.Forward += thrust; break;
                    case Direction.Backward: result.Backward += thrust; break;
                    case Direction.Left: result.Left += thrust; break;
                    case Direction.Right: result.Right += thrust; break;
                    case Direction.Up: result.Up += thrust; break;
                    case Direction.Down: result.Down += thrust; break;
                }
            }
            // Sum thrust before division to preserve the public API's rounding.
            result.Forward /= mass; result.Backward /= mass;
            result.Left /= mass; result.Right /= mass;
            result.Up /= mass; result.Down /= mass;
            return result;
        }

        public float GetCruiseSpeed(IMyCubeGrid grid)
        {
            return grid?.Physics == null ? 0 : GetCruiseSpeed(grid.Physics.Mass, grid.GridSizeEnum == MyCubeSize.Large);
        }

        public float GetMaxSpeed(IMyCubeGrid grid)
        {
            if (grid?.Physics == null) return 0;
            float speed = GetCruiseSpeed(grid);
            if (cfg.Value.EnableBoosting)
            {
                speed += GetMaximumBoost(grid);
                speed = Math.Min(speed, grid.GridSizeEnum == MyCubeSize.Large
                    ? cfg.Value.LargeGrid.MaxBoostSpeed : cfg.Value.SmallGrid.MaxBoostSpeed);
            }
            return Math.Min(speed, cfg.Value.SpeedLimit);
        }

        public float GetCruiseSpeed(float mass, bool isLargeGrid) { return cfg.Value.GetCruiseSpeed(mass, isLargeGrid); }
    }
}
