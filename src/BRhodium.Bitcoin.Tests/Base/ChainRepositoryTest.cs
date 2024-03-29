﻿using System;
using System.Collections.Generic;
using DBreeze;
using NBitcoin;
using BRhodium.Node.Base;
using BRhodium.Node.Tests.Common;
using Xunit;

namespace BRhodium.Node.Tests.Base
{
    public class ChainRepositoryTest : TestBase
    {
        public ChainRepositoryTest() : base(Network.BRhodiumRegTest)
        {
        }

        [Fact]
        public void SaveWritesChainToDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ConcurrentChain(Network.RegTest);
            this.AppendBlock(chain);

            using (var repo = new ChainRepository(dir))
            {
                repo.SaveAsync(chain).GetAwaiter().GetResult();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                ChainedHeader tip = null;
                foreach (var row in engine.GetTransaction().SelectForward<int, BlockHeader>("Chain"))
                {
                    if (tip != null && row.Value.HashPrevBlock != tip.HashBlock)
                        break;
                    tip = new ChainedHeader(row.Value, row.Value.GetHash(), tip);
                }
                Assert.Equal(tip, chain.Tip);
            }
        }

        [Fact]
        public void GetChainReturnsConcurrentChainFromDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ConcurrentChain(Network.RegTest);
            var tip = this.AppendBlock(chain);

            using (var engine = new DBreezeEngine(dir))
            {
                using (DBreeze.Transactions.Transaction transaction = engine.GetTransaction())
                {
                    ChainedHeader toSave = tip;
                    var blocks = new List<ChainedHeader>();
                    while (toSave != null)
                    {
                        blocks.Insert(0, toSave);
                        toSave = toSave.Previous;
                    }

                    foreach (ChainedHeader block in blocks)
                    {
                        transaction.Insert<int, BlockHeader>("Chain", block.Height, block.Header);
                    }

                    transaction.Commit();
                }
            }
            using (var repo = new ChainRepository(dir))
            {
                var testChain = new ConcurrentChain(Network.RegTest);
                repo.LoadAsync(testChain).GetAwaiter().GetResult();
                Assert.Equal(tip, testChain.Tip);
            }
        }

        public ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            var nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                var block = this.Network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(this.Network.Consensus.ConsensusFactory.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedHeader AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chains);
        }
    }
}
