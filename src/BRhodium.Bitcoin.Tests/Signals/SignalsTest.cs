﻿using Moq;
using NBitcoin;
using BRhodium.Node.Signals;
using Xunit;

namespace BRhodium.Node.Tests.Signals
{
    public class SignalsTest
    {
        private Mock<ISignaler<Block>> blockSignaler;
        private BRhodium.Node.Signals.Signals signals;
        private Mock<ISignaler<Transaction>> transactionSignaler;

        public SignalsTest()
        {
            this.blockSignaler = new Mock<ISignaler<Block>>();
            this.transactionSignaler = new Mock<ISignaler<Transaction>>();
            this.signals = new BRhodium.Node.Signals.Signals(this.blockSignaler.Object, this.transactionSignaler.Object);
        }

        [Fact]
        public void SignalBlockBroadcastsToBlockSignaler()
        {
            var block = new Block();

            this.signals.SignalBlock(block);

            this.blockSignaler.Verify(b => b.Broadcast(block), Times.Exactly(1));
        }

        [Fact]
        public void SignalTransactionBroadcastsToTransactionSignaler()
        {
            var transaction = new Transaction();

            this.signals.SignalTransaction(transaction);

            this.transactionSignaler.Verify(b => b.Broadcast(transaction), Times.Exactly(1));
        }
    }
}
