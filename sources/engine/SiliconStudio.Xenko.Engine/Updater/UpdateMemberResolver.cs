using System;

namespace SiliconStudio.Xenko.Updater
{
    /// <summary>
    /// Describes how to parse and resolve an <see cref="UpdatableMember"/>
    /// when parsing an <see cref="UpdateEngine"/> property path.
    /// </summary>
    public abstract class UpdateMemberResolver
    {
        /// <summary>
        /// Defines what type it does support.
        /// </summary>
        public abstract Type SupportedType { get; }

        /// <summary>
        /// Defines how to resolve a member (after a dot).
        /// </summary>
        /// <param name="memberName">The member name to resolve.</param>
        /// <returns>The resolved member if found, otherwise null.</returns>
        public virtual UpdatableMember ResolveProperty(string memberName)
        {
            return null;
        }

        /// <summary>
        /// Defines how to resolve an indexer (between angle brackets).
        /// </summary>
        /// <param name="indexerName">The indexer name to resolve.</param>
        /// <returns>The resolved indexer if found, otherwise null.</returns>
        public virtual UpdatableMember ResolveIndexer(string indexerName)
        {
            return null;
        }
    }
}