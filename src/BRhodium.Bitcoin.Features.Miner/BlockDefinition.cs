﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Base.Deployments;
using BRhodium.Bitcoin.Features.Consensus;
using BRhodium.Bitcoin.Features.Consensus.Interfaces;
using BRhodium.Bitcoin.Features.Consensus.Rules.CommonRules;
using BRhodium.Bitcoin.Features.MemoryPool;
using BRhodium.Bitcoin.Features.MemoryPool.Interfaces;
using BRhodium.Node.Mining;
using BRhodium.Node.Utilities;
using static BRhodium.Bitcoin.Features.Miner.PowBlockDefinition;

namespace BRhodium.Bitcoin.Features.Miner
{
    /// <summary>
    /// A high level class that will allow the ability to override or inject functionality based on what type of block creation logic is used.
    /// </summary>
    public abstract class BlockDefinition
    {
        /// <summary>
        /// Tip of the chain that this instance will work with without touching any shared chain resources.
        /// </summary>
        protected ChainedHeader ChainTip;

        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        protected readonly IConsensusLoop ConsensusLoop;

        /// <summary>Provider of date time functions.</summary>
        protected readonly IDateTimeProvider DateTimeProvider;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Transaction memory pool for managing transactions in the memory pool.</summary>
        protected readonly ITxMempool Mempool;

        /// <summary>Lock for memory pool access.</summary>
        protected readonly MempoolSchedulerLock MempoolLock;

        /// <summary>The current network.</summary>
        protected readonly Network Network;

        /// <summary>Assembler options specific to the assembler e.g. <see cref="BlockDefinitionOptions.BlockMaxSize"/>.</summary>
        protected BlockDefinitionOptions Options;

        /// <summary>
        /// Limit the number of attempts to add transactions to the block when it is
        /// close to full; this is just a simple heuristic to finish quickly if the
        /// mempool has a lot of entries.
        /// </summary>
        protected const int MaxConsecutiveAddTransactionFailures = 1000;

        /// <summary>
        /// Unconfirmed transactions in the memory pool often depend on other
        /// transactions in the memory pool. When we select transactions from the
        /// pool, we select by highest fee rate of a transaction combined with all
        /// its ancestors.
        /// </summary>
        protected long LastBlockTx = 0;

        protected long LastBlockSize = 0;

        protected long LastBlockWeight = 0;

        protected long MedianTimePast;

        /// <summary>
        /// The constructed block template.
        /// </summary>
        protected BlockTemplate BlockTemplate;

        /// <summary>
        /// A convenience pointer that always refers to the <see cref="Block"/> in <see cref="BlockTemplate"/>.
        /// </summary>
        protected Block block;

        /// <summary>
        /// Configuration parameters for the block size.
        /// </summary>
        protected bool IncludeWitness;

        protected uint BlockMaxWeight, BlockMaxSize;

        protected bool NeedSizeAccounting;

        protected FeeRate BlockMinFeeRate;

        /// <summary>
        /// Information on the current status of the block.
        /// </summary>
        protected long BlockWeight;

        protected long BlockSize;

        protected long BlockTx;

        protected long BlockSigOpsCost;

        public Money fees;

        protected TxMempool.SetEntries inBlock;

        protected Transaction coinbase;

        /// <summary>
        /// Chain context for the block.
        /// </summary>
        protected int height;

        protected long LockTimeCutoff;

        protected Script scriptPubKey;

        protected BlockDefinition(
            IConsensusLoop consensusLoop,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            BlockDefinitionOptions options = null)
        {
            this.ConsensusLoop = consensusLoop;
            this.DateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Mempool = mempool;
            this.MempoolLock = mempoolLock;
            this.Network = network;

            this.Options = options ?? new BlockDefinitionOptions();
            this.BlockMinFeeRate = this.Options.BlockMinFeeRate;

            // Limit weight to between 4K and MAX_BLOCK_WEIGHT-4K for sanity.
            this.BlockMaxWeight = (uint)Math.Max(4000, Math.Min(PowMining.DefaultBlockMaxWeight - 4000, this.Options.BlockMaxWeight));

            // Limit size to between 1K and MAX_BLOCK_SERIALIZED_SIZE-1K for sanity.
            this.BlockMaxSize = (uint)Math.Max(1000, Math.Min(network.Consensus.Option<PowConsensusOptions>().MaxBlockSerializedSize - 1000, this.Options.BlockMaxSize));

            // Whether we need to account for byte usage (in addition to weight usage).
            this.NeedSizeAccounting = (this.BlockMaxSize < network.Consensus.Option<PowConsensusOptions>().MaxBlockSerializedSize - 1000);

            this.Configure();
        }

        private int ComputeBlockVersion(ChainedHeader prevChainedHeader, NBitcoin.Consensus consensus)
        {
            uint version = ThresholdConditionCache.VersionbitsTopBits;
            var thresholdConditionCache = new ThresholdConditionCache(consensus);

            IEnumerable<BIP9Deployments> deployments = Enum.GetValues(typeof(BIP9Deployments)).OfType<BIP9Deployments>();

            foreach (BIP9Deployments deployment in deployments)
            {
                ThresholdState state = thresholdConditionCache.GetState(prevChainedHeader, deployment);
                if ((state == ThresholdState.LockedIn) || (state == ThresholdState.Started))
                {
                    version |= thresholdConditionCache.Mask(deployment);
                }
            }

            return (int)version;
        }

        /// <summary>
        /// Compute the block version.
        /// </summary>
        protected virtual void ComputeBlockVersion()
        {
            this.height = this.ChainTip.Height + 1;
            this.block.Header.Version = this.ComputeBlockVersion(this.ChainTip, this.Network.Consensus);
        }

        /// <summary>
        /// Create coinbase transaction.
        /// Set the coin base with zero money.
        /// Once we have the fee we can update the amount.
        /// </summary>
        protected virtual void CreateCoinbase()
        {
            this.coinbase = this.Network.Consensus.ConsensusFactory.CreateTransaction();
            this.coinbase.Time = (uint)this.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp();
            this.coinbase.AddInput(TxIn.CreateCoinbase(this.ChainTip.Height + 1));
            this.coinbase.AddOutput(new TxOut(Money.Zero, this.scriptPubKey));

            this.block.AddTransaction(this.coinbase);
            this.BlockTemplate.VTxFees.Add(-1); // Updated at end.
            this.BlockTemplate.TxSigOpsCost.Add(-1); // Updated at end.
        }

        /// <summary>
        /// Configures (resets) the builder to its default state
        /// before constructing a new block.
        /// </summary>
        private void Configure()
        {
            this.BlockSize = 4000;
            this.BlockTemplate = new BlockTemplate(this.Network);
            this.BlockTx = 0;
            this.BlockWeight = 4000;
            this.BlockSigOpsCost = 400;
            this.fees = 0;
            this.inBlock = new TxMempool.SetEntries();
            this.IncludeWitness = false;
        }

        /// <summary>
        /// Constructs a block template which will be passed to consensus.
        /// </summary>
        /// <param name="chainTip">Tip of the chain that this instance will work with without touching any shared chain resources.</param>
        /// <param name="scriptPubKey">Script that explains what conditions must be met to claim ownership of a coin.</param>
        /// <returns>The contructed <see cref="Miner.BlockTemplate"/>.</returns>
        protected void OnBuild(ChainedHeader chainTip, Script scriptPubKey)
        {
            this.Configure();

            this.ChainTip = chainTip;

            this.block = this.BlockTemplate.Block;
            this.scriptPubKey = scriptPubKey;

            this.CreateCoinbase();
            this.ComputeBlockVersion();

            // TODO: MineBlocksOnDemand
            // -regtest only: allow overriding block.nVersion with
            // -blockversion=N to test forking scenarios
            //if (this.network. chainparams.MineBlocksOnDemand())
            //    pblock->nVersion = GetArg("-blockversion", pblock->nVersion);

            this.MedianTimePast = Utils.DateTimeToUnixTime(this.ChainTip.GetMedianTimePast());
            this.LockTimeCutoff = MempoolValidator.StandardLocktimeVerifyFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast)
                ? this.MedianTimePast
                : this.block.Header.Time;

            // TODO: Implement Witness Code
            // Decide whether to include witness transactions
            // This is only needed in case the witness softfork activation is reverted
            // (which would require a very deep reorganization) or when
            // -promiscuousmempoolflags is used.
            // TODO: replace this with a call to main to assess validity of a mempool
            // transaction (which in most cases can be a no-op).
            this.IncludeWitness = false; //IsWitnessEnabled(pindexPrev, chainparams.GetConsensus()) && fMineWitnessTx;

            // add transactions from the mempool
            int nPackagesSelected;
            int nDescendantsUpdated;
            this.AddTransactions(out nPackagesSelected, out nDescendantsUpdated);

            this.LastBlockTx = this.BlockTx;
            this.LastBlockSize = this.BlockSize;
            this.LastBlockWeight = this.BlockWeight;

            // TODO: Implement Witness Code
            // pblocktemplate->CoinbaseCommitment = GenerateCoinbaseCommitment(*pblock, pindexPrev, chainparams.GetConsensus());
            this.BlockTemplate.VTxFees[0] = -this.fees;

            var powCoinviewRule = this.ConsensusLoop.ConsensusRules.GetRule<PowCoinViewRule>();

            this.coinbase.Outputs[0].Value = this.fees + powCoinviewRule.GetProofOfWorkReward(this.height);
            this.BlockTemplate.TotalFee = this.fees;

            int nSerializeSize = this.block.GetSerializedSize();
            this.logger.LogDebug("Serialized size is {0} bytes, block weight is {1}, number of txs is {2}, tx fees are {3}, number of sigops is {4}.", nSerializeSize, powCoinviewRule.GetBlockWeight(this.block), this.BlockTx, this.fees, this.BlockSigOpsCost);

            this.OnUpdateHeaders();

            //pblocktemplate->TxSigOpsCost[0] = WITNESS_SCALE_FACTOR * GetLegacySigOpCount(*pblock->vtx[0]);

            this.OnTestBlockValidity();

            //int64_t nTime2 = GetTimeMicros();

            //LogPrint(BCLog::BENCH, "CreateNewBlock() packages: %.2fms (%d packages, %d updated descendants), validity: %.2fms (total %.2fms)\n", 0.001 * (nTime1 - nTimeStart), nPackagesSelected, nDescendantsUpdated, 0.001 * (nTime2 - nTime1), 0.001 * (nTime2 - nTimeStart));
        }

        /// <summary>
        /// Adds a transaction to the block from the given mempool entry.
        /// </summary>
        private void AddToBlock(TxMempoolEntry mempoolEntry)
        {
            this.logger.LogTrace("({0}.{1}:'{2}', {3}:{4}, txSize:{5})", nameof(mempoolEntry), nameof(mempoolEntry.TransactionHash), mempoolEntry.TransactionHash, nameof(mempoolEntry.ModifiedFee), mempoolEntry.ModifiedFee, mempoolEntry.GetTxSize());

            this.block.AddTransaction(mempoolEntry.Transaction);

            this.BlockTemplate.VTxFees.Add(mempoolEntry.Fee);
            this.BlockTemplate.TxSigOpsCost.Add(mempoolEntry.SigOpCost);

            if (this.NeedSizeAccounting)
                this.BlockSize += mempoolEntry.Transaction.GetSerializedSize();

            this.BlockWeight += mempoolEntry.TxWeight;
            this.BlockTx++;
            this.BlockSigOpsCost += mempoolEntry.SigOpCost;
            this.fees += mempoolEntry.Fee;
            this.inBlock.Add(mempoolEntry);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Method for how to add transactions to a block.
        /// Add transactions based on feerate including unconfirmed ancestors
        /// Increments nPackagesSelected / nDescendantsUpdated with corresponding
        /// statistics from the package selection (for logging statistics).
        /// This transaction selection algorithm orders the mempool based
        /// on feerate of a transaction including all unconfirmed ancestors.
        /// Since we don't remove transactions from the mempool as we select them
        /// for block inclusion, we need an alternate method of updating the feerate
        /// of a transaction with its not-yet-selected ancestors as we go.
        /// This is accomplished by walking the in-mempool descendants of selected
        /// transactions and storing a temporary modified state in mapModifiedTxs.
        /// Each time through the loop, we compare the best transaction in
        /// mapModifiedTxs with the next transaction in the mempool to decide what
        /// transaction package to work on next.
        /// </summary>
        protected virtual void AddTransactions(out int nPackagesSelected, out int nDescendantsUpdated)
        {
            nPackagesSelected = 0;
            nDescendantsUpdated = 0;
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(nPackagesSelected), nPackagesSelected, nameof(nDescendantsUpdated), nDescendantsUpdated);

            // mapModifiedTx will store sorted packages after they are modified
            // because some of their txs are already in the block.
            var mapModifiedTx = new Dictionary<uint256, TxMemPoolModifiedEntry>();

            //var mapModifiedTxRes = this.mempoolScheduler.ReadAsync(() => mempool.MapTx.Values).GetAwaiter().GetResult();
            // mapModifiedTxRes.Select(s => new TxMemPoolModifiedEntry(s)).OrderBy(o => o, new CompareModifiedEntry());

            // Keep track of entries that failed inclusion, to avoid duplicate work.
            TxMempool.SetEntries failedTx = new TxMempool.SetEntries();

            // Start by adding all descendants of previously added txs to mapModifiedTx
            // and modifying them for their already included ancestors.
            this.UpdatePackagesForAdded(this.inBlock, mapModifiedTx);

            List<TxMempoolEntry> ancestorScoreList = this.MempoolLock.ReadAsync(() => this.Mempool.MapTx.AncestorScore).GetAwaiter().GetResult().ToList();

            TxMempoolEntry iter;

            int nConsecutiveFailed = 0;
            while (ancestorScoreList.Any() || mapModifiedTx.Any())
            {
                TxMempoolEntry mi = ancestorScoreList.FirstOrDefault();
                if (mi != null)
                {
                    // Skip entries in mapTx that are already in a block or are present
                    // in mapModifiedTx (which implies that the mapTx ancestor state is
                    // stale due to ancestor inclusion in the block).
                    // Also skip transactions that we've already failed to add. This can happen if
                    // we consider a transaction in mapModifiedTx and it fails: we can then
                    // potentially consider it again while walking mapTx.  It's currently
                    // guaranteed to fail again, but as a belt-and-suspenders check we put it in
                    // failedTx and avoid re-evaluation, since the re-evaluation would be using
                    // cached size/sigops/fee values that are not actually correct.

                    // First try to find a new transaction in mapTx to evaluate.
                    if (mapModifiedTx.ContainsKey(mi.TransactionHash) || this.inBlock.Contains(mi) || failedTx.Contains(mi))
                    {
                        ancestorScoreList.Remove(mi);
                        continue;
                    }
                }

                // Now that mi is not stale, determine which transaction to evaluate:
                // the next entry from mapTx, or the best from mapModifiedTx?
                bool fUsingModified = false;
                TxMemPoolModifiedEntry modit;
                var compare = new CompareModifiedEntry();
                if (mi == null)
                {
                    modit = mapModifiedTx.Values.OrderByDescending(o => o, compare).First();
                    iter = modit.iter;
                    fUsingModified = true;
                }
                else
                {
                    // Try to compare the mapTx entry to the mapModifiedTx entry
                    iter = mi;

                    modit = mapModifiedTx.Values.OrderByDescending(o => o, compare).FirstOrDefault();
                    if ((modit != null) && (compare.Compare(modit, new TxMemPoolModifiedEntry(iter)) > 0))
                    {
                        // The best entry in mapModifiedTx has higher score
                        // than the one from mapTx..
                        // Switch which transaction (package) to consider.

                        iter = modit.iter;
                        fUsingModified = true;
                    }
                    else
                    {
                        // Either no entry in mapModifiedTx, or it's worse than mapTx.
                        // Increment mi for the next loop iteration.
                        ancestorScoreList.Remove(iter);
                    }
                }

                // We skip mapTx entries that are inBlock, and mapModifiedTx shouldn't
                // contain anything that is inBlock.
                Guard.Assert(!this.inBlock.Contains(iter));

                long packageSize = iter.SizeWithAncestors;
                Money packageFees = iter.ModFeesWithAncestors;
                long packageSigOpsCost = iter.SizeWithAncestors;
                if (fUsingModified)
                {
                    packageSize = modit.SizeWithAncestors;
                    packageFees = modit.ModFeesWithAncestors;
                    packageSigOpsCost = modit.SigOpCostWithAncestors;
                }

                if (packageFees < this.BlockMinFeeRate.GetFee((int)packageSize))
                {
                    // Everything else we might consider has a lower fee rate
                    return;
                }

                if (!this.TestPackage(packageSize, packageSigOpsCost))
                {
                    if (fUsingModified)
                    {
                        // Since we always look at the best entry in mapModifiedTx,
                        // we must erase failed entries so that we can consider the
                        // next best entry on the next loop iteration
                        mapModifiedTx.Remove(modit.iter.TransactionHash);
                        failedTx.Add(iter);
                    }

                    nConsecutiveFailed++;

                    if ((nConsecutiveFailed > MaxConsecutiveAddTransactionFailures) && (this.BlockWeight > this.BlockMaxWeight - 4000))
                    {
                        // Give up if we're close to full and haven't succeeded in a while
                        break;
                    }
                    continue;
                }

                TxMempool.SetEntries ancestors = new TxMempool.SetEntries();
                long nNoLimit = long.MaxValue;
                string dummy;
                this.Mempool.CalculateMemPoolAncestors(iter, ancestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false);

                this.OnlyUnconfirmed(ancestors);
                ancestors.Add(iter);

                // Test if all tx's are Final.
                if (!this.TestPackageTransactions(ancestors))
                {
                    if (fUsingModified)
                    {
                        mapModifiedTx.Remove(modit.iter.TransactionHash);
                        failedTx.Add(iter);
                    }
                    continue;
                }

                // This transaction will make it in; reset the failed counter.
                nConsecutiveFailed = 0;

                // Package can be added. Sort the entries in a valid order.
                // Sort package by ancestor count
                // If a transaction A depends on transaction B, then A's ancestor count
                // must be greater than B's.  So this is sufficient to validly order the
                // transactions for block inclusion.
                List<TxMempoolEntry> sortedEntries = ancestors.ToList().OrderBy(o => o, new CompareTxIterByAncestorCount()).ToList();
                foreach (TxMempoolEntry sortedEntry in sortedEntries)
                {
                    this.AddToBlock(sortedEntry);
                    // Erase from the modified set, if present
                    mapModifiedTx.Remove(sortedEntry.TransactionHash);
                }

                nPackagesSelected++;

                // Update transactions that depend on each of these
                nDescendantsUpdated += this.UpdatePackagesForAdded(ancestors, mapModifiedTx);
            }

            this.logger.LogTrace("(-)");
        }
        /// <summary>
        /// Remove confirmed <see cref="inBlock"/> entries from given set.
        /// </summary>
        private void OnlyUnconfirmed(TxMempool.SetEntries testSet)
        {
            foreach (var setEntry in testSet.ToList())
            {
                // Only test txs not already in the block
                if (this.inBlock.Contains(setEntry))
                {
                    testSet.Remove(setEntry);
                }
            }
        }

        /// <summary>
        /// Test if a new package would "fit" in the block.
        /// </summary>
        private bool TestPackage(long packageSize, long packageSigOpsCost)
        {
            // TODO: Switch to weight-based accounting for packages instead of vsize-based accounting.
            if (this.BlockWeight + this.Network.Consensus.Option<PowConsensusOptions>().WitnessScaleFactor * packageSize >= this.BlockMaxWeight)
                return false;

            if (this.BlockSigOpsCost + packageSigOpsCost >= this.Network.Consensus.Option<PowConsensusOptions>().MaxBlockSigopsCost)
                return false;

            return true;
        }

        /// <summary>
        /// Perform transaction-level checks before adding to block.
        /// <para>
        /// <list>
        /// <item>Transaction finality (locktime).</item>
        /// <item>Premature witness (in case segwit transactions are added to mempool before segwit activation).</item>
        /// <item>serialized size (in case -blockmaxsize is in use).</item>
        /// </list>
        /// </para>
        /// </summary>
        private bool TestPackageTransactions(TxMempool.SetEntries package)
        {
            long nPotentialBlockSize = this.BlockSize; // only used with needSizeAccounting
            foreach (TxMempoolEntry it in package)
            {
                if (!it.Transaction.IsFinal(Utils.UnixTimeToDateTime(this.LockTimeCutoff), this.height))
                    return false;

                if (!this.IncludeWitness && it.Transaction.HasWitness)
                    return false;

                if (this.NeedSizeAccounting)
                {
                    int nTxSize = it.Transaction.GetSerializedSize();
                    if (nPotentialBlockSize + nTxSize >= this.BlockMaxSize)
                        return false;

                    nPotentialBlockSize += nTxSize;
                }
            }

            return true;
        }

        /// <summary>
        /// Add descendants of given transactions to mapModifiedTx with ancestor
        /// state updated assuming given transactions are inBlock. Returns number
        /// of updated descendants.
        /// </summary>
        private int UpdatePackagesForAdded(TxMempool.SetEntries alreadyAdded, Dictionary<uint256, TxMemPoolModifiedEntry> mapModifiedTx)
        {
            int descendantsUpdated = 0;
            foreach (TxMempoolEntry setEntry in alreadyAdded)
            {
                TxMempool.SetEntries setEntries = new TxMempool.SetEntries();
                this.MempoolLock.ReadAsync(() => this.Mempool.CalculateDescendants(setEntry, setEntries)).GetAwaiter().GetResult();
                foreach (var desc in setEntries)
                {
                    if (alreadyAdded.Contains(desc))
                        continue;

                    descendantsUpdated++;
                    TxMemPoolModifiedEntry modEntry;
                    if (!mapModifiedTx.TryGetValue(desc.TransactionHash, out modEntry))
                    {
                        modEntry = new TxMemPoolModifiedEntry(desc);
                        mapModifiedTx.Add(desc.TransactionHash, modEntry);
                    }
                    modEntry.SizeWithAncestors -= setEntry.GetTxSize();
                    modEntry.ModFeesWithAncestors -= setEntry.ModifiedFee;
                    modEntry.SigOpCostWithAncestors -= setEntry.SigOpCost;
                }
            }

            return descendantsUpdated;
        }

        /// <summary>Logic specific as to how the block will be built.</summary>
        public abstract BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey);

        /// <summary>Logic specific to how the block's header will be set.</summary>
        public abstract void OnUpdateHeaders();

        /// <summary>Logic specific to how the block will be validated.</summary>
        public abstract void OnTestBlockValidity();
    }
}
