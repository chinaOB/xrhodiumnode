﻿using BRhodium.Node.Tests.Common.TestFramework;
using Xunit;

namespace BRhodium.Node.IntegrationTests.BlockStore
{
    public partial class ReorgToLongestChainSpecification : BddSpecification
    {
        [Fact]
        public void A_cut_off_miner_advanced_ahead_of_network_causes_reorg_on_reconnect()
        {
            Given(four_miners);
            And(each_mine_a_block);
            And(mining_continues_to_maturity_to_allow_spend);
            And(jing_loses_connection_to_others_but_carries_on_mining);
            And(bob_creates_a_transaction_and_broadcasts);
            And(charlie_mines_this_block);
            And(dave_confirms_transaction_is_present);
            And(meanwhile_jings_chain_advanced_ahead_of_the_others);
            When(jings_connection_comes_back);
            Then(bob_charlie_and_dave_reorg_to_jings_longest_chain);
            And(bobs_transaction_from_shorter_chain_is_now_missing);
            And(bobs_transaction_is_not_returned_to_the_mem_pool); // TODO: Inverse this check and implement it in production code
        }
    }
}