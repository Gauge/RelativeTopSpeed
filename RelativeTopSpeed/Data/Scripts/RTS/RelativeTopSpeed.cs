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
using VRage.Game.ModAPI.Interfaces;

namespace RelativeTopSpeed
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class RelativeTopSpeed : MySessionComponentBase
	{
		private const ushort ComId = 16341;
		private const string ModName = "Relative Top Speed";
		private const string CommandKeyword = "/rts";

		private List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
		private List<IMyCubeGrid> DisabledGrids = new List<IMyCubeGrid>();
		public static Settings cfg;

		private bool showHud = false;
		private bool isReady = false;
		private bool isInitialized = false;
		private int waitInterval = 0; // this will wait for 10 seconds before sending

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

		public override void BeforeStart()
		{
			if (MyAPIGateway.Multiplayer.IsServer)
				return;

			MyAPIGateway.Session.OnSessionReady += SessionReady;
		}

		private void SessionReady()
		{
			isReady = true;
			MyAPIGateway.Session.OnSessionReady -= SessionReady;
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
				ent.OnPhysicsChanged += ValidateAndAssignGrid;
				ValidateAndAssignGrid(ent);
			}
		}

		private void RemoveGrid(IMyEntity ent)
		{
			if (ent is IMyCubeGrid)
			{
				ent.OnPhysicsChanged -= ValidateAndAssignGrid;
				Grids.Remove(ent as IMyCubeGrid);
				DisabledGrids.Remove(ent as IMyCubeGrid);
			}
		}

		private void ValidateAndAssignGrid(IMyEntity grid)
		{
			if (grid.Physics == null || // static grid 
				!grid.Physics.Enabled ||
				(grid.Flags & (EntityFlags)4) != 0) // grid is concealed 
			{
				if (!DisabledGrids.Contains(grid))
				{
					DisabledGrids.Add(grid as IMyCubeGrid);
				}
				Grids.Remove(grid as IMyCubeGrid);
			}
			else
			{
				if (!Grids.Contains(grid))
				{
					Grids.Add(grid as IMyCubeGrid);
				}
				DisabledGrids.Remove(grid as IMyCubeGrid);
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

			MyAPIGateway.Parallel.ForEach(Grids, UpdateGrid); 
		}

		private void UpdateGrid(IMyCubeGrid grid)
		{
			bool isLargeGrid = grid.GridSizeEnum == MyCubeSize.Large;
			float minSpeed;
			float boostSpeed;
			float cruiseSpeed = 0;
			float resistantForce = 0;
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

			if (speed > minSpeed)
			{
				cruiseSpeed = GetCruiseSpeed(mass, isLargeGrid);

				if (speed >= cruiseSpeed)
				{
					if (isLargeGrid)
					{
						resistantForce = cfg.LargeGrid_ResistanceMultiplier * mass * (1 - (cruiseSpeed / speed));
					}
					else
					{
						resistantForce = cfg.SmallGrid_ResistanceMultiplyer * mass * (1 - (cruiseSpeed / speed));
					}

					Vector3 velocity = grid.Physics.LinearVelocity * -resistantForce;
					grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, velocity, grid.Physics.CenterOfMassWorld, null, boostSpeed);
				}
			}

			if (showHud)
			{
				IMyControllableEntity controlledEntity = MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity;
				if (!MyAPIGateway.Utilities.IsDedicated &&
				controlledEntity != null &&
				controlledEntity is IMyCubeBlock &&
				(controlledEntity as IMyCubeBlock).CubeGrid.EntityId == grid.EntityId)
				{
					if (cruiseSpeed == 0)
					{
						cruiseSpeed = GetCruiseSpeed(mass, isLargeGrid);
					}

					MyAPIGateway.Utilities.ShowNotification($"Mass: {mass}  Cruise: {cruiseSpeed.ToString("n3")} Boost: {((speed - cruiseSpeed >= 0) ? (speed - cruiseSpeed).ToString("n3") : "0.000")}  Resistance: {resistantForce.ToString("n0")}", 1);
				}
			}

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
					cruiseSpeed = (float)(cfg.l_a * x * x + cfg.LargeGrid_MinCruise);
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
					cruiseSpeed = (float)(cfg.s_a * x * x + cfg.SmallGrid_MinCruise);
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

		public static bool IsAllowedSpecialOperations(ulong steamId)
		{
			if (MyAPIGateway.Multiplayer.IsServer)
				return true;
			return IsAllowedSpecialOperations(MyAPIGateway.Session.GetUserPromoteLevel(steamId));
		}

		public static bool IsAllowedSpecialOperations(MyPromoteLevel level)
		{
			return level == MyPromoteLevel.SpaceMaster || level == MyPromoteLevel.Admin || level == MyPromoteLevel.Owner;
		}

		#endregion
	}
}
