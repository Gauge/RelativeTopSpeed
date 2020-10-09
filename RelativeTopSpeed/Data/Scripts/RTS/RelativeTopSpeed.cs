using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SENetworkAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
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

		public NetSync<Settings> cfg;

		private bool showHud = false;
		private bool debug = false;
		private byte waitInterval = 0;
		private List<MyCubeGrid> ActiveGrids = new List<MyCubeGrid>();
		private List<MyCubeGrid> PassiveGrids = new List<MyCubeGrid>();
		private List<MyCubeGrid> DisabledGrids = new List<MyCubeGrid>();

		private MyObjectBuilderType thrustTypeId = null;

		private NetworkAPI Network => NetworkAPI.Instance;

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			thrustTypeId = MyObjectBuilderType.ParseBackwardsCompatible("Thrust");

			NetworkAPI.LogNetworkTraffic = false;

			if (!NetworkAPI.IsInitialized)
			{
				NetworkAPI.Init(ComId, ModName, CommandKeyword);
			}

			cfg = new NetSync<Settings>(this, TransferType.ServerToClient, Settings.Load(), true, false);
			cfg.ValueChangedByNetwork += SettingsChanged;

			Network.RegisterChatCommand(string.Empty, Chat_Help);
			Network.RegisterChatCommand("help", Chat_Help);
			Network.RegisterChatCommand("hud", Chat_Hud);
			Network.RegisterChatCommand("config", Chat_Config);

			if (!MyAPIGateway.Multiplayer.IsServer)
			{
				Network.RegisterChatCommand("load", (args) => { Network.SendCommand("load"); });
			}
			else
			{
				Network.RegisterNetworkCommand("load", ServerCallback_Load);
				Network.RegisterChatCommand("load", (args) => { cfg.Value = Settings.Load(); });
			}

			MyLog.Default.Info("[RelativeTopSpeed] Starting.");
			MyAPIGateway.Entities.OnEntityAdd += AddGrid;
			MyAPIGateway.Entities.OnEntityRemove += RemoveGrid;
		}

		private void SettingsChanged(Settings o, Settings n, ulong sender)
		{
			MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = n.SpeedLimit;
			MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = n.SpeedLimit;
			n.CalculateCurve();
		}

		protected override void UnloadData()
		{
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
			else if (IsMoving(grid) &&
				(cfg.Value.IgnoreGridsWithoutThrust && grid.BlocksCounters.ContainsKey(thrustTypeId) && grid.BlocksCounters[thrustTypeId] > 0))
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
						// update active / passive grids every 3 seconds
						if (waitInterval == 0)
						{
							for (int i = 0; i < PassiveGrids.Count; i++)
							{

								MyCubeGrid grid = PassiveGrids[i];
								bool isContained = grid.BlocksCounters.ContainsKey(thrustTypeId);
								if (cfg.Value.IgnoreGridsWithoutThrust && 
									(!isContained || 
										(isContained && grid.BlocksCounters[thrustTypeId] == 0)))
								{
									continue;
								}
								
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
								MyCubeGrid grid = ActiveGrids[i];
								bool isContained = grid.BlocksCounters.ContainsKey(thrustTypeId);
								if (!IsMoving(grid) || 
									cfg.Value.IgnoreGridsWithoutThrust &&
										(!isContained ||
											(isContained && grid.BlocksCounters[thrustTypeId] == 0)))
								{
									if (!PassiveGrids.Contains(grid))
									{
										PassiveGrids.Add(grid);
									}

									ActiveGrids.Remove(grid);
									i--;
								}
							}

							waitInterval = 180; // reset
						}

						MyAPIGateway.Parallel.For(0, ActiveGrids.Count, UpdateGrid);

						if (!MyAPIGateway.Utilities.IsDedicated)
						{
							if (showHud)
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
			}

			waitInterval--;
		}

		private void UpdateGrid(int index)
		{

			IMyCubeGrid grid = ActiveGrids[index];

			float speed = grid.Physics.Speed;
			bool isLargeGrid = grid.GridSizeEnum == MyCubeSize.Large;
			float minSpeed = (isLargeGrid) ? cfg.Value.LargeGrid_MinCruise : cfg.Value.SmallGrid_MinCruise;

			if (speed > minSpeed)
			{
				float mass = grid.Physics.Mass;
				float cruiseSpeed = GetCruiseSpeed(mass, isLargeGrid);

				if (cfg.Value.EnableBoosting)
				{
					if (speed >= cruiseSpeed)
					{
						float maxBoost = (isLargeGrid) ? cfg.Value.LargeGrid_MaxBoostSpeed : cfg.Value.SmallGrid_MaxBoostSpeed;
						float resistance = (isLargeGrid) ? cfg.Value.LargeGrid_ResistanceMultiplier : cfg.Value.SmallGrid_ResistanceMultiplyer;

						float resistantForce = resistance * mass * (1 - (cruiseSpeed / speed));

						Vector3 velocity = grid.Physics.LinearVelocity * -resistantForce;
						grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, velocity, grid.Physics.CenterOfMassWorld, null, maxBoost);
					}
				}
				else
				{
					if (speed > cruiseSpeed)
					{
						Vector3 linear = grid.Physics.LinearVelocity * (cruiseSpeed / speed);
						grid.Physics.SetSpeeds(linear, grid.Physics.AngularVelocity);
					}

				}
			}
		}

		private float GetCruiseSpeed(float mass, bool isLargeGrid)
		{
			float cruiseSpeed;

			if (isLargeGrid)
			{
				if (mass > cfg.Value.LargeGrid_MaxMass)
				{
					cruiseSpeed = cfg.Value.LargeGrid_MinCruise;
				}
				else if (mass < cfg.Value.LargeGrid_MinMass)
				{
					cruiseSpeed = cfg.Value.LargeGrid_MaxCruise;
				}
				else
				{
					float x = (mass - cfg.Value.LargeGrid_MaxMass);
					cruiseSpeed = (float)(cfg.Value.l_a * x * x + cfg.Value.LargeGrid_MinCruise);
				}
			}
			else
			{
				if (mass > cfg.Value.SmallGrid_MaxMass)
				{
					cruiseSpeed = cfg.Value.SmallGrid_MinCruise;
				}
				else if (mass < cfg.Value.SmallGrid_MinMass)
				{
					cruiseSpeed = cfg.Value.SmallGrid_MaxCruise;
				}
				else
				{
					float x = (mass - cfg.Value.SmallGrid_MaxMass);
					cruiseSpeed = (float)(cfg.Value.s_a * x * x + cfg.Value.SmallGrid_MinCruise);
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
			if (!MyAPIGateway.Utilities.IsDedicated)
			{
				MyAPIGateway.Utilities.ShowMissionScreen("Relative Top Speed", "Configuration", null, cfg.Value.ToString());
			}
		}

		private void ServerCallback_Load(ulong steamId, string commandString, byte[] data, DateTime timestamp)
		{
			if (IsAllowedSpecialOperations(steamId))
			{
				cfg.Value = Settings.Load();
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
