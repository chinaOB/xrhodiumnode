﻿using System.Net;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using BRhodium.Bitcoin.Features.Notifications.Interfaces;
using BRhodium.Node.Utilities.JsonErrors;

namespace BRhodium.Bitcoin.Features.Notifications.Controllers
{
    /// <summary>
    /// Controller providing operations on blocks and transactions notifications.
    /// </summary>
    [Route("api/[controller]")]
    public class NotificationsController : Controller
    {
        private readonly IBlockNotification blockNotification;

        private readonly ConcurrentChain chain;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationsController"/> class.
        /// </summary>
        /// <param name="blockNotification">The block notification.</param>
        /// <param name="chain">The chain.</param>
        public NotificationsController(IBlockNotification blockNotification, ConcurrentChain chain)
        {
            this.blockNotification = blockNotification;
            this.chain = chain;
        }

        /// <summary>
        /// Starts synchronising the chain from the provided block height or block hash.
        /// </summary>
        /// <param name="from">The height or the hash of the block from which to start syncing.</param>
        /// <returns>Http OK if the request succeeded.</returns>
        /// <example>/api/notifications/sync?from=1155695</example>
        /// <example>/api/notifications/sync?from=000000002f7105530e69b80f9295d3bc3046cd7d8643a2fb3aaaa23f90c82c77</example>
        [HttpGet]
        [Route("sync")]
        public IActionResult SyncFrom([FromQuery] string from)
        {
            if (string.IsNullOrEmpty(from))
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "'from' parameter is required.", "Please provide the height or the hash of a block from which you'd like to sync the chain.");
            }

            // Check if an integer was provided as a parameter, meaning the request specifies a block height.
            // If not, the request specified is a block hash.
            bool isHeight = int.TryParse(from, out int height);
            if (isHeight)
            {
                ChainedHeader block = this.chain.GetBlock(height);
                if (block == null)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Block at height {height} was not found on the blockchain.", string.Empty);
                }

                this.blockNotification.SyncFrom(block.HashBlock);
            }
            else
            {
                uint256 hashToSyncFrom = uint256.Parse(from);
                ChainedHeader block = this.chain.GetBlock(hashToSyncFrom);
                if (block == null)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Block with hash {from} was not found on the blockchain.", string.Empty);
                }

                this.blockNotification.SyncFrom(hashToSyncFrom);
            }

            return this.Ok();
        }
    }
}
