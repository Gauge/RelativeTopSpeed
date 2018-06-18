using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace RelativeTopSpeed
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class RelativeTopSpeed : MySessionComponentBase
    {
        private List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
        public static Settings cfg;
        private Communication coms;
        bool isInitialized = false;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyLog.Default.Info($"Starting Relative Speed");
            MyLog.Default.Flush();
            MyAPIGateway.Entities.OnEntityAdd += AddGrid;
            MyAPIGateway.Entities.OnEntityRemove += RemoveGrid;

            cfg = Settings.Load();
            coms = new Communication();
        }

        public override void BeforeStart()
        {
            MyAPIGateway.Session.OnSessionReady += coms.RequestServerSettings;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= AddGrid;
            MyAPIGateway.Entities.OnEntityRemove -= RemoveGrid;
            MyAPIGateway.Session.OnSessionReady -= coms.RequestServerSettings;
            coms.Close();
        }

        private void AddGrid(IMyEntity ent)
        {
            if (ent is IMyCubeGrid)
            {
                Grids.Add(ent as IMyCubeGrid);
            }
        }

        private void RemoveGrid(IMyEntity ent)
        {
            if (ent is IMyCubeGrid && Grids.Contains(ent))
            {
                Grids.Remove(ent as IMyCubeGrid);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            MyAPIGateway.Parallel.ForEach(Grids, (grid) =>
            {
                if (grid.Physics == null) return;

                bool isLargeGrid = grid.GridSizeEnum == MyCubeSize.Large;
                float minSpeed;
                float boostSpeed;
                Vector3 velocity = grid.Physics.LinearVelocity;
                float speed = grid.Physics.Speed;
                float mass = grid.Physics.Mass;

                if (isLargeGrid)
                {
                    minSpeed = cfg.LargeGrid_MinCruise;
                    boostSpeed = cfg.LargeGrid_MaxBoostSpeed;
                }
                else
                {
                    minSpeed = cfg.SmallGrid_MinCruise;
                    boostSpeed = cfg.SmallGrid_MaxBoostSpeed;
                }

                if (speed < minSpeed) return;

                float cruiseSpeed = GetCruiseSpeed(mass, isLargeGrid);
                if (speed >= boostSpeed)
                {
                    velocity *= boostSpeed / speed;
                    grid.Physics.SetSpeeds(velocity, grid.Physics.AngularVelocity);
                    speed = boostSpeed;
                }

                if (speed >= cruiseSpeed)
                {
                    float resistantForce = 0;

                    if (isLargeGrid)
                    {
                        resistantForce = cfg.LargeGrid_ResistanceMultiplier * mass * (1 - cruiseSpeed / speed);
                    }
                    else
                    {
                        resistantForce = cfg.SmallGrid_ResistanceMultiplyer * mass * (1 - cruiseSpeed / speed);
                    }

                    velocity *= -resistantForce;
                    grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, velocity, grid.Physics.CenterOfMassWorld, null);
                }

            });
        }

        private float GetCruiseSpeed(float mass, bool isLargeGrid)
        {
            float cruiseSpeed;
            if (isLargeGrid)
            {
                if (mass > cfg.LargeGrid_MaxMass)
                {
                    cruiseSpeed = cfg.LargeGrid_MinCruise;
                }
                else if (mass < cfg.LargeGrid_MinMass)
                {
                    cruiseSpeed = cfg.LargeGrid_MaxCruise;
                }
                else
                {
                    float x = (mass - cfg.LargeGrid_MaxMass);
                    cruiseSpeed = (cfg.l_a * x * x + cfg.LargeGrid_MinCruise);
                }
            }
            else
            {
                if (mass > cfg.SmallGrid_MaxMass)
                {
                    cruiseSpeed = cfg.SmallGrid_MinCruise;
                }
                else if (mass < cfg.SmallGrid_MinMass)
                {
                    cruiseSpeed = cfg.SmallGrid_MaxCruise;
                }
                else
                {
                    float x = (mass - cfg.SmallGrid_MaxMass);
                    cruiseSpeed = (cfg.s_a * x * x + cfg.SmallGrid_MinCruise);
                }
            }

            return cruiseSpeed;
        }
    }
}
