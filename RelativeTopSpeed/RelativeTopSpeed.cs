using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SENetworkAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;

namespace RelativeTopSpeed
{
    [MySessionComponentDescriptor(MyUpdateOrder.Simulation, 999)]
    public partial class RelativeTopSpeed : MySessionComponentBase
    {
        private const ushort ComId = 16341;
        private const string ModName = "Relative Top Speed";
        private const string CommandKeyword = "/rts";
        public NetSync<Settings> cfg;
        public static event Action<Settings> SettingsChanged;
        private NetworkAPI Network { get { return NetworkAPI.Instance; } }
        private bool showHud;
        private string pendingConfigurationNotice;
        private RtsSettingsMenu settingsMenu;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            NetworkAPI.Init(ComId, ModName, CommandKeyword);
            cfg = new NetSync<Settings>(this, TransferType.ServerToClient, Settings.Load(), false, false);
            cfg.ValueChanged += SettingChanged;
            SettingChanged(null, cfg.Value);
            cfg.Fetch();
            RtsApiBackend.Init(this);
            Network.RegisterChatCommand(string.Empty, Chat_Help);
            Network.RegisterChatCommand("help", Chat_Help);
            Network.RegisterChatCommand("hud", Chat_Hud);
            Network.RegisterChatCommand("config", Chat_Config);
            Network.RegisterChatCommand("menu", args => settingsMenu?.Open());
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                Network.RegisterNetworkCommand("load", ServerCallback_Load);
                Network.RegisterNetworkCommand("settings", ServerCallback_Settings);
                Network.RegisterChatCommand("load", args => { cfg.Value = Settings.Load(cfg.Value); });
            }
            else Network.RegisterChatCommand("load", args => Network.SendCommand("load"));
            MyAPIGateway.Entities.OnEntityAdd += AddGrid;
            MyAPIGateway.Entities.OnEntityRemove += RemoveGrid;
            MyLog.Default.Info("[RelativeTopSpeed] Starting.");
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                settingsMenu = new RtsSettingsMenu(this);
                settingsMenu.Load();
            }
        }

        public override void BeforeStart()
        {
            // Entity events can precede session initialization.
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            foreach (var entity in entities) AddGrid(entity);
        }

        private void SettingChanged(Settings previous, Settings next)
        {
            try
            {
                Settings.Validate(ref next);
            }
            catch (ArgumentException error)
            {
                MyLog.Default.Warning("[RelativeTopSpeed] Rejected invalid curve: " + error.Message);
                var fallback = previous != null && !ReferenceEquals(previous, next)
                    ? previous : Settings.CreateDefault();
                try { Settings.Validate(ref fallback); }
                catch (ArgumentException) { fallback = Settings.CreateDefault(); }
                cfg.SetValue(fallback);
                return;
            }
            if (!ReferenceEquals(next, cfg.Value))
            {
                cfg.SetValue(next);
                return;
            }
            pendingConfigurationNotice = next.ConfigurationNotice;
            next.CalculateCurve();
            Settings.Instance = next;
            refreshCountdown = 0;
            SettingsChanged?.Invoke(next);
        }

        protected override void UnloadData()
        {
            settingsMenu?.Close();
            settingsMenu = null;
            MyAPIGateway.Entities.OnEntityAdd -= AddGrid;
            MyAPIGateway.Entities.OnEntityRemove -= RemoveGrid;
            foreach (var grid in grids) grid.OnStaticChanged -= OnStaticChanged;
            grids.Clear();
            activeGrids.Clear();
            groupBuffer.Clear();
            activationCache.Clear();
            physicalGroup.Clear();
            handledBodies.Clear();
            handledGroupGrids.Clear();
            if (cfg != null)
            {
                cfg.ValueChanged -= SettingChanged;
                cfg = null;
            }
            RtsApiBackend.Close();
            Settings.Instance = null;
            SettingsChanged = null;
        }
    }
}
