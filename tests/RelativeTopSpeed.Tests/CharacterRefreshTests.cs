using SEStubs;
using VRage.Game;
using VRageMath;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class CharacterRefreshTests : TestSession
    {
        private void ChangeLimit(float limit)
        {
            var settings = Settings.CreateDefault(); settings.SpeedLimit = limit;
            Mod.cfg.Value = settings;
        }
        [Theory]
        [InlineData(600)]
        [InlineData(100)]
        public void LiveLimitChangeRebuildsOnceAndPreservesVelocityAndSuitSettings(float limit)
        {
            Start();
            var suit = Game.Entities.Add(new FakeCharacter { CurrentMovementState = MyCharacterMovementEnum.Flying });
            suit.Physics.LinearVelocity = new Vector3(151, 2, 3);
            suit.Physics.AngularVelocity = new Vector3(1, 2, 3);
            suit.Components.Jetpack.TurnedOn = true;
            suit.Components.Jetpack.DampenersTurnedOn = false;
            ChangeLimit(limit);
            Assert.Equal(0, suit.Rebuilds); // No disposal inside the settings/network callback.
            Frames(2);
            Assert.Equal(1, suit.Rebuilds);
            Assert.Equal(151, suit.Physics.LinearVelocity.X);
            Assert.Equal(2, suit.Physics.LinearVelocity.Y);
            Assert.Equal(3, suit.Physics.AngularVelocity.Z);
            Assert.True(suit.Components.Jetpack.TurnedOn);
            Assert.False(suit.Components.Jetpack.DampenersTurnedOn);
            Assert.Equal(MyCharacterMovementEnum.Flying, suit.CurrentMovementState);
            ChangeLimit(limit); Frames(1); Assert.Equal(1, suit.Rebuilds);
        }
        [Fact]
        public void StartupAndUnrelatedSettingsDoNotRebuildCharacters()
        {
            var suit = Game.Entities.Add(new FakeCharacter());
            Start(); Frames(1);
            var settings = Settings.CreateDefault(); settings.ParachuteDeployHeight = 800;
            Mod.cfg.Value = settings; Frames(1);
            Assert.Equal(0, suit.Rebuilds);
        }
        [Fact]
        public void SeatedCharacterWaitsUntilOnFootAndUsesLatestLimit()
        {
            Start(); var suit = new FakeCharacter();
            ((FakePlayer)Game.Session.LocalHumanPlayer).Character = suit;
            suit.Physics.Enabled = false;
            suit.CurrentMovementState = MyCharacterMovementEnum.Sitting;
            ChangeLimit(300); Frames(1); ChangeLimit(600); Frames(1);
            Assert.Equal(0, suit.Rebuilds);
            suit.Physics.Enabled = true; suit.CurrentMovementState = MyCharacterMovementEnum.Standing;
            Frames(1); Assert.Equal(1, suit.Rebuilds);
        }
        [Fact]
        public void ClientOnlyRebuildsItsOwnCharacter()
        {
            Game.BecomeServer(false); Start();
            var local = Game.Entities.Add(new FakeCharacter());
            var remote = Game.Entities.Add(new FakeCharacter());
            ((FakePlayer)Game.Session.LocalHumanPlayer).Character = local;
            // SetValue applies a received server configuration without posting it back.
            var settings = Settings.CreateDefault(); settings.SpeedLimit = 600;
            Mod.cfg.SetValue(settings);
            Frames(1);
            Assert.Equal(1, local.Rebuilds); Assert.Equal(0, remote.Rebuilds);
        }
        [Fact]
        public void CrouchedCharacterRestoresItsStanceWithoutJetpack()
        {
            Start(); var suit = Game.Entities.Add(new FakeCharacter { CurrentMovementState = MyCharacterMovementEnum.Crouching });
            suit.Components.Jetpack = null;
            ChangeLimit(600); Frames(1);
            Assert.Equal(1, suit.Rebuilds);
            Assert.Equal(MyCharacterMovementEnum.Crouching, suit.CurrentMovementState);
        }
        [Fact]
        public void FailedRebuildIsLoggedOnceAndDoesNotStopOtherCharacters()
        {
            Start(); var good = Game.Entities.Add(new FakeCharacter());
            var broken = Game.Entities.Add(new FakeCharacter { RebuildSucceeds = false });
            ChangeLimit(600); Frames(2);
            Assert.Equal(1, good.Rebuilds);
            Assert.Single(VRage.Utils.MyLog.Default.Entries, e => e.Message.Contains("Character speed refresh failed"));
        }
        [Fact]
        public void ClosedAndDeadCharactersAreNotRebuilt()
        {
            Start(); var dead = Game.Entities.Add(new FakeCharacter { IsDead = true });
            var closed = Game.Entities.Add(new FakeCharacter { Closed = true });
            ChangeLimit(600); Frames(1);
            Assert.Equal(0, dead.Rebuilds); Assert.Equal(0, closed.Rebuilds);
        }

        [Fact]
        public void JetpackRestoreFailureStillPreservesMomentumAndIsNotRetriedEachFrame()
        {
            Start(); var suit = Game.Entities.Add(new FakeCharacter());
            suit.Physics.LinearVelocity = new Vector3(170, 2, 3);
            suit.Physics.AngularVelocity = new Vector3(1, 2, 4);
            suit.Components.Jetpack.FailRestore = true;
            ChangeLimit(600); Frames(2);
            Assert.Equal(1, suit.Rebuilds);
            Assert.Equal(170, suit.Physics.LinearVelocity.X);
            Assert.Equal(4, suit.Physics.AngularVelocity.Z);
            Assert.Single(VRage.Utils.MyLog.Default.Entries, e => e.Message.Contains("Character speed refresh failed"));
        }

        [Fact]
        public void LadderRefreshWaitsAndMultipleChangesCoalesce()
        {
            Start(); var suit = Game.Entities.Add(new FakeCharacter { CurrentMovementState = MyCharacterMovementEnum.Ladder });
            ChangeLimit(300); ChangeLimit(600); Frames(1);
            Assert.Equal(0, suit.Rebuilds);
            suit.CurrentMovementState = MyCharacterMovementEnum.Standing;
            Frames(2);
            Assert.Equal(1, suit.Rebuilds);
        }

        [Fact]
        public void SuccessfulLocalRefreshDoesNotPostDiagnosticChat()
        {
            Start(); var suit = Game.Entities.Add(new FakeCharacter());
            ((FakePlayer)Game.Session.LocalHumanPlayer).Character = suit;
            int messages = Game.ShownMessages.Count;
            ChangeLimit(600); Frames(1);
            Assert.Equal(1, suit.Rebuilds);
            Assert.Equal(messages, Game.ShownMessages.Count);
        }

        [Fact]
        public void UnloadDiscardsDeferredRefreshes()
        {
            Start(); var suit = Game.Entities.Add(new FakeCharacter());
            suit.Physics.Enabled = false;
            ChangeLimit(600); Frames(1);
            Mod.SimulateUnload();
            suit.Physics.Enabled = true;
            Mod.UpdateBeforeSimulation();
            Assert.Equal(0, suit.Rebuilds);
        }
    }
}
