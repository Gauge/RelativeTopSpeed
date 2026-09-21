using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace RelativeTopSpeed
{
    public class RtsApi
    {
        private const long ChannelId = 2772681332;
        public bool IsReady { get; private set; }

        private Action ReadyCallback;

        private bool isRegistered = false;

        public void Load(Action readyCallback = null)
        {
            if (isRegistered)
                throw new Exception($"{GetType().Name}.Load() should not be called multiple times!");

            isRegistered = true;
            ReadyCallback = readyCallback;
            MyAPIGateway.Utilities.RegisterMessageHandler(ChannelId, HandleMessage);
            MyAPIGateway.Utilities.SendModMessage(ChannelId, "ApiEndpointRequest");
        }

        public void Unload()
        {
            if (!isRegistered) return;
            MyAPIGateway.Utilities.UnregisterMessageHandler(ChannelId, HandleMessage);
            IsReady = false;
            isRegistered = false;
            ReadyCallback = null;
            _GetCruiseSpeed = null;
            _GetMaxSpeed = null;
            _GetBoost = null;
            _GetAcceleration = null;
            _GetAccelerationByDirection = null;
        }

        private void HandleMessage(object obj)
        {
            if (obj is string) // the sent "ApiEndpointRequest" will also be received here, explicitly ignoring that
                return;

            var dict = obj as IReadOnlyDictionary<string, Delegate>;

            if (dict == null)
                return;

            if (IsReady) return;
            // Bind atomically: a malformed endpoint must not leave partial delegates.
            Func<IMyCubeGrid, float> cruise = null, max = null;
            Func<IMyCubeGrid, float[]> boost = null, acceleration = null, directions = null;
            AssignMethod(dict, "GetCruiseSpeed", ref cruise);
            AssignMethod(dict, "GetMaxSpeed", ref max);
            AssignMethod(dict, "GetBoost", ref boost);
            AssignMethod(dict, "GetAcceleration", ref acceleration);
            AssignMethod(dict, "GetAccelerationByDirection", ref directions);
            _GetCruiseSpeed = cruise; _GetMaxSpeed = max; _GetBoost = boost;
            _GetAcceleration = acceleration; _GetAccelerationByDirection = directions;

            IsReady = true;
            ReadyCallback?.Invoke();
        }

        private void EnsureReady()
        {
            if (!IsReady) throw new InvalidOperationException("Relative Top Speed API is not ready.");
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if (delegates == null)
            {
                field = null;
                return;
            }

            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");

            field = del as T;

            if (field == null)
                throw new Exception(
                    $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del?.GetType()}");
        }



        /// <summary>
        /// returns the crusing speed of the grid.
        /// </summary>
        public float GetCruiseSpeed(IMyCubeGrid grid) { EnsureReady(); return _GetCruiseSpeed.Invoke(grid); }
        private Func<IMyCubeGrid, float> _GetCruiseSpeed;

        /// <summary>
        /// gets the maximum possible speed (cruise speed + max boost)
        /// </summary>
        public float GetMaxSpeed(IMyCubeGrid grid) { EnsureReady(); return _GetMaxSpeed.Invoke(grid); }
        private Func<IMyCubeGrid, float> _GetMaxSpeed;

        /// <summary>
        /// returns 4 values: forward boost, min, average, max
        /// </summary>
        public float[] GetBoost(IMyCubeGrid grid) { EnsureReady(); return _GetBoost.Invoke(grid); }
        private Func<IMyCubeGrid, float[]> _GetBoost;



        /// <summary>
        /// Returns 4 values: forward accel, min, average, max
        /// </summary>
        public float[] GetAcceleration(IMyCubeGrid grid) { EnsureReady(); return _GetAcceleration.Invoke(grid); }
        private Func<IMyCubeGrid, float[]> _GetAcceleration;

        /// <summary>
        /// Uses Base6Directions.Direction
        /// forward = reverse accel
        /// backword = forward accel
        /// left = right accel
        /// ...
        /// </summary>
        public float[] GetAccelerationByDirection(IMyCubeGrid grid) { EnsureReady(); return _GetAccelerationByDirection.Invoke(grid); }
        private Func<IMyCubeGrid, float[]> _GetAccelerationByDirection;

    }
}
