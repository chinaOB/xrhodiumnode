﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace BRhodium.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockSizeRuleTest : TestConsensusRulesUnitTestBase
    {
        private PowConsensusOptions options;

        public BlockSizeRuleTest()
        {
            this.options = this.network.Consensus.Option<PowConsensusOptions>();
            this.ruleContext.Consensus = this.network.Consensus;
        }

        [Fact]
        public async Task RunAsync_BadBlockWeight_ThrowsBadBlockWeightConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = GenerateBlockWithWeight((this.options.MaxBlockWeight / this.options.WitnessScaleFactor) + 1, TransactionOptions.All);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockSizeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockWeight, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ZeroTransactions_ThrowsBadBlockLengthConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = new Block();

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockSizeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockLength, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TransactionCountAboveMaxBlockBaseSize_ThrowsBadBlockLengthConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = new Block();

            //10b = apporx 1 transaction
            for (var i = 0; i < (this.options.MaxBlockBaseSize / 10 + 1); i++)
            {
                this.ruleContext.BlockValidationContext.Block.Transactions.Add(new Transaction());
            }

            int blockWeight = this.CalculateBlockWeight(this.ruleContext.BlockValidationContext.Block, TransactionOptions.All);

            // increase max block weight to be able to hit this if statement
            this.options.MaxBlockWeight = this.options.MaxBlockBaseSize + 100;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockSizeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockLength, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BlockSizeAboveMaxBlockBaseSize_ThrowsBadBlockLengthConsensusErrorExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = GenerateBlockWithWeight(this.options.MaxBlockBaseSize + 1, TransactionOptions.All);
            int blockWeight = this.CalculateBlockWeight(this.ruleContext.BlockValidationContext.Block, TransactionOptions.All);

            // increase max block weight to be able to hit this if statement
            this.options.MaxBlockWeight = this.options.MaxBlockBaseSize + 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockSizeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockLength, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_AtBlockWeight_BelowMaxBlockBaseSize_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = GenerateBlockWithWeight(this.options.MaxBlockWeight / this.options.WitnessScaleFactor, TransactionOptions.All);
            this.options.MaxBlockBaseSize = this.options.MaxBlockWeight + 1000;

            await this.consensusRules.RegisterRule<BlockSizeRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_BelowBlockWeight_BelowMaxBlockBaseSize_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = GenerateBlockWithWeight((this.options.MaxBlockWeight / this.options.WitnessScaleFactor) - 1, TransactionOptions.All);
            this.options.MaxBlockBaseSize = this.options.MaxBlockBaseSize + 1000;

            await this.consensusRules.RegisterRule<BlockSizeRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task TaskAsync_TransactionCountBelowLimit_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = new Block();

            for (var i = 0; i < 10; i++)
            {
                this.ruleContext.BlockValidationContext.Block.Transactions.Add(new Transaction());
            }

            await this.consensusRules.RegisterRule<BlockSizeRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_BlockAtMaxBlockBaseSize_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = GenerateBlockWithWeight(this.options.MaxBlockBaseSize, TransactionOptions.All);
            this.options.MaxBlockWeight = this.options.MaxBlockBaseSize + 1000;

            await this.consensusRules.RegisterRule<BlockSizeRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_BlockBelowMaxBlockBaseSize_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.BlockValidationContext.Block = GenerateBlockWithWeight(this.options.MaxBlockBaseSize - 1, TransactionOptions.All);
            this.options.MaxBlockWeight = this.options.MaxBlockBaseSize + 1000;

            await this.consensusRules.RegisterRule<BlockSizeRule>().RunAsync(this.ruleContext);
        }

        private Block GenerateBlockWithWeight(int weight, TransactionOptions options)
        {
            var block = new Block();
            var transaction = new Transaction();
            transaction.Outputs.Add(new TxOut(new Money(10000000000), new Script()));
            block.Transactions.Add(transaction);

            int blockWeight = this.CalculateBlockWeight(block, options);

            var requiredScriptWeight = weight - blockWeight - 4;
            block.Transactions[0].Outputs.Clear();
            // generate nonsense script with required bytes to reach required weight.
            var script = Script.FromBytesUnsafe(new string('A', requiredScriptWeight).Select(c => (byte)c).ToArray());
            transaction.Outputs.Add(new TxOut(new Money(10000000000), script));

            blockWeight = this.CalculateBlockWeight(block, options);

            if (blockWeight == weight)
            {
                return block;
            }

            return null;
        }

        private int CalculateBlockWeight(Block block, TransactionOptions options)
        {
            using (var stream = new MemoryStream())
            {
                var bms = new BitcoinStream(stream, true);
                bms.TransactionOptions = options;
                block.ReadWrite(bms);
                return (int)bms.Counter.WrittenBytes;
            }
        }
    }
}
