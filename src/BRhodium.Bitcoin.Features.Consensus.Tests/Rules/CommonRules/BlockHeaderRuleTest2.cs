﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Node.Base.Deployments;
using BRhodium.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;
using static NBitcoin.Transaction;

namespace BRhodium.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderRuleTest2 : TestConsensusRulesUnitTestBase
    {
        public BlockHeaderRuleTest2()
        {
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_PrevHashBlockNotConsensusTip_ThrowsInvalidPrevTipConsensusErrorExceptionAsync()
        {
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(5));
            block.Header.HashPrevBlock = this.concurrentChain.GetBlock(3).HashBlock; // invalid
            block.Header.Nonce = RandomUtils.GetUInt32();

            this.ruleContext.BlockValidationContext.Block = block;
            this.ruleContext.ConsensusTip = this.concurrentChain.Tip;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.InvalidPrevTip, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ValidBlock_CreatesChainedBlockInBlockValidationContextAsync()
        {
            var tip = this.concurrentChain.Tip;
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(5));
            block.Header.HashPrevBlock = tip.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();

            this.ruleContext.BlockValidationContext.Block = block;
            this.ruleContext.ConsensusTip = tip;

            await this.consensusRules.RegisterRule<BlockHeaderRule>().RunAsync(this.ruleContext);

            var chainedHeader = this.ruleContext.BlockValidationContext.ChainedHeader;
            Assert.IsType<ChainedHeader>(chainedHeader);

            Assert.Equal(block.Header.GetHash(), chainedHeader.HashBlock);
            Assert.Equal(block.Header.Nonce, chainedHeader.Header.Nonce);
            Assert.Equal(block.Header.BlockTime, chainedHeader.Header.BlockTime);
            Assert.Equal(block.Header.HashMerkleRoot, chainedHeader.Header.HashMerkleRoot);
            Assert.Equal(tip.HashBlock, chainedHeader.Previous.HashBlock);
        }

        [Fact]
        public async Task RunAsync_ValidBlock_SetsBestBlockAsync()
        {
            var tip = this.concurrentChain.Tip;
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(5));
            block.Header.HashPrevBlock = tip.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();

            this.ruleContext.BlockValidationContext.Block = block;
            this.ruleContext.ConsensusTip = tip;

            this.dateTimeProvider.Setup(d => d.GetTimeOffset())
                .Returns(new DateTimeOffset(new DateTime(2017, 1, 1, 1, 1, 1)))
                .Verifiable();

            await this.consensusRules.RegisterRule<BlockHeaderRule>().RunAsync(this.ruleContext);

            this.dateTimeProvider.Verify();
            var chainedBlock = this.ruleContext.BlockValidationContext.ChainedHeader;
            Assert.Equal(new DateTimeOffset(new DateTime(2017, 1, 1, 1, 1, 1)), this.ruleContext.Time);
            Assert.Equal(chainedBlock.Previous.GetMedianTimePast(), this.ruleContext.BestBlock.MedianTimePast);
            Assert.Equal(tip.Height, this.ruleContext.BestBlock.Height);
            Assert.Equal(tip.Header.GetHash(), this.ruleContext.BestBlock.Header.GetHash());
        }

        [Fact]
        public async Task RunAsync_ValidBlock_SetsConsensusFlagsAsync()
        {
            this.nodeDeployments = new NodeDeployments(this.network, this.concurrentChain);
            this.consensusRules = this.InitializeConsensusRules();

            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(5));
            block.Header.HashPrevBlock = this.concurrentChain.Tip.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();

            this.ruleContext.BlockValidationContext.Block = block;
            this.ruleContext.ConsensusTip = this.concurrentChain.Tip;

            await this.consensusRules.RegisterRule<BlockHeaderRule>().RunAsync(this.ruleContext);

            Assert.NotNull(this.ruleContext.Flags);
            Assert.True(this.ruleContext.Flags.EnforceBIP30);
            Assert.True(this.ruleContext.Flags.EnforceBIP34);
            Assert.Equal(LockTimeFlags.None, this.ruleContext.Flags.LockTimeFlags);
            Assert.Equal(ScriptVerify.Mandatory | ScriptVerify.DerSig | ScriptVerify.CheckLockTimeVerify, this.ruleContext.Flags.ScriptFlags);
        }
    }
}
