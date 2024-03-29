﻿using System.Threading.Tasks;
using NBitcoin;
using BRhodium.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace BRhodium.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderRuleTest
    {
        [Fact]
        public async Task BlockReceived_IsNextBlock_ValidationSucessAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            BlockHeaderRule blockHeaderRule = testContext.CreateRule<BlockHeaderRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = Network.RegTest.Consensus.ConsensusFactory.CreateBlock();
            context.BlockValidationContext.Block.Header.HashPrevBlock = testContext.Chain.Tip.HashBlock;

            await blockHeaderRule.RunAsync(context);

            Assert.NotNull(context.BlockValidationContext.ChainedHeader);
            Assert.NotNull(context.BestBlock);
            Assert.NotNull(context.Flags);
        }

        [Fact]
        public async Task BlockReceived_NotNextBlock_ValidationFailAsync()
        {
            var testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            BlockHeaderRule blockHeaderRule = testContext.CreateRule<BlockHeaderRule>();

            var context = new RuleContext(new BlockValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip);
            context.BlockValidationContext.Block = Network.RegTest.Consensus.ConsensusFactory.CreateBlock();
            context.BlockValidationContext.Block.Header.HashPrevBlock = uint256.Zero;
            var error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await blockHeaderRule.RunAsync(context));

            Assert.Equal(ConsensusErrors.InvalidPrevTip, error.ConsensusError);
        }
    }
}
