﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using BRhodium.Node.Configuration;
using Xunit;

namespace BRhodium.Bitcoin.Features.Consensus.Tests
{
    public class ConsensusSettingsTest
    {
        [Fact]
        public void LoadConfigWithAssumeValidHexLoads()
        {
            uint256 validHexBlock = new uint256("00000000229d9fb87182d73870d53f9fdd9b76bfc02c059e6d9a6c7a3507031d");
            Network network = Network.TestNet;
            NodeSettings nodeSettings = new NodeSettings(network, args:new string[] { $"-assumevalid={validHexBlock.ToString()}" });
            ConsensusSettings settings = new ConsensusSettings().Load(nodeSettings);
            Assert.Equal(validHexBlock, settings.BlockAssumedValid);
        }

        [Fact]
        public void LoadConfigWithAssumeValidZeroSetsToNull()
        {
            LoggerFactory loggerFactory = new LoggerFactory();
            Network network = Network.TestNet;
            NodeSettings nodeSettings = new NodeSettings(network, args:new string[] { "-assumevalid=0" });
            ConsensusSettings settings = new ConsensusSettings().Load(nodeSettings);
            Assert.Null(settings.BlockAssumedValid);
        }

        [Fact]
        public void LoadConfigWithInvalidAssumeValidThrowsConfigException()
        {
            LoggerFactory loggerFactory = new LoggerFactory();
            Network network = Network.TestNet;
            NodeSettings nodeSettings = new NodeSettings(network, args:new string[] { "-assumevalid=xxx" });
            Assert.Throws<ConfigurationException>(() => new ConsensusSettings().Load(nodeSettings));
        }

        [Fact]
        public void LoadConfigWithDefaultsSetsToNetworkDefault()
        {
            Network network = Network.BRhodiumMain;
            ConsensusSettings settings = new ConsensusSettings().Load(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);

            network = Network.BRhodiumTest;
            settings = new ConsensusSettings().Load(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);

            network = Network.Main;
            settings = new ConsensusSettings().Load(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);

            network = Network.TestNet;
            settings = new ConsensusSettings().Load(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);
        }
    }
}
