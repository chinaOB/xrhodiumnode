﻿using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using BRhodium.Node.Builder;
using BRhodium.Node.Builder.Feature;
using BRhodium.Node.Configuration;
using BRhodium.Node.Configuration.Logging;
using BRhodium.Node.Connection;
using BRhodium.Bitcoin.Features.BlockStore;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Bitcoin.Features.RPC;
using BRhodium.Bitcoin.Features.Wallet.Broadcasting;
using BRhodium.Bitcoin.Features.Wallet.Controllers;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Bitcoin.Features.Wallet.Notifications;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Signals;
using System.Globalization;
using BRhodium.Node.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Wallet feature for the full node.
    /// </summary>
    /// <seealso cref="BRhodium.Node.Builder.Feature.FullNodeFeature" />
    /// <seealso cref="BRhodium.Node.Interfaces.INodeStats" />
    public class WalletFeature : FullNodeFeature, INodeStats, IFeatureStats, IOptimalization
    {
        private readonly IWalletSyncManager walletSyncManager;

        private readonly IWalletManager walletManager;

        private readonly Signals signals;

        private IDisposable blockSubscriberDisposable;

        private IDisposable transactionSubscriberDisposable;

        private ConcurrentChain chain;

        private readonly IConnectionManager connectionManager;

        private readonly BroadcasterBehavior broadcasterBehavior;

        private readonly NodeSettings nodeSettings;

        private readonly WalletSettings walletSettings;

        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="WalletFeature"/> class.
        /// </summary>
        /// <param name="walletSyncManager">The synchronization manager for the wallet, tasked with keeping the wallet synced with the network.</param>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="signals">The signals responsible for receiving blocks and transactions from the network.</param>
        /// <param name="chain">The chain of blocks.</param>
        /// <param name="connectionManager">The connection manager.</param>
        /// <param name="broadcasterBehavior">The broadcaster behavior.</param>
        /// <param name="nodeSettings">The settings for the node.</param>
        /// <param name="walletSettings">The settings for the wallet.</param>
        public WalletFeature(
            IWalletSyncManager walletSyncManager,
            IWalletManager walletManager,
            Signals signals,
            ConcurrentChain chain,
            IConnectionManager connectionManager,
            IDateTimeProvider dateTimeProvider,
            BroadcasterBehavior broadcasterBehavior,
            NodeSettings nodeSettings,
            WalletSettings walletSettings)
        {
            this.walletSyncManager = walletSyncManager;
            this.walletManager = walletManager;
            this.signals = signals;
            this.chain = chain;
            this.connectionManager = connectionManager;
            this.broadcasterBehavior = broadcasterBehavior;
            this.nodeSettings = nodeSettings;
            this.walletSettings = walletSettings;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <inheritdoc />
        public override void LoadConfiguration()
        {
            this.walletSettings.Load(this.nodeSettings);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            WalletSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            WalletSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public void OptimizeIt(StringBuilder optimalizationLog)
        {
            WalletManager walletManager = this.walletManager as WalletManager;

            if (walletManager != null)
            {
                optimalizationLog.AppendLine("DBreeze VACUUM:");
                optimalizationLog.AppendLine("Start : " + this.dateTimeProvider.GetUtcNow().ToString(CultureInfo.InvariantCulture));

                walletManager.DBreezeStorage.OptimizeStorage();

                optimalizationLog.AppendLine("End : " + this.dateTimeProvider.GetUtcNow().ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <inheritdoc />
        public void AddNodeStats(StringBuilder benchLogs)
        {
            WalletManager walletManager = this.walletManager as WalletManager;

            if (walletManager != null)
            {
                int height = walletManager.LastBlockHeight();
                ChainedHeader block = this.chain.GetBlock(height);
                uint256 hashBlock = block == null ? 0 : block.HashBlock;

                benchLogs.AppendLine("Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                        (walletManager.ContainsWallets ? height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
                                        (walletManager.ContainsWallets ? (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashBlock) : string.Empty));
            }
        }

        /// <inheritdoc />
        public void AddFeatureStats(StringBuilder benchLog)
        {
            var walletNames = this.walletManager.GetWalletsNames();

            if (walletNames.Any())
            {
                benchLog.AppendLine();
                benchLog.AppendLine("======Wallets======");

                foreach (var walletName in walletNames)
                {
                    var items = this.walletManager.GetSpendableTransactionsInWallet(walletName, 1);
                    benchLog.AppendLine("Wallet: " + (walletName + ",").PadRight(LoggingConfiguration.ColumnLength) + " Confirmed balance: " + new Money(items.Sum(s => s.Transaction.Amount)).ToString());
                }
            }
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            // subscribe to receiving blocks and transactions
            this.blockSubscriberDisposable = this.signals.SubscribeForBlocks(new BlockObserver(this.walletSyncManager));
            this.transactionSubscriberDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.walletSyncManager));

            this.walletManager.Start();
            this.walletSyncManager.Start();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.blockSubscriberDisposable.Dispose();
            this.transactionSubscriberDisposable.Dispose();

            this.walletManager.Stop();
            this.walletSyncManager.Stop();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderWalletExtension
    {
        public static IFullNodeBuilder UseWallet(this IFullNodeBuilder fullNodeBuilder, Action<WalletSettings> setup = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<WalletFeature>("wallet");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<WalletFeature>()
                .DependOn<MempoolFeature>()
                .DependOn<BlockStoreFeature>()
                .DependOn<RPCFeature>()
                .FeatureServices(services =>
                    {
                        var walletSettings = new WalletSettings(setup);

                        services.AddSingleton<IWalletSyncManager, WalletSyncManager>();
                        services.AddSingleton<IWalletTransactionHandler, WalletTransactionHandler>();
                        services.AddSingleton<IWalletManager, WalletManager>();
                        services.AddSingleton<IWalletFeePolicy, WalletFeePolicy>();
                        services.AddSingleton<WalletController>();
                        services.AddSingleton<WalletRPCController>();
                        services.AddSingleton<TransactionRPCController>();
                        services.AddSingleton<UtilRPCController>();
                        services.AddSingleton<IBroadcasterManager, FullNodeBroadcasterManager>();
                        services.AddSingleton<BroadcasterBehavior>();
                        services.AddSingleton<WalletSettings>(walletSettings);
                        services.AddSingleton<IWalletKeyPool, WalletKeyPool>();
                        services.AddSingleton<IDataProtectionProvider, EphemeralDataProtectionProvider>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
