﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.RPC
{
    /// <summary>
    /// An interface for a factory that can create <see cref="IRPCClient"/> instances.
    /// </summary>
    public interface IRPCClientFactory
    {
        /// <summary>
        /// Create a new RPCClient instance.
        /// </summary>
        /// <param name="authenticationString">username:password or the content of the .cookie file or null to auto configure.</param>
        /// <param name="address">The binding address.</param>
        /// <param name="network">The network.</param>
        IRPCClient Create(string authenticationString, Uri address, Network network);
    }

    /// <summary>
    /// A factory for creating new instances of an <see cref="RPCClient"/>.
    /// </summary>
    public class RPCClientFactory : IRPCClientFactory
    {
        /// <inheritdoc/>
        public IRPCClient Create(string authenticationString, Uri address, Network network)
        {
            Guard.NotNull(authenticationString, nameof(authenticationString));
            Guard.NotNull(address, nameof(address));
            Guard.NotNull(network, nameof(network));

            return new RPCClient(authenticationString, address, network);
        }
    }
}
