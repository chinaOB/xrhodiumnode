﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.BlockPulling;
using BRhodium.Node.Configuration;
using BRhodium.Node.Configuration.Logging;
using BRhodium.Node.Configuration.Settings;
using BRhodium.Node.P2P;
using BRhodium.Node.P2P.Peer;
using BRhodium.Node.P2P.Protocol.Payloads;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.Extensions;

using Open.Nat;
using System.Threading;

namespace BRhodium.Node.Connection
{
    public interface IConnectionManager : IDisposable
    {
        /// <summary>
        /// Adds a peer to the address manager's collection as well as
        /// the connection manager's add node collection.
        /// </summary>
        void AddNodeAddress(IPEndPoint ipEndpoint);

        /// <summary>
        /// Adds a peer to the address manager's connected nodes collection.
        /// <para>
        /// This list is inspected by the peer connectors to determine if the peer
        /// isn't already connected.
        /// </para>
        /// </summary>
        void AddConnectedPeer(INetworkPeer peer);

        void AddDiscoveredNodesRequirement(NetworkPeerServices services);

        Task<INetworkPeer> ConnectAsync(IPEndPoint ipEndpoint);

        IReadOnlyNetworkPeerCollection ConnectedPeers { get; }

        INetworkPeer FindLocalNode();

        INetworkPeer FindNodeByEndpoint(IPEndPoint ipEndpoint);

        INetworkPeer FindNodeByIp(IPAddress ipAddress);

        string GetNodeStats();

        string GetStats();

        /// <summary>Initializes and starts each peer connection as well as peer discovery.</summary>
        void Initialize();

        /// <summary>The network the node is running on.</summary>
        Network Network { get; }

        /// <summary>Factory for creating P2P network peers.</summary>
        INetworkPeerFactory NetworkPeerFactory { get; }

        /// <summary>User defined node settings.</summary>
        NodeSettings NodeSettings { get; }

        /// <summary>The network peer parameters for the <see cref="IConnectionManager"/>.</summary>
        NetworkPeerConnectionParameters Parameters { get; }

        /// <summary>Includes the add node, connect and discovery peer connectors.</summary>
        IEnumerable<IPeerConnector> PeerConnectors { get; }

        /// <summary>Connection settings.</summary>
        ConnectionManagerSettings ConnectionSettings { get; }

        void RemoveNodeAddress(IPEndPoint ipEndpoint);

        List<NetworkPeerServer> Servers { get; }

        bool IsActive { get; }
    }

    public sealed class ConnectionManager : IConnectionManager
    {
        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The maximum number of entries in an 'inv' protocol message.</summary>
        public const int MaxInventorySize = 50000;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <inheritdoc/>
        public Network Network { get; private set; }

        /// <inheritdoc/>
        public INetworkPeerFactory NetworkPeerFactory { get; private set; }

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <inheritdoc/>
        public NodeSettings NodeSettings { get; private set; }

        /// <inheritdoc/>
        public ConnectionManagerSettings ConnectionSettings { get; private set; }

        /// <inheritdoc/>
        public NetworkPeerConnectionParameters Parameters { get; }

        /// <inheritdoc/>
        public IEnumerable<IPeerConnector> PeerConnectors { get; private set; }

        /// <summary>Manager class that handles peers and their respective states.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>Async loop that discovers new peers to connect to.</summary>
        private IPeerDiscovery peerDiscovery;

        private readonly NetworkPeerCollection connectedPeers;

        public IReadOnlyNetworkPeerCollection ConnectedPeers
        {
            get { return this.connectedPeers; }
        }

        private readonly Dictionary<INetworkPeer, PerformanceSnapshot> downloads;

        private NetworkPeerServices discoveredNodeRequiredService = NetworkPeerServices.Network;

        public List<NetworkPeerServer> Servers { get; }

        /// <summary>Maintains a list of connected peers and ensures their proper disposal.</summary>
        private readonly NetworkPeerDisposer networkPeerDisposer;

        public bool IsActive { get; private set; }


        private NatDiscoverer nat;
        private CancellationTokenSource cts;
        private NatDevice device;

        public ConnectionManager(

            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            NodeSettings nodeSettings,
            INodeLifetime nodeLifetime,
            NetworkPeerConnectionParameters parameters,
            IPeerAddressManager peerAddressManager,
            IEnumerable<IPeerConnector> peerConnectors,
            IPeerDiscovery peerDiscovery,
            ConnectionManagerSettings connectionSettings)
        {
            this.connectedPeers = new NetworkPeerCollection();
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Network = network;
            this.NetworkPeerFactory = networkPeerFactory;
            this.NodeSettings = nodeSettings;
            this.nodeLifetime = nodeLifetime;
            this.peerAddressManager = peerAddressManager;
            this.PeerConnectors = peerConnectors;
            this.peerDiscovery = peerDiscovery;
            this.ConnectionSettings = connectionSettings;
            this.networkPeerDisposer = new NetworkPeerDisposer(this.loggerFactory);
            this.Servers = new List<NetworkPeerServer>();

            this.Parameters = parameters;
            this.Parameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;
            this.Parameters.UserAgent = $"{this.NodeSettings.Agent}:{this.GetVersion()}";
            this.Parameters.Version = this.NodeSettings.ProtocolVersion;

            this.downloads = new Dictionary<INetworkPeer, PerformanceSnapshot>();



            this.ConnectionSettings.Load(this.NodeSettings);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            this.logger.LogTrace("()");

            this.peerDiscovery.DiscoverPeers(this);

            foreach (IPeerConnector peerConnector in this.PeerConnectors)
            {
                peerConnector.Initialize(this);
                peerConnector.StartConnectAsync();
            }

            this.StartNodeServer();

            this.IsActive = true;

            this.logger.LogTrace("(-)");
        }

        private void StartNodeServer()
        {

            var logs = new StringBuilder();
            if (this.ConnectionSettings.EnableUPnP)
            {
                this.logger.LogInformation("Looking for UPnP devices");
                if (this.nat == null) this.nat = new NatDiscoverer();
                if (this.cts == null) this.cts = new CancellationTokenSource(5000);
                // Add any UPnP entires that may be required
                var t = Task.Run(async () =>
                {
                    this.device = await this.nat.DiscoverDeviceAsync(PortMapper.Upnp, this.cts);
                    var ip = await this.device.GetExternalIPAsync();
                //logs.AppendLine($"External IP: {ip}");
                this.logger.LogInformation("External IP: {0}", ip);
                    foreach (NodeServerEndpoint listen in this.ConnectionSettings.Listen)
                    {
                        await this.device.CreatePortMapAsync(new Mapping(Protocol.Tcp, listen.Endpoint.Port, listen.Endpoint.Port, "xRhodium"));
                    }
                });

                try
                {
                    t.Wait();
                }
                catch (AggregateException e)
                {
                    if (e.InnerException is NatDeviceNotFoundException)
                    {
                        this.logger.LogWarning("No NAT devoices found");
                    }
                }
            }
            logs.AppendLine("Node listening on:");

            foreach (NodeServerEndpoint listen in this.ConnectionSettings.Listen)
            {
                NetworkPeerConnectionParameters cloneParameters = this.Parameters.Clone();
                NetworkPeerServer server = this.NetworkPeerFactory.CreateNetworkPeerServer(listen.Endpoint, this.ConnectionSettings.ExternalEndpoint);

                this.Servers.Add(server);
                cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(true, this, this.loggerFactory)
                {
                    Whitelisted = listen.Whitelisted
                });

                server.InboundNetworkPeerConnectionParameters = cloneParameters;
                try
                {
                    server.Listen();
                }
                catch (SocketException e)
                {
                    this.logger.LogCritical("Unable to listen on port {0} (you can change the port using '-port=[number]'). Error message: {1}", listen.Endpoint.Port, e.Message);
                    throw e;
                }

                logs.Append(listen.Endpoint.Address + ":" + listen.Endpoint.Port);
                if (listen.Whitelisted)
                    logs.Append(" (whitelisted)");

                logs.AppendLine();
            }

            this.logger.LogInformation(logs.ToString());
        }

        public void AddDiscoveredNodesRequirement(NetworkPeerServices services)
        {
            this.logger.LogTrace("({0}:{1})", nameof(services), services);

            this.discoveredNodeRequiredService |= services;

            IPeerConnector peerConnector = this.PeerConnectors.FirstOrDefault(pc => pc is PeerConnectorDiscovery);
            if ((peerConnector != null) && !peerConnector.Requirements.RequiredServices.HasFlag(services))
            {
                peerConnector.Requirements.RequiredServices |= NetworkPeerServices.NODE_WITNESS;
                foreach (INetworkPeer peer in peerConnector.ConnectorPeers)
                {
                    if (!peer.PeerVersion.Services.HasFlag(services))
                        peer.Disconnect("The peer does not support the required services requirement.");
                }
            }

            this.logger.LogTrace("(-)");
        }

        public string GetStats()
        {
            StringBuilder builder = new StringBuilder();
            lock (this.downloads)
            {
                PerformanceSnapshot diffTotal = new PerformanceSnapshot(0, 0);
                builder.AppendLine("=======Connections=======");
                foreach (INetworkPeer peer in this.ConnectedPeers)
                {
                    PerformanceSnapshot newSnapshot = peer.Counter.Snapshot();
                    PerformanceSnapshot lastSnapshot = null;
                    if (this.downloads.TryGetValue(peer, out lastSnapshot))
                    {
                        BlockPullerBehavior behavior = peer.Behaviors.OfType<BlockPullerBehavior>()
                            .FirstOrDefault(b => b.Puller.GetType() == typeof(LookaheadBlockPuller));

                        PerformanceSnapshot diff = newSnapshot - lastSnapshot;
                        diffTotal = new PerformanceSnapshot(diff.TotalReadBytes + diffTotal.TotalReadBytes, diff.TotalWrittenBytes + diffTotal.TotalWrittenBytes) { Start = diff.Start, Taken = diff.Taken };
                        builder.Append((peer.RemoteSocketAddress + ":" + peer.RemoteSocketPort).PadRight(LoggingConfiguration.ColumnLength * 2) + "R:" + this.ToKBSec(diff.ReadenBytesPerSecond) + "\tW:" + this.ToKBSec(diff.WrittenBytesPerSecond));
                        if (behavior != null)
                        {
                            int intQuality = (int)behavior.QualityScore;
                            builder.Append("\tQualityScore: " + intQuality + (intQuality < 10 ? "\t" : "") + "\tPendingBlocks: " + behavior.PendingDownloadsCount);
                        }

                        builder.AppendLine();
                    }

                    this.downloads.AddOrReplace(peer, newSnapshot);
                }

                builder.AppendLine("=================");
                builder.AppendLine("Total".PadRight(LoggingConfiguration.ColumnLength * 2) + "R:" + this.ToKBSec(diffTotal.ReadenBytesPerSecond) + "\tW:" + this.ToKBSec(diffTotal.WrittenBytesPerSecond));
                builder.AppendLine("==========================");

                //TODO: Hack, we should just clean nodes that are not connect anymore.
                if (this.downloads.Count > 1000)
                    this.downloads.Clear();
            }

            return builder.ToString();
        }

        public string GetNodeStats()
        {
            var builder = new StringBuilder();

            foreach (INetworkPeer peer in this.ConnectedPeers)
            {
                ConnectionManagerBehavior connectionManagerBehavior = peer.Behavior<ConnectionManagerBehavior>();
                ChainHeadersBehavior chainHeadersBehavior = peer.Behavior<ChainHeadersBehavior>();

                string agent = peer.PeerVersion != null ? peer.PeerVersion.UserAgent : "[Unknown]";
                builder.AppendLine(
                    "Peer:" + (peer.RemoteSocketEndpoint + ", ").PadRight(LoggingConfiguration.ColumnLength + 15) +
                    (" connected:" + (connectionManagerBehavior.Inbound ? "inbound" : "outbound") + ",").PadRight(LoggingConfiguration.ColumnLength + 7) +
                    (" height:" + (chainHeadersBehavior.PendingTip != null ? chainHeadersBehavior.PendingTip.Height.ToString() : peer.PeerVersion?.StartHeight.ToString() ?? "unknown") + ",").PadRight(LoggingConfiguration.ColumnLength + 2) +
                    " agent:" + agent);
            }

            return builder.ToString();
        }

        private string ToKBSec(ulong bytesPerSec)
        {
            double speed = ((double)bytesPerSec / 1024.0);
            return speed.ToString("0.00") + " KB/S";
        }

        private string GetVersion()
        {
            Match match = Regex.Match(this.GetType().AssemblyQualifiedName, "Version=([0-9]+\\.[0-9]+\\.[0-9]+)\\.");
            return match.Groups[1].Value;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.logger.LogInformation("Stopping peer discovery...");
            this.peerDiscovery?.Dispose();

            foreach (IPeerConnector peerConnector in this.PeerConnectors)
                peerConnector.Dispose();

            foreach (NetworkPeerServer server in this.Servers)
                server.Dispose();

            this.networkPeerDisposer.Dispose();

            this.IsActive = false;

            this.logger.LogTrace("(-)");
            if (this.ConnectionSettings.EnableUPnP)
            {
                // Delete any entries made
                if (this.device != null)
                {
                    this.logger.LogInformation("Removing UPnP ports");
                    var t = Task.Run(async () =>
                    {

                        var mappings = await this.device.GetAllMappingsAsync();
                        foreach (Mapping map in mappings)
                        {
                            await this.device.DeletePortMapAsync(map);
                        }
                    });

                    try
                    {
                        t.Wait();
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError($"Could not remove ports: {ex}");
                    }

                }
            }
        }

        /// <inheritdoc />
        public void AddConnectedPeer(INetworkPeer peer)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(peer), peer.RemoteSocketEndpoint);

            this.connectedPeers.Add(peer);

            this.logger.LogTrace("(-)");
        }

        internal void RemoveConnectedPeer(INetworkPeer peer, string reason)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(reason), reason);

            this.connectedPeers.Remove(peer);

            this.logger.LogTrace("(-)");
        }

        public INetworkPeer FindNodeByEndpoint(IPEndPoint ipEndpoint)
        {
            return this.connectedPeers.FindByEndpoint(ipEndpoint);
        }

        public INetworkPeer FindNodeByIp(IPAddress ipAddress)
        {
            return this.connectedPeers.FindByIp(ipAddress);
        }

        public INetworkPeer FindLocalNode()
        {
            return this.connectedPeers.FindLocal();
        }

        /// <summary>
        /// Adds a node to the -addnode collection.
        /// <para>
        /// Usually called via RPC.
        /// </para>
        /// </summary>
        /// <param name="ipEndpoint">The endpoint of the peer to add.</param>
        public void AddNodeAddress(IPEndPoint ipEndpoint)
        {
            Guard.NotNull(ipEndpoint, nameof(ipEndpoint));

            this.logger.LogTrace("({0}:'{1}')", nameof(ipEndpoint), ipEndpoint);

            this.peerAddressManager.AddPeer(ipEndpoint.MapToIpv6(), IPAddress.Loopback);

            if (!this.ConnectionSettings.AddNode.Any(p => p.Match(ipEndpoint)))
            {
                this.ConnectionSettings.AddNode.Add(ipEndpoint);
                this.PeerConnectors.FirstOrDefault(pc => pc is PeerConnectorAddNode).MaxOutboundConnections++;
            }
            else
                this.logger.LogTrace("The endpoint already exists in the add node collection.");

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Disconnect a peer.
        /// <para>
        /// Usually called via RPC.
        /// </para>
        /// </summary>
        /// <param name="ipEndpoint">The endpoint of the peer to disconnect.</param>
        public void RemoveNodeAddress(IPEndPoint ipEndpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(ipEndpoint), ipEndpoint);

            INetworkPeer peer = this.connectedPeers.FindByEndpoint(ipEndpoint);
            peer?.Disconnect("Requested by user");

            this.logger.LogTrace("(-)");
        }

        public async Task<INetworkPeer> ConnectAsync(IPEndPoint ipEndpoint)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(ipEndpoint), ipEndpoint);

            NetworkPeerConnectionParameters cloneParameters = this.Parameters.Clone();
            cloneParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, this, this.loggerFactory)
            {
                OneTry = true
            });

            INetworkPeer peer = await this.NetworkPeerFactory.CreateConnectedNetworkPeerAsync(ipEndpoint, cloneParameters, this.networkPeerDisposer).ConfigureAwait(false);

            try
            {
                this.peerAddressManager.PeerAttempted(ipEndpoint, this.dateTimeProvider.GetUtcNow());
                await peer.VersionHandshakeAsync(this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                peer.Disconnect("Connection failed");
                this.logger.LogTrace("(-)[ERROR]");
                throw e;
            }

            this.logger.LogTrace("(-)");
            return peer;
        }
    }
}
