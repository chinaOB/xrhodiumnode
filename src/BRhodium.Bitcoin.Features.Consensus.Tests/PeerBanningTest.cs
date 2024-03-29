﻿using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using BRhodium.Node.Connection;
using BRhodium.Node.P2P;
using BRhodium.Node.P2P.Peer;
using BRhodium.Node.Tests.Common;
using Xunit;

namespace BRhodium.Bitcoin.Features.Consensus.Tests
{
    public class PeerBanningTest : TestBase
    {
        private static readonly Script MinerScriptPubKey;

        public PeerBanningTest() : base(Network.RegTest)
        {
        }

        static PeerBanningTest()
        {
            MinerScriptPubKey = new Key().ScriptPubKey;
        }

        private async Task<(TestChainContext context, IPEndPoint peerEndPoint)> InitialiseContextAndPeerEndpointAsync(string dirPrefix, string ip = "1.2.3.4")
        {
            string dataDir = GetTestDirectoryPath(this) + dirPrefix;

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peerEndPoint = new IPEndPoint(IPAddress.Parse(ip), context.Network.DefaultPort);
            context.PeerAddressManager.AddPeer(peerEndPoint, peerEndPoint.Address.MapToIPv6());

            return (context, peerEndPoint);
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndPeerDisconnected_ThePeerGetsBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerDisconnected_ThePeerGetsBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync, "PeerSendsABlockWithBadPrevHashAndPeerDisconnected", "1.2.4.2");
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndPeerDisconnected_ThePeerGetsBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerDisconnected_ThePeerGetsBanned_Async(
                MineAMutatedBlockAsync, "PeerSendsAMutatedBlockAndPeerDisconnected", "1.2.4.1");
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndPeerDisconnected_ThePeerGetsBanned_Async(
            Func<TestChainContext, Task<Block>> createBadBlock, string dirPrefix, string ip)
        {
            var (context, peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync(dirPrefix, ip);
            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>()))
                .Returns((INetworkPeer)null);

            var badBlock = await createBadBlock(context);
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = badBlock, Peer = peerEndPoint });

            Assert.True(context.PeerBanning.IsBanned(peerEndPoint));
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndPeerIsConnected_ThePeerGetsBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsConnected_ThePeerGetsBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync, "PeerSendsABlockWithBadPrevHashAndPeerIsConnected");
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndPeerIsConnected_ThePeerGetsBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsConnected_ThePeerGetsBanned_Async(
                MineAMutatedBlockAsync, "PeerSendsAMutatedBlockAndPeerIsConnected");
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndPeerIsConnected_ThePeerGetsBanned_Async(
            Func<TestChainContext, Task<Block>> createBadBlock, string dirPrefix)
        {
            var (context, peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync(dirPrefix);

            MockPeerConnection(context, false);

            var badBlock = await createBadBlock(context);
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = badBlock, Peer = peerEndPoint });

            Assert.True(context.PeerBanning.IsBanned(peerEndPoint));
        }

        private static void MockPeerConnection(TestChainContext context, bool whiteListedPeer)
        {
            var connectionManagerBehavior = new ConnectionManagerBehavior(false, context.ConnectionManager, context.LoggerFactory)
            { Whitelisted = whiteListedPeer };
            var peer = new Mock<INetworkPeer>();
            peer.Setup(p => p.Behavior<ConnectionManagerBehavior>()).Returns(connectionManagerBehavior);

            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns(peer.Object);
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndPeerIsWhitelisted_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync, "PeerSendsABlockWithBadPrevHashAndPeerIsWhitelisted");
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async(
                MineAMutatedBlockAsync, "PeerSendsAMutatedBlockAndPeerIsWhitelisted");
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async(
            Func<TestChainContext, Task<Block>> createBadBlock, string dirPrefix)
        {
            var (context, peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync(dirPrefix);

            MockPeerConnection(context, true);
            var badBlock = await createBadBlock(context);
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = badBlock, Peer = peerEndPoint });

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndErrorIsNotBanError_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync, "PeerSendsABadBlockAndErrorIsNotBanError");
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async(
                MineAMutatedBlockAsync, "PeerSendsAMutatedBlockAndErrorIsNotBanError");
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async(
            Func<TestChainContext, Task<Block>> createBadBlock, string dirPrefix)
        {
            var (context, peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync(dirPrefix);

            MockPeerConnection(context, false);
            var badBlock = await createBadBlock(context);

            var blockValidationContext = new BlockValidationContext
            {
                Block = badBlock,
                Peer = peerEndPoint,
                BanDurationSeconds = BlockValidationContext.BanDurationNoBan
            };

            await context.Consensus.AcceptBlockAsync(blockValidationContext);

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync, "PeerSendsABlockWithBadPrevHashAndPeerIsBannedAndBanIsExpired");
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async(
                MineAMutatedBlockAsync, "PeerSendsAMutatedBlockAndPeerIsBannedAndBanIsExpired");
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async(
            Func<TestChainContext, Task<Block>> createBadBlock, string dirPrefix)
        {
            var (context, peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync(dirPrefix);

            MockPeerConnection(context, false);
            var badBlock = await createBadBlock(context);

            var blockValidationContext = new BlockValidationContext
            {
                Block = badBlock,
                Peer = peerEndPoint,
                BanDurationSeconds = 1,
            };

            await context.Consensus.AcceptBlockAsync(blockValidationContext);

            // wait 1 sec for ban to expire.
            Thread.Sleep(1000);

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }

        private static async Task<Block> Mine2BlocksAndCreateABlockWithBadPrevHashAsync(TestChainContext context)
        {
            var blocks = await TestChainFactory.MineBlocksAsync(context, 2, MinerScriptPubKey);

            var block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            return block;
        }

        private static async Task<Block> MineAMutatedBlockAsync(TestChainContext context)
        {
            var blocks = await TestChainFactory.MineBlocksWithLastBlockMutatedAsync(context, 1, MinerScriptPubKey);
            var block = blocks.Last();
            return block;
        }

        [Fact]
        public async Task PeerBanning_AddingBannedPeerToAddressManagerStoreAsync()
        {
            // Arrange
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            context.PeerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            // Act
            context.PeerBanning.BanPeer(endpoint, context.ConnectionManager.ConnectionSettings.BanTimeSeconds, nameof(PeerBanningTest));

            // Assert
            PeerAddress peer = context.PeerAddressManager.FindPeer(endpoint);
            Assert.True(peer.BanUntil.HasValue);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);
        }

        [Fact]
        public async Task PeerBanning_SavingAndLoadingBannedPeerToAddressManagerStoreAsync()
        {
            // Arrange
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            context.PeerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            // Act - Ban Peer, save store, clear current Peers, load store
            context.PeerBanning.BanPeer(endpoint, context.ConnectionManager.ConnectionSettings.BanTimeSeconds, nameof(PeerBanningTest));
            context.PeerAddressManager.SavePeers();
            context.PeerAddressManager.Peers.Clear();
            context.PeerAddressManager.LoadPeers();

            // Assert
            PeerAddress peer = context.PeerAddressManager.FindPeer(endpoint);
            Assert.NotNull(peer.BanTimeStamp);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);
        }

        [Fact]
        public async Task PeerBanning_ResettingExpiredBannedPeerAsync()
        {
            // Arrange
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            context.PeerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            // Act
            context.PeerBanning.BanPeer(endpoint, 1, nameof(PeerBanningTest));
            context.PeerAddressManager.SavePeers();

            // Wait one second for ban to expire.
            Thread.Sleep(1000);

            context.PeerAddressManager.Peers.Clear();
            context.PeerAddressManager.LoadPeers();

            // Assert
            PeerAddress peer = context.PeerAddressManager.FindPeer(endpoint);
            Assert.Null(peer.BanTimeStamp);
            Assert.Null(peer.BanUntil);
            Assert.Empty(peer.BanReason);
        }
    }
}