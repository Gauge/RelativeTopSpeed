using RelativeTopSpeed.Coms;
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
        private const ushort ModId = 16341;
        private const string ModName = "Relative Top Speed";
        private const string CommandKeyword = "/rts";

        private List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
        public static Settings cfg;
        private ICommunicate coms;

        private bool showHud = false;
        private bool isReady = false;
        private bool isInitialized = false;
        private int waitInterval = 0; // this will wait for 5 seconds before sending

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            Tools.Log(MyLogSeverity.Info, "Starting Relative Speed");
            MyAPIGateway.Entities.OnEntityAdd += AddGrid;
            MyAPIGateway.Entities.OnEntityRemove += RemoveGrid;

            cfg = Settings.Load();

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                coms = new Server(ModId, CommandKeyword);
                coms.OnCommandRecived += HandleClientCommand;
                coms.OnTerminalInput += HandleServerTerminalInput;
                isInitialized = true;
            }
            else
            {
                coms = new Client(ModId, CommandKeyword);
                coms.OnCommandRecived += HandleServerCommand;
                coms.OnTerminalInput += HandleClientTerminalInput;
            }

        }

        protected override void UnloadData()
        {
            coms.Close();
            MyAPIGateway.Entities.OnEntityAdd -= AddGrid;
            MyAPIGateway.Entities.OnEntityRemove -= RemoveGrid;

            if (MyAPIGateway.Multiplayer.IsServer)
            {
                coms = new Server(ModId, CommandKeyword);
                coms.OnCommandRecived -= HandleClientCommand;
                coms.OnTerminalInput -= HandleServerTerminalInput;
            }
            else
            {
                coms = new Client(ModId, CommandKeyword);
                coms.OnCommandRecived -= HandleServerCommand;
                coms.OnTerminalInput -= HandleClientTerminalInput;
            }
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
                    coms.SendCommand(new Command() { Arguments = "update" });
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

        private void HandleClientTerminalInput(string message)
        {
            if (message == "config")
            {
                MyAPIGateway.Utilities.ShowMissionScreen("Relative Top Speed", "Configuration", null, cfg.ToString());
            }
            else if (message == "hud")
            {
                showHud = !showHud;
                MyAPIGateway.Utilities.ShowMessage(ModName, $"Hud display is {(showHud ? "ON" : "OFF")}");
            }
            else
            {
                coms.SendCommand(message, steamId: MyAPIGateway.Session.Player.SteamUserId);
            }
        }

        private void HandleServerTerminalInput(string message)
        {
            HandleClientCommand(new Command() { Arguments = message });
        }

        private void HandleClientCommand(Command cmd)
        {
            string[] args = cmd.Arguments.Split(' ');

            if (args[0] == "update")
            {
                coms.SendCommand(new Command()
                {
                    DataType = typeof(Settings).FullName,
                    XMLData = MyAPIGateway.Utilities.SerializeToXML(cfg)
                }, cmd.SteamId);
            }
            else if (args[0] == "load")
            {
                if (IsAllowedSpecialOperations(cmd.SteamId))
                {
                    cfg = Settings.Load();

                    coms.SendCommand(new Command()
                    {
                        DataType = typeof(Settings).FullName,
                        XMLData = MyAPIGateway.Utilities.SerializeToXML(cfg)
                    });

                    DisplayMessage("Settings loaded", cmd.SteamId);
                }
                else
                {
                    DisplayMessage("Load command requires Admin status.", cmd.SteamId);
                }
            }
            else if (args[0] == "hud")
            {
                showHud = !showHud;
                DisplayMessage($"Hud display is {(showHud ? "ON" : "OFF")}", cmd.SteamId);
            }
            else if (args[0] == "config")
            {
                if (coms.MultiplayerType != MultiplayerTypes.Dedicated)
                {
                    MyAPIGateway.Utilities.ShowMissionScreen("Relative Top Speed", "Configuration", null, cfg.ToString());
                }
            }
            else if (args[0] == string.Empty || args[0] == "help")
            {
                DisplayMessage("Relative Top Speed\nHUD: displays ship stats when in cockpit\nCONFIG: [BETA] Displays the current config\nLOAD: load world configuration\nUPDATE: requests current server settings", cmd.SteamId);
            }
            else
            {
                DisplayMessage("Unrecognized Command.", cmd.SteamId);
            }
        }

        private void HandleServerCommand(Command cmd)
        {

            if (cmd.Message != string.Empty && cmd.Message != null)
            {
                MyAPIGateway.Utilities.ShowMessage(ModName, cmd.Message);
            }

            if (cmd.DataType == typeof(Settings).FullName)
            {
                try
                {
                    cfg = MyAPIGateway.Utilities.SerializeFromXML<Settings>(cmd.XMLData);
                    cfg.CalculateCurve();
                }
                catch (Exception e)
                {
                    Tools.Log(MyLogSeverity.Error, $"Failed to deserialize settings from server\n{e.ToString()}");
                }
            }
        }

        private void DisplayMessage(string message, ulong steamId)
        {
            if (coms.MultiplayerType == MultiplayerTypes.Dedicated)
            {
                coms.SendCommand(new Command() { Message = message }, steamId);
            }
            else if (coms.MultiplayerType == MultiplayerTypes.Server && steamId == 0)
            {
                MyAPIGateway.Utilities.ShowMessage(ModName, message);
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
