﻿using NBitcoin;

namespace BRhodium.Node.Utilities
{
    /// <summary>
    /// Extension methods for NBitcoin's Network class.
    /// </summary>
    public static class NetworkExtensions
    {
        /// <summary>Fake height value used in Coins to signify they are only in the memory pool (since 0.8).</summary>
        public const int MempoolHeight = 0x7FFFFFFF;

        /// <summary>
        /// Determines whether this network is a test network.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <returns><c>true</c> if the specified network is test, <c>false</c> otherwise.</returns>
        public static bool IsTest(this Network network)
        {
            return network.Name.ToLowerInvariant().Contains("test");
        }
    }
}
