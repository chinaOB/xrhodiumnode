﻿using NBitcoin;
using BRhodium.Node.Signals;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.Notifications
{
    /// <summary>
    /// Class used to broadcast about new transactions.
    /// </summary>
    public class TransactionNotification
    {
        private readonly ISignals signals;

        public TransactionNotification(ISignals signals)
        {
            Guard.NotNull(signals, nameof(signals));

            this.signals = signals;
        }

        /// <summary>
        /// Broadcast new transactions to subscribers.
        /// </summary>
        /// <param name="transaction">The transaction to braodcast.</param>
        public virtual void Notify(Transaction transaction)
        {
            if (transaction != null)
            {
                // broadcast the transaction to the registered observers
                this.signals.SignalTransaction(transaction);
            }
        }
    }
}
