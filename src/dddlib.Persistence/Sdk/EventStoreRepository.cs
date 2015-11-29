﻿// <copyright file="EventStoreRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System.Globalization;
    using System.Linq;
    using dddlib.Persistence.Sdk;
    using dddlib.Runtime;

    /// <summary>
    /// Represents an event store repository.
    /// </summary>
    public class EventStoreRepository : RepositoryBase, IEventStoreRepository
    {
        private readonly IEventStore eventStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStoreRepository" /> class.
        /// </summary>
        /// <param name="identityMap">The identity map.</param>
        /// <param name="eventStore">The event store.</param>
        public EventStoreRepository(IIdentityMap identityMap, IEventStore eventStore)
            : base(identityMap)
        {
            Guard.Against.Null(() => eventStore);

            this.eventStore = eventStore;
        }

        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="aggregateRoot">The aggregate root.</param>
        public void Save<T>(T aggregateRoot) where T : AggregateRoot
        {
            Guard.Against.Null(() => aggregateRoot);

            var streamId = this.GetId(aggregateRoot);

            var events = aggregateRoot.GetUncommittedEvents();

            var state = aggregateRoot.State;
            if (state == null && !events.Any())
            {
                // NOTE (Cameron): This is the initial commit so there should be events.
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cannot save initial commit for aggregate root of type '{0}' as it has no events.",
                        aggregateRoot.GetType()));
            }

            if (!events.Any())
            {
                // TODO (Cameron): Nothing to commit. Log info?
                return;
            }

            // TODO (Cameron): Try catch around commit stream.
            var newState = default(string);
            this.eventStore.CommitStream(streamId, events, state, out newState);

            // TODO (Cameron): Save the memento with the new commits if the state is the same as the old state and replace the state with the new state.
            aggregateRoot.CommitEvents(newState);

            ////if (heuristic.ShouldSaveSnapshot)
            ////{
            ////    ////// option 1. simple
            ////    ////var memento = aggregateRoot.GetMemento(out streamRevision);
            ////    ////this.eventStore.AddSnapshot(streamId, aggregateRoot.Revision, memento);

            ////    // option 2. complex
            ////    var memento = aggregateRoot.GetMemento();
            ////    var recycledMemento = new AggregateRootFactory().Create<T>(memento, new object[0], "test").GetMemento();
            ////    if (memento != recycledMemento)
            ////    {
            ////        throw new Exception("Memento implementation is wrong!");
            ////    }

            ////    this.eventStore.AddSnapshot(streamId, aggregateRoot.Revision, memento);
            ////}
        }

        /// <summary>
        /// Loads the aggregate root with the specified natural key.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The aggregate root.</returns>
        public T Load<T>(object naturalKey) where T : AggregateRoot
        {
            var streamId = this.GetId<T>(naturalKey);

            var state = default(string);
            var snapshot = this.eventStore.GetSnapshot(streamId) ?? new Snapshot();
            var events = this.eventStore.GetStream(streamId, snapshot.StreamRevision, out state);

            return this.Reconstitute<T>(snapshot.Memento, snapshot.StreamRevision, events, state);
        }
    }
}