﻿using System;
using System.Reactive;
using BRhodium.Node.Utilities;

namespace BRhodium.Node.Signals
{
    /// <summary>
    /// Consumer of messages produced by <see cref="Signaler{T}"/>.
    /// </summary>
    /// <typeparam name="T">Type of messages being consumed.</typeparam>
    public interface ISignalObserver<T> : IObserver<T>, IDisposable
    {
    }

    /// <inheritdoc />
    public abstract class SignalObserver<T> : ObserverBase<T>, ISignalObserver<T>
    {
        /// <inheritdoc />
        protected override void OnErrorCore(Exception error)
        {
            Guard.NotNull(error, nameof(error));
        }

        /// <inheritdoc />
        protected override void OnCompletedCore()
        {
        }
    }
}
