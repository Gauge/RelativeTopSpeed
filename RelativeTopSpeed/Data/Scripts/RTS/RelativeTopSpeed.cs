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
using ModNetworkAPI;

namespace RelativeTopSpeed
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class RelativeTopSpeed : MySessionComponentBase
    {
        private const ushort ComId = 16341;
        private const string ModName = "Relative Top Speed";
        private const string CommandKeyword = "/rts";

        private List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
        public static Settings cfg;

        private bool showHud = false;
        private bool isReady = false;
        private bool isInitialized = false;
        private int waitInterval = 0; // this will wait for 5 seconds before sending

        private NetworkAPI Network => NetworkAPI.Instance;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (!NetworkAPI.IsInitialized)
            {
                NetworkAPI.Init(ComId, ModName, CommandKeyword);
            }

            Network.RegisterChatCommand(string.Empty, Chat_Help);
            Network.RegisterChatCommand("help", Chat_Help);
            Network.RegisterChatCommand("hud", Chat_Hud);
            Network.RegisterChatCommand("config", Chat_Config);

            if (Network.NetworkType == NetworkTypes.Client)
            {
                Network.RegisterNetworkCommand(null, ClientCallback_Update);
                Network.RegisterChatCommand("update", (args) => { Network.SendCommand("update"); });
                Network.RegisterChatCommand("load", (args) => { Network.SendCommand("load"); });
            }
            else
            {
                Network.RegisterNetworkCommand("update", ServerCallback_Update);
                Network.RegisterNetworkCommand("load", ServerCallback_Load);
                Network.RegisterChatCommand("load", (args) => { cfg = Settings.Load(); });
            }

            MyLog.Default.Info("[RelativeTopSpeed] Starting.");
            MyAPIGateway.Entities.OnEntityAdd += AddGrid;
            MyAPIGateway.Entities.OnEntityRemove += RemoveGrid;

            cfg = Settings.Load();
        }

        protected override void UnloadData()
        {
            Network.Close();
            MyAPIGateway.Entities.OnEntityAdd -= AddGrid;
            MyAPIGateway.Entities.OnEntityRemove -= RemoveGrid;
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
            /*
             * this is a dumb hack to fix crashing when clients connect.
             * the session ready event sometimes does not have everything loaded when i trigger the send command
             */
            if (!isInitialized && isReady)
            {
                if (waitInterval == 600)
                {
                    Network.SendCommand("update");
                    isInitialized = true;
                }

                waitInterval++;
            }

            MyAPIGateway.Parallel.ForEach(Grids, (grid) =>
            {
                if (grid.Physics == null) return;

                bool isLargeGrid = grid.GridSizeEnum == MyCubeSize.Large;
                float minSpeed;
                float boostSpeed;
                Vector3 velocity = grid.Physics.LinearVelocity;
                float speed = grid.Physics.Speed;
                float mass = grid.Physics.Mass;
                float cruiseSpeed = 0;
                float resistantForce = 0;

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

                if (speed < minSpeed)
                {
                    if (showHud)
                    {
                        cruiseSpeed = GetCruiseSpeed(mass, isLargeGrid);
                    }
                }
                else
                {
                    cruiseSpeed = GetCruiseSpeed(mass, isLargeGrid);
                    if (speed >= boostSpeed)
                    {
                        velocity *= boostSpeed / speed;
                        grid.Physics.SetSpeeds(velocity, grid.Physics.AngularVelocity);
                        speed = boostSpeed;
                    }

                    if (speed >= cruiseSpeed)
                    {
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
                }

                if (showHud &&
                    !MyAPIGateway.Utilities.IsDedicated &&
                    MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity != null &&
                    MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity is IMyCubeBlock &&
                    (MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity as IMyCubeBlock).CubeGrid.EntityId == grid.EntityId)
                {
                    MyAPIGateway.Utilities.ShowNotification($"Mass: {mass}  Cruise: {cruiseSpeed.ToString("n3")} Boost: {((speed - cruiseSpeed >= 0) ? (speed - cruiseSpeed).ToString("n3") : "0.000")}  Resistance: {resistantForce.ToString("n0")}", 1);
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

                    if (cfg.UseLogarithmic)
                    {
                        cruiseSpeed = (float)(Math.Pow(cfg.l_a, x) + cfg.LargeGrid_MinCruise);
                    }
                    else
                    {
                        cruiseSpeed = (float)(cfg.l_a * x * x + cfg.LargeGrid_MinCruise);
                    }
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

                    if (cfg.UseLogarithmic)
                    {
                        cruiseSpeed = (float)(Math.Pow(cfg.s_a, x) + cfg.SmallGrid_MinCruise);
                    }
                    else
                    {
                        cruiseSpeed = (float)(cfg.s_a * x * x + cfg.SmallGrid_MinCruise);
                    }
                }
            }

            return cruiseSpeed;
        }

        #region Communications

        private void Chat_Help(string arguments)
        {
            MyAPIGateway.Utilities.ShowMessage(Network.ModName, "Relative Top Speed\nHUD: displays ship stats when in cockpit\nCONFIG: Displays the current config\nLOAD: load world configuration\nUPDATE: requests current server settings");
        }

        private void Chat_Hud(string arguments)
        {
            showHud = !showHud;
            MyAPIGateway.Utilities.ShowMessage(ModName, $"Hud display is {(showHud ? "ON" : "OFF")}");
        }

        private void Chat_Config(string arguments)
        {
            if (Network.NetworkType != NetworkTypes.Dedicated)
            {
                MyAPIGateway.Utilities.ShowMissionScreen("Relative Top Speed", "Configuration", null, cfg.ToString());
            }
        }

        private void ClientCallback_Update(ulong steamId, string CommandString, byte[] data)
        {
            if (data != null)
            {
                cfg = MyAPIGateway.Utilities.SerializeFromBinary<Settings>(data);
                cfg.CalculateCurve();
            }
        }

        private void ServerCallback_Update(ulong steamId, string commandString, byte[] data)
        {
            Network.SendCommand(null, data: MyAPIGateway.Utilities.SerializeToBinary(cfg), steamId: steamId);
        }

        private void ServerCallback_Load(ulong steamId, string commandString, byte[] data)
        {
            if (IsAllowedSpecialOperations(steamId))
            {
                cfg = Settings.Load();

                Network.SendCommand(null, "Settings loaded", MyAPIGateway.Utilities.SerializeToBinary(cfg));
            }
            else
            {
                Network.SendCommand(null, "Load command requires Admin status.", steamId: steamId);
            }
        }

        public override void BeforeStart()
        {
            if (MyAPIGateway.Multiplayer.IsServer) return;

            MyAPIGateway.Session.OnSessionReady += SessionReady;
        }

        private void SessionReady()
        {
            isReady = true;
            MyAPIGateway.Session.OnSessionReady -= SessionReady;
        }

        public static bool IsAllowedSpecialOperations(ulong steamId)
        {
            if (MyAPIGateway.Multiplayer.IsServer) return true;
            return IsAllowedSpecialOperations(MyAPIGateway.Session.GetUserPromoteLevel(steamId));
        }

        public static bool IsAllowedSpecialOperations(MyPromoteLevel level)
        {
            return level == MyPromoteLevel.SpaceMaster || level == MyPromoteLevel.Admin || level == MyPromoteLevel.Owner;
        }

        #endregion
    }
}
