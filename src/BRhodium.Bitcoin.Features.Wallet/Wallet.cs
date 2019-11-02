﻿using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonConverters;
using System.ComponentModel;
using DateTimeOffsetConverter = BRhodium.Node.Utilities.JsonConverters.DateTimeOffsetConverter;
using ProtoBuf;
using ProtoBuf.Meta;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A wallet.
    /// </summary>
    [ProtoContract]
    public class Wallet:  IProtoBufSerializeable
    {
        /// <summary>
        /// Initializes a new instance of the wallet.
        /// </summary>
        public Wallet()
        {
            this.AccountsRoot = new List<AccountRoot>();
        }
        private bool changed = false;

        [ProtoMember(1)]
        [JsonIgnore]
        public long Id { get; set; }

        /// <summary>
        /// The name of this wallet.
        /// </summary>
        [ProtoMember(2)]
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// The seed for this wallet, password encrypted.
        /// </summary>
        [ProtoMember(3)]
        [JsonProperty(PropertyName = "encryptedSeed")]
        public string EncryptedSeed { get; set; }

   
        /// <summary>
        /// The chain code.
        /// </summary>
        [ProtoMember(4)]
        [JsonProperty(PropertyName = "chainCode")]
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] ChainCode { get; set; }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>

        [ProtoMember(7)]
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// Gets or sets block locator.
        /// </summary>
        //[ProtoMember(5)]//skipping from protobuff serialization as saved as separate high churn entity
        [JsonProperty(PropertyName = "blockLocator", ItemConverterType = typeof(UInt256JsonConverter))]
        public List<uint256> BlockLocator { get; set; }

        /// <summary>
        /// The network this wallet is for.
        /// </summary>
        [ProtoMember(6)]
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        public Network Network { get; set; }
       

        /// <summary>
        /// The root of the accounts tree.
        /// </summary>

        [ProtoMember(8)]
        [JsonProperty(PropertyName = "accountsRoot")]
        public List<AccountRoot> AccountsRoot { get; set; }


        /// <summary>
        /// This is sets a runtime flag to show if wallet has been changed since last save operation.
        /// </summary>
        public void Changed()
        {
            this.changed = true;
        }
        /// <summary>
        /// This resets a runtime chaged flag after wallet has been saved;
        /// </summary>
        public void Saved()
        {
            this.changed = false;
        }
        /// <summary>
        /// Returns bool describing if wallet has been changed since last save.
        /// </summary>
        /// <returns></returns>
        public bool IsChanged()
        {
            return this.changed;
        }

        /// <summary>
        /// Gets the accounts the wallet has for this type of coin.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns>The accounts in the wallet corresponding to this type of coin.</returns>
        public IEnumerable<HdAccount> GetAccountsByCoinType(CoinType coinType)
        {
            return this.AccountsRoot.Where(a => a.CoinType == coinType).SelectMany(a => a.Accounts);
        }

        /// <summary>
        /// Gets an account from the wallet's accounts.
        /// </summary>
        /// <param name="accountName">The name of the account to retrieve.</param>
        /// <param name="coinType">The type of the coin this account is for.</param>
        /// <returns>The requested account.</returns>
        public HdAccount GetAccountByCoinType(string accountName, CoinType coinType)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);
            return accountRoot?.GetAccountByName(accountName);
        }


        public HdAccount GetAccountByHdPathCoinType(string hdPath, CoinType coinType)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);
            return accountRoot?.GetAccountByHdPath(hdPath);
        }

        /// <summary>
        /// Update the last block synced height and hash in the wallet.
        /// </summary>
        /// <param name="coinType">The type of the coin this account is for.</param>
        /// <param name="block">The block whose details are used to update the wallet.</param>
        public void SetLastBlockDetailsByCoinType(CoinType coinType, ChainedHeader block)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);

            if (accountRoot == null) return;

            accountRoot.LastBlockSyncedHeight = block.Height;
            accountRoot.LastBlockSyncedHash = block.HashBlock;
        }

        /// <summary>
        /// Gets all the transactions by coin type.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetAllTransactionsByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            foreach (TransactionData txData in accounts.SelectMany(x => x.ExternalAddresses).SelectMany(x => x.Transactions))
            {
                yield return txData;
            }

            foreach (TransactionData txData in accounts.SelectMany(x => x.InternalAddresses).SelectMany(x => x.Transactions))
            {
                yield return txData;
            }
        }

        /// <summary>
        /// Gets all the pub keys contained in this wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<Script> GetAllPubKeysByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            foreach (Script script in accounts.SelectMany(x => x.ExternalAddresses).Select(x => x.ScriptPubKey))
            {
                yield return script;
            }

            foreach (Script script in accounts.SelectMany(x => x.InternalAddresses).Select(x => x.ScriptPubKey))
            {
                yield return script;
            }
        }

        /// <summary>
        /// Gets all the addresses contained in this wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns>A list of all the addresses contained in this wallet.</returns>
        public IEnumerable<HdAddress> GetAllAddressesByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            List<HdAddress> allAddresses = new List<HdAddress>();
            foreach (HdAccount account in accounts)
            {
                allAddresses.AddRange(account.GetCombinedAddresses());
            }
            return allAddresses;
        }


        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="password">The password used to decrypt the wallet's <see cref="EncryptedSeed"/>.</param>
        /// <param name="coinType">The type of coin this account is for.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, CoinType coinType, DateTimeOffset accountCreationTime)
        {
            Guard.NotEmpty(password, nameof(password));

            var accountRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);
            return accountRoot.AddNewAccount(password, this.EncryptedSeed, this.ChainCode, this.Network, accountCreationTime);
        }

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account.</returns>
        public HdAccount GetFirstUnusedAccount(CoinType coinType)
        {
            // Get the accounts root for this type of coin.
            var accountsRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);

            if (accountsRoot.Accounts.Any())
            {
                // Get an unused account.
                var firstUnusedAccount = accountsRoot.GetFirstUnusedAccount();
                if (firstUnusedAccount != null)
                {
                    return firstUnusedAccount;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether the wallet contains the specified address.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <returns>A value indicating whether the wallet contains the specified address.</returns>
        public bool ContainsAddress(HdAddress address)
        {
            if (!this.AccountsRoot.Any(r => r.Accounts.Any(
                a => a.ExternalAddresses.Any(i => i.Address == address.Address) ||
                     a.InternalAddresses.Any(i => i.Address == address.Address))))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the extended private key for the given address.
        /// </summary>
        /// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
        /// <param name="address">The address to get the private key for.</param>
        /// <returns>The extended private key.</returns>
        public ISecret GetExtendedPrivateKeyForAddress(string password, HdAddress address)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(address, nameof(address));

            // Check if the wallet contains the address.
            if (!this.ContainsAddress(address))
            {
                throw new WalletException("Address not found on wallet.");
            }

            // get extended private key
            Key privateKey = HdOperations.DecryptSeed(this.EncryptedSeed, password, this.Network);
            return HdOperations.GetExtendedPrivateKey(privateKey, this.ChainCode, address.HdPath, this.Network);
        }

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin to get transactions from.</param>
        /// <param name="currentChainHeight">Height of the current chain, used in calculating the number of confirmations.</param>
        /// <param name="confirmations">The number of confirmations required to consider a transaction spendable.</param>
        /// <returns>A collection of spendable outputs.</returns>
        public IEnumerable<UnspentOutputReference> GetAllSpendableTransactions(CoinType coinType, int currentChainHeight, int confirmations = 0)
        {
            IEnumerable<HdAccount> accounts = this.GetAccountsByCoinType(coinType);

            return accounts
                .SelectMany(x => x.GetSpendableTransactions(currentChainHeight, confirmations));
        }

    }

    /// <summary>
    /// The root for the accounts for any type of coins.
    /// </summary>
    [ProtoContract]
    public class AccountRoot : IProtoBufSerializeable
    {
        private List<HdAccount> _accounts;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        public AccountRoot()
        {
            this.Accounts = new List<HdAccount>();
        }

        /// <summary>
        /// The type of coin, Bitcoin or BRhodium.
        /// </summary>
        [ProtoMember(1)]
        [JsonProperty(PropertyName = "coinType")]
        public CoinType CoinType { get; set; }

        /// <summary>
        /// The height of the last block that was synced.
        /// </summary>
        [ProtoMember(2)]
        [JsonProperty(PropertyName = "lastBlockSyncedHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? LastBlockSyncedHeight { get; set; }

        /// <summary>
        /// The hash of the last block that was synced.
        /// </summary>
        [ProtoMember(3)]
        [JsonProperty(PropertyName = "lastBlockSyncedHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 LastBlockSyncedHash { get; set; }

        /// <summary>
        /// The accounts used in the wallet.
        /// </summary>
        [ProtoMember(4)]
        [JsonProperty(PropertyName = "accounts")]
        public List<HdAccount> Accounts { get; set; }

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account</returns>
        public HdAccount GetFirstUnusedAccount()
        {
            if (this.Accounts == null)
                return null;

            var unusedAccounts = this.Accounts.Where(acc => !acc.ExternalAddresses.Any() && !acc.InternalAddresses.Any()).ToList();
            if (!unusedAccounts.Any())
                return null;

            // gets the unused account with the lowest index
            var index = unusedAccounts.Min(a => a.Index);
            return unusedAccounts.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the account matching the name passed as a parameter.
        /// </summary>
        /// <param name="accountName">The name of the account to get.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public HdAccount GetAccountByName(string accountName)
        {
            if (this.Accounts == null)
                throw new WalletException($"No account with the name {accountName} could be found.");

            // get the account
            HdAccount account = this.Accounts.SingleOrDefault(a => a.Name == accountName);
            if (account == null)
                throw new WalletException($"No account with the name {accountName} could be found.");

            return account;
        }

        public HdAccount GetAccountByHdPath(string hdPath)
        {
            if (this.Accounts == null)
                throw new WalletException($"No account with the hd path {hdPath} could be found.");

            // get the account
            HdAccount account = this.Accounts.SingleOrDefault(a => hdPath.Contains(a.HdPath));
            if (account == null)
                throw new WalletException($"No account with the hd path {hdPath} could be found.");

            return account;
        }

        /// <summary>
        /// Adds an account to the current account root.
        /// </summary>
        /// <remarks>The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains transactions.
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/></remarks>
        /// <param name="password">The password used to decrypt the wallet's encrypted seed.</param>
        /// <param name="encryptedSeed">The encrypted private key for this wallet.</param>
        /// <param name="chainCode">The chain code for this wallet.</param>
        /// <param name="network">The network for which this account will be created.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, string encryptedSeed, byte[] chainCode, Network network, DateTimeOffset accountCreationTime)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            // Get the current collection of accounts.
            var accounts = this.Accounts.ToList();

            int newAccountIndex = 0;
            if (accounts.Any())
            {
                newAccountIndex = accounts.Max(a => a.Index) + 1;
            }

            // Get the extended pub key used to generate addresses for this account.
            string accountHdPath = HdOperations.GetAccountHdPath((int)this.CoinType, newAccountIndex);
            Key privateKey = HdOperations.DecryptSeed(encryptedSeed, password, network);
            ExtPubKey accountExtPubKey = HdOperations.GetExtendedPublicKey(privateKey, chainCode, accountHdPath);

            var newAccount = new HdAccount
            {
                Index = newAccountIndex,
                ExtendedPubKey = accountExtPubKey.ToString(network),
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = $"account {newAccountIndex}",
                HdPath = accountHdPath,
                CreationTime = accountCreationTime
            };

            accounts.Add(newAccount);
            this.Accounts = accounts;

            return newAccount;
        }


    }

    /// <summary>
    /// An HD account's details.
    /// </summary>
    [ProtoContract]
    public class HdAccount : IProtoBufSerializeable
    {
        public HdAccount()
        {
            this.ExternalAddresses = new List<HdAddress>();
            this.InternalAddresses = new List<HdAddress>();
        }

        /// <summary>
        /// The index of the account.
        /// </summary>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        [ProtoMember(1)]
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The name of this account.
        /// </summary>
        [ProtoMember(2)]
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }


        /// <summary>
        /// A path to the account as defined in BIP44.
        /// </summary>
        [ProtoMember(3)]
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }


        /// <summary>
        /// An extended pub key used to generate addresses.
        /// </summary>
        [ProtoMember(4)]
        [JsonProperty(PropertyName = "extPubKey")]
        public string ExtendedPubKey { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [ProtoMember(5)]
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// The list of external addresses, typically used for receiving money.
        /// </summary>
        [ProtoMember(6)]
        [JsonProperty(PropertyName = "externalAddresses")]
        public ICollection<HdAddress> ExternalAddresses { get; set; }

        /// <summary>
        /// The list of internal addresses, typically used to receive change.
        /// </summary>
        [ProtoMember(7)]
        [JsonProperty(PropertyName = "internalAddresses")]
        public ICollection<HdAddress> InternalAddresses { get; set; }

        [JsonIgnore]
        public long Id { get; internal set; }

        /// <summary>
        /// Gets the type of coin this account is for.
        /// </summary>
        /// <returns>A <see cref="CoinType"/>.</returns>
        public CoinType GetCoinType()
        {
            return (CoinType)HdOperations.GetCoinType(this.HdPath);
        }

        /// <summary>
        /// Gets the first receiving address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        public HdAddress GetFirstUnusedReceivingAddress()
        {
            return this.GetFirstUnusedAddress(false);
        }

        /// <summary>
        /// Gets the first change address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        public HdAddress GetFirstUnusedChangeAddress()
        {
            return this.GetFirstUnusedAddress(true);
        }

        /// <summary>
        /// Gets the first receiving address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        private HdAddress GetFirstUnusedAddress(bool isChange)
        {
            IEnumerable<HdAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;
            if (addresses == null)
                return null;

            var unusedAddresses = addresses.Where(acc => !acc.Transactions.Any()).ToList();
            if (!unusedAddresses.Any())
            {
                return null;
            }

            // gets the unused address with the lowest index
            var index = unusedAddresses.Min(a => a.Index);
            return unusedAddresses.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the last address that contains transactions.
        /// </summary>
        /// <param name="isChange">Whether the address is a change (internal) address or receiving (external) address.</param>
        /// <returns></returns>
        public HdAddress GetLastUsedAddress(bool isChange)
        {
            IEnumerable<HdAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;
            if (addresses == null)
                return null;

            var usedAddresses = addresses.Where(acc => acc.Transactions.Any()).ToList();
            if (!usedAddresses.Any())
            {
                return null;
            }

            // gets the used address with the highest index
            var index = usedAddresses.Max(a => a.Index);
            return usedAddresses.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets a collection of transactions by id.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetTransactionsById(uint256 id)
        {
            Guard.NotNull(id, nameof(id));

            var addresses = this.GetCombinedAddresses();
            return addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.Id == id));
        }

        /// <summary>
        /// Gets a collection of transactions with spendable outputs.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetSpendableTransactions()
        {
            var addresses = this.GetCombinedAddresses();
            return addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.IsSpendable()));
        }

        /// <summary>
        /// Get the accounts total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money ConfirmedAmount, Money UnConfirmedAmount) GetSpendableAmount()
        {
            var allTransactions = this.ExternalAddresses.SelectMany(a => a.Transactions)
                .Concat(this.InternalAddresses.SelectMany(i => i.Transactions)).ToList();

            var confirmed = allTransactions.Sum(t => t.SpendableAmount(true));
            var total = allTransactions.Sum(t => t.SpendableAmount(false));

            return (confirmed, total - confirmed);
        }

        /// <summary>
        /// Finds the addresses in which a transaction is contained.
        /// </summary>
        /// <remarks>
        /// Returns a collection because a transaction can be contained in a change address as well as in a receive address (as a spend).
        /// </remarks>
        /// <param name="predicate">A predicate by which to filter the transactions.</param>
        /// <returns></returns>
        public IEnumerable<HdAddress> FindAddressesForTransaction(Func<TransactionData, bool> predicate)
        {
            Guard.NotNull(predicate, nameof(predicate));

            var addresses = this.GetCombinedAddresses();
            return addresses.Where(t => t.Transactions != null).Where(a => a.Transactions.Any(predicate));
        }

        /// <summary>
        /// Return both the external and internal (change) address from an account.
        /// </summary>
        /// <returns>All addresses that belong to this account.</returns>
        public IEnumerable<HdAddress> GetCombinedAddresses()
        {
            IEnumerable<HdAddress> addresses = new List<HdAddress>();
            if (this.ExternalAddresses != null)
            {
                addresses = this.ExternalAddresses;
            }

            if (this.InternalAddresses != null)
            {
                addresses = addresses.Concat(this.InternalAddresses);
            }

            return addresses;
        }

        /// <summary>
        /// Creates a number of additional addresses in the current account.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <param name="network">The network these addresses will be for.</param>
        /// <param name="addressesQuantity">The number of addresses to create.</param>
        /// <param name="isChange">Whether the addresses added are change (internal) addresses or receiving (external) addresses.</param>
        /// <returns>The created addresses.</returns>
        public IEnumerable<HdAddress> CreateAddresses(Network network, int addressesQuantity, bool isChange = false)
        {
            var addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;

            // Get the index of the last address.
            int firstNewAddressIndex = 0;
            if (addresses.Any())
            {
                firstNewAddressIndex = addresses.Max(add => add.Index) + 1;
            }

            List<HdAddress> addressesCreated = new List<HdAddress>();
            for (int i = firstNewAddressIndex; i < firstNewAddressIndex + addressesQuantity; i++)
            {
                // Generate a new address.
                PubKey pubkey = HdOperations.GeneratePublicKey(this.ExtendedPubKey, i, isChange);
                BitcoinPubKeyAddress address = pubkey.GetAddress(network);

                // Add the new address details to the list of addresses.
                HdAddress newAddress = new HdAddress
                {
                    Index = i,
                    HdPath = HdOperations.CreateHdPath((int)this.GetCoinType(), this.Index, i, isChange),
                    ScriptPubKey = address.ScriptPubKey,
                    Pubkey = pubkey.ScriptPubKey,
                    Address = address.ToString(),
                    Transactions = new List<TransactionData>()
                };

                addresses.Add(newAddress);
                addressesCreated.Add(newAddress);
            }

            if (isChange)
            {
                this.InternalAddresses = addresses;
            }
            else
            {
                this.ExternalAddresses = addresses;
            }

            return addressesCreated;
        }

        /// <summary>
        /// Creates a number of additional addresses in the current account.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <param name="network">The network.</param>
        /// <param name="pubKey">The pub key.</param>
        /// <returns>The created address.</returns>
        public HdAddress CreateAddresses(Network network, string pubKey)
        {
            var addresses = this.ExternalAddresses;

            // Get the index of the last address.
            int firstNewAddressIndex = 0;
            if (addresses.Any())
            {
                firstNewAddressIndex = addresses.Max(add => add.Index) + 1;
            }

            var pubkey = new PubKey(pubKey);
            BitcoinPubKeyAddress address = pubkey.GetAddress(network);

            // Add the new address details to the list of addresses.
            HdAddress newAddress = new HdAddress
            {
                Index = firstNewAddressIndex,
                HdPath = HdOperations.CreateHdPath((int)this.GetCoinType(), this.Index, firstNewAddressIndex),
                ScriptPubKey = address.ScriptPubKey,
                Pubkey = pubkey.ScriptPubKey,
                Address = address.ToString(),
                Transactions = new List<TransactionData>()
            };

            addresses.Add(newAddress);

            this.ExternalAddresses = addresses;

            return newAddress;
        }

        /// <summary>
        /// Imports the address.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <param name="base58Address">The base58 address.</param>
        /// <returns>The created address.</returns>
        public HdAddress ImportBase58Address(Network network, string base58Address)
        {
            var address = new BitcoinPubKeyAddress(base58Address, network);
            return ImportAddress(network, address.ScriptPubKey, address.ToString());
        }

        /// <summary>
        /// Imports the address.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <param name="scriptAddress">The script address.</param>
        /// <returns>The created address.</returns>
        public HdAddress ImportScriptAddress(Network network, string scriptAddress)
        {
            var address = new BitcoinScriptAddress(scriptAddress, network);
            return ImportAddress(network, address.ScriptPubKey, address.ToString());
        }

        /// <summary>
        /// Imports script address.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <param name="scriptPubKey">ScriptPubKey.</param>
        /// <param name="address">PubKey.</param>
        /// <returns>The created address.</returns>
        private HdAddress ImportAddress(Network network, Script scriptPubKey, string address)
        {
            // Get the index of the last address.
            int firstNewAddressIndex = 0;
            if (this.ExternalAddresses.Any())
            {
                firstNewAddressIndex = this.ExternalAddresses.Max(add => add.Index) + 1;
            }

            PubKey pubkey = HdOperations.GeneratePublicKey(this.ExtendedPubKey, firstNewAddressIndex, false);

            // Add the new address details to the list of addresses.
            HdAddress importAddress = new HdAddress
            {
                Index = firstNewAddressIndex,
                HdPath = HdOperations.CreateHdPath((int)this.GetCoinType(), this.Index, firstNewAddressIndex),
                ScriptPubKey = scriptPubKey,
                Pubkey = pubkey.ScriptPubKey,
                Address = address,
                Transactions = new List<TransactionData>()
            };

            this.ExternalAddresses.Add(importAddress);

            return importAddress;
        }

        /// <summary>
        /// Lists all spendable transactions in the current account.
        /// </summary>
        /// <param name="currentChainHeight">The current height of the chain. Used for calculating the number of confirmations a transaction has.</param>
        /// <param name="confirmations">The minimum number of confirmations required for transactions to be considered.</param>
        /// <returns>A collection of spendable outputs that belong to the given account.</returns>
        public IEnumerable<UnspentOutputReference> GetSpendableTransactions(int currentChainHeight, int confirmations = 0)
        {
            // This will take all the spendable coins that belong to the account and keep the reference to the HDAddress and HDAccount.
            // This is useful so later the private key can be calculated just from a given UTXO.
            foreach (var address in this.GetCombinedAddresses())
            {
                // A block that is at the tip has 1 confirmation.
                // When calculating the confirmations the tip must be advanced by one.

                int countFrom = currentChainHeight + 1;
                foreach (TransactionData transactionData in address.UnspentTransactions())
                {
                    int? confirmationCount = 0;
                    if (transactionData.BlockHeight != null)
                        confirmationCount = countFrom >= transactionData.BlockHeight ? countFrom - transactionData.BlockHeight : 0;

                    if (confirmationCount >= confirmations)
                    {
                        yield return new UnspentOutputReference
                        {
                            Account = this,
                            Address = address,
                            Transaction = transactionData
                        };
                    }
                }
            }
        }

    }
    /// <summary>
    /// Provides a link composition between wallets and addresses from cached objects
    /// </summary>
    public class WalletLinkedHdAddress
    {
        private readonly HdAddress hdAddress;
        private readonly long walletId;
        /// <summary>
        /// Creates an instance of the linker object.
        /// </summary>
        /// <param name="hdAddress">Address ref</param>
        /// <param name="wallet">Wallet ref</param>
        public WalletLinkedHdAddress(HdAddress hdAddress, long walletId)
        {
            this.hdAddress = hdAddress;
            this.walletId = walletId;
        }

        public HdAddress HdAddress
        {
            get
            {
                return this.hdAddress;
            }
        }

        public long WalletId
        {
            get
            {
                return this.walletId;
            }
        }
    }
    /// <summary>
    /// An HD address.
    /// </summary>
    [ProtoContract]
    public class HdAddress : IProtoBufSerializeable
    {
        private List<TransactionData> _transactions;
        public HdAddress()
        {
            _transactions = new List<TransactionData>();
        }
        /// <summary>
        /// The index of the address.
        /// </summary>
        [ProtoMember(1)]
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [ProtoMember(2)]
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [ProtoMember(3)]
        [JsonProperty(PropertyName = "pubkey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script Pubkey { get; set; }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [ProtoMember(4)]
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// A path to the address as defined in BIP44.
        /// </summary>
        [ProtoMember(5)]
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }

        /// <summary>
        /// A list of transactions involving this address.
        /// </summary>
        [ProtoMember(6)]
        [JsonProperty(PropertyName = "transactions")]
        public List<TransactionData> Transactions
        {
            get
            {
                return _transactions;
            }
            set
            {
                _transactions = value;
            }
        }

        [JsonIgnore]
        [ProtoMember(7)]
        public long WalletId { get; set; }

        [JsonIgnore]
        [ProtoMember(8)]
        public long Id { get; set; }

        /// <summary>
        /// Determines whether this is a change address or a receive address.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if it is a change address; otherwise, <c>false</c>.
        /// </returns>
        public bool IsChangeAddress()
        {
            return HdOperations.IsChangeAddress(this.HdPath);
        }

        /// <summary>
        /// List all spendable transactions in an address.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TransactionData> UnspentTransactions()
        {
            if (this.Transactions == null)
            {
                return new List<TransactionData>();
            }

            return this.Transactions.Where(t => t.IsSpendable());
        }

        /// <summary>
        /// Get the address total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money confirmedAmount, Money unConfirmedAmount) GetSpendableAmount()
        {
            List<TransactionData> allTransactions = this.Transactions.ToList();

            long confirmed = allTransactions.Sum(t => t.SpendableAmount(true));
            long total = allTransactions.Sum(t => t.SpendableAmount(false));

            return (confirmed, total - confirmed);
        }
    }

    /// <summary>
    /// An object containing transaction data.
    /// </summary>
    [ProtoContract]
    public class TransactionData : IProtoBufSerializeable
    {
        [JsonIgnore]
        public long DbId { get; set; }
        [JsonIgnore]
        public long AddressId { get; set; }
        /// <summary>
        /// Transaction id.
        /// </summary>
        [ProtoMember(1)]
        [JsonProperty(PropertyName = "id")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Id { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [ProtoMember(2)]
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }


        /// <summary>
        /// The index of this scriptPubKey in the transaction it is contained.
        /// </summary>
        /// <remarks>
        /// This is effectively the index of the output, the position of the output in the parent transaction.
        /// </remarks>
        [ProtoMember(3)]
        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public int Index { get; set; }

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [ProtoMember(4)]
        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight { get; set; }

        /// <summary>
        /// The hash of the block including this transaction.
        /// </summary>
        [ProtoMember(5)]
        [JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [ProtoMember(6)]
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// Gets or sets the Merkle proof for this transaction.
        /// </summary>
        [ProtoMember(7)]
        [JsonProperty(PropertyName = "merkleProof", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(BitcoinSerializableJsonConverter))]
        public PartialMerkleTree MerkleProof { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [ProtoMember(8)]
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// Hexadecimal representation of this transaction.
        /// </summary>
        [ProtoMember(9)]
        [DefaultValue("")]
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Hex { get; set; }

        /// <summary>
        /// Propagation state of this transaction.
        /// </summary>
        /// <remarks>Assume it's <c>true</c> if the field is <c>null</c>.</remarks>
        [ProtoMember(10)]
        [DefaultValue(false)]
        [JsonProperty(PropertyName = "isPropagated", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsPropagated { get; set; }

        /// <summary>
        /// Gets or sets the full transaction object.
        /// </summary>
        [JsonIgnore]
        public Transaction Transaction => this.Hex == null ? null : Transaction.Parse(this.Hex);

        /// <summary>
        /// The details of the transaction in which the output referenced in this transaction is spent.
        /// </summary>
        [ProtoMember(11)]
        [JsonProperty(PropertyName = "spendingDetails", NullValueHandling = NullValueHandling.Ignore)]
        public SpendingDetails SpendingDetails { get; set; }
        [JsonIgnore]
        public bool IsFinal { get; internal set; }
        [JsonIgnore]
        public int Position { get; internal set; }

        /// <summary>
        /// Determines whether this transaction is confirmed.
        /// </summary>
        public bool IsConfirmed()
        {
            return this.BlockHeight != null;
        }

        /// <summary>
        /// Indicates an output is spendable.
        /// </summary>
        public bool IsSpendable()
        {
            return this.SpendingDetails == null;
        }

        public Money SpendableAmount(bool confirmedOnly)
        {
            // This method only returns a UTXO that has no spending output.
            // If a spending output exists (even if its not confirmed) this will return as zero balance.
            if (this.IsSpendable())
            {
                // If the 'confirmedOnly' flag is set check that the UTXO is confirmed.
                if (confirmedOnly && !this.IsConfirmed())
                {
                    return Money.Zero;
                }

                return this.Amount;
            }

            return Money.Zero;
        }
    }

    /// <summary>
    /// An object representing a payment.
    /// </summary>
    [ProtoContract]
    public class PaymentDetails : IProtoBufSerializeable
    {
        [JsonIgnore]
        public long DbId { get; set; }

        [JsonIgnore]
        public long DbSpendingTransactionId { get; set; }
        [JsonIgnore]
        public long DbTransactionId { get; set; }
        [JsonIgnore]
        public long DbAddressId { get; set; }
        [JsonIgnore]
        public uint256 TransactionId { get; set; }
        /// <summary>
        /// The script pub key of the destination address.
        /// </summary>
        [ProtoMember(1)]
        [JsonProperty(PropertyName = "destinationScriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script DestinationScriptPubKey { get; set; }

        /// <summary>
        /// The Base58 representation of the destination  address.
        /// </summary>
        [ProtoMember(2)]
        [JsonProperty(PropertyName = "destinationAddress")]
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [ProtoMember(3)]
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }

    }
    [ProtoContract]
    public class SpendingDetails : IProtoBufSerializeable
    {
        [JsonIgnore]
        public long DbId { get; set; }
        [JsonIgnore]
        public long ParentTransactionDbId { get; set; }
        [JsonIgnore]
        public long AddressDbId { get; set; }
        [JsonIgnore]
        public uint256 ParentTransactionHash{ get; set; }

        private List<PaymentDetails> _payments;

        public SpendingDetails()
        {
            _payments = new List<PaymentDetails>();
        }
        /// <summary>
        /// The id of the transaction in which the output referenced in this transaction is spent.
        /// </summary>
        [ProtoMember(1)]
        [JsonProperty(PropertyName = "transactionId", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionId { get; set; }

        /// <summary>
        /// A list of payments made out in this transaction.
        /// </summary>
        [ProtoMember(2)]
        [JsonProperty(PropertyName = "payments", NullValueHandling = NullValueHandling.Ignore)]
        public List<PaymentDetails> Payments
        {
            get
            {
                return _payments;
            }
            set
            {
                _payments = value;
            }
        }

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [ProtoMember(3)]
        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [ProtoMember(4)]
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// Hexadecimal representation of this spending transaction.
        /// </summary>
        [ProtoMember(5)]
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex { get; set; }

        /// <summary>
        /// Gets or sets the full transaction object.
        /// </summary>
        [JsonIgnore]
        public Transaction Transaction => this.Hex == null ? null : Transaction.Parse(this.Hex);
        [JsonIgnore]
        public bool IsFinal { get; internal set; }

        /// <summary>
        /// Determines whether this transaction being spent is confirmed.
        /// </summary>
        public bool IsSpentConfirmed()
        {
            return this.BlockHeight != null;
        }

    }

    /// <summary>
    /// Represents an UTXO that keeps a reference to <see cref="HdAddress"/> and <see cref="HdAccount"/>.
    /// </summary>
    /// <remarks>
    /// This is useful when an UTXO needs access to its HD properties like the HD path when reconstructing a private key.
    /// </remarks>
    public class UnspentOutputReference 
    {
        /// <summary>
        /// The account associated with this UTXO
        /// </summary>
        public HdAccount Account { get; set; }

        /// <summary>
        /// The address associated with this UTXO
        /// </summary>
        /// 
        public HdAddress Address { get; set; }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction { get; set; }

        /// <summary>
        /// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
        /// </summary>
        /// <returns>The corresponding <see cref="OutPoint"/>.</returns>
        public OutPoint ToOutPoint()
        {
            return new OutPoint(this.Transaction.Id, (uint)this.Transaction.Index);
        }
    }
}
