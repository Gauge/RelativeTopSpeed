// Relative Top Speed API client. Copy this file into your mod unchanged.
// Call Load() while your session component loads and Unload() when it unloads.
// Methods throw until IsReady is true; pass a callback to Load() to be told when.
// Call them on the game thread.
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

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
            _GetWorldSpeedLimit = null;
            _GetCruiseSpeedForMass = null;
            _GetSpeedCeilingForMass = null;
            _GetEffectiveMass = null;
            _GetEffectiveCruiseSpeed = null;
            _IsGridGroupsEnabled = null;
            _GetEffectiveSpeedCeiling = null;
            _IsGoverned = null;
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
            Func<IMyCubeGrid, float> boundGetCruiseSpeed = null;
            AssignMethod(dict, "GetCruiseSpeed", ref boundGetCruiseSpeed);
            Func<IMyCubeGrid, float> boundGetMaxSpeed = null;
            AssignMethod(dict, "GetMaxSpeed", ref boundGetMaxSpeed);
            Func<IMyCubeGrid, float[]> boundGetBoost = null;
            AssignMethod(dict, "GetBoost", ref boundGetBoost);
            Func<IMyCubeGrid, float[]> boundGetAcceleration = null;
            AssignMethod(dict, "GetAcceleration", ref boundGetAcceleration);
            Func<IMyCubeGrid, float[]> boundGetAccelerationByDirection = null;
            AssignMethod(dict, "GetAccelerationByDirection", ref boundGetAccelerationByDirection);
            Func<float> boundGetWorldSpeedLimit = null;
            AssignMethod(dict, "GetWorldSpeedLimit", ref boundGetWorldSpeedLimit);
            Func<float, bool, float> boundGetCruiseSpeedForMass = null;
            AssignMethod(dict, "GetCruiseSpeedForMass", ref boundGetCruiseSpeedForMass);
            Func<float, bool, float> boundGetSpeedCeilingForMass = null;
            AssignMethod(dict, "GetSpeedCeilingForMass", ref boundGetSpeedCeilingForMass);
            Func<IMyCubeGrid, float> boundGetEffectiveMass = null;
            AssignMethod(dict, "GetEffectiveMass", ref boundGetEffectiveMass);
            Func<IMyCubeGrid, float> boundGetEffectiveCruiseSpeed = null;
            AssignMethod(dict, "GetEffectiveCruiseSpeed", ref boundGetEffectiveCruiseSpeed);
            Func<bool> boundIsGridGroupsEnabled = null;
            AssignMethod(dict, "IsGridGroupsEnabled", ref boundIsGridGroupsEnabled);
            Func<IMyCubeGrid, float> boundGetEffectiveSpeedCeiling = null;
            AssignMethod(dict, "GetEffectiveSpeedCeiling", ref boundGetEffectiveSpeedCeiling);
            Func<IMyCubeGrid, bool> boundIsGoverned = null;
            AssignMethod(dict, "IsGoverned", ref boundIsGoverned);
            _GetCruiseSpeed = boundGetCruiseSpeed;
            _GetMaxSpeed = boundGetMaxSpeed;
            _GetBoost = boundGetBoost;
            _GetAcceleration = boundGetAcceleration;
            _GetAccelerationByDirection = boundGetAccelerationByDirection;
            _GetWorldSpeedLimit = boundGetWorldSpeedLimit;
            _GetCruiseSpeedForMass = boundGetCruiseSpeedForMass;
            _GetSpeedCeilingForMass = boundGetSpeedCeilingForMass;
            _GetEffectiveMass = boundGetEffectiveMass;
            _GetEffectiveCruiseSpeed = boundGetEffectiveCruiseSpeed;
            _IsGridGroupsEnabled = boundIsGridGroupsEnabled;
            _GetEffectiveSpeedCeiling = boundGetEffectiveSpeedCeiling;
            _IsGoverned = boundIsGoverned;

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
        /// Returns the cruising speed of the grid.
        /// </summary>
        public float GetCruiseSpeed(IMyCubeGrid grid) { EnsureReady(); return _GetCruiseSpeed.Invoke(grid); }
        private Func<IMyCubeGrid, float> _GetCruiseSpeed;

        /// <summary>
        /// Returns the maximum speed, including boost and configured caps.
        /// </summary>
        public float GetMaxSpeed(IMyCubeGrid grid) { EnsureReady(); return _GetMaxSpeed.Invoke(grid); }
        private Func<IMyCubeGrid, float> _GetMaxSpeed;

        /// <summary>
        /// Returns forward boost, minimum, average, and maximum.
        /// </summary>
        public float[] GetBoost(IMyCubeGrid grid) { EnsureReady(); return _GetBoost.Invoke(grid); }
        private Func<IMyCubeGrid, float[]> _GetBoost;

        /// <summary>
        /// Returns forward acceleration, minimum, average, and maximum.
        /// </summary>
        public float[] GetAcceleration(IMyCubeGrid grid) { EnsureReady(); return _GetAcceleration.Invoke(grid); }
        private Func<IMyCubeGrid, float[]> _GetAcceleration;

        /// <summary>
        /// Returns acceleration indexed by Base6Directions.Direction (Forward, Backward, Left, Right, Up, Down). Each entry describes thrust in that grid direction; movement acceleration is in the opposite direction.
        /// </summary>
        public float[] GetAccelerationByDirection(IMyCubeGrid grid) { EnsureReady(); return _GetAccelerationByDirection.Invoke(grid); }
        private Func<IMyCubeGrid, float[]> _GetAccelerationByDirection;

        /// <summary>
        /// Returns the configured world speed limit, m/s.
        /// </summary>
        public float GetWorldSpeedLimit() { EnsureReady(); return _GetWorldSpeedLimit.Invoke(); }
        private Func<float> _GetWorldSpeedLimit;

        /// <summary>
        /// Evaluates the configured curve for a supplied mass in kg and grid class. Invalid mass returns zero.
        /// </summary>
        public float GetCruiseSpeedForMass(float mass, bool largeGrid) { EnsureReady(); return _GetCruiseSpeedForMass.Invoke(mass, largeGrid); }
        private Func<float, bool, float> _GetCruiseSpeedForMass;

        /// <summary>
        /// Returns the speed ceiling for a supplied mass: the boost ceiling when boosting is enabled, otherwise the cruise speed.
        /// </summary>
        public float GetSpeedCeilingForMass(float mass, bool largeGrid) { EnsureReady(); return _GetSpeedCeilingForMass.Invoke(mass, largeGrid); }
        private Func<float, bool, float> _GetSpeedCeilingForMass;

        /// <summary>
        /// Returns the mass enforcement uses: the grid's own mass, or the combined mass of its physical group when IsGridGroupsEnabled is true. Call on the game thread.
        /// </summary>
        public float GetEffectiveMass(IMyCubeGrid grid) { EnsureReady(); return _GetEffectiveMass.Invoke(grid); }
        private Func<IMyCubeGrid, float> _GetEffectiveMass;

        /// <summary>
        /// Returns the cruise speed enforcement uses for this grid, from its effective mass. Mixed physical groups use the large-grid curve. Call on the game thread.
        /// </summary>
        public float GetEffectiveCruiseSpeed(IMyCubeGrid grid) { EnsureReady(); return _GetEffectiveCruiseSpeed.Invoke(grid); }
        private Func<IMyCubeGrid, float> _GetEffectiveCruiseSpeed;

        /// <summary>
        /// Returns true when grids in a physical group (docked, connected by rotors, pistons or connectors) share one cruise speed from their combined mass.
        /// </summary>
        public bool IsGridGroupsEnabled() { EnsureReady(); return _IsGridGroupsEnabled.Invoke(); }
        private Func<bool> _IsGridGroupsEnabled;

        /// <summary>
        /// Returns the speed ceiling enforcement uses for this grid, from its effective mass and grid class. Mixed physical groups use the large-grid settings. Call on the game thread.
        /// </summary>
        public float GetEffectiveSpeedCeiling(IMyCubeGrid grid) { EnsureReady(); return _GetEffectiveSpeedCeiling.Invoke(grid); }
        private Func<IMyCubeGrid, float> _GetEffectiveSpeedCeiling;

        /// <summary>
        /// Returns true when RTS applies its speed force to this grid: it has physics, neither it nor its physical group is static, and it passes the thrust and cockpit filters. Call on the game thread.
        /// </summary>
        public bool IsGoverned(IMyCubeGrid grid) { EnsureReady(); return _IsGoverned.Invoke(grid); }
        private Func<IMyCubeGrid, bool> _IsGoverned;
    }
}
