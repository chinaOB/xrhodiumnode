﻿using System;
using System.Transactions;
using NBitcoin;

namespace BRhodium.Bitcoin.Features.Wallet.Broadcasting
{
    public class TransactionBroadcastEntry
    {
        public NBitcoin.Transaction Transaction { get; }

        public State State { get; set; }

        public string ErrorMessage { get; set; }

        public TransactionBroadcastEntry(NBitcoin.Transaction transaction, State state, string errorMessage)
        {
            this.Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            this.State = state;
            this.ErrorMessage = errorMessage;
        }
    }
}
