﻿using System.Collections.Generic;
using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Configuration;

namespace BRhodium.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// Configuration related to storage of transactions.
    /// </summary>
    public class StoreSettings
    {
        /// <summary><c>true</c> to maintain a full transaction index.</summary>
        public bool TxIndex { get; set; }

        /// <summary><c>true</c> to rebuild chain state and block index from block data files on disk.</summary>
        public bool ReIndex { get; set; }

        /// <summary><c>true</c> to enable pruning to reduce storage requirements by enabling deleting of old blocks.</summary>
        public bool Prune { get; set; }

        /// <summary>Command to run when best-block changes</summary>
        public List<string> BlockNotify
        {
            get
            {
                return _blockNotify;
            }
        }

        /// <summary>Command to notify Zero MQ subscribers when best-block changes</summary>
        public List<ZeroMqTopicAddress> BlockNotifyZeroMq
        {
            get
            {
                return _blockNotifyZeroMq;
            }
        }

        private List<string> _blockNotify = new List<string>();
        private List<ZeroMqTopicAddress> _blockNotifyZeroMq = new List<ZeroMqTopicAddress>();

        private Action<StoreSettings> callback = null;

        public StoreSettings()
        {
        }

        public StoreSettings(Action<StoreSettings> callback)
            : this()
        {
            this.callback = callback;
        }

        public StoreSettings(NodeSettings nodeSettings, Action<StoreSettings> callback = null)
            : this(callback)
        {
            this.Load(nodeSettings);
        }

        /// <summary>
        /// Loads the storage related settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public virtual void Load(NodeSettings nodeSettings)
        {
            var config = nodeSettings.ConfigReader;

            this.Prune = config.GetOrDefault<bool>("prune", false);
            this.TxIndex = config.GetOrDefault<bool>("txindex", true);
            this.ReIndex = config.GetOrDefault<bool>("reindex", false);
            var blockNotify = config.GetAll("blocknotify");
            var blockNotifyZeroMQ = config.GetAll("zmqBlocknotify");

            foreach (var blockNotifyCmd in blockNotify)
            {
                this._blockNotify.Add(blockNotifyCmd);
            }
            foreach (var blockNotifyCmd in blockNotifyZeroMQ)
            {
                this._blockNotifyZeroMq.Add(new ZeroMqTopicAddress(blockNotifyCmd));
            }

            this.callback?.Invoke(this);

            if (this.Prune && this.TxIndex)
                throw new ConfigurationException("Prune mode is incompatible with -txindex");
        }

        /// <summary>Prints the help information on how to configure the block store settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-txindex=<0 or 1>         Enable to maintain a full transaction index.");
            builder.AppendLine($"-reindex=<0 or 1>         Rebuild chain state and block index from block data files on disk.");
            builder.AppendLine($"-prune=<0 or 1>           Enable pruning to reduce storage requirements by enabling deleting of old blocks.");

            NodeSettings.Default().Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####BlockStore Settings####");
            builder.AppendLine($"#Enable to maintain a full transaction index.");
            builder.AppendLine($"#txindex=1");
            builder.AppendLine($"#Rebuild chain state and block index from block data files on disk.");
            builder.AppendLine($"#reindex=0");
            builder.AppendLine($"#Enable pruning to reduce storage requirements by enabling deleting of old blocks.");
            builder.AppendLine($"#prune=0");
            builder.AppendLine($"#When a block is available, trigger a program. (%s is the block hash");
            builder.AppendLine($"#blocknotify=prog %s");
            builder.AppendLine($"#zmqBlocknotify=tcp://127.0.0.1:15101|hashblock|s%");
        }
    }
}
