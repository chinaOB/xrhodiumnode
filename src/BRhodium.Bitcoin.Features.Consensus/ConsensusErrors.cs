﻿using System;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.Consensus
{
    /// <summary>
    /// An exception that is used when consensus breaking errors are found.
    /// </summary>
    public class ConsensusErrorException : Exception
    {
        /// <summary>
        /// Initialize a new instance of <see cref="ConsensusErrorException"/>.
        /// </summary>
        /// <param name="error">The error that triggered this exception.</param>
        public ConsensusErrorException(ConsensusError error) : base(error.Message)
        {
            this.ConsensusError = error;
        }

        /// <summary>The error that triggered this exception. </summary>
        public ConsensusError ConsensusError { get; private set; }
    }

    /// <summary>
    /// A consensus error that is used to specify different types of reasons a block does not confirm to the consensus rules.
    /// </summary>
    public class ConsensusError
    {
        /// <summary>
        /// The code representing this consensus error.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// A user friendly message to describe this error.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// A method that will throw a <see cref="ConsensusErrorException"/> with the current consensus error.
        /// </summary>
        public void Throw()
        {
            throw new ConsensusErrorException(this);
        }

        /// <summary>
        /// Initialize a new instance of <see cref="ConsensusErrorException"/>.
        /// </summary>
        /// <param name="code">The error code that represents the current consensus breaking error.</param>
        /// <param name="message">A user friendly message to describe this error.</param>
        public ConsensusError(string code, string message)
        {
            Guard.NotEmpty(code, nameof(code));
            Guard.NotEmpty(message, nameof(message));

            this.Code = code;
            this.Message = message;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            ConsensusError item = obj as ConsensusError;

            return (item != null) && (this.Code.Equals(item.Code));
        }

        /// <summary>
        /// Compare two instances of <see cref="ConsensusError"/> are the same.
        /// </summary>
        /// <param name="a">first instance to compare.</param>
        /// <param name="b">Second instance to compare.</param>
        /// <returns><c>true</c> if bother instances are the same.</returns>
        public static bool operator ==(ConsensusError a, ConsensusError b)
        {
            if (Object.ReferenceEquals(a, b))
                return true;

            if (((object)a == null) || ((object)b == null))
                return false;

            return a.Code == b.Code;
        }

        /// <summary>
        /// Compare two instances of <see cref="ConsensusError"/> are not the same.
        /// </summary>
        public static bool operator !=(ConsensusError a, ConsensusError b)
        {
            return !(a == b);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.Code.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0} : {1}", this.Code, this.Message);
        }
    }

    /// <summary>
    /// A class that holds consensus errors.
    /// </summary>
    public static class ConsensusErrors
    {
        public static readonly ConsensusError InvalidPrevTip = new ConsensusError("invalid-prev-tip", "invalid previous tip");
        public static readonly ConsensusError HighHash = new ConsensusError("high-hash", "proof of work failed");
        public static readonly ConsensusError BadCoinbaseHeight = new ConsensusError("bad-cb-height", "block height mismatch in coinbase");
        public static readonly ConsensusError BadTransactionNonFinal = new ConsensusError("bad-txns-nonfinal", "non-final transaction");
        public static readonly ConsensusError BadWitnessNonceSize = new ConsensusError("bad-witness-nonce-size", "invalid witness nonce size");
        public static readonly ConsensusError BadWitnessMerkleMatch = new ConsensusError("bad-witness-merkle-match", "witness merkle commitment mismatch");
        public static readonly ConsensusError UnexpectedWitness = new ConsensusError("unexpected-witness", "unexpected witness data found");
        public static readonly ConsensusError BadBlockWeight = new ConsensusError("bad-blk-weight", "weight limit failed");
        public static readonly ConsensusError BadDiffBits = new ConsensusError("bad-diffbits", "incorrect proof of work");
        public static readonly ConsensusError TimeTooOld = new ConsensusError("time-too-old", "block's timestamp is too early");
        public static readonly ConsensusError TimeTooNew = new ConsensusError("time-too-new", "block timestamp too far in the future");
        public static readonly ConsensusError BadVersion = new ConsensusError("bad-version", "block version rejected");
        public static readonly ConsensusError BadMerkleRoot = new ConsensusError("bad-txnmrklroot", "hashMerkleRoot mismatch");
        public static readonly ConsensusError BadBlockLength = new ConsensusError("bad-blk-length", "size limits failed");
        public static readonly ConsensusError BadCoinbaseMissing = new ConsensusError("bad-cb-missing", "first tx is not coinbase");
        public static readonly ConsensusError BadCoinbaseSize = new ConsensusError("bad-cb-length", "invalid coinbase size");
        public static readonly ConsensusError BadMultipleCoinbase = new ConsensusError("bad-cb-multiple", "more than one coinbase");

        public static readonly ConsensusError BadBlockSigOps = new ConsensusError("bad-blk-sigops", "out-of-bounds SigOpCount");

        public static readonly ConsensusError BadTransactionDuplicate = new ConsensusError("bad-txns-duplicate", "duplicate transaction");
        public static readonly ConsensusError BadTransactionNoInput = new ConsensusError("bad-txns-vin-empty", "no input in the transaction");
        public static readonly ConsensusError BadTransactionTooManyInputs = new ConsensusError("bad-txns-too-many-inputs", "too many inputs in the transaction");
        public static readonly ConsensusError BadTransactionNoOutput = new ConsensusError("bad-txns-vout-empty", "no output in the transaction");
        public static readonly ConsensusError BadTransactionOversize = new ConsensusError("bad-txns-oversize", "oversized transaction");
        public static readonly ConsensusError BadTransactionEmptyOutput = new ConsensusError("user-txout-empty", "user transaction output is empty");
        public static readonly ConsensusError BadTransactionNegativeOutput = new ConsensusError("bad-txns-vout-negative", "the transaction contains a negative value output");
        public static readonly ConsensusError BadTransactionTooLargeOutput = new ConsensusError("bad-txns-vout-toolarge", "the transaction contains a too large value output");
        public static readonly ConsensusError BadTransactionTooLargeTotalOutput = new ConsensusError("bad-txns-txouttotal-toolarge", "the sum of outputs'value is too large for this transaction");
        public static readonly ConsensusError BadTransactionDuplicateInputs = new ConsensusError("bad-txns-inputs-duplicate", "duplicate inputs");
        public static readonly ConsensusError BadTransactionNullPrevout = new ConsensusError("bad-txns-prevout-null", "this transaction contains a null prevout");
        public static readonly ConsensusError BadTransactionBIP30 = new ConsensusError("bad-txns-BIP30", "tried to overwrite transaction");
        public static readonly ConsensusError BadTransactionMissingInput = new ConsensusError("bad-txns-inputs-missingorspent", "input missing/spent");

        public static readonly ConsensusError BadCoinbaseAmount = new ConsensusError("bad-cb-amount", "coinbase pays too much");

        public static readonly ConsensusError BadTransactionPrematureCoinbaseSpending = new ConsensusError("bad-txns-premature-spend-of-coinbase", "tried to spend coinbase before maturity");

        public static readonly ConsensusError BadTransactionInputValueOutOfRange = new ConsensusError("bad-txns-inputvalues-outofrange", "input value out of range");
        public static readonly ConsensusError BadTransactionInBelowOut = new ConsensusError("bad-txns-in-belowout", "input value below output value");
        public static readonly ConsensusError BadTransactionNegativeFee = new ConsensusError("bad-txns-fee-negative", "negative fee");
        public static readonly ConsensusError BadTransactionFeeOutOfRange = new ConsensusError("bad-txns-fee-outofrange", "fee out of range");

        public static readonly ConsensusError BadTransactionScriptError = new ConsensusError("bad-txns-script-failed", "a script failed");

        public static readonly ConsensusError ReadTxPrevFailed = new ConsensusError("read-txPrev-failed", "read txPrev failed");

        public static readonly ConsensusError ModifierNotFound = new ConsensusError("modifier-not-found", "unable to get last modifier");
        public static readonly ConsensusError FailedSelectBlock = new ConsensusError("failed-select-block", "unable to select block at round");

        public static readonly ConsensusError BlockTimestampTooFar = new ConsensusError("block-timestamp-to-far", "block timestamp too far in the future");
        public static readonly ConsensusError BlockTimestampTooEarly = new ConsensusError("block-timestamp-to-early", "block timestamp too early");
        public static readonly ConsensusError BadBlockSignature = new ConsensusError("bad-block-signature", "bad block signature");
        public static readonly ConsensusError BlockTimeBeforeTrx = new ConsensusError("block-time-before-trx", "block timestamp earlier than transaction timestamp");
        public static readonly ConsensusError ProofOfWorkTooHigh = new ConsensusError("proof-of-work-too-heigh", "proof of work too heigh");

        public static readonly ConsensusError CheckpointViolation = new ConsensusError("checkpoint-violation", "block header hash does not match the checkpointed value");
    }
}
