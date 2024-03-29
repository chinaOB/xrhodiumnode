﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.Builder.Feature;
using BRhodium.Node.Configuration;
using BRhodium.Node.Connection;
using BRhodium.Node.Controllers.Models;
using BRhodium.Node.Interfaces;
using BRhodium.Node.P2P.Peer;
using BRhodium.Node.Utilities;
using System;
using System.Diagnostics;
using System.Reflection;

namespace BRhodium.Node.Controllers
{
    [Route("api/[controller]")]
    public class NodeController : Controller
    {
        /// <summary>Full Node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Information about node's chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Provider of date and time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The settings for the node.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>The connection manager.</summary>
        private readonly IConnectionManager connectionManager;

        public NodeController(IFullNode fullNode, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, IChainState chainState, NodeSettings nodeSettings, IConnectionManager connectionManager)
        {
            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
            this.chainState = chainState;
            this.nodeSettings = nodeSettings;
            this.connectionManager = connectionManager;
        }

        /// <summary>
        /// Returns some general information about the status of the underlying node.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("status")]
        public IActionResult Status()
        {
            StatusModel model = new StatusModel
            {
                Version = this.fullNode.Version?.ToString() ?? "0",
                Agent = this.nodeSettings.Agent,
                ProcessId = Process.GetCurrentProcess().Id,
                Network = this.fullNode.Network.Name,
                ConsensusHeight = this.chainState.ConsensusTip.Height,
                DataDirectoryPath = this.nodeSettings.DataDir,
                RunningTime = this.dateTimeProvider.GetUtcNow() - this.fullNode.StartTime
            };

            // Add the list of features that are enabled.
            foreach (IFullNodeFeature feature in this.fullNode.Services.Features)
            {
                model.EnabledFeatures.Add(feature.GetType().ToString());

                // Include BlockStore Height if enabled
                if (feature is IBlockStore)
                    model.BlockStoreHeight = ((IBlockStore)feature).GetHighestPersistedBlock().Height;
            }

            // Add the details of connected nodes.
            foreach (INetworkPeer peer in this.connectionManager.ConnectedPeers)
            {
                ConnectionManagerBehavior connectionManagerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                ChainHeadersBehavior chainHeadersBehavior = peer.Behavior<ChainHeadersBehavior>();

                ConnectedPeerModel connectedPeer = new ConnectedPeerModel
                {
                    Version = peer.PeerVersion != null ? peer.PeerVersion.UserAgent : "[Unknown]",
                    RemoteSocketEndpoint = peer.RemoteSocketEndpoint.ToString(),
                    TipHeight = chainHeadersBehavior.PendingTip != null ? chainHeadersBehavior.PendingTip.Height : peer.PeerVersion?.StartHeight ?? -1,
                    IsInbound = connectionManagerBehavior.Inbound
                };

                if (connectedPeer.IsInbound)
                {
                    model.InboundPeers.Add(connectedPeer);
                }
                else
                {
                    model.OutboundPeers.Add(connectedPeer);
                }
            }

            return this.Json(model);
        }

        /// <summary>
        /// Trigger a shoutdown of the current running node.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("shutdown")]
        public IActionResult Shutdown()
        {
            // start the node shutdown process
            this.fullNode.Dispose();

            return this.Ok();
        }
    }
}
