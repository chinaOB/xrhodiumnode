﻿#if !NOJSONNET
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NBitcoin.RPC
{
    /*
        Category            Name                        Implemented
        ------------------ --------------------------- -----------------------
        ------------------ Overall control/query calls
        control            getinfo
        control            help
        control            stop

        ------------------ P2P networking
        network            getnetworkinfo
        network            addnode                      Yes
        network            disconnectnode
        network            getaddednodeinfo             Yes
        network            getconnectioncount
        network            getnettotals
        network            getpeerinfo                  Yes
        network            ping
        network            setban
        network            listbanned
        network            clearbanned

        ------------------ Block chain and UTXO
        blockchain         getblockchaininfo
        blockchain         getbestblockhash             Yes
        blockchain         getblockcount                Yes
        blockchain         getblock                     Yes
        blockchain         getblockhash                 Yes
        blockchain         getchaintips
        blockchain         getdifficulty
        blockchain         getmempoolinfo
        blockchain         getrawmempool                Yes
        blockchain         gettxout                    Yes
        blockchain         gettxoutproof
        blockchain         verifytxoutproof
        blockchain         gettxoutsetinfo
        blockchain         verifychain

        ------------------ Mining
        mining             getblocktemplate
        mining             getmininginfo
        mining             getnetworkhashps
        mining             prioritisetransaction
        mining             submitblock

        ------------------ Coin generation
        generating         getgenerate
        generating         setgenerate
        generating         generate

        ------------------ Raw transactions
        rawtransactions    createrawtransaction
        rawtransactions    decoderawtransaction
        rawtransactions    decodescript
        rawtransactions    getrawtransaction
        rawtransactions    sendrawtransaction
        rawtransactions    signrawtransaction
        rawtransactions    fundrawtransaction

        ------------------ Utility functions
        util               createmultisig
        util               validateaddress              Yes
        util               verifymessage
        util               estimatefee                  Yes

        ------------------ Not shown in help
        hidden             invalidateblock
        hidden             reconsiderblock
        hidden             setmocktime
        hidden             resendwallettransactions

        ------------------ Wallet
        wallet             addmultisigaddress
        wallet             backupwallet                 Yes
        wallet             dumpprivkey                  Yes
        wallet             dumpwallet
        wallet             encryptwallet
        wallet             getaccountaddress            Yes
        wallet             getaccount
        wallet             getaddressesbyaccount
        wallet             getbalance
        wallet             getnewaddress
        wallet             getrawchangeaddress
        wallet             getreceivedbyaccount
        wallet             getreceivedbyaddress
        wallet             gettransaction
        wallet             getunconfirmedbalance
        wallet             getwalletinfo
        wallet             importprivkey                Yes
        wallet             importwallet
        wallet             importaddress                Yes
        wallet             keypoolrefill
        wallet             listaccounts                 Yes
        wallet             listaddressgroupings         Yes
        wallet             listlockunspent
        wallet             listreceivedbyaccount
        wallet             listreceivedbyaddress
        wallet             listsinceblock
        wallet             listtransactions
        wallet             listunspent                  Yes
        wallet             lockunspent                  Yes
        wallet             move
        wallet             sendfrom
        wallet             sendmany
        wallet             sendtoaddress
        wallet             setaccount
        wallet             settxfee
        wallet             signmessage
        wallet             walletlock
        wallet             walletpassphrasechange
        wallet             walletpassphrase            yes
    */
    public partial class RPCClient : INBitcoinBlockRepository, IRPCClient
    {
        private string authentication;
        private readonly Uri address;
        private readonly Network network;
        private static ConcurrentDictionary<Network, string> defaultPaths = new ConcurrentDictionary<Network, string>();
        private ConcurrentQueue<Tuple<RPCRequest, TaskCompletionSource<RPCResponse>>> batchedRequests;
        private RPCCredentialString credentialString;

        public Uri Address
        {
            get
            {
                return this.address;
            }
        }

        public RPCCredentialString CredentialString
        {
            get
            {
                return this.credentialString;
            }
        }

        public Network Network
        {
            get
            {
                return this.network;
            }
        }

        /// <summary>
        /// Use default bitcoin parameters to configure a RPCClient.
        /// </summary>
        /// <param name="network">The network used by the node. Must not be null.</param>
        public RPCClient(Network network) : this(null as string, BuildUri(null, network.RPCPort), network)
        {
        }

        [Obsolete("Use RPCClient(ConnectionString, string, Network)")]
        public RPCClient(NetworkCredential credentials, string host, Network network)
            : this(credentials, BuildUri(host, network.RPCPort), network)
        {
        }

        public RPCClient(RPCCredentialString credentials, string host, Network network)
            : this(credentials, BuildUri(host, network.RPCPort), network)
        {
        }

        public RPCClient(RPCCredentialString credentials, Uri address, Network network)
        {
            credentials = credentials ?? new RPCCredentialString();

            if (address != null && network == null)
            {
                network = Network.GetNetworks().FirstOrDefault(n => n.RPCPort == address.Port);
                if (network == null)
                    throw new ArgumentNullException("network");
            }

            if (credentials.UseDefault && network == null)
                throw new ArgumentException("network parameter is required if you use default credentials");

            if (address == null && network == null)
                throw new ArgumentException("network parameter is required if you use default uri");

            if (address == null)
                address = new Uri("http://127.0.0.1:" + network.RPCPort + "/");

            if (credentials.UseDefault)
            {
                //will throw impossible to get the cookie path
                GetDefaultCookieFilePath(network);
            }

            this.credentialString = credentials;
            this.address = address;
            this.network = network;

            if (credentials.UserPassword != null)
                this.authentication = $"{credentials.UserPassword.UserName}:{credentials.UserPassword.Password}";

            if (this.authentication == null)
                RenewCookie();

            if (this.authentication == null)
                throw new ArgumentException("Impossible to infer the authentication of the RPCClient");
        }

        static RPCClient()
        {
#if !NOFILEIO
            var home = Environment.GetEnvironmentVariable("HOME");
            var localAppData = Environment.GetEnvironmentVariable("APPDATA");

            if (string.IsNullOrEmpty(home) && string.IsNullOrEmpty(localAppData))
                return;

            if (!string.IsNullOrEmpty(home))
            {
                var bitcoinFolder = Path.Combine(home, ".bitcoin");

                var mainnet = Path.Combine(bitcoinFolder, ".cookie");
                RegisterDefaultCookiePath(Network.Main, mainnet);

                var testnet = Path.Combine(bitcoinFolder, "testnet3", ".cookie");
                RegisterDefaultCookiePath(Network.TestNet, testnet);

                var regtest = Path.Combine(bitcoinFolder, "regtest", ".cookie");
                RegisterDefaultCookiePath(Network.RegTest, regtest);
            }
            else if (!string.IsNullOrEmpty(localAppData))
            {
                var bitcoinFolder = Path.Combine(localAppData, "Bitcoin");

                var mainnet = Path.Combine(bitcoinFolder, ".cookie");
                RegisterDefaultCookiePath(Network.BRhodiumRegTest, mainnet);

                var testnet = Path.Combine(bitcoinFolder, "BRhodiumTest", ".cookie");
                RegisterDefaultCookiePath(Network.BRhodiumRegTest, testnet);

                var regtest = Path.Combine(bitcoinFolder, "regtest", ".cookie");
                RegisterDefaultCookiePath(Network.BRhodiumRegTest, regtest);
            }
#endif
        }

        public static void RegisterDefaultCookiePath(Network network, string path)
        {
            defaultPaths.TryAdd(network, path);
        }

        private string GetCookiePath()
        {
            if (this.CredentialString.UseDefault && this.Network == null)
                throw new InvalidOperationException("NBitcoin bug, report to the developers");

            if (this.CredentialString.UseDefault)
                return GetDefaultCookieFilePath(this.Network);

            if (this.CredentialString.CookieFile != null)
                return this.CredentialString.CookieFile;

            return null;
        }

        public static string GetDefaultCookieFilePath(Network network)
        {
            return TryGetDefaultCookieFilePath(network) ?? throw new ArgumentException(
                "This network has no default cookie file path registered, use RPCClient.RegisterDefaultCookiePath to register", "network");
        }

        public static string TryGetDefaultCookieFilePath(Network network)
        {
            return defaultPaths.TryGetValue(network, out string path) ? path : null;
        }

        /// <summary>
        /// Create a new RPCClient instance
        /// </summary>
        /// <param name="authenticationString">username:password, the content of the .cookie file, or cookiefile=pathToCookieFile</param>
        /// <param name="hostOrUri"></param>
        /// <param name="network"></param>
        public RPCClient(string authenticationString, string hostOrUri, Network network)
            : this(authenticationString, BuildUri(hostOrUri, network.RPCPort), network)
        {
        }

        private static Uri BuildUri(string hostOrUri, int port)
        {
            if (hostOrUri != null)
            {
                hostOrUri = hostOrUri.Trim();
                try
                {
                    if (hostOrUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        hostOrUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                        return new Uri(hostOrUri, UriKind.Absolute);
                }
                catch
                {
                }
            }

            hostOrUri = hostOrUri ?? "127.0.0.1";
            var indexOfPort = hostOrUri.IndexOf(":");
            if (indexOfPort != -1)
            {
                port = int.Parse(hostOrUri.Substring(indexOfPort + 1));
                hostOrUri = hostOrUri.Substring(0, indexOfPort);
            }

            UriBuilder builder = new UriBuilder();
            builder.Host = hostOrUri;
            builder.Scheme = "http";
            builder.Port = port;
            return builder.Uri;
        }

        public RPCClient(NetworkCredential credentials, Uri address, Network network = null)
            : this(credentials == null ? null : (credentials.UserName + ":" + credentials.Password), address, network)
        {
        }

        /// <summary>
        /// Create a new RPCClient instance
        /// </summary>
        /// <param name="authenticationString">username:password or the content of the .cookie file or null to auto configure</param>
        /// <param name="address"></param>
        /// <param name="network"></param>
        public RPCClient(string authenticationString, Uri address, Network network = null)
            : this(authenticationString == null ? null as RPCCredentialString : RPCCredentialString.Parse(authenticationString), address, network)
        {
        }

        public RPCClient PrepareBatch()
        {
            return new RPCClient(this.CredentialString, this.Address, this.Network)
            {
                batchedRequests = new ConcurrentQueue<Tuple<RPCRequest, TaskCompletionSource<RPCResponse>>>()
            };
        }

        public RPCResponse SendCommand(RPCOperations commandName, params object[] parameters)
        {
            return SendCommand(commandName.ToString(), parameters);
        }

        public BitcoinAddress GetNewAddress()
        {
            return BitcoinAddress.Create(SendCommand(RPCOperations.getnewaddress).Result.ToString(), this.Network);
        }

        public async Task<BitcoinAddress> GetNewAddressAsync()
        {
            RPCResponse result = await SendCommandAsync(RPCOperations.getnewaddress).ConfigureAwait(false);
            return BitcoinAddress.Create(result.Result.ToString(), this.Network);
        }

        public BitcoinAddress GetRawChangeAddress()
        {
            return GetRawChangeAddressAsync().GetAwaiter().GetResult();
        }

        public async Task<BitcoinAddress> GetRawChangeAddressAsync()
        {
            RPCResponse result = await SendCommandAsync(RPCOperations.getrawchangeaddress).ConfigureAwait(false);
            return BitcoinAddress.Create(result.Result.ToString(), this.Network);
        }

        public Task<RPCResponse> SendCommandAsync(RPCOperations commandName, params object[] parameters)
        {
            return SendCommandAsync(commandName.ToString(), parameters);
        }

        /// <summary>Get the a whole block.</summary>
        /// <param name="blockId"></param>
        /// <returns></returns>
        public async Task<RPCBlock> GetRPCBlockAsync(uint256 blockId)
        {
            RPCResponse resp = await SendCommandAsync(RPCOperations.getblock, blockId.ToString(), false).ConfigureAwait(false);
            return SatoshiBlockFormatter.Parse(resp.Result as JObject);
        }

        /// <summary>Send a command.</summary>
        /// <param name="commandName">https://en.bitcoin.it/wiki/Original_Bitcoin_client/API_calls_list</param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public RPCResponse SendCommand(string commandName, params object[] parameters)
        {
            return SendCommand(new RPCRequest(commandName, parameters));
        }

        public Task<RPCResponse> SendCommandAsync(string commandName, params object[] parameters)
        {
            return SendCommandAsync(new RPCRequest(commandName, parameters));
        }

        public RPCResponse SendCommand(RPCRequest request, bool throwIfRPCError = true)
        {
            return SendCommandAsync(request, throwIfRPCError).GetAwaiter().GetResult();
        }

        /// <summary>Send all commands in one batch.</summary>
        public void SendBatch()
        {
            SendBatchAsync().GetAwaiter().GetResult();
        }

        /// <summary>Cancel all commands.</summary>
        public void CancelBatch()
        {
            ConcurrentQueue<Tuple<RPCRequest, TaskCompletionSource<RPCResponse>>> batches;
            lock (this)
            {
                if (this.batchedRequests == null)
                    throw new InvalidOperationException("This RPCClient instance is not a batch, use PrepareBatch");
                batches = this.batchedRequests;
                this.batchedRequests = null;
            }

            Tuple<RPCRequest, TaskCompletionSource<RPCResponse>> req;
            while (batches.TryDequeue(out req))
                req.Item2.TrySetCanceled();
        }

        /// <summary>Send all commands in one batch.</summary>
        public async Task SendBatchAsync()
        {
            ConcurrentQueue<Tuple<RPCRequest, TaskCompletionSource<RPCResponse>>> batches;
            lock (this)
            {
                if (this.batchedRequests == null)
                    throw new InvalidOperationException("This RPCClient instance is not a batch, use PrepareBatch");

                batches = this.batchedRequests;

                this.batchedRequests = null;
            }

            var requests = new List<Tuple<RPCRequest, TaskCompletionSource<RPCResponse>>>();
            while (batches.TryDequeue(out Tuple<RPCRequest, TaskCompletionSource<RPCResponse>> req))
                requests.Add(req);

            if (!requests.Any())
                return;

            try
            {
                await SendBatchAsyncCoreAsync(requests).ConfigureAwait(false);
            }
            catch (WebException ex)
            {
                if (!IsUnauthorized(ex))
                    throw;

                if (GetCookiePath() == null)
                    throw;

                TryRenewCookie(ex);

                await SendBatchAsyncCoreAsync(requests).ConfigureAwait(false);
            }
        }

        private async Task SendBatchAsyncCoreAsync(List<Tuple<RPCRequest, TaskCompletionSource<RPCResponse>>> requests)
        {
            var writer = new StringWriter();
            writer.Write("[");

            bool first = true;
            foreach (Tuple<RPCRequest, TaskCompletionSource<RPCResponse>> item in requests)
            {
                if (!first)
                    writer.Write(",");
                else
                    first = false;

                item.Item1.WriteJSON(writer);
            }

            writer.Write("]");
            writer.Flush();

            var json = writer.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            HttpWebRequest webRequest = CreateWebRequest();

#if !(PORTABLE || NETCORE)
            webRequest.ContentLength = bytes.Length;
#endif

            Stream dataStream = await webRequest.GetRequestStreamAsync().ConfigureAwait(false);
            await dataStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            await dataStream.FlushAsync().ConfigureAwait(false);

            dataStream.Dispose();

            JArray response;
            WebResponse webResponse = null;
            WebResponse errorResponse = null;

            try
            {
                webResponse = await webRequest.GetResponseAsync().ConfigureAwait(false);

                response = JArray.Load(new JsonTextReader(
                        new StreamReader(
                                await ToMemoryStreamAsync(webResponse.GetResponseStream()).ConfigureAwait(false), Encoding.UTF8)));

                int responseIndex = 0;

                foreach (JObject jobj in response.OfType<JObject>())
                {
                    try
                    {
                        RPCResponse rpcResponse = new RPCResponse(jobj);
                        requests[responseIndex].Item2.TrySetResult(rpcResponse);
                    }
                    catch(Exception ex)
                    {
                        requests[responseIndex].Item2.TrySetException(ex);
                    }

                    responseIndex++;
                }
            }
            catch (WebException ex)
            {
                if (IsUnauthorized(ex))
                    throw;
                if (ex.Response == null || ex.Response.ContentLength == 0
                    || !ex.Response.ContentType.Equals("application/json", StringComparison.Ordinal))
                {
                    foreach (Tuple<RPCRequest, TaskCompletionSource<RPCResponse>> item in requests)
                        item.Item2.TrySetException(ex);
                }
                else
                {
                    errorResponse = ex.Response;

                    try
                    {

                        RPCResponse rpcResponse = RPCResponse.Load(await ToMemoryStreamAsync(errorResponse.GetResponseStream()).ConfigureAwait(false));
                        foreach (Tuple<RPCRequest, TaskCompletionSource<RPCResponse>> item in requests)
                            item.Item2.TrySetResult(rpcResponse);
                    }
                    catch (Exception)
                    {
                        foreach (Tuple<RPCRequest, TaskCompletionSource<RPCResponse>> item in requests)
                            item.Item2.TrySetException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (Tuple<RPCRequest, TaskCompletionSource<RPCResponse>> item in requests)
                    item.Item2.TrySetException(ex);
            }
            finally
            {
                if (errorResponse != null)
                {
                    errorResponse.Dispose();
                    errorResponse = null;
                }

                if (webResponse != null)
                {
                    webResponse.Dispose();
                    webResponse = null;
                }
            }
        }

        private static bool IsUnauthorized(WebException ex)
        {
            var httpResp = ex.Response as HttpWebResponse;
            var isUnauthorized = httpResp != null && httpResp.StatusCode == HttpStatusCode.Unauthorized;
            return isUnauthorized;
        }

        public async Task<RPCResponse> SendCommandAsync(RPCRequest request, bool throwIfRPCError = true)
        {
            try
            {
                return await SendCommandAsyncCoreAsync(request, throwIfRPCError).ConfigureAwait(false);
            }
            catch(WebException ex)
            {
                if (!IsUnauthorized(ex))
                    throw;

                if (GetCookiePath() == null)
                    throw;

                TryRenewCookie(ex);

                return await SendCommandAsyncCoreAsync(request, throwIfRPCError).ConfigureAwait(false);
            }
        }

        private void RenewCookie()
        {
            if (GetCookiePath() == null)
                throw new InvalidOperationException("Bug in NBitcoin notify the developers");
#if !NOFILEIO
            var auth = File.ReadAllText(GetCookiePath());
            if (!auth.StartsWith("__cookie__:", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The authentication string to RPC is not provided and can't be inferred");

            this.authentication = auth;
#else
            throw new NotSupportedException("Cookie authentication is not supported for this plateform");
#endif
        }

        private void TryRenewCookie(WebException ex)
        {
            if (GetCookiePath() == null)
                throw new InvalidOperationException("Bug in NBitcoin notify the developers");

#if !NOFILEIO
            try
            {
                this.authentication = File.ReadAllText(GetCookiePath());
            }
            // We are only interested into the previous exception
            catch
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
            }
#else
            throw new NotSupportedException("Cookie authentication is not supported for this plateform");
#endif
        }

        private async Task<RPCResponse> SendCommandAsyncCoreAsync(RPCRequest request, bool throwIfRPCError)
        {
            RPCResponse response = null;
            ConcurrentQueue<Tuple<RPCRequest, TaskCompletionSource<RPCResponse>>> batches = this.batchedRequests;

            if (batches != null)
            {
                TaskCompletionSource<RPCResponse> source = new TaskCompletionSource<RPCResponse>();
                batches.Enqueue(Tuple.Create(request, source));
                response = await source.Task.ConfigureAwait(false);
            }

            HttpWebRequest webRequest = response == null ? CreateWebRequest() : null;

            if (response == null)
            {
                var writer = new StringWriter();
                request.WriteJSON(writer);
                writer.Flush();
                var json = writer.ToString();
                byte[] bytes = Encoding.UTF8.GetBytes(json);
#if !(PORTABLE || NETCORE)
                webRequest.ContentLength = bytes.Length;
#endif
                Stream dataStream = await webRequest.GetRequestStreamAsync().ConfigureAwait(false);
                await dataStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await dataStream.FlushAsync().ConfigureAwait(false);
                dataStream.Dispose();
            }

            WebResponse webResponse = null;
            WebResponse errorResponse = null;

            try
            {
                webResponse = response == null ? await webRequest.GetResponseAsync().ConfigureAwait(false) : null;
                response = response ?? RPCResponse.Load(await ToMemoryStreamAsync(webResponse.GetResponseStream()).ConfigureAwait(false));

                if (throwIfRPCError)
                    response.ThrowIfError();
            }
            catch (WebException ex)
            {
                if (ex.Response == null || ex.Response.ContentLength == 0 ||
                    !ex.Response.ContentType.Equals("application/json", StringComparison.Ordinal))
                    throw;

                errorResponse = ex.Response;
                response = RPCResponse.Load(await ToMemoryStreamAsync(errorResponse.GetResponseStream()).ConfigureAwait(false));
                if (throwIfRPCError)
                    response.ThrowIfError();
            }
            finally
            {
                if (errorResponse != null)
                {
                    errorResponse.Dispose();
                    errorResponse = null;
                }

                if (webResponse != null)
                {
                    webResponse.Dispose();
                    webResponse = null;
                }
            }

            return response;
        }

        private HttpWebRequest CreateWebRequest()
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(this.Address);
            webRequest.Headers[HttpRequestHeader.Authorization] = "Basic " + Encoders.Base64.EncodeData(Encoders.ASCII.DecodeData(this.authentication));
            webRequest.ContentType = "application/json-rpc";
            webRequest.Method = "POST";
            webRequest.Timeout = 1800000;//1 minute
            return webRequest;
        }

        private async Task<Stream> ToMemoryStreamAsync(Stream stream)
        {
            MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;
            return ms;
        }

#region P2P Networking
#if !NOSOCKET
        public PeerInfo[] GetPeersInfo()
        {
            PeerInfo[] peers = null;

            peers = GetPeersInfoAsync().GetAwaiter().GetResult();
            return peers;
        }

        public async Task<PeerInfo[]> GetPeersInfoAsync()
        {
            RPCResponse resp = await SendCommandAsync(RPCOperations.getpeerinfo).ConfigureAwait(false);
            var peers = resp.Result as JArray;
            var result = new PeerInfo[peers.Count];
            var i = 0;

            foreach (JToken peer in peers)
            {
                var localAddr = (string)peer["addrlocal"];
                var pingWait = peer["pingwait"] != null ? (double)peer["pingwait"] : 0;

                localAddr = string.IsNullOrEmpty(localAddr) ? "127.0.0.1:8333" : localAddr;

                result[i++] = new PeerInfo
                {
                    Id = (int)peer["id"],
                    Address = Utils.ParseIpEndpoint((string)peer["addr"], this.Network.DefaultPort),
                    LocalAddress = Utils.ParseIpEndpoint(localAddr, this.Network.DefaultPort),
                    Services = ulong.Parse((string)peer["services"]),
                    LastSend = Utils.UnixTimeToDateTime((uint)peer["lastsend"]),
                    LastReceive = Utils.UnixTimeToDateTime((uint)peer["lastrecv"]),
                    BytesSent = (long)peer["bytessent"],
                    BytesReceived = (long)peer["bytesrecv"],
                    ConnectionTime = Utils.UnixTimeToDateTime((uint)peer["conntime"]),
                    TimeOffset = TimeSpan.FromSeconds(Math.Min((long)int.MaxValue, (long)peer["timeoffset"])),
                    PingTime = peer["pingtime"] == null ? (TimeSpan?)null : TimeSpan.FromSeconds((double)peer["pingtime"]),
                    PingWait = TimeSpan.FromSeconds(pingWait),
                    Blocks = peer["blocks"] != null ? (int)peer["blocks"] : -1,
                    Version = (int)peer["version"],
                    SubVersion = (string)peer["subver"],
                    Inbound = (bool)peer["inbound"],
                    StartingHeight = (int)peer["startingheight"],
                    SynchronizedBlocks = (int)peer["synced_blocks"],
                    SynchronizedHeaders = (int)peer["synced_headers"],
                    IsWhiteListed = (bool)peer["whitelisted"],
                    BanScore = peer["banscore"] == null ? 0 : (int)peer["banscore"],
                    Inflight = peer["inflight"].Select(x => uint.Parse((string)x)).ToArray()
                };
            }

            return result;
        }

        public async Task<PeerInfo[]> GetBRhodiumPeersInfoAsync()
        {
            RPCResponse resp = await SendCommandAsync(RPCOperations.getpeerinfo).ConfigureAwait(false);
            var peers = resp.Result as JArray;
            var result = new PeerInfo[peers.Count];
            var i = 0;

            foreach (JToken peer in peers)
            {
                var localAddr = (string)peer["addrlocal"];
                var pingWait = peer["pingwait"] != null ? (double)peer["pingwait"] : 0;

                localAddr = string.IsNullOrEmpty(localAddr) ? "127.0.0.1:8333" : localAddr;

                result[i++] = new PeerInfo
                {
                    //Id = (int)peer["id"],
                    Address = Utils.ParseIpEndpoint((string)peer["addr"], this.Network.DefaultPort),
                    LocalAddress = Utils.ParseIpEndpoint(localAddr, this.Network.DefaultPort),
                    Services = ulong.Parse((string)peer["services"]),
                    LastSend = Utils.UnixTimeToDateTime((uint)peer["lastsend"]),
                    LastReceive = Utils.UnixTimeToDateTime((uint)peer["lastrecv"]),
                    BytesSent = (long)peer["bytessent"],
                    BytesReceived = (long)peer["bytesrecv"],
                    ConnectionTime = Utils.UnixTimeToDateTime((uint)peer["conntime"]),
                    TimeOffset = TimeSpan.FromSeconds(Math.Min((long)int.MaxValue, (long)peer["timeoffset"])),
                    PingTime = TimeSpan.FromSeconds((double)peer["pingtime"]),
                    PingWait = TimeSpan.FromSeconds(pingWait),
                    //Blocks = peer["blocks"] != null ? (int)peer["blocks"] : -1,
                    Version = (int)peer["version"],
                    SubVersion = (string)peer["subver"],
                    Inbound = (bool)peer["inbound"],
                    StartingHeight = (int)peer["startingheight"],
                    //SynchronizedBlocks = (int)peer["synced_blocks"],
                    //SynchronizedHeaders = (int)peer["synced_headers"],
                    //IsWhiteListed = (bool)peer["whitelisted"],
                    BanScore = peer["banscore"] == null ? 0 : (int)peer["banscore"],
                    //Inflight = peer["inflight"].Select(x => uint.Parse((string)x)).ToArray()
                };
            }

            return result;
        }

        public void AddNode(EndPoint nodeEndPoint, bool onetry = false)
        {
            if (nodeEndPoint == null)
                throw new ArgumentNullException("nodeEndPoint");

            SendCommand(RPCOperations.addnode, nodeEndPoint.ToString(), onetry ? "onetry" : "add");
        }

        public async Task AddNodeAsync(EndPoint nodeEndPoint, bool onetry = false)
        {
            if (nodeEndPoint == null)
                throw new ArgumentNullException("nodeEndPoint");

            await SendCommandAsync(RPCOperations.addnode, nodeEndPoint.ToString(), onetry ? "onetry" : "add").ConfigureAwait(false);
        }

        public void RemoveNode(EndPoint nodeEndPoint)
        {
            if (nodeEndPoint == null)
                throw new ArgumentNullException("nodeEndPoint");

            SendCommandAsync(RPCOperations.addnode, nodeEndPoint.ToString(), "remove");
        }

        public async Task RemoveNodeAsync(EndPoint nodeEndPoint)
        {
            if (nodeEndPoint == null)
                throw new ArgumentNullException("nodeEndPoint");

            await SendCommandAsync(RPCOperations.addnode, nodeEndPoint.ToString(), "remove").ConfigureAwait(false);
        }

        public async Task<AddedNodeInfo[]> GetAddedNodeInfoAsync()
        {
            RPCResponse result = await SendCommandAsync(RPCOperations.getaddednodeinfo).ConfigureAwait(false);
            JToken obj = result.Result;
            return obj.Select(entry => new AddedNodeInfo
            {
                AddedNode = Utils.ParseIpEndpoint((string)entry["addednode"], 8333),
                Connected = (bool)entry["connected"],
                Addresses = entry["addresses"].Select(x => new NodeAddressInfo
                {
                    Address = Utils.ParseIpEndpoint((string)x["address"], 8333),
                    Connected = x["connected"].ToString()
                })
            }).ToArray();
        }

        public AddedNodeInfo[] GetAddedNodeInfo()
        {
            AddedNodeInfo[] addedNodesInfo = null;

            addedNodesInfo = GetAddedNodeInfoAsync().GetAwaiter().GetResult();

            return addedNodesInfo;
        }

        public AddedNodeInfo GetAddedNodeInfo(EndPoint nodeEndPoint)
        {
            AddedNodeInfo addedNodeInfo = null;

            addedNodeInfo = GetAddedNodeInfoAsync(nodeEndPoint).GetAwaiter().GetResult();

            return addedNodeInfo;
        }

        public async Task<AddedNodeInfo> GetAddedNodeInfoAsync(EndPoint nodeEndPoint)
        {
            if (nodeEndPoint == null)
                throw new ArgumentNullException("nodeEndPoint");

            try
            {
                RPCResponse result = await SendCommandAsync(RPCOperations.getaddednodeinfo, nodeEndPoint.ToString()).ConfigureAwait(false);
                JToken e = result.Result;
                return e.Select(entry => new AddedNodeInfo
                {
                    AddedNode = Utils.ParseIpEndpoint((string)entry["addednode"], 8333),
                    Connected = (bool)entry["connected"],
                    Addresses = entry["addresses"].Select(x => new NodeAddressInfo
                    {
                        Address = Utils.ParseIpEndpoint((string)x["address"], 8333),
                        Connected = x["connected"].ToString()
                    })
                }).FirstOrDefault();
            }
            catch (RPCException ex)
            {
                if(ex.RPCCode == RPCErrorCode.RPC_CLIENT_NODE_NOT_ADDED)
                    return null;
                throw;
            }
        }
#endif

#endregion

#region Block chain and UTXO

        public uint256 GetBestBlockHash()
        {
            return uint256.Parse((string)SendCommand(RPCOperations.getbestblockhash).Result);
        }

        public async Task<uint256> GetBestBlockHashAsync()
        {
            return uint256.Parse((string)(await SendCommandAsync(RPCOperations.getbestblockhash).ConfigureAwait(false)).Result);
        }

        public BlockHeader GetBlockHeader(int height)
        {
            uint256 hash = GetBlockHash(height);
            return GetBlockHeader(hash);
        }

        public async Task<BlockHeader> GetBlockHeaderAsync(int height)
        {
            uint256 hash = await GetBlockHashAsync(height).ConfigureAwait(false);
            return await GetBlockHeaderAsync(hash).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the a whole block
        /// </summary>
        /// <param name="blockId"></param>
        /// <returns></returns>
        public async Task<Block> GetBlockAsync(uint256 blockId)
        {
            RPCResponse resp = await SendCommandAsync(RPCOperations.getblock, blockId.ToString(), false).ConfigureAwait(false);
            return new Block(Encoders.Hex.DecodeData(resp.Result.ToString()));
        }

        /// <summary>
        /// Get the a whole block
        /// </summary>
        /// <param name="blockId"></param>
        /// <returns></returns>
        public Block GetBlock(uint256 blockId)
        {
            return GetBlockAsync(blockId).GetAwaiter().GetResult();
        }

        public Block GetBlock(int height)
        {
            return GetBlockAsync(height).GetAwaiter().GetResult();
        }

        public async Task<Block> GetBlockAsync(int height)
        {
            uint256 hash = await GetBlockHashAsync(height).ConfigureAwait(false);
            return await GetBlockAsync(hash).ConfigureAwait(false);
        }

        public BlockHeader GetBlockHeader(uint256 blockHash)
        {
            RPCResponse resp = SendCommand("getblockheader", blockHash.ToString());
            return ParseBlockHeader(resp, this.network);
        }

        public async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
        {
            RPCResponse resp = await SendCommandAsync("getblockheader", blockHash.ToString()).ConfigureAwait(false);
            return ParseBlockHeader(resp, this.network);
        }

        private static BlockHeader ParseBlockHeader(RPCResponse resp, Network network)
        {
            var header = network.Consensus.ConsensusFactory.CreateBlockHeader();
            header.Version = (int)resp.Result["version"];
            header.Nonce = (uint)resp.Result["nonce"];
            header.Bits = new Target(Encoders.Hex.DecodeData((string)resp.Result["bits"]));

            if (!String.IsNullOrEmpty((string)resp.Result["previousblockhash"]))
                header.HashPrevBlock = uint256.Parse((string)resp.Result["previousblockhash"]);

            if (resp.Result["time"] != null)
                header.BlockTime = Utils.UnixTimeToDateTime((uint)resp.Result["time"]);

            if (resp.Result["merkleroot"] != null)
                header.HashMerkleRoot = uint256.Parse((string)resp.Result["merkleroot"]);

            return header;
        }

        public uint256 GetBlockHash(int height)
        {
            RPCResponse resp = SendCommand(RPCOperations.getblockhash, height);
            return uint256.Parse(resp.Result.ToString());
        }

        public async Task<uint256> GetBlockHashAsync(int height)
        {
            RPCResponse resp = await SendCommandAsync(RPCOperations.getblockhash, height).ConfigureAwait(false);
            return uint256.Parse(resp.Result.ToString());
        }

        public int GetBlockCount()
        {
            return (int)SendCommand(RPCOperations.getblockcount).Result;
        }

        public async Task<int> GetBlockCountAsync()
        {
            return (int)(await SendCommandAsync(RPCOperations.getblockcount).ConfigureAwait(false)).Result;
        }

        public uint256[] GetRawMempool()
        {
            RPCResponse result = SendCommand(RPCOperations.getrawmempool);
            var array = (JArray)result.Result;
            return array.Select(o => (string)o).Select(uint256.Parse).ToArray();
        }

        public async Task<uint256[]> GetRawMempoolAsync()
        {
            RPCResponse result = await SendCommandAsync(RPCOperations.getrawmempool).ConfigureAwait(false);
            var array = (JArray)result.Result;
            return array.Select(o => (string)o).Select(uint256.Parse).ToArray();
        }

        public UnspentTransaction GetTxOut(uint256 txid, uint vout, bool includeMemPool = true)
        {
            RPCResponse response = SendCommand(RPCOperations.gettxout, txid.ToString(), vout, includeMemPool);
            var responseObject = response.Result as JObject;
            if (responseObject == null)
                return null;

            return new UnspentTransaction(response.Result as JObject);
        }

        public async Task<UnspentTransaction> GetTxOutAsync(uint256 txid, uint vout, bool includeMemPool = true)
        {
            RPCResponse response = await SendCommandAsync(RPCOperations.gettxout, txid.ToString(), vout, includeMemPool).ConfigureAwait(false);
            var responseObject = response.Result as JObject;
            if (responseObject == null)
                return null;

            return new UnspentTransaction(responseObject);
        }

        /// <summary>
        /// GetTransactions only returns on txn which are not entirely spent unless you run bitcoinq with txindex=1.
        /// </summary>
        /// <param name="blockHash"></param>
        /// <returns></returns>
        public IEnumerable<Transaction> GetTransactions(uint256 blockHash)
        {
            if (blockHash == null)
                throw new ArgumentNullException("blockHash");

            RPCResponse resp = SendCommand(RPCOperations.getblock, blockHash.ToString());

            JArray tx = resp.Result["tx"] as JArray;
            if (tx != null)
            {
                foreach (JToken item in tx)
                {
                    Transaction result = GetRawTransaction(uint256.Parse(item.ToString()), false);
                    if (result != null)
                        yield return result;
                }
            }
        }

        public IEnumerable<Transaction> GetTransactions(int height)
        {
            return GetTransactions(GetBlockHash(height));
        }

#endregion

#region Coin generation

#endregion

#region Raw Transaction

        public Transaction DecodeRawTransaction(string rawHex)
        {
            RPCResponse response = SendCommand(RPCOperations.decoderawtransaction, rawHex);
            return Transaction.Parse(response.Result.ToString(), RawFormat.Satoshi);
        }

        public Transaction DecodeRawTransaction(byte[] raw)
        {
            return DecodeRawTransaction(Encoders.Hex.EncodeData(raw));
        }

        public async Task<Transaction> DecodeRawTransactionAsync(string rawHex)
        {
            RPCResponse response = await SendCommandAsync(RPCOperations.decoderawtransaction, rawHex).ConfigureAwait(false);
            return Transaction.Parse(response.Result.ToString(), RawFormat.Satoshi);
        }

        public Task<Transaction> DecodeRawTransactionAsync(byte[] raw)
        {
            return DecodeRawTransactionAsync(Encoders.Hex.EncodeData(raw));
        }

        /// <summary>
        /// getrawtransaction only returns on txn which are not entirely spent unless you run bitcoinq with txindex=1.
        /// </summary>
        /// <param name="txid"></param>
        /// <returns></returns>
        public Transaction GetRawTransaction(uint256 txid, bool throwIfNotFound = true)
        {
            return GetRawTransactionAsync(txid, throwIfNotFound).GetAwaiter().GetResult();
        }

        public async Task<Transaction> GetRawTransactionAsync(uint256 txid, bool throwIfNotFound = true)
        {
            RPCResponse response = await SendCommandAsync(new RPCRequest(RPCOperations.getrawtransaction, new[] { txid.ToString() }), throwIfNotFound).ConfigureAwait(false);

            if (throwIfNotFound)
                response.ThrowIfError();

            if (response.Error != null && response.Error.Code == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY)
                return null;

            response.ThrowIfError();

            var tx = new Transaction();
            tx.ReadWrite(Encoders.Hex.DecodeData(response.Result.ToString()));
            return tx;
        }

        public void SendRawTransaction(Transaction tx)
        {
            SendRawTransaction(tx.ToBytes());
        }

        public void SendRawTransaction(byte[] bytes)
        {
            SendCommand(RPCOperations.sendrawtransaction, Encoders.Hex.EncodeData(bytes));
        }

        public Task SendRawTransactionAsync(Transaction tx)
        {
            return SendRawTransactionAsync(tx.ToBytes());
        }

        public Task SendRawTransactionAsync(byte[] bytes)
        {
            return SendCommandAsync(RPCOperations.sendrawtransaction, Encoders.Hex.EncodeData(bytes));
        }

#endregion

#region Utility functions
        /// <summary>
        /// Returns information about a base58 or bech32 Bitcoin address
        /// </summary>
        /// <param name="address">a Bitcoin Address</param>
        /// <returns>{ IsValid }</returns>
        public ValidatedAddress ValidateAddress(BitcoinAddress address)
        {
            RPCResponse res = SendCommand(RPCOperations.validateaddress, address.ToString());
            return JsonConvert.DeserializeObject<ValidatedAddress>(res.Result.ToString());
       }

        /// <summary>
        /// Get the estimated fee per kb for being confirmed in nblock
        /// </summary>
        /// <param name="nblock"></param>
        /// <returns></returns>
        [Obsolete("Use EstimateFeeRate or TryEstimateFeeRate instead")]
        public FeeRate EstimateFee(int nblock)
        {
            RPCResponse response = SendCommand(RPCOperations.estimatefee, nblock);
            var result = response.Result.Value<decimal>();
            var money = Money.Coins(result);
            if (money.Satoshi < 0)
                money = Money.Zero;
            return new FeeRate(money);
        }

        /// <summary>
        /// Get the estimated fee per kb for being confirmed in nblock
        /// </summary>
        /// <param name="nblock"></param>
        /// <returns></returns>
        [Obsolete("Use EstimateFeeRateAsync instead")]
        public async Task<Money> EstimateFeeAsync(int nblock)
        {
            RPCResponse response = await SendCommandAsync(RPCOperations.estimatefee, nblock).ConfigureAwait(false);
            return Money.Parse(response.Result.ToString());
        }

        /// <summary>
        /// Get the estimated fee per kb for being confirmed in nblock
        /// </summary>
        /// <param name="nblock">The time expected, in block, before getting confirmed</param>
        /// <returns>The estimated fee rate</returns>
        /// <exception cref="NoEstimationException">The Fee rate couldn't be estimated because of insufficient data from Bitcoin Core</exception>
        public FeeRate EstimateFeeRate(int nblock)
        {
            return EstimateFeeRateAsync(nblock).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Tries to get the estimated fee per kb for being confirmed in nblock
        /// </summary>
        /// <param name="nblock">The time expected, in block, before getting confirmed</param>
        /// <returns>The estimated fee rate or null</returns>
        public async Task<FeeRate> TryEstimateFeeRateAsync(int nblock)
        {
            return await EstimateFeeRateImplAsync(nblock).ConfigureAwait(false);
        }

        /// <summary>
        /// Tries to get the estimated fee per kb for being confirmed in nblock
        /// </summary>
        /// <param name="nblock">The time expected, in block, before getting confirmed</param>
        /// <returns>The estimated fee rate or null</returns>
        public FeeRate TryEstimateFeeRate(int nblock)
        {
            return TryEstimateFeeRateAsync(nblock).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the estimated fee per kb for being confirmed in nblock
        /// </summary>
        /// <param name="nblock">The time expected, in block, before getting confirmed</param>
        /// <returns>The estimated fee rate</returns>
        /// <exception cref="NoEstimationException">when fee couldn't be estimated</exception>
        public async Task<FeeRate> EstimateFeeRateAsync(int nblock)
        {
            FeeRate feeRate = await EstimateFeeRateImplAsync(nblock);
            if (feeRate == null)
                throw new NoEstimationException(nblock);

            return feeRate;
        }

        private async Task<FeeRate> EstimateFeeRateImplAsync(int nblock)
        {
            RPCResponse response = await SendCommandAsync(RPCOperations.estimatefee, nblock).ConfigureAwait(false);
            var result = response.Result.Value<decimal>();
            var money = Money.Coins(result);
            if (money.Satoshi < 0)
                return null;

            return new FeeRate(money);
        }

        [Obsolete("Removed by Bitcoin Core v0.15.0 Release")]
        public decimal EstimatePriority(int nblock)
        {
            decimal priority = 0;

            priority = EstimatePriorityAsync(nblock).GetAwaiter().GetResult();
            return priority;
        }

        [Obsolete("Removed by Bitcoin Core v0.15.0 Release")]
        public async Task<decimal> EstimatePriorityAsync(int nblock)
        {
            if (nblock < 0)
                throw new ArgumentOutOfRangeException("nblock", "nblock must be greater or equal to zero");

            RPCResponse response = await SendCommandAsync("estimatepriority", nblock).ConfigureAwait(false);
            return response.Result.Value<decimal>();
        }

        /// <summary>
        /// Requires wallet support. Requires an unlocked wallet or an unencrypted wallet.
        /// </summary>
        /// <param name="address">A P2PKH or P2SH address to which the bitcoins should be sent</param>
        /// <param name="amount">The amount to spend</param>
        /// <param name="commentTx">A locally-stored (not broadcast) comment assigned to this transaction. Default is no comment</param>
        /// <param name="commentDest">A locally-stored (not broadcast) comment assigned to this transaction. Meant to be used for describing who the payment was sent to. Default is no comment</param>
        /// <returns>The TXID of the sent transaction</returns>
        public uint256 SendToAddress(BitcoinAddress address, Money amount, string commentTx = null, string commentDest = null)
        {
            uint256 txid = null;

            txid = SendToAddressAsync(address, amount, commentTx, commentDest).GetAwaiter().GetResult();
            return txid;
        }


        public uint256 SendToAddress(string walletName, string password, string address, decimal amount)
        {
            return SendToAddressAsync(walletName, password, address, amount).GetAwaiter().GetResult();
        }

        public async Task<uint256> SendToAddressAsync(string walletName, string password, string address, decimal amount)
        {
            List<object> parameters = new List<object>();
            parameters.Add(walletName);
            parameters.Add(password);
            parameters.Add(address);
            parameters.Add(amount.ToString());

            RPCResponse resp = await SendCommandAsync(RPCOperations.sendtoaddress, parameters.ToArray()).ConfigureAwait(false);
            return uint256.Parse(resp.Result.ToString());
        }

        /// <summary>
        /// Requires wallet support. Requires an unlocked wallet or an unencrypted wallet.
        /// </summary>
        /// <param name="address">A P2PKH or P2SH address to which the bitcoins should be sent</param>
        /// <param name="amount">The amount to spend</param>
        /// <param name="commentTx">A locally-stored (not broadcast) comment assigned to this transaction. Default is no comment</param>
        /// <param name="commentDest">A locally-stored (not broadcast) comment assigned to this transaction. Meant to be used for describing who the payment was sent to. Default is no comment</param>
        /// <returns>The TXID of the sent transaction</returns>
        public async Task<uint256> SendToAddressAsync(BitcoinAddress address, Money amount, string commentTx = null, string commentDest = null)
        {
            List<object> parameters = new List<object>();
            parameters.Add(address.ToString());
            parameters.Add(amount.ToString());

            if (commentTx != null)
                parameters.Add(commentTx);

            if (commentDest != null)
                parameters.Add(commentDest);

            RPCResponse resp = await SendCommandAsync(RPCOperations.sendtoaddress, parameters.ToArray()).ConfigureAwait(false);
            return uint256.Parse(resp.Result.ToString());
        }

        public bool SetTxFee(FeeRate feeRate)
        {
            return SendCommand(RPCOperations.settxfee, new[] { feeRate.FeePerK.ToString() }).Result.ToString() == "true";
        }

#endregion

        public async Task<uint256[]> GenerateAsync(int nBlocks)
        {
            if (nBlocks < 0)
                throw new ArgumentOutOfRangeException("nBlocks");

            var result = (JArray)(await SendCommandAsync(RPCOperations.generate, nBlocks).ConfigureAwait(false)).Result;

            return result.Select(r => new uint256(r.Value<string>())).ToArray();
        }

        public uint256[] Generate(int nBlocks)
        {
            return GenerateAsync(nBlocks).GetAwaiter().GetResult();
        }
    }

#if !NOSOCKET
    public class PeerInfo
    {
        public int Id { get; internal set; }
        public IPEndPoint Address { get; internal set; }
        public IPEndPoint LocalAddress { get; internal set; }
        public ulong Services { get; internal set; }
        public DateTimeOffset LastSend { get; internal set; }
        public DateTimeOffset LastReceive { get; internal set; }
        public long BytesSent { get; internal set; }
        public long BytesReceived { get; internal set; }
        public DateTimeOffset ConnectionTime { get; internal set; }
        public TimeSpan? PingTime { get; internal set; }
        public int Version { get; internal set; }
        public string SubVersion { get; internal set; }
        public bool Inbound { get; internal set; }
        public int StartingHeight { get; internal set; }
        public int BanScore { get; internal set; }
        public int SynchronizedHeaders { get; internal set; }
        public int SynchronizedBlocks { get; internal set; }
        public uint[] Inflight { get; internal set; }
        public bool IsWhiteListed { get; internal set; }
        public TimeSpan PingWait { get; internal set; }
        public int Blocks { get; internal set; }
        public TimeSpan TimeOffset { get; internal set; }
    }

    public class AddedNodeInfo
    {
        public EndPoint AddedNode { get; internal set; }
        public bool Connected { get; internal set; }
        public IEnumerable<NodeAddressInfo> Addresses { get; internal set; }
    }

    public class NodeAddressInfo
    {
        public IPEndPoint Address { get; internal set; }
        public string Connected { get; internal set; }
    }
#endif

    public class NoEstimationException : Exception
    {
        public NoEstimationException(int nblock)
            : base("The FeeRate couldn't be estimated because of insufficient data from Bitcoin Core. Try to use smaller nBlock, or wait Bitcoin Core to gather more data.")
        {
        }
    }
}
#endif