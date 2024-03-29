﻿using System;
using NBitcoin;
using BRhodium.Node.Signals;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.Extensions;

namespace BRhodium.Node.IntegrationTests.EnvironmentMockUpHelpers
{
    /// <summary>
    /// This date time provider substitutes the node's usual DTP when running certain
    /// integration tests so that we can generate coins faster.
    /// </summary>
    public sealed class GenerateCoinsFastDateTimeProvider : SignalObserver<Block>, IDateTimeProvider
    {
        private TimeSpan adjustedTimeOffset;
        private DateTime startFrom;

        public GenerateCoinsFastDateTimeProvider(Signals.Signals signals)
        {
            this.adjustedTimeOffset = TimeSpan.Zero;
            this.startFrom = new DateTime(2018, 1, 1);

            signals.SubscribeForBlocks(this);
        }

        public long GetTime()
        {
            return this.startFrom.ToUnixTimestamp();
        }

        public DateTime GetUtcNow()
        {
            return this.startFrom;
        }

        /// <summary>
        /// This gets called when the Transaction's time gets set in <see cref="Features.Miner.PowBlockDefinition"/>.
        /// </summary>
        public DateTime GetAdjustedTime()
        {
            return this.startFrom;
        }

        /// <summary>
        /// This gets called when the Block Header's time gets set in <see cref="Features.Miner.PowBlockDefinition"/>.
        /// <para>
        /// Please see the <see cref="Features.Miner.PowBlockDefinition.UpdateHeaders"/> method.
        /// </para>
        /// <para>
        /// Add 5 seconds to the time so that the block header's time stamp is after
        /// the transaction's creation time.
        /// </para>
        /// </summary>
        public DateTimeOffset GetTimeOffset()
        {
            return this.startFrom;
        }

        /// <summary>
        /// This gets called when the transaction's time gets set in <see cref="Features.Miner.PowBlockDefinition"/>.
        /// <para>
        /// Please see the <see cref="Features.Miner.PowBlockDefinition.CreateCoinbase"/> method.
        /// </para>
        /// </summary>
        public long GetAdjustedTimeAsUnixTimestamp()
        {
            return this.startFrom.ToUnixTimestamp();
        }

        public void SetAdjustedTimeOffset(TimeSpan adjustedTimeOffset)
        {
            this.adjustedTimeOffset = adjustedTimeOffset;
        }

        /// <summary>
        /// Every time a new block gets generated, this date time provider will be signaled,
        /// updating the last block time by 65 seconds.
        /// </summary>
        protected override void OnNextCore(Block value)
        {
            this.startFrom = this.startFrom.AddSeconds(65);
        }

        public DateTime GetUtcFromUnixTimeSeconds(int value)
        {
            var absoluteOffset = DateTimeOffset.FromUnixTimeSeconds(value);
            this.startFrom = absoluteOffset.UtcDateTime;
            return this.startFrom;
        }
    }
}