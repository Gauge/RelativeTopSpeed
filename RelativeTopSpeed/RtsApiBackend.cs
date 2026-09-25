// Publishes the methods RtsApi binds to. Keep names and delegate types in step with RtsApi.cs.
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace RelativeTopSpeed
{
    public class RtsApiBackend
    {
        private const long ChannelId = 2772681332;
        private static Dictionary<string, Delegate> APIMethods;

        private static RelativeTopSpeed RTS;
        public static bool IsInitialized => RTS != null;

        public static void Init(RelativeTopSpeed rts)
        {
            if (rts == null) throw new ArgumentNullException("rts");
            if (IsInitialized) Close();
            RTS = rts;

            APIMethods = new Dictionary<string, Delegate>()
            {
                ["GetCruiseSpeed"] = new Func<IMyCubeGrid, float>(RTS.GetCruiseSpeed),
                ["GetMaxSpeed"] = new Func<IMyCubeGrid, float>(RTS.GetMaxSpeed),
                ["GetBoost"] = new Func<IMyCubeGrid, float[]>(RTS.GetBoost),
                ["GetAcceleration"] = new Func<IMyCubeGrid, float[]>(RTS.GetAcceleration),
                ["GetAccelerationByDirection"] = new Func<IMyCubeGrid, float[]>(RTS.GetAccelerationsByDirection),
                ["GetWorldSpeedLimit"] = new Func<float>(RTS.GetWorldSpeedLimit),
                ["GetCruiseSpeedForMass"] = new Func<float, bool, float>(RTS.GetCruiseSpeedForMass),
                ["GetSpeedCeilingForMass"] = new Func<float, bool, float>(RTS.GetSpeedCeilingForMass),
                ["GetEffectiveMass"] = new Func<IMyCubeGrid, float>(RTS.GetEffectiveMass),
                ["GetEffectiveCruiseSpeed"] = new Func<IMyCubeGrid, float>(RTS.GetEffectiveCruiseSpeed),
                ["IsGridGroupsEnabled"] = new Func<bool>(RTS.IsGridGroupsEnabled),
                ["GetEffectiveSpeedCeiling"] = new Func<IMyCubeGrid, float>(RTS.GetEffectiveSpeedCeiling),
                ["IsGoverned"] = new Func<IMyCubeGrid, bool>(RTS.IsGoverned),
            };

            MyAPIGateway.Utilities.RegisterMessageHandler(ChannelId, OnMessageRecieved);
            MyAPIGateway.Utilities.SendModMessage(ChannelId, APIMethods);
        }

        public static void Close()
        {
            if (!IsInitialized) return;
            MyAPIGateway.Utilities.UnregisterMessageHandler(ChannelId, OnMessageRecieved);
            RTS = null;
            APIMethods = null;
        }

        private static void OnMessageRecieved(object o)
        {
            if ((o as string) == "ApiEndpointRequest")
                MyAPIGateway.Utilities.SendModMessage(ChannelId, APIMethods);
        }
    }
}
