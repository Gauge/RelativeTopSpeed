// Models only the documented rebuild boundary, not Havok or multiplayer prediction.
// Runtime viability must be verified in Space Engineers.
using System;
using Sandbox.Game.Entities.Character.Components;
using VRage.Game.Components;

namespace VRage.Game
{
    public enum MyCharacterMovementEnum { Standing = 0, Sitting = 1, Crouching = 2, Flying = 3, Falling = 4, Died = 6, Ladder = 7, LadderOut = 16384 }
    public static class CharacterMovementExtensions
    {
        public static int GetMode(this MyCharacterMovementEnum state) => (int)state & 15;
    }
}
namespace VRage.Game.ModAPI
{
    public interface IMyCharacter : VRage.ModAPI.IMyEntity
    {
        bool Closed { get; }
        bool IsDead { get; }
        bool InScene { get; }
        object UsingEntity { get; }
        FakePhysics Physics { get; }
        MyCharacterMovementEnum CurrentMovementState { get; set; }
        SEStubs.CharacterComponents Components { get; }
    }
    public partial interface IMyPlayer { IMyCharacter Character { get; } }
}
namespace Sandbox.Game.Entities.Character.Components
{
    public class MyCharacterJetpackComponent
    {
        public bool TurnedOn { get; set; }
        public bool DampenersTurnedOn { get; set; }
        public int Restores;
        public bool FailRestore;
        public void TurnOnJetpack(bool on, bool fromInit = false, bool fromLoad = false) { Restores++; if (FailRestore) throw new InvalidOperationException("Injected jetpack restoration failure"); TurnedOn = on; }
        public void EnableDampeners(bool on) { DampenersTurnedOn = on; }
    }
}
namespace SEStubs
{
    public partial class FakePlayer { public VRage.Game.ModAPI.IMyCharacter Character { get; set; } }
    public class CharacterComponents
    {
        public MyCharacterJetpackComponent Jetpack = new MyCharacterJetpackComponent();
        public T Get<T>() where T : class => Jetpack as T;
    }
    public class FakeCharacter : VRage.Game.Entity.MyEntity, VRage.Game.ModAPI.IMyCharacter
    {
        public bool IsDead { get; set; }
        public bool InScene { get; set; } = true;
        public object UsingEntity { get; set; }
        public FakePhysics Physics { get; set; } = new FakePhysics();
        public CharacterComponents Components { get; } = new CharacterComponents();
        public int Rebuilds;
        public bool RebuildSucceeds = true;
        private VRage.Game.MyCharacterMovementEnum movement;
        public VRage.Game.MyCharacterMovementEnum CurrentMovementState
        {
            get => movement;
            set
            {
                bool wasCrouched = movement == VRage.Game.MyCharacterMovementEnum.Crouching;
                movement = value;
                if (Physics.Closed && wasCrouched != (value == VRage.Game.MyCharacterMovementEnum.Crouching) && RebuildSucceeds)
                { Physics = new FakePhysics(); Rebuilds++; }
            }
        }
    }
}
