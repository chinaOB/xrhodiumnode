﻿using System;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Builder;
using BRhodium.Node.Builder.Feature;
using BRhodium.Node.Configuration;
using BRhodium.Node.Configuration.Logging;
using BRhodium.Node.Connection;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.RPC.Controllers;
using BRhodium.Node;

namespace BRhodium.Bitcoin.Features.RPC
{
    public class RPCFeature : FullNodeFeature
    {
        private readonly FullNode fullNode;

        private readonly NodeSettings nodeSettings;

        private readonly ILogger logger;

        private readonly IFullNodeBuilder fullNodeBuilder;

        private readonly RpcSettings rpcSettings;

        public RPCFeature(IFullNodeBuilder fullNodeBuilder, FullNode fullNode, NodeSettings nodeSettings, ILoggerFactory loggerFactory, RpcSettings rpcSettings)
        {
            this.fullNodeBuilder = fullNodeBuilder;
            this.fullNode = fullNode;
            this.nodeSettings = nodeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.rpcSettings = rpcSettings;
        }

        /// <inheritdoc />
        public override void LoadConfiguration()
        {
            this.rpcSettings.Load(this.nodeSettings);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            RpcSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####RPC Settings####");
            builder.AppendLine("#Activate RPC Server (default: 0)");
            builder.AppendLine("#server=0");
            builder.AppendLine("#Where the RPC Server binds (default: 127.0.0.1 and ::1)");
            builder.AppendLine("#rpcbind=127.0.0.1");
            builder.AppendLine("#Ip address allowed to connect to RPC (default all: 0.0.0.0 and ::)");
            builder.AppendLine("#rpcallowip=127.0.0.1");
        }

        public override void Initialize()
        {
            if (this.rpcSettings.Server)
            {
                // TODO: The web host wants to create IServiceProvider, so build (but not start)
                // earlier, if you want to use dependency injection elsewhere
                this.fullNode.RPCHost = new WebHostBuilder()
                .UseKestrel()
                .ForFullNode(this.fullNode)
                .UseUrls(this.rpcSettings.GetUrls())
                .UseIISIntegration()
                .ConfigureServices(collection =>
                {
                    if (this.fullNodeBuilder != null && this.fullNodeBuilder.Services != null && this.fullNode != null)
                    {
                        // copies all the services defined for the full node to the Api.
                        // also copies over singleton instances already defined
                        foreach (var service in this.fullNodeBuilder.Services)
                        {
                            object obj = this.fullNode.Services.ServiceProvider.GetService(service.ServiceType);

                            if (obj != null && service.Lifetime == ServiceLifetime.Singleton && service.ImplementationInstance == null)
                            {
                                collection.AddSingleton(service.ServiceType, obj);
                            }
                            else
                            {
                                collection.Add(service);
                            }
                        }
                    }
                })
                .UseStartup<Startup>()
                .Build();

                this.fullNode.RPCHost.Start();
                this.fullNode.Resources.Add(this.fullNode.RPCHost);
                this.logger.LogInformation("RPC Server listening on: " + Environment.NewLine + string.Join(Environment.NewLine, this.rpcSettings.GetUrls()));
            }
            else
            {
                this.logger.LogWarning("RPC Server is off based on configuration.");
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderRPCExtension
    {
        public static IFullNodeBuilder AddRPC(this IFullNodeBuilder fullNodeBuilder, Action<RpcSettings> setup = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<RPCFeature>("rpc");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<RPCFeature>()
                .DependOn<ConsensusFeature>()
                .FeatureServices(services => services.AddSingleton(fullNodeBuilder));
            });

            fullNodeBuilder.ConfigureServices(service =>
            {
                service.AddSingleton<FullNodeRPCController>();
                service.AddSingleton<RpcSettings>(new RpcSettings(setup));
                service.AddSingleton<IRPCClientFactory, RPCClientFactory>();
                service.AddSingleton<RPCController>();
                service.AddSingleton<ExplorerRPCController>();
                service.AddSingleton<NetworkRPCController>();
                service.AddSingleton<BlockChainRPCController>();
            });

            return fullNodeBuilder;
        }
    }
}
