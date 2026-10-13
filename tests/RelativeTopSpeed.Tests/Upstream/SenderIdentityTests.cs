using VRage.Game.Components;
using Xunit;

namespace SENetworkAPI.Tests
{
    // Supply transport identity separately from the packet's claimed identity.
    public class SenderIdentityTests : NetworkTestBase
    {
        private class TestSessionComponent : MySessionComponentBase { }
        private const ulong SomeoneElse = 76561190000000009;

        [Fact]
        public void ServerCommandUsesTransportSenderInsteadOfClaimedSender()
        {
            var server = GivenServer();
            ulong callbackSender = 0, eventSender = 0;
            server.RegisterNetworkCommand("admin", (s, c, d, t) => callbackSender = s);
            server.OnCommandRecived += (s, c, d, t) => eventSender = s;
            Receive(EncodeCommandPacket("admin", from: SomeoneElse), sender: ClientId, fromServer: false);
            Assert.Equal(ClientId, callbackSender);
            Assert.Equal(ClientId, eventSender);
        }

        [Fact]
        public void ServerPropertyEventUsesTransportSenderInsteadOfClaimedSender()
        {
            GivenServer();
            var property = new NetSync<int>(new TestSessionComponent(), TransferType.Both, 0, syncOnLoad: false);
            ulong reported = 0;
            property.ValueChangedByNetwork += (o, n, s) => reported = s;
            Receive(EncodePropertyPacket(property.Id, 0, SyncType.Post, 5, from: SomeoneElse), sender: ClientId);
            Assert.Equal(5, property.Value);
            Assert.Equal(ClientId, reported);
        }

        [Theory]
        [InlineData(SyncType.Post)]
        [InlineData(SyncType.Broadcast)]
        public void ServerRejectsClientWritesToServerOwnedProperties(SyncType syncType)
        {
            GivenServer();
            var property = new NetSync<int>(new TestSessionComponent(), TransferType.ServerToClient, 1, syncOnLoad: false);
            int events = 0;
            property.ValueChangedByNetwork += (o, n, s) => events++;
            Game.ClearTraffic();
            Receive(EncodePropertyPacket(property.Id, 0, syncType, 999, from: HostId), sender: ClientId);
            Game.NextFrame();
            Assert.Equal(1, property.Value);
            Assert.Equal(0, events);
            Assert.Empty(Game.Sent);
        }

        [Fact]
        public void ClientRejectsNonServerPacketsEvenWhenEnvelopeClaimsServer()
        {
            var client = GivenClient();
            var property = new NetSync<int>(new TestSessionComponent(), TransferType.ServerToClient, 1, syncOnLoad: false);
            int commands = 0;
            client.RegisterNetworkCommand("admin", (s, c, d, t) => commands++);
            Receive(EncodePropertyPacket(property.Id, 0, SyncType.Post, 42, from: HostId), sender: SomeoneElse, fromServer: false);
            Receive(EncodeCommandPacket("admin", from: HostId), sender: SomeoneElse, fromServer: false);
            Assert.Equal(1, property.Value);
            Assert.Equal(0, commands);
        }

        [Fact]
        public void ClientAcceptsServerRelayAndPreservesOriginalSender()
        {
            GivenClient();
            var property = new NetSync<int>(new TestSessionComponent(), TransferType.ServerToClient, 1, syncOnLoad: false);
            ulong reported = 0;
            property.ValueChangedByNetwork += (o, n, s) => reported = s;
            Receive(EncodePropertyPacket(property.Id, 0, SyncType.Post, 42, from: SomeoneElse), sender: HostId, fromServer: true);
            Assert.Equal(42, property.Value);
            Assert.Equal(SomeoneElse, reported);
        }
    }
}
