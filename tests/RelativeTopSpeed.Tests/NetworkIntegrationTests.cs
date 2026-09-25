using System;
using SENetworkAPI;
using VRage.Game.ModAPI;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class NetworkIntegrationTests : TestSession
    {
        private void Receive(Command command)
        {
            command.Timestamp = DateTime.UtcNow.Ticks;
            Game.Multiplayer.DeliverSecure(16341, Game.Utilities.SerializeToBinary(command), command.SteamId, !Game.Multiplayer.IsServer);
        }

        [Fact]
        public void ClientFetchesSettingsUsingUpstreamInlinePropertyProtocol()
        {
            Game.BecomeServer(false);
            Start();
            Assert.Empty(Game.Sent);
            Game.NextFrame();
            var packet = Game.Utilities.SerializeFromBinary<Command>(Assert.Single(Game.Sent).Data);
            Assert.True(packet.IsProperty);
            Assert.Equal(SyncType.Fetch, packet.Property.SyncType);
            Assert.Equal(Mod.cfg.Id, packet.Property.Id);

            var settings = Settings.CreateDefault();
            settings.SpeedLimit = 600;
            Receive(new Command
            {
                IsProperty = true,
                Property = new SyncData
                {
                    Id = Mod.cfg.Id, SyncType = SyncType.Post,
                    Data = Game.Utilities.SerializeToBinary(settings)
                }
            });
            Assert.Equal(600, Mod.cfg.Value.SpeedLimit);
            Assert.Same(Mod.cfg.Value, Settings.Instance);
            Assert.Equal(600, Sandbox.Definitions.MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed);
        }

        [Fact]
        public void ServerAnswersClientFetchAfterUpstreamFlush()
        {
            Start();
            Game.ClearTraffic();
            Receive(new Command
            {
                SteamId = 200, IsProperty = true,
                Property = new SyncData { Id = Mod.cfg.Id, SyncType = SyncType.Fetch }
            });
            Game.NextFrame();
            var sent = Assert.Single(Game.Sent);
            Assert.Equal(200UL, sent.Recipient);
            var packet = Game.Utilities.SerializeFromBinary<Command>(sent.Data);
            Assert.Equal(SyncType.Post, packet.Property.SyncType);
            var settings = Game.Utilities.SerializeFromBinary<Settings>(packet.Property.Data);
            Assert.Equal(Mod.cfg.Value.ToString(), settings.ToString());
        }

        [Theory]
        [InlineData(MyPromoteLevel.None, 140)]
        [InlineData(MyPromoteLevel.Admin, 600)]
        public void ReloadChecksPromotionOfTransportSender(MyPromoteLevel promotion, float expected)
        {
            // The secure handler receives the sender independently from the serialized packet.
            Start();
            var settings = Settings.CreateDefault(); settings.SpeedLimit = 600;
            Game.Utilities.World[Settings.Filename] = settings.ToString();
            Game.Session.Promotions[200] = promotion;
            Receive(new Command { SteamId = 200, CommandString = "load" });
            Assert.Equal(expected, Mod.cfg.Value.SpeedLimit);
        }
    }
}
