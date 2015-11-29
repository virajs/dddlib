﻿// <copyright file="MemoryEventPersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Feature
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using dddlib.Configuration;
    using dddlib.Persistence;
    using dddlib.Persistence.Memory;
    using dddlib.Persistence.Sdk;
    using dddlib.Persistence.Tests.Sdk;
    using dddlib.Runtime;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib [with event sourcing]
    // In order save state
    // I need to be able to persist an aggregate root (in memory)
    public abstract class MemoryEventPersistence : Feature
    {
        private IIdentityMap identityMap;
        private IEventStore eventStore;
        private IEventStoreRepository repository;

        [Background]
        public override void Background()
        {
            base.Background();

            "Given an identity map"
                .f(() => this.identityMap = new MemoryIdentityMap());

            "And an event store"
                .f(() => this.eventStore = new MemoryEventStore());

            "And an event store repository"
                .f(() => this.repository = new EventStoreRepository(this.identityMap, this.eventStore));
        }

        public class UndefinedNaturalKey : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with no defined natural key"
                    .f(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then a runtime exception is thrown"
                    .f(() => action.ShouldThrow<RuntimeException>());
            }

            public class Subject : AggregateRoot
            {
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class UndefinedUnititializedFactory : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with no defined uninitialized factory"
                    .f(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then a runtime exception is thrown"
                    .f(() => action.ShouldThrow<RuntimeException>());
            }

            public class Subject : AggregateRoot
            {
                [NaturalKey]
                public string Id { get; set; }
            }
        }

        public class NullNaturalKey : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with a null natural key"
                    .f(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then a persistence exception is thrown"
                    .f(() => action.ShouldThrow<ArgumentException>());
            }

            public class Subject : AggregateRoot
            {
                [NaturalKey]
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SaveAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "When that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "Then the loaded instance should be the saved instance"
                    .f(() => loaded.Should().Be(saved));

                "And their revisions should be equal"
                    .f(() => loaded.Revision.Should().Be(saved.Revision));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));
            }

            public class Subject : AggregateRoot
            {
                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                protected override object GetState()
                {
                    return this.Id;
                }

                protected override void SetState(object memento)
                {
                    this.Id = memento.ToString();
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SaveAndSaveAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "And something happened to that instance"
                    .f(() => saved.DoSomething());

                "And that instance is saved again to the repository"
                    .f(() => this.repository.Save(saved));

                "When that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "Then the loaded instance should be the saved instance"
                    .f(() => loaded.Should().Be(saved));

                "And their revisions should be equal"
                    .f(() => loaded.Revision.Should().Be(saved.Revision));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));
            }

            public class Subject : AggregateRoot
            {
                private bool hasDoneSomething;

                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                public void DoSomething()
                {
                    this.Apply(new SubjectDidSomething { Id = this.Id });
                }

                protected override object GetState()
                {
                    return new Memento
                    {
                        Id = this.Id,
                        HasDoneSomething = this.hasDoneSomething,
                    };
                }

                protected override void SetState(object memento)
                {
                    var subject = memento as Memento;

                    this.Id = subject.Id;
                    this.hasDoneSomething = subject.HasDoneSomething;
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }

                private void Handle(SubjectDidSomething @event)
                {
                    this.hasDoneSomething = true;
                }

                private class Memento
                {
                    public string Id { get; set; }

                    public bool HasDoneSomething { get; set; }
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            public class SubjectDidSomething
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SaveAndLoadAndSaveAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded, Subject anotherLoaded)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "And that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "And something happened to that loaded instance"
                    .f(() => loaded.DoSomething());

                "And that loaded instance is saved to the repository"
                    .f(() => this.repository.Save(loaded));

                "When another instance is loaded from the repository"
                    .f(() => anotherLoaded = this.repository.Load<Subject>(saved.Id));

                "Then the other loaded instance should be the loaded instance"
                    .f(() => anotherLoaded.Should().Be(loaded));

                "And their revisions should be equal"
                    .f(() => anotherLoaded.Revision.Should().Be(loaded.Revision));

                "And their mementos should match"
                    .f(() => anotherLoaded.GetMemento().ShouldMatch(loaded.GetMemento()));
            }

            public class Subject : AggregateRoot
            {
                private bool hasDoneSomething;

                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                public void DoSomething()
                {
                    this.Apply(new SubjectDidSomething { Id = this.Id });
                }

                protected override object GetState()
                {
                    return new Memento
                    {
                        Id = this.Id,
                        HasDoneSomething = this.hasDoneSomething,
                    };
                }

                protected override void SetState(object memento)
                {
                    var subject = memento as Memento;

                    this.Id = subject.Id;
                    this.hasDoneSomething = subject.HasDoneSomething;
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }

                private void Handle(SubjectDidSomething @event)
                {
                    this.hasDoneSomething = true;
                }

                private class Memento
                {
                    public string Id { get; set; }

                    public bool HasDoneSomething { get; set; }
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            public class SubjectDidSomething
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SnapshotAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "And that instance is snapshot to the repository"
                    .f(() =>
                    {
                        Guid streamId;
                        this.identityMap.TryGet(typeof(Subject), typeof(string), saved.Id, out streamId);
                        this.eventStore.AddSnapshot(
                            streamId,
                            new Snapshot
                            {
                                StreamRevision = saved.Revision,
                                Memento = saved.GetMemento(),
                            });
                    });

                "When that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "Then the loaded instance should be the saved instance"
                    .f(() => loaded.Should().Be(saved));

                "And their revisions should be equal"
                    .f(() => loaded.Revision.Should().Be(saved.Revision));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));
            }

            public class Subject : AggregateRoot
            {
                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                protected override object GetState()
                {
                    return this.Id;
                }

                protected override void SetState(object memento)
                {
                    this.Id = memento.ToString();
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SnapshotAndSaveAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded, IEnumerable<object> events)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "And that instance is snapshot to the repository"
                    .f(() =>
                    {
                        Guid streamId;
                        this.identityMap.TryGet(typeof(Subject), typeof(string), saved.Id, out streamId);
                        this.eventStore.AddSnapshot(
                            streamId,
                            new Snapshot
                            {
                                StreamRevision = saved.Revision,
                                Memento = saved.GetMemento(),
                            });
                    });

                "And something happened to that instance"
                    .f(() => saved.DoSomething());

                "And that instance is saved again to the repository"
                    .f(() => this.repository.Save(saved));

                "When that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "And the events for that instance are loaded from the event store"
                    .f(() => 
                    {
                        Guid streamId;
                        this.identityMap.TryGet(typeof(Subject), typeof(string), saved.Id, out streamId);

                        string state;
                        events = this.eventStore.GetStream(streamId, 0, out state);
                    });

                "Then the loaded instance should be the saved instance"
                    .f(() => loaded.Should().Be(saved));

                "And their revisions should be equal"
                    .f(() => loaded.Revision.Should().Be(saved.Revision));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));

                "And the loaded events should contain two matching events"
                    .f(() =>
                    {
                        events.Should().HaveCount(2);
                        events.First().Should().BeOfType<NewSubject>();
                        events.First().As<NewSubject>().Should().Match<NewSubject>(@event => @event.Id == saved.Id);
                        events.Last().Should().BeOfType<SubjectDidSomething>();
                        events.Last().As<SubjectDidSomething>().Should().Match<SubjectDidSomething>(@event => @event.Id == saved.Id);
                    });
            }

            public class Subject : AggregateRoot
            {
                private bool hasDoneSomething;

                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                public void DoSomething()
                {
                    this.Apply(new SubjectDidSomething { Id = this.Id });
                }

                protected override object GetState()
                {
                    return new Memento
                    {
                        Id = this.Id,
                        HasDoneSomething = this.hasDoneSomething,
                    };
                }

                protected override void SetState(object memento)
                {
                    var subject = memento as Memento;

                    this.Id = subject.Id;
                    this.hasDoneSomething = subject.HasDoneSomething;
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }

                private void Handle(SubjectDidSomething @event)
                {
                    this.hasDoneSomething = true;
                }

                private class Memento
                {
                    public string Id { get; set; }

                    public bool HasDoneSomething { get; set; }
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            public class SubjectDidSomething
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        /*
         * in all - validate with memento comparison
        X*  1. can save and get
        X*  2. can save and save and get
        X*  3. can save and get and save and get
        X*  4. can save and snapshot and get (with snapshot)
        X*  5. can save and snapshot and save and get (with snapshot)
         *  6. can save and snapshot and get (without snapshot)
         *  7. can save and snapshot and save and get (without snapshot)
         *  
         * duplicate add snapshot?
         */
    }
}