﻿using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using BRhodium.Node.BlockPulling;
using BRhodium.Bitcoin.Features.Notifications.Controllers;
using BRhodium.Node.Tests.Common.Logging;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonErrors;
using Xunit;
using BRhodium.Node.Signals;

namespace BRhodium.Bitcoin.Features.Notifications.Tests
{
    public class NotificationsControllerTest : LogsTestBase
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [Trait("Module", "NotificationsController")]
        public void Given_SyncActionIsCalled_When_QueryParameterIsNullOrEmpty_Then_ReturnBadRequest(string from)
        {
            var chain = new Mock<ConcurrentChain>();
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, new Mock<ILookaheadBlockPuller>().Object, new Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);
            IActionResult result = notificationController.SyncFrom(from);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);
            Assert.Equal(400, errorResponse.Status);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_ABlockHeightIsSpecified_Then_TheChainIsSyncedFromTheHash()
        {
            // Set up
            int heightLocation = 480946;
            string hashLocation = "000000000000000000c03dbe6ee5fedb25877a12e32aa95bc1d3bd480d7a93f9";
            uint256 hash = uint256.Parse(hashLocation);

            ChainedHeader chainedHeader = new ChainedHeader(new BlockHeader(), hash, null);
            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(heightLocation)).Returns(chainedHeader);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, new Mock<ILookaheadBlockPuller>().Object, new Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);
            IActionResult result = notificationController.SyncFrom(heightLocation.ToString());

            // Assert
            chain.Verify(c => c.GetBlock(heightLocation), Times.Once);
            blockNotification.Verify(b => b.SyncFrom(hash), Times.Once);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_ABlockHashIsSpecified_Then_TheChainIsSyncedFromTheHash()
        {
            // Set up
            int heightLocation = 480946;
            string hashLocation = "000000000000000000c03dbe6ee5fedb25877a12e32aa95bc1d3bd480d7a93f9";
            uint256 hash = uint256.Parse(hashLocation);

            ChainedHeader chainedHeader = new ChainedHeader(new BlockHeader(), hash, null);
            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(uint256.Parse(hashLocation))).Returns(chainedHeader);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, new Mock<ILookaheadBlockPuller>().Object, new Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);
            IActionResult result = notificationController.SyncFrom(hashLocation);

            // Assert
            chain.Verify(c => c.GetBlock(heightLocation), Times.Never);
            blockNotification.Verify(b => b.SyncFrom(hash), Times.Once);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_ANonExistingBlockHashIsSpecified_Then_ABadRequestErrorIsReturned()
        {
            // Set up
            string hashLocation = "000000000000000000c03dbe6ee5fedb25877a12e32aa95bc1d3bd480d7a93f9";

            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(uint256.Parse(hashLocation))).Returns((ChainedHeader)null);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, new Mock<ILookaheadBlockPuller>().Object, new Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);

            // Assert
            IActionResult result = notificationController.SyncFrom(hashLocation);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);
            Assert.Equal(400, errorResponse.Status);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_AnInvalidBlockHashIsSpecified_Then_AnExceptionIsThrown()
        {
            // Set up
            string hashLocation = "notAValidHash";
            var chain = new Mock<ConcurrentChain>();
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, new Mock<ILookaheadBlockPuller>().Object, new Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);

            // Assert
            Assert.Throws<FormatException>(() => notificationController.SyncFrom(hashLocation));
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_HeightNotOnChain_Then_ABadRequestErrorIsReturned()
        {
            // Set up
            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(15)).Returns((ChainedHeader)null);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, new Mock<ILookaheadBlockPuller>().Object, new Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);

            // Assert
            IActionResult result = notificationController.SyncFrom("15");

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorModel errorResponse = Assert.IsType<ErrorModel>(errorResult.Value);
            Assert.Equal(400, errorResponse.Status);
        }
    }
}
