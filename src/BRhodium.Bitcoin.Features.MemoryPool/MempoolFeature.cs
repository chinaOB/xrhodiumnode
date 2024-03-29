﻿using System;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Builder;
using BRhodium.Node.Builder.Feature;
using BRhodium.Node.Configuration;
using BRhodium.Node.Configuration.Logging;
using BRhodium.Node.Connection;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.MemoryPool.Fee;
using BRhodium.Bitcoin.Features.MemoryPool.Interfaces;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Signals;
using BRhodium.Bitcoin.Features.MemoryPool.Controller;

[assembly: InternalsVisibleTo("BRhodium.Bitcoin.Features.MemoryPool.Tests")]

namespace BRhodium.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Transaction memory pool feature for the Full Node.
    /// </summary>
    /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/6dbcc74a0e0a7d45d20b03bb4eb41a027397a21d/src/txmempool.cpp"/>
    public class MempoolFeature : FullNodeFeature, IFeatureStats
    {
        /// <summary>Node notifications available to subscribe to.</summary>
        private readonly Signals signals;

        /// <summary>Connection manager for managing node connections.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Observes block signal notifications from signals.</summary>
        private readonly MempoolSignaled mempoolSignaled;

        /// <summary>Memory pool node behavior for managing attached node messages.</summary>
        private readonly MempoolBehavior mempoolBehavior;

        /// <summary>Memory pool manager for managing external access to memory pool.</summary>
        private readonly MempoolManager mempoolManager;

        /// <summary>Instance logger for the memory pool component.</summary>
        private readonly ILogger mempoolLogger;

        /// <summary>Settings for the memory pool component.</summary>
        private readonly MempoolSettings mempoolSettings;

        /// <summary>Settings for the node.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>
        /// Constructs a memory pool feature.
        /// </summary>
        /// <param name="connectionManager">Connection manager for managing node connections.</param>
        /// <param name="signals">Node notifications available to subscribe to.</param>
        /// <param name="mempoolSignaled">Observes block signal notifications from signals.</param>
        /// <param name="mempoolBehavior">Memory pool node behavior for managing attached node messages.</param>
        /// <param name="mempoolManager">Memory pool manager for managing external access to memory pool.</param>
        /// <param name="nodeSettings">User defined node settings.</param>
        /// <param name="loggerFactory">Logger factory for creating instance logger.</param>
        /// <param name="mempoolSettings">Mempool settings.</param>
        public MempoolFeature(
            IConnectionManager connectionManager,
            Signals signals,
            MempoolSignaled mempoolSignaled,
            MempoolBehavior mempoolBehavior,
            MempoolManager mempoolManager,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            MempoolSettings mempoolSettings)
        {
            this.signals = signals;
            this.connectionManager = connectionManager;
            this.mempoolSignaled = mempoolSignaled;
            this.mempoolBehavior = mempoolBehavior;
            this.mempoolManager = mempoolManager;
            this.mempoolLogger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.mempoolSettings = mempoolSettings;
            this.nodeSettings = nodeSettings;
        }

        /// <inheritdoc />
        public override void LoadConfiguration()
        {
            this.mempoolSettings.Load(this.nodeSettings);
        }

        public void AddFeatureStats(StringBuilder benchLogs)
        {
            if (this.mempoolManager != null)
            {
                benchLogs.AppendLine();
                benchLogs.AppendLine("======Mempool======");
                benchLogs.AppendLine(this.mempoolManager.PerformanceCounter.ToString());
            }
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            this.mempoolManager.LoadPoolAsync().GetAwaiter().GetResult();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.mempoolBehavior);
            this.signals.SubscribeForBlocks(this.mempoolSignaled);
            this.mempoolSignaled.Start();


            this.mempoolManager.LoadFeeStats();
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            MempoolSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            MempoolSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (this.mempoolManager != null)
            {
                this.mempoolManager.SaveFeeStats();
                this.mempoolLogger.LogInformation("Saving Memory Pool...");

                MemPoolSaveResult result = this.mempoolManager.SavePool();
                if (result.Succeeded)
                {
                    this.mempoolLogger.LogInformation($"...Memory Pool Saved {result.TrxSaved} transactions");
                }
                else
                {
                    this.mempoolLogger.LogWarning("...Memory Pool Not Saved!");
                }
            }

            if (this.mempoolSignaled != null)
                this.mempoolSignaled.Stop();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderMempoolExtension
    {
        /// <summary>
        /// Include the memory pool feature and related services in the full node.
        /// </summary>
        /// <param name="fullNodeBuilder">Full node builder.</param>
        /// <returns>Full node builder.</returns>
        public static IFullNodeBuilder UseMempool(this IFullNodeBuilder fullNodeBuilder, Action<MempoolSettings> setup = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MempoolFeature>("mempool");
            LoggingConfiguration.RegisterFeatureNamespace<BlockPolicyEstimator>("estimatefee");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<MempoolFeature>()
                .DependOn<ConsensusFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton<MempoolSchedulerLock>();
                        services.AddSingleton<ITxMempool, TxMempool>();
                        services.AddSingleton<BlockPolicyEstimator>();
                        services.AddSingleton<IMempoolValidator, MempoolValidator>();
                        services.AddSingleton<MempoolOrphans>();
                        services.AddSingleton<MempoolManager>();
                        services.AddSingleton<IPooledTransaction, MempoolManager>();
                        services.AddSingleton<IPooledGetUnspentTransaction, MempoolManager>();
                        services.AddSingleton<MempoolBehavior>();
                        services.AddSingleton<MempoolSignaled>();
                        services.AddSingleton<IMempoolPersistence, MempoolPersistence>();
                        services.AddSingleton<MempoolController>();
                        services.AddSingleton<MempoolSettings>(new MempoolSettings(setup));
                        services.AddSingleton<MemPoolRPCController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
