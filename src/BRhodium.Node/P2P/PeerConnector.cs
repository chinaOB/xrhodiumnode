﻿using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Configuration;
using BRhodium.Node.Configuration.Settings;
using BRhodium.Node.Connection;
using BRhodium.Node.P2P.Peer;
using BRhodium.Node.Utilities;

namespace BRhodium.Node.P2P
{
    /// <summary>Contract for <see cref="PeerConnector"/>.</summary>
    public interface IPeerConnector : IDisposable
    {
        /// <summary>The collection of peers the connector is currently connected to.</summary>
        NetworkPeerCollection ConnectorPeers { get; }

        /// <summary>Peer connector initialization as called by the <see cref="ConnectionManager"/>.</summary>
        void Initialize(IConnectionManager connectionManager);

        /// <summary>The maximum amount of peers the node can connect to (defaults to 8).</summary>
        int MaxOutboundConnections { get; set; }

        /// <summary>Specification of requirements the <see cref="PeerConnector"/> has when connecting to other peers.</summary>
        NetworkPeerRequirement Requirements { get; }

        /// <summary>
        /// Starts an asynchronous loop that connects to peers in one second intervals.
        /// <para>
        /// If the maximum amount of connections has been reached (<see cref="MaxOutboundConnections"/>), the action gets skipped.
        /// </para>
        /// </summary>
        void StartConnectAsync();
    }

    /// <summary>
    /// Connects to peers asynchronously.
    /// </summary>
    public abstract class PeerConnector : IPeerConnector
    {
        /// <summary>The async loop we need to wait upon before we can dispose of this connector.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Collection of connected peers that is managed by the <see cref="ConnectionManager"/>.
        /// </summary>
        private IReadOnlyNetworkPeerCollection connectedPeers;

        /// <inheritdoc/>
        public NetworkPeerCollection ConnectorPeers { get; private set; }

        /// <summary>The parameters cloned from the connection manager.</summary>
        public NetworkPeerConnectionParameters CurrentParameters { get; private set; }

        /// <summary>Logger factory to create loggers.</summary>
        private ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <inheritdoc/>
        public int MaxOutboundConnections { get; set; }

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        protected INodeLifetime nodeLifetime;

        /// <summary>User defined node settings.</summary>
        public NodeSettings NodeSettings;

        /// <summary>User defined connection settings.</summary>
        public ConnectionManagerSettings ConnectionSettings;

        /// <summary>The network the node is running on.</summary>
        private Network network;

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        protected IPeerAddressManager peerAddressManager;

        /// <summary>Tracker for endpoints known to be self.</summary>
        private readonly ISelfEndpointTracker selfEndpointTracker;

        /// <summary>Factory for creating P2P network peers.</summary>
        private INetworkPeerFactory networkPeerFactory;

        /// <inheritdoc/>
        public NetworkPeerRequirement Requirements { get; internal set; }

        /// <summary>Connections number after which burst connectivity mode (connection attempts with no delay in between) will be disabled.</summary>
        public int BurstModeTargetConnections { get; private set; }

        /// <summary>Default time interval between making a connection attempt.</summary>
        private readonly TimeSpan defaultConnectionInterval;

        /// <summary>Burst time interval between making a connection attempt.</summary>
        private readonly TimeSpan burstConnectionInterval;

        /// <summary>Maintains a list of connected peers and ensures their proper disposal.</summary>
        private readonly NetworkPeerDisposer networkPeerDisposer;

        /// <summary>Constructor for dependency injection.</summary>
        protected PeerConnector(
            IAsyncLoopFactory asyncLoopFactory,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            ConnectionManagerSettings connectionSettings,
            IPeerAddressManager peerAddressManager,
            ISelfEndpointTracker selfEndpointTracker)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.ConnectorPeers = new NetworkPeerCollection();
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.networkPeerFactory = networkPeerFactory;
            this.nodeLifetime = nodeLifetime;
            this.NodeSettings = nodeSettings;
            this.ConnectionSettings = connectionSettings;
            this.peerAddressManager = peerAddressManager;
            this.networkPeerDisposer = new NetworkPeerDisposer(this.loggerFactory, this.OnPeerDisposed);
            this.selfEndpointTracker = selfEndpointTracker;
            this.Requirements = new NetworkPeerRequirement { MinVersion = this.NodeSettings.MinProtocolVersion };

            this.defaultConnectionInterval = TimeSpans.Second;
            this.burstConnectionInterval = TimeSpan.Zero;
            this.BurstModeTargetConnections = this.NodeSettings.ConfigReader.GetOrDefault("burstModeTargetConnections", 1);
        }

        /// <inheritdoc/>
        public void Initialize(IConnectionManager connectionManager)
        {
            this.connectedPeers = connectionManager.ConnectedPeers;

            this.CurrentParameters = connectionManager.Parameters.Clone();
            this.CurrentParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, connectionManager, this.loggerFactory));

            this.OnInitialize();
        }

        /// <summary>
        /// Adds a peer to the <see cref="ConnectorPeers"/>.
        /// <para>
        /// This will only happen if the peer successfully handshaked with another.
        /// </para>
        /// </summary>
        /// <param name="peer">Peer to be added.</param>
        private void AddPeer(INetworkPeer peer)
        {
            Guard.NotNull(peer, nameof(peer));

            this.ConnectorPeers.Add(peer);

            if (this.asyncLoop != null && this.ConnectorPeers.Count >= this.BurstModeTargetConnections)
                this.asyncLoop.RepeatEvery = this.defaultConnectionInterval;
        }

        /// <summary>
        /// Removes a given peer from the <see cref="ConnectorPeers"/>.
        /// <para>
        /// This will happen if the peer state changed to "disconnecting", "failed" or "offline".
        /// </para>
        /// </summary>
        /// <param name="peer">Peer to be removed.</param>
        private void RemovePeer(INetworkPeer peer)
        {
            this.ConnectorPeers.Remove(peer);

            if (this.asyncLoop != null && this.ConnectorPeers.Count < this.BurstModeTargetConnections)
                this.asyncLoop.RepeatEvery = this.burstConnectionInterval;
        }

        /// <summary>Determines whether or not a connector can be started.</summary>
        public abstract bool CanStartConnect { get; }

        /// <summary>Initialization logic specific to each concrete implementation of this class.</summary>
        public abstract void OnInitialize();

        /// <summary>Start up logic specific to each concrete implementation of this class.</summary>
        public abstract void OnStartConnect();

        /// <summary>Connect logic specific to each concrete implementation of this class.</summary>
        public abstract Task OnConnectAsync();

        /// <summary>
        /// <c>true</c> if the peer is already connected.
        /// </summary>
        /// <param name="ipEndpoint">The endpoint to check.</param>
        internal bool IsPeerConnected(IPEndPoint ipEndpoint)
        {
            return this.connectedPeers.FindByEndpoint(ipEndpoint) != null;
        }

        /// <inheritdoc/>
        public void StartConnectAsync()
        {
            if (!this.CanStartConnect)
                return;

            this.OnStartConnect();

            this.asyncLoop = this.asyncLoopFactory.Run($"{this.GetType().Name}.{nameof(this.ConnectAsync)}", async token =>
            {
                if (!this.peerAddressManager.Peers.Any() || (this.ConnectorPeers.Count >= this.MaxOutboundConnections))
                {
                    await Task.Delay(2000, this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
                    return;
                }

                await this.OnConnectAsync().ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: this.burstConnectionInterval);
        }

        /// <summary>Attempts to connect to a random peer.</summary>
        public async Task ConnectAsync(PeerAddress peerAddress)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(peerAddress), peerAddress.Endpoint);

            if (this.selfEndpointTracker.IsSelf(peerAddress.Endpoint))
            {
                this.logger.LogTrace("{0} is self. Therefore not connecting.", peerAddress.Endpoint);
                this.logger.LogTrace("(-)");
                return;
            }

            INetworkPeer peer = null;

            try
            {
                using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping))
                {
                    this.peerAddressManager.PeerAttempted(peerAddress.Endpoint, this.dateTimeProvider.GetUtcNow());

                    var clonedConnectParamaters = this.CurrentParameters.Clone();
                    timeoutTokenSource.CancelAfter(5000);
                    clonedConnectParamaters.ConnectCancellation = timeoutTokenSource.Token;

                    peer = await this.networkPeerFactory.CreateConnectedNetworkPeerAsync(peerAddress.Endpoint, clonedConnectParamaters, this.networkPeerDisposer).ConfigureAwait(false);

                    await peer.VersionHandshakeAsync(this.Requirements, timeoutTokenSource.Token).ConfigureAwait(false);
                    this.AddPeer(peer);
                }
            }
            catch (OperationCanceledException)
            {
                if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    this.logger.LogDebug("Peer {0} connection canceled because application is stopping.", peerAddress.Endpoint);
                    peer?.Disconnect("Application stopping");
                }
                else
                {
                    this.logger.LogDebug("Peer {0} connection timeout.", peerAddress.Endpoint);
                    peer?.Disconnect("Connection timeout");
                }
            }
            catch (Exception exception)
            {
                this.logger.LogTrace("Exception occurred while connecting: {0}", exception.ToString());
                peer?.Disconnect("Error while connecting", exception);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Callback that is called before the peer is disposed.
        /// </summary>
        /// <param name="peer">Peer that is being disposed.</param>
        private void OnPeerDisposed(INetworkPeer peer)
        {
            this.RemovePeer(peer);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
            this.networkPeerDisposer.Dispose();
        }
    }
}