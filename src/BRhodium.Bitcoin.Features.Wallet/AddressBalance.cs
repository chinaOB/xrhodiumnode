﻿using System.Collections.Generic;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A class that represents the balance of an address.
    /// </summary>
    public class AddressBalance
    {
        /// <summary>
        /// The address for which the balance is calculated.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The coin type of this balance.
        /// </summary>
        public CoinType CoinType { get; set; }

        /// <summary>
        /// The balance of confirmed transactions.
        /// </summary>
        public Money AmountConfirmed { get; set; }

        /// <summary>
        /// The balance of unconfirmed transactions.
        /// </summary>
        public Money AmountUnconfirmed { get; set; }

        /// <summary>
        /// Gets or sets the transactions.
        /// </summary>
        public ICollection<TransactionData> Transactions { get; set; }

        /// <summary>
        /// Shows immature coinbase transactions
        /// </summary>
        public Money AmountImmature { get; set; }

        /// <summary>
        /// Gets the total amount.
        /// </summary>
        /// <returns>Money object with amount.</returns>
        public Money GetTotalAmount()
        {
            return this.AmountConfirmed + this.AmountUnconfirmed;
        }
    }
}
