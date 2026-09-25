using Sandbox.ModAPI;
using System;
using VRage.Game.ModAPI;

namespace RelativeTopSpeed
{
    public partial class RelativeTopSpeed
    {
        #region Communications

        private void Chat_Help(string arguments)
        {
            MyAPIGateway.Utilities.ShowMessage(Network.ModName, "Relative Top Speed\nHUD: displays ship stats when in cockpit\nMENU: opens the settings window (needs Rich HUD Master)\nCONFIG: Displays the current config\nLOAD: load world configuration");
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
                cfg.Value = Settings.Load(cfg.Value);
            }
            else
            {
                Network.SendCommand(null, "Load command requires Admin status.", steamId: steamId);
            }
        }

        public static bool IsAllowedSpecialOperations(ulong steamId)
        {
            if (MyAPIGateway.Multiplayer.IsServer &&
                MyAPIGateway.Session.LocalHumanPlayer?.SteamUserId == steamId)
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
