﻿using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.Configuration;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Provides IBD (Initial Block Download) state.
    /// </summary>
    /// <seealso cref="IInitialBlockDownloadState" />
    public class InitialBlockDownloadState : IInitialBlockDownloadState
    {
        /// <summary>A provider of the date and time.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Provider of block header hash checkpoints.</summary>
        private readonly ICheckpoints checkpoints;

        /// <summary>Information about node's chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>User defined node settings.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>
        /// Creates a new instance of the <see cref="InitialBlockDownloadState" /> class.
        /// </summary>
        /// <param name="chainState">Information about node's chain.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="nodeSettings">User defined node settings.</param>
        /// <param name="checkpoints">Provider of block header hash checkpoints.</param>
        public InitialBlockDownloadState(IChainState chainState, Network network, NodeSettings nodeSettings, ICheckpoints checkpoints)
        {
            this.network = network;
            this.nodeSettings = nodeSettings;
            this.chainState = chainState;
            this.checkpoints = checkpoints;
            this.dateTimeProvider = DateTimeProvider.Default;
        }

        /// <inheritdoc />
        public bool IsInitialBlockDownload()
        {
            if (this.chainState == null)
                return false;

            if (this.chainState.ConsensusTip == null)
                return true;

            if (this.checkpoints.GetLastCheckpointHeight() > this.chainState.ConsensusTip.Height)
                return true;

            if (this.chainState.ConsensusTip.ChainWork < (this.network.Consensus.MinimumChainWork ?? uint256.Zero))
                return true;

            if (this.chainState.ConsensusTip.Header.BlockTime.ToUnixTimeSeconds() < (this.dateTimeProvider.GetTime() - this.nodeSettings.MaxTipAge))
                return true;

            return false;
        }
    }
}
