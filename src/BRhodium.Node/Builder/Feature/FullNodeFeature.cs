﻿using System;

namespace BRhodium.Node.Builder.Feature
{
    /// <summary>
    /// Defines methods for features that are managed by the FullNode.
    /// </summary>
    public interface IFullNodeFeature : IDisposable
    {
        /// <summary>
        /// Called on all the features prior to calling Initialize on all the features.
        /// </summary>
        void LoadConfiguration();

        /// <summary>
        /// Triggered when the FullNode host has fully started.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Validates the feature's required dependencies are all present.
        /// </summary>
        /// <exception cref="MissingDependencyException">should be thrown if dependency is missing</exception>
        /// <param name="services">Services and features registered to node.</param>
        void ValidateDependencies(IFullNodeServiceProvider services);
    }

    /// <summary>
    /// A feature is used to extend functionality into the full node.
    /// It can manage its life time or use the full node disposable resources.
    /// <para>
    /// If a feature adds an option of a certain functionality to be available to be used by the node
    /// (it may be disabled/enabled by the configuration) the naming convention is
    /// <c>Add[Feature]()</c>. Conversely, when a feature is inclined to be used if included,
    /// the naming convention should be <c>Use[Feature]()</c>.
    /// </para>
    /// </summary>
    public abstract class FullNodeFeature : IFullNodeFeature
    {
        /// <inheritdoc />
        public virtual void LoadConfiguration()
        {
        }

        /// <inheritdoc />
        public abstract void Initialize();

        /// <inheritdoc />
        public virtual void Dispose()
        {
        }

        /// <inheritdoc />
        public virtual void ValidateDependencies(IFullNodeServiceProvider services)
        {
        }
    }
}
