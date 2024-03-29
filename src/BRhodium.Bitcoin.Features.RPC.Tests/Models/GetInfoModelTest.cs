﻿using System.Linq;
using NBitcoin;
using Newtonsoft.Json.Linq;
using BRhodium.Bitcoin.Features.RPC.Models;
using Xunit;

namespace BRhodium.Bitcoin.Features.RPC.Tests.Models
{
    public class GetInfoModelTest 
    {
        private static readonly string[] AllPropertyNames = new string[] {
                "version",
                "protocolversion",
                "walletversion",
                "balance",
                "blocks",
                "timeoffset",
                "connections",
                "proxy",
                "difficulty",
                "testnet",
                "keypoololdest",
                "keypoolsize",
                "unlocked_until",
                "paytxfee",
                "relayfee",
                "errors",
            };

        private static readonly string[] RequiredPropertyNames = new string[] {
                "version",
                "protocolversion",
                "walletversion",
                "blocks",
                "timeoffset",
                "proxy",
                "difficulty",
                "testnet",
                "relayfee",
                "errors",
            };

        [Fact]
        public void GetInfoSerializeFullTest()
        {
            var expectedOrderedPropertyNames = AllPropertyNames;
            var info = new GetInfoModel
            {
                Connections = 0,
                WalletVersion = default(uint),
                Balance = default(decimal),
                KeypoolOldest = default(long),
                KeypoolSize = default(int),
                UnlockedUntil = default(uint),
                PayTxFee = default(decimal)
            };

            JObject obj = JObject.FromObject(info);
            Assert.True(obj.HasValues);
            var actualOrderedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name).ToArray<string>();

            Assert.Equal(expectedOrderedPropertyNames, actualOrderedPropertyNames);
        }

        [Fact]
        public void GetInfoSerializeSparseTest()
        {
            var expectedOrderedPropertyNames = RequiredPropertyNames;
            var info = new GetInfoModel();

            JObject obj = JObject.FromObject(info);
            Assert.True(obj.HasValues);
            var actualOrderedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name).ToArray<string>();

            Assert.Equal(expectedOrderedPropertyNames, actualOrderedPropertyNames);
        }

        [Fact]
        public void GetInfoDeserializeSparseTest()
        {
            IOrderedEnumerable<string> expectedSortedPropertyNames = RequiredPropertyNames.OrderBy(name => name);
            string json = "{\n" +
                         "     \"version\": 1010000,\n" +
                         "     \"protocolversion\": 70012,\n" +
                         "     \"blocks\": 460828,\n" +
                         "     \"timeoffset\": 0,\n" +
                         "     \"proxy\": \"\",\n" +
                         "     \"difficulty\": 499635929816.6675,\n" +
                         "     \"testnet\": false,\n" +
                         "     \"relayfee\": 0.00001000,\n" +
                         "     \"errors\": \"URGENT: Alert key compromised, upgrade required\",\n" +
                         "     \"walletversion\": \"1.0\"\n" +
                         "   }\n";

            JObject obj = JObject.Parse(json);
            IOrderedEnumerable<string> actualSortedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name).OrderBy(name => name);

            Assert.Equal(expectedSortedPropertyNames, actualSortedPropertyNames);
        }

        [Fact]
        public void GetInfoDeserializeFullTest()
        {
            IOrderedEnumerable<string> expectedSortedPropertyNames = AllPropertyNames.OrderBy(name => name);
            string json = "{\n" +
                         "     \"version\": 1010000,\n" +
                         "     \"protocolversion\": 70012,\n" +
                         "     \"walletversion\": 60000,\n" +
                         "     \"balance\": 0.00000002,\n" +
                         "     \"blocks\": 460828,\n" +
                         "     \"timeoffset\": 0,\n" +
                         "     \"connections\": 44,\n" +
                         "     \"proxy\": \"\",\n" +
                         "     \"difficulty\": 499635929816.6675,\n" +
                         "     \"testnet\": false,\n" +
                         "     \"keypoololdest\": 1437418454,\n" +
                         "     \"keypoolsize\": 101,\n" +
                         "     \"unlocked_until\": 0,\n" +
                         "     \"paytxfee\": 0.00010000,\n" +
                         "     \"relayfee\": 0.00001000,\n" +
                         "     \"errors\": \"URGENT: Alert key compromised, upgrade required\"\n" +
                         "   }\n";

            JObject obj = JObject.Parse(json);
            IOrderedEnumerable<string> actualSortedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name).OrderBy(name => name);
            GetInfoModel model = Newtonsoft.Json.JsonConvert.DeserializeObject<GetInfoModel>(json);

            Assert.Equal(expectedSortedPropertyNames, actualSortedPropertyNames);
            Assert.Equal(1010000u, model.Version);
            Assert.Equal(70012u, model.ProtocolVersion);
            Assert.Equal(60000u, model.WalletVersion);
            Assert.Equal(Money.Satoshis(2).ToUnit(MoneyUnit.XRC), model.Balance);
            Assert.Equal(460828, model.Blocks);
            Assert.Equal(0, model.TimeOffset);
            Assert.Equal(44, model.Connections);
            Assert.Empty(model.Proxy);
            Assert.Equal(499635929816.6675, model.Difficulty, 3);
            Assert.False(model.Testnet);
            Assert.Equal(1437418454, model.KeypoolOldest);
            Assert.Equal(101, model.KeypoolSize);
            Assert.Equal(0u, model.UnlockedUntil);
            Assert.Equal(Money.Satoshis(10000).ToUnit(MoneyUnit.XRC), model.PayTxFee);
            Assert.Equal(Money.Satoshis(1000).ToUnit(MoneyUnit.XRC), model.RelayFee);
            Assert.Equal("URGENT: Alert key compromised, upgrade required", model.Errors);
        }
    }
}
