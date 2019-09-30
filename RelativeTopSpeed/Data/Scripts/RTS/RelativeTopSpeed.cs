﻿using ModNetworkAPI;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace RelativeTopSpeed
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class RelativeTopSpeed : MySessionComponentBase
	{
		private const ushort ComId = 16341;
		private const string ModName = "Relative Top Speed";
		private const string CommandKeyword = "/rts";

		public static Settings cfg;

		private bool showHud = false;
		private bool debug = false;
		private byte waitInterval = 0;
		private List<IMyCubeGrid> ActiveGrids = new List<IMyCubeGrid>();
		private List<IMyCubeGrid> PassiveGrids = new List<IMyCubeGrid>();
		private List<IMyCubeGrid> DisabledGrids = new List<IMyCubeGrid>();

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
			Network.RegisterChatCommand("debug", Chat_Debug);

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
			MyCubeGrid grid = ent as MyCubeGrid;
			if (grid == null || grid.Physics == null)
				return;

			RegisterOrUpdateGridStatus(grid, grid.IsStatic);
			grid.OnStaticChanged += RegisterOrUpdateGridStatus;
		}

		private void RemoveGrid(IMyEntity ent)
		{
			MyCubeGrid grid = ent as MyCubeGrid;
			if (grid == null || grid.Physics == null)
				return;

			grid.OnStaticChanged -= RegisterOrUpdateGridStatus;
			ActiveGrids.Remove(grid);
			PassiveGrids.Remove(grid);
			DisabledGrids.Remove(grid);
		}

		private bool IsMoving(IMyEntity ent)
		{
			return ent.Physics.LinearVelocity.LengthSquared() > 1 || ent.Physics.LinearAcceleration.LengthSquared() > 1;
		}

		private void RegisterOrUpdateGridStatus(MyCubeGrid grid, bool isStatic)
		{
			if (isStatic)
			{
				if (!DisabledGrids.Contains(grid))
				{
					DisabledGrids.Add(grid);
				}

				PassiveGrids.Remove(grid);
				ActiveGrids.Remove(grid);
			}
			else if (IsMoving(grid))
			{
				if (!ActiveGrids.Contains(grid))
				{
					ActiveGrids.Add(grid);
				}

				PassiveGrids.Remove(grid);
				DisabledGrids.Remove(grid);
			}
			else
			{
				if (!PassiveGrids.Contains(grid))
				{
					PassiveGrids.Add(grid);
				}

				ActiveGrids.Remove(grid);
				DisabledGrids.Remove(grid);
			}
		}

		public override void UpdateBeforeSimulation()
		{
			lock (ActiveGrids)
			{
				lock (DisabledGrids)
				{
					lock (PassiveGrids)
					{
						// continue to request server settings till received
						if (!cfg.IsInitialized && waitInterval == 120)
						{
							Network.SendCommand("update");
						}

						// update active / passive grids every 4 seconds
						if (waitInterval == 0)
						{
							for (int i = 0; i < PassiveGrids.Count; i++)
							{
								IMyCubeGrid grid = PassiveGrids[i];

								//if (grid == null || grid.Physics == null)
								//{
								//	MyLog.Default.Info($"[RTS] {grid == null}  {((grid == null) ? "" : (grid.Physics == null).ToString())}");
								//	PassiveGrids.RemoveAt(i);
								//	i--;
								//}
								//else 
								if (IsMoving(grid))
								{
									if (!ActiveGrids.Contains(grid))
									{
										ActiveGrids.Add(grid);
									}

									PassiveGrids.Remove(grid);
									i--;
								}
							}

							for (int i = 0; i < ActiveGrids.Count; i++)
							{
								IMyCubeGrid grid = ActiveGrids[i];

								//if (grid == null || grid.Physics == null)
								//{
								//	MyLog.Default.Info($"[RTS] {grid == null}  {((grid == null) ? "" : (grid.Physics == null).ToString())}");
								//	ActiveGrids.RemoveAt(i);
								//	i--;
								//}
								//else 
								if (!IsMoving(grid))
								{
									if (!PassiveGrids.Contains(grid))
									{
										PassiveGrids.Add(grid);
									}

									ActiveGrids.Remove(grid);
									i--;
								}
							}

							waitInterval = 255; // reset
						}

						MyAPIGateway.Parallel.For(0, ActiveGrids.Count, UpdateGrid, 50);

						if (showHud && !MyAPIGateway.Utilities.IsDedicated)
						{
							IMyControllableEntity controlledEntity = MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity;
							if (controlledEntity != null && controlledEntity is IMyCubeBlock && (controlledEntity as IMyCubeBlock).CubeGrid.Physics != null)
							{
								IMyCubeGrid grid = (controlledEntity as IMyCubeBlock).CubeGrid;
								float mass = grid.Physics.Mass;
								float speed = grid.Physics.Speed;
								float cruiseSpeed = GetCruiseSpeed(mass, grid.GridSizeEnum == MyCubeSize.Large);

								MyAPIGateway.Utilities.ShowNotification($"Mass: {mass}  Cruise: {cruiseSpeed.ToString("n3")} Boost: {((speed - cruiseSpeed >= 0) ? (speed - cruiseSpeed).ToString("n3") : "0.000")}", 1);
							}
						}

						if (debug && IsAllowedSpecialOperations(MyAPIGateway.Session.LocalHumanPlayer.SteamUserId))
						{
							MyAPIGateway.Utilities.ShowNotification($"Grids - Active: {ActiveGrids.Count}  Passive: {PassiveGrids.Count}  Disabled: {DisabledGrids.Count}", 1);
						}
					}
				}
			}

			waitInterval--;
		}

		private void UpdateGrid(int index)
		{
			IMyCubeGrid grid = ActiveGrids[index];

			float speed = grid.Physics.Speed;
			bool isLargeGrid = grid.GridSizeEnum == MyCubeSize.Large;
			float minSpeed = (isLargeGrid) ? cfg.LargeGrid_MinCruise : cfg.SmallGrid_MinCruise;

			if (speed > minSpeed)
			{
				float resistantForce;
				float mass = grid.Physics.Mass;
				float cruiseSpeed = GetCruiseSpeed(mass, isLargeGrid);

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
					grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, velocity, grid.Physics.CenterOfMassWorld, null, ((isLargeGrid) ? cfg.LargeGrid_MaxBoostSpeed : cfg.SmallGrid_MaxBoostSpeed));
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

		private void Chat_Debug(string arguments)
		{
			debug = !debug;
			MyAPIGateway.Utilities.ShowMessage(ModName, $"Debug display is {(showHud ? "ON" : "OFF")}");
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
				cfg.IsInitialized = true;
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
