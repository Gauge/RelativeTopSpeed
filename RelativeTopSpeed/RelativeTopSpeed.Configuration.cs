using System;
using ProtoBuf;
using Sandbox.ModAPI;

namespace RelativeTopSpeed
{
    [ProtoContract]
    public class ConfigurationEdit
    {
        [ProtoMember(1)] public string ExpectedXml;
        [ProtoMember(2)] public string UpdatedXml;
    }

    public partial class RelativeTopSpeed
    {
        internal bool ShowHud { get { return showHud; } set { showHud = value; } }
        internal bool CanEditSettings
        {
            get
            {
                var player = MyAPIGateway.Session.LocalHumanPlayer;
                return player != null && IsAllowedSpecialOperations(player.SteamUserId);
            }
        }

        internal string SubmitSettings(string expectedXml, Settings updated)
        {
            if (!CanEditSettings) return "World settings require Space Master, Admin or Owner access.";
            var edit = new ConfigurationEdit { ExpectedXml = expectedXml, UpdatedXml = updated.ToString() };
            if (MyAPIGateway.Multiplayer.IsServer) return ApplySettingsEdit(edit);
            Network.SendCommand("settings", data: MyAPIGateway.Utilities.SerializeToBinary(edit));
            return "Sent to server. Its response will appear in chat.";
        }

        private void ServerCallback_Settings(ulong steamId, string command, byte[] data, DateTime timestamp)
        {
            if (!IsAllowedSpecialOperations(steamId))
            {
                Network.SendCommand(null, "World settings require Space Master, Admin or Owner access.", steamId: steamId);
                return;
            }
            string result;
            try
            {
                if (data == null || data.Length > 65536) throw new ArgumentException("Settings request is too large or empty.");
                result = ApplySettingsEdit(MyAPIGateway.Utilities.SerializeFromBinary<ConfigurationEdit>(data));
            }
            catch (Exception error) { result = "Settings rejected: " + error.Message; }
            Network.SendCommand(null, result, steamId: steamId);
        }

        internal string ApplySettingsEdit(ConfigurationEdit edit)
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return "Only the server can apply world settings.";
            try
            {
                if (edit == null || edit.UpdatedXml == null || edit.UpdatedXml.Length > 32768)
                    throw new ArgumentException("Settings are missing or too large.");
                if (edit.ExpectedXml != cfg.Value.ToString())
                    return "Settings changed since you opened them. Reload current values, then apply again.";
                var next = MyAPIGateway.Utilities.SerializeFromXML<Settings>(edit.UpdatedXml);
                if (next == null || next.Version != Settings.CurrentVersion)
                    throw new ArgumentException("Unsupported configuration version.");
                Settings.Validate(ref next);
                next.ConfigurationNotice = null;
                if (!Settings.TrySave(next)) return "Could not save settings; no live changes were applied.";
                cfg.Value = next;
                return "Settings saved and applied. All players receive the updated configuration.";
            }
            catch (Exception error) { return "Settings rejected: " + error.Message; }
        }
    }
}
