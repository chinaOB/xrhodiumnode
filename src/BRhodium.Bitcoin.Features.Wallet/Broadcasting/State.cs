﻿namespace BRhodium.Bitcoin.Features.Wallet.Broadcasting
{
    public enum State
    {
        CantBroadcast,
        ToBroadcast,
        Broadcasted,
        Propagated
    }
}
