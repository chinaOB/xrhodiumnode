﻿using NBitcoin;
using NBitcoin.Protocol.Payloads;

namespace BRhodium.Node.P2P.Protocol.Payloads
{
    [Payload("utxos")]
    public class UTxOutputPayload : Payload
    {
        private UTxOutputs uTxOutputs;

        public override void ReadWriteCore(BitcoinStream stream)
        {
            this.uTxOutputs = new UTxOutputs();
            stream.ReadWrite(ref this.uTxOutputs);
        }
    }
}