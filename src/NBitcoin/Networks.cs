using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace NBitcoin
{
    public enum CoinType
    {
        /// <summary>
        /// BRhodium
        /// </summary>
        BRhodium = 10291,

        /// <summary>
        /// Testnet (all coins)
        /// </summary>
        Testnet = 1,

        /// <summary>
        /// RegTest
        /// </summary>
        RegTest = 1
    }

    public partial class Network
    {
        /// <summary> Bitcoin maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int BitcoinMaxTimeOffsetSeconds = 70 * 60;

        /// <summary> BRhodium maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int BRhodiumMaxTimeOffsetSeconds = 25 * 60;

        /// <summary> Bitcoin default value for the maximum tip age in seconds to consider the node in initial block download (24 hours). </summary>
        public const int BitcoinDefaultMaxTipAgeInSeconds = 24 * 60 * 60;

        /// <summary> BRhodium default value for the maximum tip age in seconds to consider the node in initial block download (2 hours). </summary>
        public const int BRhodiumDefaultMaxTipAgeInSeconds = 2 * 60 * 60;

        /// <summary> The name of the root folder containing the different BRhodium blockchains (BRhodiumMain, BRhodiumTest, BRhodiumRegTest). </summary>
        public const string BRhodiumRootFolderName = "BRhodium";

        /// <summary> The default name used for the BRhodium configuration file. </summary>
        public const string BRhodiumDefaultConfigFilename = "BRhodium.conf";

        /// <summary>
        /// Default name for base network
        /// </summary>
        public const string BRhodiumBaseName = "BRhodiumMain";

        public static Network Main => Network.GetNetwork(BRhodiumBaseName) ?? InitBRhodiumMain();

        public static Network TestNet => Network.GetNetwork("BRhodiumTest") ?? InitBRhodiumTest();

        public static Network RegTest => Network.GetNetwork("BRhodiumRegTest") ?? InitBRhodiumRegTest();

        public static Network BRhodiumMain => Network.GetNetwork(BRhodiumBaseName) ?? InitBRhodiumMain();

        public static Network BRhodiumTest => Network.GetNetwork("BRhodiumTest") ?? InitBRhodiumTest();

        public static Network BRhodiumRegTest => Network.GetNetwork("BRhodiumRegTest") ?? InitBRhodiumRegTest();

        private static Network InitBRhodiumMain()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0x33;
            messageStart[1] = 0x33;
            messageStart[2] = 0x34;
            messageStart[3] = 0x35;
            var magic = BitConverter.ToUInt32(messageStart, 0);

            Network network = new Network
            {
                Name = "BRhodiumMain",
                RootFolderName = BRhodiumRootFolderName,
                DefaultConfigFilename = BRhodiumDefaultConfigFilename,
                Magic = magic,
                DefaultPort = 37270,
                RPCPort = 19660,
                MaxTimeOffsetSeconds = BRhodiumMaxTimeOffsetSeconds,
                MaxTipAge = 604800, //one week
                MinTxFee = 1000,
                FallbackFee = 20000,
                MinRelayTxFee = 1000
            };

            network.Consensus.SubsidyHalvingInterval = 210000;
            network.Consensus.MajorityEnforceBlockUpgrade = 750;
            network.Consensus.MajorityRejectBlockOutdated = 950;
            network.Consensus.MajorityWindow = 1000;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            network.Consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            network.Consensus.PowLimit = new Target(uint256.Parse("0000411a69900000000000000000000000000000000000000000000000000000"));
            network.Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            network.Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            network.Consensus.PowAllowMinDifficultyBlocks = false;
            network.Consensus.PowNoRetargeting = false;
            network.Consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            network.Consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            network.Consensus.CoinType = (int)CoinType.BRhodium;
            network.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            network.Consensus.ConsensusFactory = new PowConsensusFactory() { Consensus = network.Consensus};

            var hexMain = "02000000010000000000000000000000000000000000000000000000000000000000000000ffffffff2204691a411e01041a52656c6561736520746865204b72616b656e212121205a657573ffffffff0100000000000000000000000000";
            PowBlock genesis = CreateBRhodiumGenesisBlock((PowConsensusFactory)network.Consensus.ConsensusFactory, 1512043200, 0, network.Consensus.PowLimit.ToCompact(), 45, network, hexMain);
            network.genesis = genesis;
            network.Consensus.HashGenesisBlock = genesis.GetHash(network);

            network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (61) };
            network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (123) };
            network.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (100) };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            network.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("rh");
            network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            //network.DNSSeeds.AddRange(new[]
            //{
            //    new DNSSeedData("testnet1.BRhodiumplatform.com", "testnet1.BRhodiumplatform.com"),
            //    new DNSSeedData("testnet2.BRhodiumplatform.com", "testnet2.BRhodiumplatform.com"),
            //    new DNSSeedData("testnet3.BRhodiumplatform.com", "testnet3.BRhodiumplatform.com"),
            //    new DNSSeedData("testnet4.BRhodiumplatform.com", "testnet4.BRhodiumplatform.com")
            //});

            //string[] seeds = { "1.34.168.128:8333", "1.202.128.218:8333" };
            //Random rand = new Random();
            //TimeSpan oneWeek = TimeSpan.FromDays(7);
            //foreach (string seed in seeds)
            //{
            //    // It'll only connect to one or two seed nodes because once it connects,
            //    // it'll get a pile of addresses with newer timestamps.
            //    // Seed nodes are given a random 'last seen time' of between one and two weeks ago.
            //    NetworkAddress addr = new NetworkAddress
            //    {
            //        Time = DateTime.UtcNow - (TimeSpan.FromSeconds(rand.NextDouble() * oneWeek.TotalSeconds)) - oneWeek,
            //        Endpoint = Utils.ParseIpEndpoint(seed, network.DefaultPort)
            //    };
            //    network.SeedNodes.Add(addr);
            //}

            Network.Register(network);

            return network;
        }

        private static Network InitBRhodiumTest()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0x39;
            messageStart[1] = 0x33;
            messageStart[2] = 0x34;
            messageStart[3] = 0x35;
            var magic = BitConverter.ToUInt32(messageStart, 0); // 0xefc0f2cd

            Network network = new Network
            {
                Name = "BRhodiumTest",
                RootFolderName = BRhodiumRootFolderName,
                DefaultConfigFilename = BRhodiumDefaultConfigFilename,
                Magic = magic,
                DefaultPort = 16665,
                RPCPort = 16661,
                MaxTimeOffsetSeconds = BRhodiumMaxTimeOffsetSeconds,
                MaxTipAge = 604800, //one week
                MinTxFee = 10000,
                FallbackFee = 60000,
                MinRelayTxFee = 10000
            };

            network.Consensus.SubsidyHalvingInterval = 210000;
            network.Consensus.MajorityEnforceBlockUpgrade = 750;
            network.Consensus.MajorityRejectBlockOutdated = 950;
            network.Consensus.MajorityWindow = 1000;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            network.Consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            network.Consensus.PowLimit = new Target(uint256.Parse("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")); //0.00000000046565418188 
            network.Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            network.Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            network.Consensus.PowAllowMinDifficultyBlocks = true;
            network.Consensus.PowNoRetargeting = false;
            network.Consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            network.Consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            network.Consensus.CoinType = (int)CoinType.Testnet;
            network.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            network.Consensus.ConsensusFactory = new PowConsensusFactory() { Consensus = network.Consensus };

            PowBlock genesis = CreateBRhodiumGenesisBlock((PowConsensusFactory)network.Consensus.ConsensusFactory, 1527811200, 0, network.Consensus.PowLimit.ToCompact(), 45, network, null);
            genesis.Header.Bits = network.Consensus.PowLimit;
            network.genesis = genesis;
            network.Consensus.HashGenesisBlock = genesis.GetHash(network);

            network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (65) };
            network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (128) };
            network.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (100) };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            network.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("th");
            network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            Network.Register(network);

            return network;
        }

        private static Network InitBRhodiumRegTest()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0x34;
            messageStart[1] = 0x33;
            messageStart[2] = 0x34;
            messageStart[3] = 0x35;
            var magic = BitConverter.ToUInt32(messageStart, 0); // 0xefc0f2cd

            Network network = new Network
            {
                Name = "BRhodiumRegTest",
                RootFolderName = BRhodiumRootFolderName,
                DefaultConfigFilename = BRhodiumDefaultConfigFilename,
                Magic = magic,
                DefaultPort = 16665,
                RPCPort = 16661,
                MaxTimeOffsetSeconds = BRhodiumMaxTimeOffsetSeconds,
                MaxTipAge = BRhodiumDefaultMaxTipAgeInSeconds
            };

            network.Consensus.SubsidyHalvingInterval = 210000;
            network.Consensus.MajorityEnforceBlockUpgrade = 750;
            network.Consensus.MajorityRejectBlockOutdated = 950;
            network.Consensus.MajorityWindow = 1000;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            network.Consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            network.Consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            network.Consensus.PowLimit = new Target(uint256.Parse("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")); //0.00000000046565418188 
            network.Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            network.Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            network.Consensus.PowAllowMinDifficultyBlocks = true;
            network.Consensus.PowNoRetargeting = false;
            network.Consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            network.Consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            network.Consensus.CoinType = (int)CoinType.RegTest;
            network.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            network.Consensus.ConsensusFactory = new PowConsensusFactory() { Consensus = network.Consensus };

            PowBlock genesis = CreateBRhodiumGenesisBlock((PowConsensusFactory)network.Consensus.ConsensusFactory, 1527811200, 0, network.Consensus.PowLimit.ToCompact(), 45, network);
            genesis.Header.Bits = network.Consensus.PowLimit;
            network.genesis = genesis;
            network.Consensus.HashGenesisBlock = genesis.GetHash(network);

            network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (65) };
            network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (128) };
            network.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (100) };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            network.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("th");
            network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            Network.Register(network);

            return network;
        }

        private static PowBlock CreateBRhodiumGenesisBlock(PowConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Network network, string hexNew = null)
        {
            string message = "Release the Kraken!!! Zeus";
            return CreateBRhodiumGenesisBlock(consensusFactory, message, nTime, nNonce, nBits, nVersion, network, hexNew);
        }

        private static PowBlock CreateBRhodiumGenesisBlock(PowConsensusFactory consensusFactory, string message, uint nTime, uint nNonce, uint nBits, int nVersion, Network network, string hexNew = null)
        {
            //nTime = 1512043200 => Thursday, November 30, 2017 12:00:00 PM (born BTR)
            //nTime = 1527811200 => Friday, Jun 1, 2017 12:00:00 PM (born TestBTR)
            //nBits = 0x1d00ffff (it is exactly 0x1b = 27 bytes long) => 0x00ffff0000000000000000000000000000000000000000000000000000 => 1
            //nNonce = XTimes to trying to find a genesis block
            var hex = Encoders.Hex.DecodeData("02000000010000000000000000000000000000000000000000000000000000000000000000ffffffff2204ffff7f2001041a52656c6561736520746865204b72616b656e212121205a657573ffffffff0100000000000000000000000000");
            if (hexNew != null) hex = Encoders.Hex.DecodeData(hexNew);

            var scriptPubKey = new Script(Op.GetPushOp(hex), OpcodeType.OP_CHECKSIG);

            PowTransaction txNew = consensusFactory.CreateTransaction() as PowTransaction;
            txNew.Version = 2;
            txNew.Time = nTime;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(nBits), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)4 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(message)))
            });
            txNew.AddOutput(new TxOut()
            {
                Value = Money.Zero,
                ScriptPubKey = scriptPubKey
            });
            
            //var hexNewx = Encoders.Hex.EncodeData(txNew.ToBytes(ProtocolVersion.BTR_PROTOCOL_VERSION, network));

            PowBlock genesis = consensusFactory.CreateBlock() as PowBlock;
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(nTime);
            genesis.Header.Bits = nBits;
            genesis.Header.Nonce = nNonce;
            genesis.Header.Version = nVersion;
            genesis.Transactions.Add(txNew);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();
            return genesis;
        }
    }
}
