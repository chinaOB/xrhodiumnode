﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Connection;
using BRhodium.Bitcoin.Features.Wallet.Broadcasting;
using BRhodium.Bitcoin.Features.Wallet.Helpers;
using BRhodium.Bitcoin.Features.Wallet.Interfaces;
using BRhodium.Bitcoin.Features.Wallet.Models;
using BRhodium.Node.Utilities;
using BRhodium.Node.Utilities.JsonErrors;

namespace BRhodium.Bitcoin.Features.Wallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class WalletController : Controller
    {
        private readonly IWalletManager walletManager;

        private readonly IWalletTransactionHandler walletTransactionHandler;

        private readonly IWalletSyncManager walletSyncManager;

        private readonly CoinType coinType;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        private readonly IConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public WalletController(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IWalletSyncManager walletSyncManager,
            IConnectionManager connectionManager,
            Network network,
            ConcurrentChain chain,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider)
        {
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.walletSyncManager = walletSyncManager;
            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Generates a new mnemonic. The call can optionally specify a language and the number of words in the mnemonic.
        /// </summary>
        /// <param name="language">The language for the words in the mnemonic. Options are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional. The default is 'English'.</param>
        /// <param name="wordCount">The number of words in the mnemonic. Options are: 12,15,18,21 or 24. the default is 12.</param>
        /// <returns>A JSON object containing the mnemonic generated.</returns>
        [Route("mnemonic")]
        [HttpGet]
        public IActionResult GenerateMnemonic([FromQuery] string language = "English", int wordCount = 12)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(language), language, nameof(wordCount), wordCount);

            try
            {
                Wordlist wordList;
                switch (language.ToLowerInvariant())
                {
                    case "english":
                        wordList = Wordlist.English;
                        break;

                    case "french":
                        wordList = Wordlist.French;
                        break;

                    case "spanish":
                        wordList = Wordlist.Spanish;
                        break;

                    case "japanese":
                        wordList = Wordlist.Japanese;
                        break;

                    case "chinesetraditional":
                        wordList = Wordlist.ChineseTraditional;
                        break;

                    case "chinesesimplified":
                        wordList = Wordlist.ChineseSimplified;
                        break;

                    default:
                        throw new FormatException($"Invalid language '{language}'. Choices are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional.");
                }

                WordCount count = (WordCount)wordCount;

                // generate the mnemonic
                Mnemonic mnemonic = new Mnemonic(wordList, count);
                return this.Json(mnemonic.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Creates a new wallet on the local machine.
        /// </summary>
        /// <param name="request">The object containing the parameters used to create the wallet.</param>
        /// <returns>A JSON object containing the mnemonic created for the new wallet.</returns>
        [Route("create")]
        [HttpPost]
        public IActionResult Create([FromBody]WalletCreationRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                Mnemonic mnemonic = this.walletManager.CreateWallet(request.Password, request.Name, mnemonic: request.Mnemonic);

                // start syncing the wallet from the creation date
                this.walletSyncManager.SyncFromDate(this.dateTimeProvider.GetUtcNow());

                return this.Json(mnemonic.ToString());
            }
            catch (WalletException e)
            {
                // indicates that this wallet already exists
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, e.Message, e.ToString());
            }
            catch (NotSupportedException e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "There was a problem creating a wallet.", e.ToString());
            }
        }

        /// <summary>
        /// Loads a wallet previously created by the user.
        /// </summary>
        /// <param name="request">The name of the wallet to load.</param>
        /// <returns></returns>
        [Route("load")]
        [HttpPost]
        public IActionResult Load([FromBody]WalletLoadRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.LoadWallet(request.Password, request.Name);
                return this.Ok();
            }
            catch (FileNotFoundException e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, "This wallet was not found at the specified location.", e.ToString());
            }
            catch (SecurityException e)
            {
                // indicates that the password is wrong
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Wrong password, please try again.", e.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Recovers a wallet.
        /// </summary>
        /// <param name="request">The object containing the parameters used to recover a wallet.</param>
        /// <returns></returns>
        [Route("recover")]
        [HttpPost]
        public IActionResult Recover([FromBody]WalletRecoveryRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.RecoverWallet(request.Password, request.Name, request.Mnemonic, request.CreationDate);

                // start syncing the wallet from the creation date
                this.walletSyncManager.SyncFromDate(request.CreationDate);

                return this.Ok();
            }
            catch (WalletException e)
            {
                // indicates that this wallet already exists
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, e.Message, e.ToString());
            }
            catch (FileNotFoundException e)
            {
                // indicates that this wallet does not exist
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, "Wallet not found.", e.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Get some general info about a wallet.
        /// </summary>
        /// <param name="request">The name of the wallet.</param>
        /// <returns></returns>
        [Route("general-info")]
        [HttpGet]
        public IActionResult GetGeneralInfo([FromQuery] WalletName request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.GetWallet(request.Name);

                var model = new WalletGeneralInfoModel
                {
                    Network = wallet.Network,
                    CreationTime = wallet.CreationTime,
                    LastBlockSyncedHeight = wallet.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight,
                    ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                    ChainTip = this.chain.Tip.Height,
                    IsChainSynced = this.chain.IsDownloaded(),
                    IsDecrypted = true
                };

                // Get the wallet's file path.
                (string folder, IEnumerable<string> fileNameCollection) = this.walletManager.GetWalletsFiles();
                string searchFile = Path.ChangeExtension(request.Name, this.walletManager.GetWalletFileExtension());
                string fileName = fileNameCollection.FirstOrDefault(i => i.Equals(searchFile));
                if (folder != null && fileName != null)
                    model.WalletFilePath = Path.Combine(folder, fileName);

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Retrieves the history of a wallet.
        /// </summary>
        /// <param name="request">The request parameters.</param>
        /// <returns></returns>
        [Route("history")]
        [HttpGet]
        public IActionResult GetHistory([FromQuery] WalletHistoryRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                WalletHistoryModel model = new WalletHistoryModel();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistory(request.WalletName, request.AccountName);

                foreach (var accountHistory in accountsHistory)
                {
                    List<TransactionItemModel> transactionItems = new List<TransactionItemModel>();

                    List<FlatHistory> items = accountHistory.History.OrderByDescending(o => o.Transaction.CreationTime).Take(200).ToList();

                    // Represents a sublist containing only the transactions that have already been spent.
                    List<FlatHistory> spendingDetails = items.Where(t => t.Transaction.SpendingDetails != null).ToList();

                    // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
                    // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
                    List<FlatHistory> history = items.Where(t => !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && !t.Transaction.IsSpendable())).ToList();

                    // Represents a sublist of 'change' transactions.
                    List<FlatHistory> allchange = items.Where(t => t.Address.IsChangeAddress()).ToList();

                    foreach (var item in history)
                    {
                        var transaction = item.Transaction;
                        var address = item.Address;

                        // Create a record for a 'receive' transaction.
                        if (!address.IsChangeAddress())
                        {
                            // Add incoming fund transaction details.
                            TransactionItemModel receivedItem = new TransactionItemModel
                            {
                                Type = TransactionItemType.Received,
                                ToAddress = address.Address,
                                Amount = transaction.Amount,
                                Id = transaction.Id,
                                Timestamp = transaction.CreationTime,
                                ConfirmedInBlock = transaction.BlockHeight
                            };

                            transactionItems.Add(receivedItem);
                        }

                        // If this is a normal transaction (not staking) that has been spent, add outgoing fund transaction details.
                        if (transaction.SpendingDetails != null)
                        {
                            // Create a record for a 'send' transaction.
                            var spendingTransactionId = transaction.SpendingDetails.TransactionId;
                            TransactionItemModel sentItem = new TransactionItemModel
                            {
                                Type = TransactionItemType.Sent,
                                Id = spendingTransactionId,
                                Timestamp = transaction.SpendingDetails.CreationTime,
                                ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                                Amount = Money.Zero
                            };

                            // If this 'send' transaction has made some external payments, i.e the funds were not sent to another address in the wallet.
                            if (transaction.SpendingDetails.Payments != null)
                            {
                                sentItem.Payments = new List<PaymentDetailModel>();
                                foreach (var payment in transaction.SpendingDetails.Payments)
                                {
                                    sentItem.Payments.Add(new PaymentDetailModel
                                    {
                                        DestinationAddress = payment.DestinationAddress,
                                        Amount = payment.Amount
                                    });

                                    sentItem.Amount += payment.Amount;
                                }
                            }

                            // Get the change address for this spending transaction.
                            var changeAddress = allchange.FirstOrDefault(a => a.Transaction.Id == spendingTransactionId);

                            // Find all the spending details containing the spending transaction id and aggregate the sums.
                            // This is our best shot at finding the total value of inputs for this transaction.
                            var inputsAmount = new Money(spendingDetails.Where(t => t.Transaction.SpendingDetails.TransactionId == spendingTransactionId).Sum(t => t.Transaction.Amount));

                            // The fee is calculated as follows: funds in utxo - amount spent - amount sent as change.
                            sentItem.Fee = inputsAmount - sentItem.Amount - (changeAddress == null ? 0 : changeAddress.Transaction.Amount);

                            // Mined coins add more coins to the total out.
                            // That makes the fee negative. If that's the case ignore the fee.
                            if (sentItem.Fee < 0)
                                sentItem.Fee = 0;

                            if (!transactionItems.Contains(sentItem, new SentTransactionItemModelComparer()))
                            {
                                transactionItems.Add(sentItem);
                            }
                        }
                    }

                    model.AccountsHistoryModel.Add(new AccountHistoryModel
                    {
                        TransactionsHistory = transactionItems.OrderByDescending(t => t.Timestamp).ToList(),
                        Name = accountHistory.Account.Name,
                        CoinType = this.coinType,
                        HdPath = accountHistory.Account.HdPath
                    });
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the balance of a wallet.
        /// </summary>
        /// <param name="request">The request parameters.</param>
        /// <returns></returns>
        [Route("balance")]
        [HttpGet]
        public IActionResult GetBalance([FromQuery] WalletBalanceRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                WalletBalanceModel model = new WalletBalanceModel();

                IEnumerable<AccountBalance> balances = this.walletManager.GetBalances(request.WalletName, request.AccountName);

                foreach (AccountBalance balance in balances)
                {
                    HdAccount account = balance.Account;
                    model.AccountsBalances.Add(new AccountBalanceModel
                    {
                        CoinType = this.coinType,
                        Name = account.Name,
                        HdPath = account.HdPath,
                        AmountConfirmed = balance.AmountConfirmed,
                        AmountUnconfirmed = balance.AmountUnconfirmed
                    });
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the balance for an address.
        /// </summary>
        /// <param name="request">The request parameters.</param>
        /// <returns>The address balance for an address.</returns>
        [Route("received-by-address")]
        [HttpGet]
        public IActionResult GetReceivedByAddress([FromQuery] ReceivedByAddressRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                AddressBalance balanceResult = this.walletManager.GetAddressBalance(request.Address);
                return this.Json(new AddressBalanceModel
                {
                    CoinType = this.coinType,
                    Address = balanceResult.Address,
                    AmountConfirmed = balanceResult.AmountConfirmed,
                    AmountUnconfirmed = balanceResult.AmountUnconfirmed
                });
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the maximum spendable balance on an account, along with the fee required to spend it.
        /// </summary>
        /// <param name="request">The request parameters.</param>
        /// <returns></returns>
        [Route("maxbalance")]
        [HttpGet]
        public IActionResult GetMaximumSpendableBalance([FromQuery] WalletMaximumBalanceRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var transactionResult = this.walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference(request.WalletName, request.AccountName), FeeParser.Parse(request.FeeType), request.AllowUnconfirmed);
                return this.Json(new MaxSpendableAmountModel
                {
                    MaxSpendableAmount = transactionResult.maximumSpendableAmount,
                    Fee = transactionResult.Fee
                });
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets a transaction fee estimate.
        /// Fee can be estimated by creating a <see cref="TransactionBuildContext"/> with no password
        /// and then building the transaction and retrieving the fee from the context.
        /// </summary>
        /// <param name="request">The transaction parameters.</param>
        /// <returns>The estimated fee for the transaction.</returns>
        [Route("estimate-txfee")]
        [HttpGet]
        public IActionResult GetTransactionFeeEstimate([FromQuery]TxFeeEstimateRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var destination = BitcoinAddress.Create(request.DestinationAddress, this.network).ScriptPubKey;
                var context = new TransactionBuildContext(
                    new WalletAccountReference(request.WalletName, request.AccountName),
                    new[] { new Recipient { Amount = request.Amount, ScriptPubKey = destination } }.ToList())
                {
                    FeeType = FeeParser.Parse(request.FeeType),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                };

                return this.Json(this.walletTransactionHandler.EstimateFee(context));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Builds a transaction.
        /// </summary>
        /// <param name="request">The transaction parameters.</param>
        /// <returns>All the details of the transaction, including the hex used to execute it.</returns>
        [Route("build-transaction")]
        [HttpPost]
        public IActionResult BuildTransaction([FromBody] BuildTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var destination = BitcoinAddress.Create(request.DestinationAddress, this.network).ScriptPubKey;
                var context = new TransactionBuildContext(
                    new WalletAccountReference(request.WalletName, request.AccountName),
                    new[] { new Recipient { Amount = request.Amount, ScriptPubKey = destination } }.ToList(),
                    request.Password, request.OpReturnData)
                {
                    TransactionFee = string.IsNullOrEmpty(request.FeeAmount) ? null : Money.Parse(request.FeeAmount),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                    Shuffle = request.ShuffleOutputs ?? true // We shuffle transaction outputs by default as it's better for anonymity.
                };

                if (!string.IsNullOrEmpty(request.FeeType))
                {
                    context.FeeType = FeeParser.Parse(request.FeeType);
                }

                var transactionResult = this.walletTransactionHandler.BuildTransaction(context);

                var model = new WalletBuildTransactionModel
                {
                    Hex = transactionResult.ToHex(),
                    Fee = context.TransactionFee,
                    TransactionId = transactionResult.GetHash()
                };

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Sends a transaction.
        /// </summary>
        /// <param name="request">The hex representing the transaction.</param>
        /// <returns></returns>
        [Route("send-transaction")]
        [HttpPost]
        public IActionResult SendTransaction([FromBody] SendTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            if (!this.connectionManager.ConnectedPeers.Any())
                throw new WalletException("Can't send transaction: sending transaction requires at least one connection!");

            try
            {
                var transaction = Transaction.Load(request.Hex, this.network);

                WalletSendTransactionModel model = new WalletSendTransactionModel
                {
                    TransactionId = transaction.GetHash(),
                    Outputs = new List<TransactionOutputModel>()
                };

                foreach (var output in transaction.Outputs)
                {
                    var isUnspendable = output.ScriptPubKey.IsUnspendable;
                    model.Outputs.Add(new TransactionOutputModel
                    {
                        Address = isUnspendable ? null : output.ScriptPubKey.GetDestinationAddress(this.network).ToString(),
                        Amount = output.Value,
                        OpReturnData = isUnspendable ? Encoding.UTF8.GetString(output.ScriptPubKey.ToOps().Last().PushData) : null
                    });
                }

                this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

                if (!string.IsNullOrEmpty(transactionBroadCastEntry?.ErrorMessage))
                {
                    this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Lists all the wallet files found under the default folder.
        /// </summary>
        /// <returns>A list of the wallets files found.</returns>
        [Route("files")]
        [HttpGet]
        public IActionResult ListWalletsFiles()
        {
            try
            {
                (string folderPath, IEnumerable<string> filesNames) result = this.walletManager.GetWalletsFiles();
                WalletFileModel model = new WalletFileModel
                {
                    WalletsPath = result.folderPath,
                    WalletsFiles = result.filesNames
                };

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Creates a new account for a wallet.
        /// </summary>
        /// <returns>An account name.</returns>
        [Route("account")]
        [HttpPost]
        public IActionResult CreateNewAccount([FromBody]GetUnusedAccountModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result = this.walletManager.GetUnusedAccount(request.WalletName, request.Password);
                return this.Json(result.Name);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets a list of accounts for the specified wallet.
        /// </summary>
        /// <returns>The list of accounts for the specified wallet</returns>
        [Route("accounts")]
        [HttpGet]
        public IActionResult ListAccounts([FromQuery]ListAccountsModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result = this.walletManager.GetAccounts(request.WalletName);
                return this.Json(result.Select(a => a.Name));
            }
            catch(Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets an unused address.
        /// </summary>
        /// <returns>The last created and unused address or creates a new address (in Base58 format).</returns>
        [Route("unusedaddress")]
        [HttpGet]
        public IActionResult GetUnusedAddress([FromQuery]GetUnusedAddressModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result = this.walletManager.GetUnusedAddress(new WalletAccountReference(request.WalletName, request.AccountName));
                return this.Json(result.Address);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the specified number of unused addresses.
        /// </summary>
        [Route("unusedaddresses")]
        [HttpGet]
        public IActionResult GetUnusedAddresses([FromQuery]GetUnusedAddressesModel request)
        {
            Guard.NotNull(request, nameof(request));
            var count = int.Parse(request.Count);

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result = this.walletManager.GetUnusedAddresses(new WalletAccountReference(request.WalletName, request.AccountName), count);
                return this.Json(result.Select(x => x.Address).ToArray());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the specified number of unused addresses.
        /// </summary>
        [Route("addresses")]
        [HttpGet]
        public IActionResult GetAllAddresses([FromQuery]GetAllAddressesModel request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                HdAccount account = wallet.GetAccountByCoinType(request.AccountName, this.coinType);

                AddressesModel model = new AddressesModel
                {
                    Addresses = account.GetCombinedAddresses().Select(address => new AddressModel
                    {
                        Address = address.Address,
                        IsUsed = address.Transactions.Any(),
                        IsChange = address.IsChangeAddress()
                    })
                };

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Removed transactions from the wallet.
        /// </summary>
        [Route("remove-transactions")]
        [HttpDelete]
        public IActionResult RemoveTransactions([FromQuery]RemoveTransactionsModel request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                HashSet<(uint256 transactionId, DateTimeOffset creationTime)> result;

                if (request.DeleteAll)
                {
                    result = this.walletManager.RemoveAllTransactions(request.WalletName);
                }
                else
                {
                    IEnumerable<uint256> ids = request.TransactionsIds.Select(uint256.Parse);
                    result = this.walletManager.RemoveTransactionsByIds(request.WalletName, ids);
                }

                // If the user chose to resync the wallet after removing transactions.
                if (result.Any() && request.ReSync)
                {
                    // From the list of removed transactions, check which one is the oldest and retrieve the block right before that time.
                    DateTimeOffset earliestDate = result.Min(r => r.creationTime);
                    ChainedHeader chainedHeader = this.chain.GetBlock(this.chain.GetHeightAtTime(earliestDate.DateTime));

                    // Update the wallet and save it to the file system.
                    Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                    wallet.SetLastBlockDetailsByCoinType(this.coinType, chainedHeader);
                    this.walletManager.SaveWallet(wallet);

                    // Start the syncing process from the block before the earliest transaction was seen.
                    this.walletSyncManager.SyncFromHeight(chainedHeader.Height - 1);
                }

                IEnumerable<RemovedTransactionModel> model = result.Select(r => new RemovedTransactionModel
                {
                    TransactionId = r.transactionId,
                    CreationTime = r.creationTime
                });

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the extpubkey of the specified account.
        /// </summary>
        [Route("extpubkey")]
        [HttpGet]
        public IActionResult GetExtPubKey([FromQuery]GetExtPubKeyModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                string result = this.walletManager.GetExtPubKey(new WalletAccountReference(request.WalletName, request.AccountName));
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Starts sending block to the wallet for synchronisation.
        /// This is for demo and testing use only.
        /// </summary>
        /// <param name="model">The hash of the block from which to start syncing.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("sync")]
        public IActionResult Sync([FromBody] HashModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            ChainedHeader block = this.chain.GetBlock(uint256.Parse(model.Hash));

            if (block == null)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Block with hash {model.Hash} was not found on the blockchain.", string.Empty);
            }

            this.walletSyncManager.SyncFromHeight(block.Height);
            return this.Ok();
        }

        /// <summary>
        /// Builds an <see cref="IActionResult"/> containing errors contained in the <see cref="ControllerBase.ModelState"/>.
        /// </summary>
        /// <returns>A result containing the errors.</returns>
        private static IActionResult BuildErrorResponse(ModelStateDictionary modelState)
        {
            List<ModelError> errors = modelState.Values.SelectMany(e => e.Errors).ToList();
            return ErrorHelpers.BuildErrorResponse(
                HttpStatusCode.BadRequest,
                string.Join(Environment.NewLine, errors.Select(m => m.ErrorMessage)),
                string.Join(Environment.NewLine, errors.Select(m => m.Exception?.Message)));
        }
    }
}