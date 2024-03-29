﻿using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.OpenAsset;
using NBitcoin.Stealth;

namespace NBitcoin
{
    public interface IColoredCoin : ICoin
    {
        AssetId AssetId
        {
            get;
        }
        Coin Bearer
        {
            get;
        }
    }
    public interface ICoin
    {
        IMoney Amount
        {
            get;
        }
        OutPoint Outpoint
        {
            get;
        }
        TxOut TxOut
        {
            get;
        }

        /// <summary>
        /// Returns the script actually signed and executed
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Additional information needed to get the ScriptCode</exception>
        /// <returns>The executed script</returns>
        Script GetScriptCode(Network network);
        void OverrideScriptCode(Script scriptCode);
        bool CanGetScriptCode(Network network);
        HashVersion GetHashVersion(Network network);
    }

    public class IssuanceCoin : IColoredCoin
    {
        public IssuanceCoin()
        {

        }
        public IssuanceCoin(Coin bearer)
        {
            Bearer = bearer;
        }

        public IssuanceCoin(OutPoint outpoint, TxOut txout)
        {
            Bearer = new Coin(outpoint, txout);
        }


        public AssetId AssetId
        {
            get
            {
                return Bearer.TxOut.ScriptPubKey.Hash.ToAssetId();
            }
        }

        public Uri DefinitionUrl
        {
            get;
            set;
        }

        #region ICoin Members


        public Money Amount
        {
            get
            {
                return Bearer.TxOut.Value;
            }
            set
            {
                Bearer.TxOut.Value = value;
            }
        }

        public TxOut TxOut
        {
            get
            {
                return Bearer.TxOut;
            }
        }

        #endregion

        public Script ScriptPubKey
        {
            get
            {
                return Bearer.TxOut.ScriptPubKey;
            }
        }

        #region IColoredCoin Members


        public Coin Bearer
        {
            get;
            set;
        }


        public OutPoint Outpoint
        {
            get
            {
                return Bearer.Outpoint;
            }
        }

        #endregion

        #region IColoredCoin Members

        AssetId IColoredCoin.AssetId
        {
            get
            {
                return AssetId;
            }
        }

        Coin IColoredCoin.Bearer
        {
            get
            {
                return Bearer;
            }
        }

        #endregion

        #region ICoin Members

        IMoney ICoin.Amount
        {
            get
            {
                return Amount;
            }
        }

        OutPoint ICoin.Outpoint
        {
            get
            {
                return Outpoint;
            }
        }

        TxOut ICoin.TxOut
        {
            get
            {
                return TxOut;
            }
        }

        #endregion

        #region ICoin Members


        public Script GetScriptCode(Network network)
        {
            return this.Bearer.GetScriptCode(network);
        }

        public bool CanGetScriptCode(Network network)
        {
            return this.Bearer.CanGetScriptCode(network);
        }

        public HashVersion GetHashVersion(Network network)
        {
            return this.Bearer.GetHashVersion(network);
        }

        public void OverrideScriptCode(Script scriptCode)
        {
            this.Bearer.OverrideScriptCode(scriptCode);
        }

        #endregion
    }

    public class ColoredCoin : IColoredCoin
    {
        public ColoredCoin()
        {

        }
        public ColoredCoin(AssetMoney asset, Coin bearer)
        {
            Amount = asset;
            Bearer = bearer;
        }

        public ColoredCoin(Transaction tx, ColoredEntry entry)
            : this(entry.Asset, new Coin(tx, entry.Index))
        {
            if(tx == null)
                throw new ArgumentNullException("tx");
            if(entry == null)
                throw new ArgumentNullException("entry");
        }

        public AssetId AssetId
        {
            get
            {
                return Amount.Id;
            }
        }

        public AssetMoney Amount
        {
            get;
            set;
        }

        [Obsolete("Use Amount instead")]
        public AssetMoney Asset
        {
            get
            {
                return Amount;
            }
            set
            {
                Amount = value;
            }
        }

        public Coin Bearer
        {
            get;
            set;
        }

        public TxOut TxOut
        {
            get
            {
                return Bearer.TxOut;
            }
        }

        #region ICoin Members

        public OutPoint Outpoint
        {
            get
            {
                return Bearer.Outpoint;
            }
        }

        public Script ScriptPubKey
        {
            get
            {
                return Bearer.ScriptPubKey;
            }
        }

        #endregion

        public static IEnumerable<ColoredCoin> Find(Transaction tx, ColoredTransaction colored)
        {
            return Find(null, tx, colored);
        }
        public static IEnumerable<ColoredCoin> Find(uint256 txId, Transaction tx, ColoredTransaction colored)
        {
            if(colored == null)
                throw new ArgumentNullException("colored");
            if(tx == null)
                throw new ArgumentNullException("tx");
            if(txId == null)
                txId = tx.GetHash();
            foreach(var entry in colored.Issuances.Concat(colored.Transfers))
            {
                var txout = tx.Outputs[entry.Index];
                yield return new ColoredCoin(entry.Asset, new Coin(new OutPoint(txId, entry.Index), txout));
            }
        }

        public static IEnumerable<ColoredCoin> Find(Transaction tx, IColoredTransactionRepository repo)
        {
            return Find(null, tx, repo);
        }
        public static IEnumerable<ColoredCoin> Find(uint256 txId, Transaction tx, IColoredTransactionRepository repo)
        {
            if(txId == null)
                txId = tx.GetHash();
            var colored = tx.GetColoredTransaction(repo);
            return Find(txId, tx, colored);
        }

        #region IColoredCoin Members

        AssetId IColoredCoin.AssetId
        {
            get
            {
                return AssetId;
            }
        }

        Coin IColoredCoin.Bearer
        {
            get
            {
                return Bearer;
            }
        }

        #endregion

        #region ICoin Members

        IMoney ICoin.Amount
        {
            get
            {
                return Amount;
            }
        }

        OutPoint ICoin.Outpoint
        {
            get
            {
                return Outpoint;
            }
        }

        TxOut ICoin.TxOut
        {
            get
            {
                return TxOut;
            }
        }

        public Script GetScriptCode(Network network)
        {
            return this.Bearer.GetScriptCode(network);
        }

        public bool CanGetScriptCode(Network network)
        {
            return this.Bearer.CanGetScriptCode(network);
        }

        public HashVersion GetHashVersion(Network network)
        {
            return this.Bearer.GetHashVersion(network);
        }

        public void OverrideScriptCode(Script scriptCode)
        {
            this.Bearer.OverrideScriptCode(scriptCode);
        }

        #endregion
    }
    public class Coin : ICoin
    {
        public Coin()
        {

        }
        public Coin(OutPoint fromOutpoint, TxOut fromTxOut)
        {
            Outpoint = fromOutpoint;
            TxOut = fromTxOut;
        }

        public Coin(Transaction fromTx, uint fromOutputIndex)
        {
            if(fromTx == null)
                throw new ArgumentNullException("fromTx");
            Outpoint = new OutPoint(fromTx, fromOutputIndex);
            TxOut = fromTx.Outputs[fromOutputIndex];
        }

        public Coin(Transaction fromTx, TxOut fromOutput)
        {
            if(fromTx == null)
                throw new ArgumentNullException("fromTx");
            if(fromOutput == null)
                throw new ArgumentNullException("fromOutput");
            uint outputIndex = (uint)fromTx.Outputs.FindIndex(r => Object.ReferenceEquals(fromOutput, r));
            Outpoint = new OutPoint(fromTx, outputIndex);
            TxOut = fromOutput;
        }
        public Coin(IndexedTxOut txOut)
        {
            Outpoint = new OutPoint(txOut.Transaction.GetHash(), txOut.N);
            TxOut = txOut.TxOut;
        }

        public Coin(uint256 fromTxHash, uint fromOutputIndex, Money amount, Script scriptPubKey)
        {
            Outpoint = new OutPoint(fromTxHash, fromOutputIndex);
            TxOut = new TxOut(amount, scriptPubKey);
        }

        public virtual Script GetScriptCode(Network network)
        {
            if(!CanGetScriptCode(network))
                throw new InvalidOperationException("You need to provide P2WSH or P2SH redeem script with Coin.ToScriptCoin()");
            if(_OverrideScriptCode != null)
                return _OverrideScriptCode;
            var key = PayToWitPubKeyHashTemplate.Instance.ExtractScriptPubKeyParameters(network, ScriptPubKey);
            if(key != null)
                return key.AsKeyId().ScriptPubKey;
            return ScriptPubKey;
        }

        public virtual bool CanGetScriptCode(Network network)
        {
                return _OverrideScriptCode != null || !ScriptPubKey.IsPayToScriptHash(network) && !PayToWitScriptHashTemplate.Instance.CheckScriptPubKey(network, ScriptPubKey);
        }

        public virtual HashVersion GetHashVersion(Network network)
        {
            if(PayToWitTemplate.Instance.CheckScriptPubKey(network, ScriptPubKey))
                return HashVersion.Witness;
            return HashVersion.Original;
        }

        public ScriptCoin ToScriptCoin(Script redeemScript)
        {
            if(redeemScript == null)
                throw new ArgumentNullException("redeemScript");
            var scriptCoin = this as ScriptCoin;
            if(scriptCoin != null)
                return scriptCoin;
            return new ScriptCoin(this, redeemScript);
        }

        public ColoredCoin ToColoredCoin(AssetId asset, ulong quantity)
        {
            return ToColoredCoin(new AssetMoney(asset, quantity));
        }
        public ColoredCoin ToColoredCoin(BitcoinAssetId asset, ulong quantity)
        {
            return ToColoredCoin(new AssetMoney(asset, quantity));
        }
        public ColoredCoin ToColoredCoin(AssetMoney asset)
        {
            return new ColoredCoin(asset, this);
        }

        public OutPoint Outpoint
        {
            get;
            set;
        }
        public TxOut TxOut
        {
            get;
            set;
        }

        #region ICoin Members


        public Money Amount
        {
            get
            {
                if(TxOut == null)
                    return Money.Zero;
                return TxOut.Value;
            }
            set
            {
                EnsureTxOut();
                TxOut.Value = value;
            }
        }

        private void EnsureTxOut()
        {
            if(TxOut == null)
                TxOut = new TxOut();
        }

        protected Script _OverrideScriptCode;
        public void OverrideScriptCode(Script scriptCode)
        {
            _OverrideScriptCode = scriptCode;
        }

        #endregion

        public Script ScriptPubKey
        {
            get
            {
                if(TxOut == null)
                    return Script.Empty;
                return TxOut.ScriptPubKey;
            }
            set
            {
                EnsureTxOut();
                TxOut.ScriptPubKey = value;
            }
        }

        #region ICoin Members

        IMoney ICoin.Amount
        {
            get
            {
                return Amount;
            }
        }

        OutPoint ICoin.Outpoint
        {
            get
            {
                return Outpoint;
            }
        }

        TxOut ICoin.TxOut
        {
            get
            {
                return TxOut;
            }
        }

        #endregion
    }


    public enum RedeemType
    {
        P2SH,
        WitnessV0
    }


    /// <summary>
    /// Represent a coin which need a redeem script to be spent (P2SH or P2WSH)
    /// </summary>
    public class ScriptCoin : Coin
    {
        public ScriptCoin()
        {

        }

        internal ScriptCoin(OutPoint fromOutpoint, TxOut fromTxOut, Script redeem)
            : base(fromOutpoint, fromTxOut)
        {
            Redeem = redeem;
        }

        internal ScriptCoin(Transaction fromTx, uint fromOutputIndex, Script redeem)
            : base(fromTx, fromOutputIndex)
        {
            Redeem = redeem;
        }

        internal ScriptCoin(Transaction fromTx, TxOut fromOutput, Script redeem)
            : base(fromTx, fromOutput)
        {
            Redeem = redeem;
        }

        internal ScriptCoin(ICoin coin, Script redeem)
            : base(coin.Outpoint, coin.TxOut)
        {
            Redeem = redeem;
        }

        internal ScriptCoin(IndexedTxOut txOut, Script redeem)
            : base(txOut)
        {
            Redeem = redeem;
        }

        internal ScriptCoin(uint256 txHash, uint outputIndex, Money amount, Script scriptPubKey, Script redeem)
            : base(txHash, outputIndex, amount, scriptPubKey)
        {
            Redeem = redeem;
        }

        public static ScriptCoin Create(Network network, OutPoint fromOutpoint, TxOut fromTxOut, Script redeem)
        {
            return new ScriptCoin(fromOutpoint, fromTxOut, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, Transaction fromTx, uint fromOutputIndex, Script redeem)
        {
            return new ScriptCoin(fromTx, fromOutputIndex, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, Transaction fromTx, TxOut fromOutput, Script redeem)
        {
            return new ScriptCoin(fromTx, fromOutput, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, ICoin coin, Script redeem)
        {
            return new ScriptCoin(coin, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, IndexedTxOut txOut, Script redeem)
        {
            return new ScriptCoin(txOut, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, uint256 txHash, uint outputIndex, Money amount, Script scriptPubKey, Script redeem)
        {
            return new ScriptCoin(txHash, outputIndex, amount, scriptPubKey, redeem).AssertCoherent(network);
        }

        public bool IsP2SH
        {
            get
            {
                return ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_HASH160;
            }
        }

        public Script GetP2SHRedeem()
        {
            if(!IsP2SH)
                return null;
            var p2shRedeem = RedeemType == RedeemType.P2SH ? Redeem :
                            RedeemType == RedeemType.WitnessV0 ? Redeem.WitHash.ScriptPubKey :
                            null;
            if(p2shRedeem == null)
                throw new NotSupportedException("RedeemType not supported for getting the P2SH script, contact the library author");
            return p2shRedeem;
        }

        public RedeemType RedeemType
        {
            get
            {
                return
                    Redeem.Hash.ScriptPubKey == TxOut.ScriptPubKey ?
                    RedeemType.P2SH :
                    RedeemType.WitnessV0;
            }
        }

        public ScriptCoin AssertCoherent(Network network)
        {
            if(Redeem == null)
                throw new ArgumentException("redeem cannot be null", "redeem");

            var expectedDestination = GetRedeemHash(network, TxOut.ScriptPubKey);
            if(expectedDestination == null)
            {
                throw new ArgumentException("the provided scriptPubKey is not P2SH or P2WSH");
            }
            if(expectedDestination is ScriptId)
            {
                if(PayToWitScriptHashTemplate.Instance.CheckScriptPubKey(network, Redeem))
                {
                    throw new ArgumentException("The redeem script provided must be the witness one, not the P2SH one");
                }

                if(expectedDestination.ScriptPubKey != Redeem.Hash.ScriptPubKey)
                {
                    if(Redeem.WitHash.ScriptPubKey.Hash.ScriptPubKey != expectedDestination.ScriptPubKey)
                        throw new ArgumentException("The redeem provided does not match the scriptPubKey of the coin");
                }
            }
            else if(expectedDestination is WitScriptId)
            {
                if(expectedDestination.ScriptPubKey != Redeem.WitHash.ScriptPubKey)
                    throw new ArgumentException("The redeem provided does not match the scriptPubKey of the coin");
            }
            else
                throw new NotSupportedException("Not supported redeemed scriptPubkey");

            return this;
        }


        public Script Redeem
        {
            get;
            set;
        }

        public override Script GetScriptCode(Network network)
        {
            if(!CanGetScriptCode(network))
                throw new InvalidOperationException("You need to provide the P2WSH redeem script with ScriptCoin.ToScriptCoin()");
            if(_OverrideScriptCode != null)
                return _OverrideScriptCode;
            var key = PayToWitPubKeyHashTemplate.Instance.ExtractScriptPubKeyParameters(network, Redeem);
            if(key != null)
                return key.AsKeyId().ScriptPubKey;
            return Redeem;
        }

        public override bool CanGetScriptCode(Network network)
        {
                return _OverrideScriptCode != null || !IsP2SH || !PayToWitScriptHashTemplate.Instance.CheckScriptPubKey(network, Redeem);
        }

        public override HashVersion GetHashVersion(Network network)
        {
            var isWitness = PayToWitTemplate.Instance.CheckScriptPubKey(network, ScriptPubKey) ||
                            PayToWitTemplate.Instance.CheckScriptPubKey(network, Redeem) ||
                            RedeemType == NBitcoin.RedeemType.WitnessV0;
            return isWitness ? HashVersion.Witness : HashVersion.Original;
        }

        /// <summary>
        /// Returns the hash contained in the scriptPubKey (P2SH or P2WSH)
        /// </summary>
        /// <param name="scriptPubKey">The scriptPubKey</param>
        /// <returns>The hash of the scriptPubkey</returns>
        public static TxDestination GetRedeemHash(Network network, Script scriptPubKey)
        {
            if(scriptPubKey == null)
                throw new ArgumentNullException("scriptPubKey");
            return PayToScriptHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey) as TxDestination
                    ??
                    PayToWitScriptHashTemplate.Instance.ExtractScriptPubKeyParameters(network, scriptPubKey);
        }
    }

    public class StealthCoin : Coin
    {
        public StealthCoin()
        {
        }
        public StealthCoin(OutPoint outpoint, TxOut txOut, Script redeem, StealthMetadata stealthMetadata, BitcoinStealthAddress address)
            : base(outpoint, txOut)
        {
            StealthMetadata = stealthMetadata;
            Address = address;
            Redeem = redeem;
        }
        public StealthMetadata StealthMetadata
        {
            get;
            set;
        }

        public BitcoinStealthAddress Address
        {
            get;
            set;
        }

        public Script Redeem
        {
            get;
            set;
        }

        public override Script GetScriptCode(Network network)
        {
            if(_OverrideScriptCode != null)
                return _OverrideScriptCode;
            if(Redeem == null)
                return base.GetScriptCode(network);
            else
                return ScriptCoin.Create(network, this, Redeem).GetScriptCode(network);
        }

        public override HashVersion GetHashVersion(Network network)
        {
            if(Redeem == null)
                return base.GetHashVersion(network);
            else
                return ScriptCoin.Create(network, this, Redeem).GetHashVersion(network);
        }

        /// <summary>
        /// Scan the Transaction for StealthCoin given address and scan key
        /// </summary>
        /// <param name="tx">The transaction to scan</param>
        /// <param name="address">The stealth address</param>
        /// <param name="scan">The scan private key</param>
        /// <returns></returns>
        public static StealthCoin Find(Transaction tx, BitcoinStealthAddress address, Key scan)
        {
            var payment = address.GetPayments(tx, scan).FirstOrDefault();
            if(payment == null)
                return null;
            var txId = tx.GetHash();
            var txout = tx.Outputs.First(o => o.ScriptPubKey == payment.ScriptPubKey);
            return new StealthCoin(new OutPoint(txId, tx.Outputs.IndexOf(txout)), txout, payment.Redeem, payment.Metadata, address);
        }

        public StealthPayment GetPayment()
        {
            return new StealthPayment(TxOut.ScriptPubKey, Redeem, StealthMetadata);
        }

        public PubKey[] Uncover(PubKey[] spendPubKeys, Key scanKey)
        {
            var pubKeys = new PubKey[spendPubKeys.Length];
            for(int i = 0; i < pubKeys.Length; i++)
            {
                pubKeys[i] = spendPubKeys[i].UncoverReceiver(scanKey, StealthMetadata.EphemKey);
            }
            return pubKeys;
        }

        public Key[] Uncover(Key[] spendKeys, Key scanKey)
        {
            var keys = new Key[spendKeys.Length];
            for(int i = 0; i < keys.Length; i++)
            {
                keys[i] = spendKeys[i].Uncover(scanKey, StealthMetadata.EphemKey);
            }
            return keys;
        }
    }
}
