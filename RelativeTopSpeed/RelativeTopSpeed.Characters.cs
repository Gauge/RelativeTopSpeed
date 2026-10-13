using System;
using System.Collections.Generic;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace RelativeTopSpeed
{
    public partial class RelativeTopSpeed
    {
        private float? characterSpeedLimit;
        private bool scanCharacterSpeeds;
        private readonly HashSet<IMyEntity> characterScan = new HashSet<IMyEntity>();
        private readonly List<IMyCharacter> pendingCharacterRefresh = new List<IMyCharacter>();
        private readonly List<IMyPlayer> characterPlayers = new List<IMyPlayer>();

        private void QueueCharacterRefresh(IMyCharacter character)
        {
            if (character != null && !pendingCharacterRefresh.Contains(character))
                pendingCharacterRefresh.Add(character);
        }

        private void CharacterSpeedLimitChanged(float limit)
        {
            if (characterSpeedLimit.HasValue && characterSpeedLimit.Value != limit)
            {
                scanCharacterSpeeds = true;
                MyLog.Default.Info($"[RelativeTopSpeed] Character speed refresh: speed limit {characterSpeedLimit.Value} -> {limit}; refresh queued.");
            }
            characterSpeedLimit = limit;
        }

        public override void UpdateBeforeSimulation()
        {
            if (cfg == null) return;
            if (scanCharacterSpeeds)
            {
                scanCharacterSpeeds = false;
                characterScan.Clear();
                MyAPIGateway.Entities.GetEntities(characterScan);
                foreach (var entity in characterScan)
                {
                    QueueCharacterRefresh(entity as IMyCharacter);
                }
                characterScan.Clear();
                // Seated pilots may be absent from the top-level entity collection.
                characterPlayers.Clear();
                MyAPIGateway.Players.GetPlayers(characterPlayers);
                foreach (var player in characterPlayers) QueueCharacterRefresh(player.Character);
                characterPlayers.Clear();
            }

            for (int i = pendingCharacterRefresh.Count - 1; i >= 0; i--)
            {
                var character = pendingCharacterRefresh[i];
                if (character.Closed || character.IsDead)
                {
                    pendingCharacterRefresh.RemoveAt(i);
                    continue;
                }
                if (!MyAPIGateway.Multiplayer.IsServer)
                {
                    var local = MyAPIGateway.Session.LocalHumanPlayer?.Character;
                    if (local == null) continue;
                    if (local != character)
                    {
                        pendingCharacterRefresh.RemoveAt(i);
                        continue;
                    }
                }
                try
                {
                    if (!TryRefreshCharacterSpeed(character)) continue;
                    pendingCharacterRefresh.RemoveAt(i);
                    MyLog.Default.Info($"[RelativeTopSpeed] Character speed refresh: rebuilt character {character.EntityId} at world limit {characterSpeedLimit} m/s.");
                }
                catch (Exception error)
                {
                    // Do not repeatedly dispose physics after a failed rebuild.
                    pendingCharacterRefresh.RemoveAt(i);
                    MyLog.Default.Error($"[RelativeTopSpeed] Character speed refresh failed for {character.EntityId}: {error}");
                    if (MyAPIGateway.Session.LocalHumanPlayer?.Character == character)
                        MyAPIGateway.Utilities.ShowMessage(ModName, "Suit physics refresh failed. Reload the world and check SpaceEngineers.log.");
                }
            }
        }

        internal static bool TryRefreshCharacterSpeed(IMyCharacter character)
        {
            var physics = character.Physics;
            var movement = character.CurrentMovementState;
            var mode = movement.GetMode();
            if (character.Closed || character.IsDead || physics == null ||
                !physics.Enabled || physics.IsStatic || !character.InScene ||
                character.UsingEntity != null || mode == (int)MyCharacterMovementEnum.Sitting ||
                mode == (int)MyCharacterMovementEnum.Ladder || movement == MyCharacterMovementEnum.LadderOut)
                return false;

            var linear = physics.LinearVelocity;
            var angular = physics.AngularVelocity;
            var jetpack = character.Components.Get<MyCharacterJetpackComponent>();
            bool jetpackOn = jetpack != null && jetpack.TurnedOn;
            bool dampeners = jetpack != null && jetpack.DampenersTurnedOn;

            // Close clears the proxy but leaves its physics component attached.
            // A crouch-mode transition with no proxy makes SE's UpdateCrouchState
            // call UpdateCharacterPhysics(true), rebuilding the cached speed cap
            // and the engine's contact callbacks through whitelisted APIs.
            try
            {
                physics.Close();
                character.CurrentMovementState = mode == (int)MyCharacterMovementEnum.Crouching
                    ? MyCharacterMovementEnum.Standing : MyCharacterMovementEnum.Crouching;
                if (character.Physics == null || ReferenceEquals(character.Physics, physics))
                    throw new InvalidOperationException("SE did not rebuild physics after the crouch-mode transition.");

                character.CurrentMovementState = movement;
                if (jetpack != null)
                {
                    jetpack.TurnOnJetpack(jetpackOn, fromInit: true, fromLoad: true);
                    jetpack.EnableDampeners(dampeners);
                }
            }
            finally
            {
                // A callback or jetpack restoration can fail after SE has already
                // rebuilt the body. Preserve momentum even on that failure path;
                // never write back into the disposed original body.
                var replacement = character.Physics;
                if (replacement != null && !ReferenceEquals(replacement, physics))
                {
                    replacement.LinearVelocity = linear;
                    replacement.AngularVelocity = angular;
                }
            }
            return true;
        }
    }
}
