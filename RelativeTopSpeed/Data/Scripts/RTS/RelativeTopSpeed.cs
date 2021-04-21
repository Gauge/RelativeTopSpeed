﻿using Sandbox.Definitions;
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
using static VRageMath.Base6Directions;
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
		public static event Action<Settings> SettingsChanged;


		private bool showHud = false;
		private bool debug = false;
		private byte waitInterval = 0;
		private List<MyCubeGrid> ActiveGrids = new List<MyCubeGrid>();
		private List<MyCubeGrid> PassiveGrids = new List<MyCubeGrid>();
		private List<MyCubeGrid> DisabledGrids = new List<MyCubeGrid>();

		private MyObjectBuilderType thrustTypeId = null;
		private MyObjectBuilderType cockpitTypeId = null;

		private NetworkAPI Network => NetworkAPI.Instance;

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			thrustTypeId = MyObjectBuilderType.ParseBackwardsCompatible("Thrust");
			cockpitTypeId = MyObjectBuilderType.ParseBackwardsCompatible("Cockpit");

			NetworkAPI.LogNetworkTraffic = false;

			if (!NetworkAPI.IsInitialized)
			{
				NetworkAPI.Init(ComId, ModName, CommandKeyword);
			}

			if (!RtsApiBackend.IsInitialized)
			{
				RtsApiBackend.Init(this);
			}

			cfg = new NetSync<Settings>(this, TransferType.ServerToClient, Settings.Load(), true, false);
			cfg.ValueChangedByNetwork += SettingChanged;
			Settings.Instance = cfg.Value;

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

		private void SettingChanged(Settings o, Settings n, ulong sender)
		{
			MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = n.SpeedLimit;
			MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = n.SpeedLimit;
			n.CalculateCurve();
			Settings.Instance = n;
			SettingsChanged?.Invoke(n);
		}

		protected override void UnloadData()
		{
			MyAPIGateway.Entities.OnEntityAdd -= AddGrid;
			MyAPIGateway.Entities.OnEntityRemove -= RemoveGrid;

			RtsApiBackend.Close();
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
				!(cfg.Value.IgnoreGridsWithoutThrust && grid.BlocksCounters.ContainsKey(thrustTypeId) && grid.BlocksCounters[thrustTypeId] == 0) &&
				!(cfg.Value.IgnoreGridsWithoutCockpit && grid.BlocksCounters.ContainsKey(cockpitTypeId) && grid.BlocksCounters[cockpitTypeId] == 0))
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
								if ((cfg.Value.IgnoreGridsWithoutThrust && (!grid.BlocksCounters.ContainsKey(thrustTypeId) || grid.BlocksCounters[thrustTypeId] == 0)) ||
									(cfg.Value.IgnoreGridsWithoutCockpit && (!grid.BlocksCounters.ContainsKey(cockpitTypeId) || grid.BlocksCounters[cockpitTypeId] == 0)))
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
								if (!IsMoving(grid) ||
									(cfg.Value.IgnoreGridsWithoutThrust && (!grid.BlocksCounters.ContainsKey(thrustTypeId) || grid.BlocksCounters[thrustTypeId] == 0)) ||
									(cfg.Value.IgnoreGridsWithoutCockpit && (!grid.BlocksCounters.ContainsKey(cockpitTypeId) || grid.BlocksCounters[cockpitTypeId] == 0)))
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

									float boost = GetBoost(grid)[3];
									float resistance = (grid.GridSizeEnum == MyCubeSize.Large) ? cfg.Value.LargeGrid_ResistanceMultiplier : cfg.Value.SmallGrid_ResistanceMultiplyer;

									MyAPIGateway.Utilities.ShowNotification($"Mass: {mass.ToString("n0")}   Cruise: {cruiseSpeed.ToString("n2")}   Max Boost: {(boost).ToString("n2")}", 1);
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

		public float[] GetAcceleration(IMyCubeGrid grid)
		{
			float[] accels = GetAccelerationsByDirection(grid);

			float min = float.MaxValue;
			float average = 0;
			float max = 0;

			for (int i = 0; i < 6; i++)
			{
				average += accels[i];

				if (accels[i] < min)
				{
					min = accels[i];
				}
			}

			average /= 6;

			if (accels[0] > accels[1])
			{
				max += accels[0];
			}
			else
			{
				max += accels[1];
			}

			if (accels[2] > accels[3])
			{
				max += accels[2];
			}
			else
			{
				max += accels[3];
			}

			if (accels[4] > accels[5])
			{
				max += accels[4];
			}
			else
			{
				max += accels[5];
			}

			return new float[] { accels[1], min, average, max };

		}

		public float[] GetBoost(IMyCubeGrid grid) 
		{
			float[] accels = GetAcceleration(grid);
			float resistance = (grid.GridSizeEnum == MyCubeSize.Large) ? cfg.Value.LargeGrid_ResistanceMultiplier : cfg.Value.SmallGrid_ResistanceMultiplyer;

			accels[0] /= resistance;
			accels[1] /= resistance;
			accels[2] /= resistance;
			accels[3] /= resistance;

			return accels;
		}

		public float[] GetAccelerationsByDirection(IMyCubeGrid grid)
		{
			if (grid == null || grid.Physics == null)
				return new float[6];

			float mass = grid.Physics.Mass;

			float[] accelerations = new float[6];

			foreach (IMySlimBlock slim in (grid as MyCubeGrid).CubeBlocks)
			{
				if (!(slim.FatBlock is IMyThrust))
					continue;
				
				IMyThrust thruster = slim.FatBlock as IMyThrust;

				Direction direction = GetDirection(thruster.GridThrustDirection);

				accelerations[(int)direction] += thruster.MaxThrust;
			}

			// convert from force to accleration (m = f/a)
			for (int i = 0; i < 6; i++)
			{
				accelerations[i] /= mass;
			}

			return accelerations;
		}

		public float GetCruiseSpeed(IMyCubeGrid grid) 
		{
			if (grid != null && grid.Physics != null)
			{
				return GetCruiseSpeed(grid.Physics.Mass, grid.GridSizeEnum == MyCubeSize.Large);
			}

			return 0;
		}

		public float GetMaxSpeed(IMyCubeGrid grid) 
		{
			return GetCruiseSpeed(grid) + GetBoost(grid)[3];
		}

		public float GetCruiseSpeed(float mass, bool isLargeGrid)
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
					cruiseSpeed = (float)(cfg.Value.l_a * (mass * mass) + cfg.Value.l_b * mass + cfg.Value.l_c);
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
					cruiseSpeed = (float)(cfg.Value.s_a * (mass * mass) + cfg.Value.s_b * mass + cfg.Value.s_c);
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
