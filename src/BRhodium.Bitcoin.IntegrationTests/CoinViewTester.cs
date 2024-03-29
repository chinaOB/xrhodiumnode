﻿using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using BRhodium.Bitcoin.Features.Consensus.CoinViews;
using BRhodium.Node.Utilities;

namespace BRhodium.Node.IntegrationTests
{
    public class CoinViewTester
    {
        private CoinView coinView;
        private List<UnspentOutputs> pendingCoins = new List<UnspentOutputs>();
        private uint256 hash;

        public CoinViewTester(CoinView coinView)
        {
            this.coinView = coinView;
            this.hash = coinView.GetBlockHashAsync().Result;
        }

        public Coin[] CreateCoins(int coinCount)
        {
            var tx = new Transaction();
            tx.Outputs.AddRange(Enumerable.Range(0, coinCount)
                .Select(t => new TxOut(Money.Zero, new Key()))
                .ToArray());
            var output = new UnspentOutputs(1, tx);
            this.pendingCoins.Add(output);
            return tx.Outputs.AsCoins().ToArray();
        }

        public bool Exists(Coin c)
        {
            var result = this.coinView.FetchCoinsAsync(new[] { c.Outpoint.Hash }).Result;
            if (result.BlockHash != this.hash)
                throw new InvalidOperationException("Unexepected hash");
            if (result.UnspentOutputs[0] == null)
                return false;
            return result.UnspentOutputs[0].IsAvailable(c.Outpoint.N);
        }

        public void Spend(Coin c)
        {
            var coin = this.pendingCoins.FirstOrDefault(u => u.TransactionId == c.Outpoint.Hash);
            if (coin == null)
            {
                var result = this.coinView.FetchCoinsAsync(new[] { c.Outpoint.Hash }).Result;
                if (result.BlockHash != this.hash)
                    throw new InvalidOperationException("Unexepected hash");
                if (result.UnspentOutputs[0] == null)
                    throw new InvalidOperationException("Coin unavailable");

                if (!result.UnspentOutputs[0].Spend(c.Outpoint.N))
                    throw new InvalidOperationException("Coin unspendable");
                this.pendingCoins.Add(result.UnspentOutputs[0]);
            }
            else
            {
                if (!coin.Spend(c.Outpoint.N))
                    throw new InvalidOperationException("Coin unspendable");
            }
        }

        public uint256 NewBlock()
        {
            var newHash = new uint256(RandomUtils.GetBytes(32));
            this.coinView.SaveChangesAsync(this.pendingCoins, null, this.hash, newHash).Wait();
            this.pendingCoins.Clear();
            this.hash = newHash;
            return newHash;
        }

        public uint256 Rewind()
        {
            this.hash = this.coinView.Rewind().Result;
            return this.hash;
        }
    }
}
