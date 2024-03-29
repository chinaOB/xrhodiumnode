﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Context for the inner steps, <see cref="BlockStoreInnerStepFindBlocks"/> and <see cref="BlockStoreInnerStepReadBlocks"/>.
    /// <para>
    /// The context also initializes the inner step <see cref="InnerSteps"/>.
    /// </para>
    /// </summary>
    public sealed class BlockStoreInnerStepContext
    {
        /// <summary>Number of milliseconds to wait after each failed attempt to get a block from the block puller.</summary>
        internal const int StallDelayMs = 100;

        /// <summary><see cref="DownloadStack"/> is flushed to the disk if more than this amount of milliseconds passed since the last flush was made.</summary>
        internal const int MaxDownloadStackFlushTimeMs = 20 * 1000;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        internal readonly IDateTimeProvider DateTimeProvider;

        /// <summary>Number of attempts to obtain a block from the block puller before giving up and requesting the block again.</summary>
        /// <remarks>If the threshold is reached, it is increased to allow more attempts next time.</remarks>
        internal int StallCountThreshold = 1800;

        /// <summary>Timestamp of the last flush of <see cref="DownloadStack"/> to the disk.</summary>
        internal DateTime LastDownloadStackFlushTime;

        public BlockStoreInnerStepContext(CancellationToken cancellationToken, BlockStoreLoop blockStoreLoop, ChainedHeader nextChainedHeader, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
        {
            Guard.NotNull(blockStoreLoop, nameof(blockStoreLoop));
            Guard.NotNull(nextChainedHeader, nameof(nextChainedHeader));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.BlockStoreLoop = blockStoreLoop;
            this.CancellationToken = cancellationToken;
            this.DateTimeProvider = dateTimeProvider;
            this.DownloadStack = new Queue<ChainedHeader>();
            this.InnerSteps = new List<BlockStoreInnerStep> { new BlockStoreInnerStepFindBlocks(loggerFactory), new BlockStoreInnerStepReadBlocks(loggerFactory) };
            this.InsertBlockSize = 0;
            this.LastDownloadStackFlushTime = this.DateTimeProvider.GetUtcNow();
            this.NextChainedHeader = nextChainedHeader;
            this.StallCount = 0;
            this.Store = new List<BlockPair>();
        }

        /// <summary>The number of blocks pushed to repository. This gets reset when the next
        /// set of blocks are asked from the puller</summary>
        public int BlocksPushedCount { get; set; }

        /// <summary>A queue of blocks to be downloaded.</summary>
        public Queue<ChainedHeader> DownloadStack { get; private set; }

        /// <summary>The maximum number of blocks to ask for.</summary>
        public const int DownloadStackThreshold = 100;

        /// <summary>The maximum number of blocks to read from the puller before asking for blocks again.</summary>
        public const int DownloadStackPushThreshold = 50;

        public BlockStoreLoop BlockStoreLoop { get; private set; }

        /// <summary>The chained block header the inner step starts on.</summary>
        public ChainedHeader InputChainedHeader { get; private set; }

        public ChainedHeader NextChainedHeader { get; private set; }

        /// <summary>The routine (list of inner steps) the DownloadBlockStep executes.</summary>
        public List<BlockStoreInnerStep> InnerSteps { get; private set; }

        public CancellationToken CancellationToken;

        /// <summary>
        /// A store of blocks that will be pushed to the repository once the <see cref="BlockStoreLoop.MaxInsertBlockSize"/> has been reached.
        /// </summary>
        public List<BlockPair> Store;

        public int InsertBlockSize;

        public int StallCount;

        /// <summary> Sets the next chained block header to process.</summary>
        internal void GetNextBlock()
        {
            this.logger.LogTrace("()");

            this.InputChainedHeader = this.NextChainedHeader;
            this.NextChainedHeader = this.BlockStoreLoop.Chain.GetBlock(this.InputChainedHeader.Height + 1);

            this.logger.LogTrace("(-):{0}='{1}'", nameof(this.NextChainedHeader), this.NextChainedHeader);
        }

        /// <summary> Removes BlockStoreInnerStepFindBlocks from the routine.</summary>
        internal void StopFindingBlocks()
        {
            this.logger.LogTrace("()");

            this.InnerSteps.Remove(this.InnerSteps.OfType<BlockStoreInnerStepFindBlocks>().First());

            this.logger.LogTrace("(-)");
        }
    }

    /// <summary>Abstract class that all DownloadBlockSteps implement</summary>
    public abstract class BlockStoreInnerStep
    {
        public abstract Task<InnerStepResult> ExecuteAsync(BlockStoreInnerStepContext context);
    }
}
