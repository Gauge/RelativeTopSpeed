using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Text;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace RelativeTopSpeed
{
    class Communication
    {
        private const ushort ComId = 16341;
        private const string ModName = "Relative Top Speed";
        private const string CommandKeyword = "/rspeed";

        private enum Commands { none, load, request, help }

        public Communication()
        {
            MyAPIGateway.Utilities.MessageEntered += HandleChatInput;

            if (MyAPIGateway.Session.IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(ComId, HandleClientMessage);
            }
            else
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(ComId, HandleServerMessage);
            }
        }

        public void Close()
        {
            MyAPIGateway.Utilities.MessageEntered -= HandleChatInput;

            if (MyAPIGateway.Session.IsServer)
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(ComId, HandleClientMessage);
            }
            else
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(ComId, HandleServerMessage);
            }
        }

        private void HandleClientMessage(byte[] msg)
        {
            StringBuilder response = new StringBuilder();
            string[] args = Encoding.UTF8.GetString(msg).ToLower().Split(' ');

            ulong clientSteamId;
            if (!ulong.TryParse(args[0], out clientSteamId)) return;

            if (args.Length > 1 && args[1] == CommandKeyword)
            {
                Commands pcmd = Commands.none;
                float value = float.MinValue;
                if (args.Length == 2)
                {
                    pcmd = Commands.help;
                }
                if (args.Length >= 3)
                {
                    if (!Enum.TryParse(args[2], out pcmd))
                    {
                        response.AppendLine($"Command \"{args[2]}\" is not recognised");
                        return;
                    }
                }

                switch (pcmd)
                {
                    case Commands.load:
                        RelativeTopSpeed.cfg = Settings.Load();
                        MyAPIGateway.Multiplayer.SendMessageToOthers(ComId, MyAPIGateway.Utilities.SerializeToBinary(RelativeTopSpeed.cfg));
                        response.Append("Loading from file");
                        break;

                    case Commands.request:
                        Logger.Log(MyLogSeverity.Info, $"Setting Request Recived");
                        MyAPIGateway.Multiplayer.SendMessageTo(ComId, MyAPIGateway.Utilities.SerializeToBinary(RelativeTopSpeed.cfg), clientSteamId);
                        return;
                        
                    case Commands.help:
                        response.AppendLine("Type: \"/rspeed load\" to load changes from the config file");
                        break;
                }

                if (MyAPIGateway.Session.LocalHumanPlayer != null && MyAPIGateway.Session.LocalHumanPlayer.SteamUserId == clientSteamId)
                {
                    MyAPIGateway.Utilities.ShowMessage(ModName, response.ToString());
                }
                else
                {
                    SendMessageToClient(response.ToString(), clientSteamId);
                }
            }
        }

        private void SendMessageToClient(string message, ulong steamId = ulong.MinValue)
        {
            if (steamId == ulong.MinValue)
            {
                MyAPIGateway.Multiplayer.SendMessageToOthers(ComId, Encoding.UTF8.GetBytes(message));
            }
            else
            {
                MyAPIGateway.Multiplayer.SendMessageTo(ComId, Encoding.UTF8.GetBytes(message), steamId);
            }
        }

        private void HandleChatInput(string msg, ref bool sendToOthers)
        {
            string[] args = msg.ToLower().Split(' ');
            if (args[0] != CommandKeyword) return;
            sendToOthers = false;

            Commands cmd;
            if (args.Length == 1)
            {
                cmd = Commands.help;
            }
            else
            {
                if (!Enum.TryParse(args[1], out cmd))
                {
                    MyAPIGateway.Utilities.ShowMessage(ModName, $"Unrecognised command \"{args[1]}\". Type \"/rspeed help\" for a list of commands.");
                    return;
                }
            }

            bool shouldSendToServer = true;
            switch (cmd)
            {
                case Commands.load:
                    shouldSendToServer = IsAllowedSpecialOperations(MyAPIGateway.Session.LocalHumanPlayer.PromoteLevel);
                    break;
            }

            if (!shouldSendToServer)
            {
                MyAPIGateway.Utilities.ShowMessage(ModName, $"Error: The command \"{cmd.ToString()}\" requires admin status");
                return;

            }

            if (MyAPIGateway.Session.IsServer)
            {
                HandleClientMessage(Encoding.UTF8.GetBytes($"{MyAPIGateway.Session.Player.SteamUserId} {msg}"));
            }
            else
            {
                SendMessageToServer(msg);
            }
        }

        private void HandleServerMessage(byte[] data)
        {
            try
            {
                RelativeTopSpeed.cfg = MyAPIGateway.Utilities.SerializeFromBinary<Settings>(data);
                MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = RelativeTopSpeed.cfg.SpeedLimit;
                MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = RelativeTopSpeed.cfg.SpeedLimit;
                RelativeTopSpeed.cfg.CalculateCurve();
            }
            catch
            {
                string message = Encoding.UTF8.GetString(data);
                MyAPIGateway.Utilities.ShowMessage(ModName, message);
            }
        }

        public void RequestServerSettings()
        {
            if (!MyAPIGateway.Session.IsServer)
            {
                SendMessageToServer("/rspeed request");
            }
        }

        private void SendMessageToServer(string message)
        {
            MyAPIGateway.Multiplayer.SendMessageToServer(ComId, Encoding.UTF8.GetBytes($"{MyAPIGateway.Session.Player.SteamUserId} {message}"));
        }

        public static bool IsAllowedSpecialOperations(ulong steamId)
        {
            return IsAllowedSpecialOperations(MyAPIGateway.Session.GetUserPromoteLevel(steamId));
        }

        public static bool IsAllowedSpecialOperations(MyPromoteLevel level)
        {
            return level == MyPromoteLevel.SpaceMaster || level == MyPromoteLevel.Admin || level == MyPromoteLevel.Owner;
        }
    }
}