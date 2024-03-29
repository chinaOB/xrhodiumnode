﻿using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Node.Interfaces;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.Consensus
{
    public class ConsensusManager : INetworkDifficulty, IGetUnspentTransaction
    {
        public IConsensusLoop ConsensusLoop { get; }

        public Network Network { get; }

        public ConsensusManager(Network network, IConsensusLoop consensusLoop = null)
        {
            this.Network = network;
            this.ConsensusLoop = consensusLoop;
        }

        public Target GetNetworkDifficulty()
        {
            return this.ConsensusLoop?.Tip?.GetWorkRequired(this.Network.Consensus);
        }

        /// <inheritdoc />
        public async Task<UnspentOutputs> GetUnspentTransactionAsync(uint256 trxid)
        {
            CoinViews.FetchCoinsResponse response = null;
            if (this.ConsensusLoop?.UTXOSet != null)
                response = await this.ConsensusLoop.UTXOSet.FetchCoinsAsync(new[] { trxid }).ConfigureAwait(false);
            return response?.UnspentOutputs?.SingleOrDefault();
        }
    }
}
