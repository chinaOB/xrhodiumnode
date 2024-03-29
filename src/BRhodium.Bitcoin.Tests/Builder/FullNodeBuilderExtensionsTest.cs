﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using BRhodium.Node.Builder;
using BRhodium.Node.Builder.Feature;
using BRhodium.Node.Configuration;
using Xunit;

namespace BRhodium.Node.Tests.Builder
{
    public class FullNodeBuilderExtensionsTest
    {
        private FeatureCollection featureCollection;
        private List<Action<IFeatureCollection>> featureCollectionDelegates;
        private FullNodeBuilder fullNodeBuilder;
        private List<Action<IServiceCollection>> serviceCollectionDelegates;
        private List<Action<IServiceProvider>> serviceProviderDelegates;

        public FullNodeBuilderExtensionsTest()
        {
            this.serviceCollectionDelegates = new List<Action<IServiceCollection>>();
            this.serviceProviderDelegates = new List<Action<IServiceProvider>>();
            this.featureCollectionDelegates = new List<Action<IFeatureCollection>>();
            this.featureCollection = new FeatureCollection();

            this.fullNodeBuilder = new FullNodeBuilder(this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);
        }

        [Fact]
        public void UseNodeSettingsConfiguresNodeBuilderWithNodeSettings()
        {
            FullNodeBuilderNodeSettingsExtension.UseDefaultNodeSettings(this.fullNodeBuilder);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(NodeSettings.Default().ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(NodeSettings.Default().DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(NodeSettings.Default().Network, this.fullNodeBuilder.Network);
            Assert.Single(this.serviceCollectionDelegates);
        }

        [Fact]
        public void UseDefaultNodeSettingsConfiguresNodeBuilderWithDefaultSettings()
        {
            var nodeSettings = new NodeSettings(args: new string[] {
                "-datadir=TestData/FullNodeBuilder/UseNodeSettings" });

            FullNodeBuilderNodeSettingsExtension.UseNodeSettings(this.fullNodeBuilder, nodeSettings);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(Network.Main, this.fullNodeBuilder.Network);
            Assert.Single(this.serviceCollectionDelegates);
        }

        [Fact]
        public void UseNodeSettingsUsingTestNetConfiguresNodeBuilderWithTestnetSettings()
        {
            NodeSettings nodeSettings = new NodeSettings(Network.TestNet, args:new string[] {
                "-datadir=TestData/FullNodeBuilder/UseNodeSettings" });

            FullNodeBuilderNodeSettingsExtension.UseNodeSettings(this.fullNodeBuilder, nodeSettings);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(Network.TestNet, this.fullNodeBuilder.Network);
            Assert.Single(this.serviceCollectionDelegates);
        }

        [Fact]
        public void UseNodeSettingsUsingRegTestNetConfiguresNodeBuilderWithRegTestNet()
        {
            NodeSettings nodeSettings = new NodeSettings(Network.RegTest, args:new string[] {
                "-datadir=TestData/FullNodeBuilder/UseNodeSettings" });

            FullNodeBuilderNodeSettingsExtension.UseNodeSettings(this.fullNodeBuilder, nodeSettings);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(Network.RegTest, this.fullNodeBuilder.Network);
            Assert.Single(this.serviceCollectionDelegates);
        }
    }
}
