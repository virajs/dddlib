﻿// <copyright file="IEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Collections.Generic;
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Exposes the public members of the event store.
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Commits the events to a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="events">The events to commit.</param>
        /// <param name="preCommitState">The pre-commit state of the stream.</param>
        /// <param name="postCommitState">The post-commit state of stream.</param>
        void CommitStream(Guid streamId, IEnumerable<object> events, string preCommitState, out string postCommitState);

        /// <summary>
        /// Gets the events for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="streamRevision">The stream revision to get the events from.</param>
        /// <param name="state">The state of the steam.</param>
        /// <returns>The events.</returns>
        IEnumerable<object> GetStream(Guid streamId, int streamRevision, out string state);

        /// <summary>
        /// Adds a snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="snapshot">The snapshot.</param>
        void AddSnapshot(Guid streamId, Snapshot snapshot);

        /// <summary>
        /// Gets the latest snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <returns>The snapshot.</returns>
        Snapshot GetSnapshot(Guid streamId);
    }
}