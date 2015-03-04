﻿namespace Cedar.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using Cedar.Domain;
    using Cedar.Domain.Persistence;
    using Cedar.Testing.Printing;

    public static partial class Scenario
    {
        public static Aggregate.IGiven<T> ForAggregate<T>(Func<string, T> factory = null, string aggregateId = null, [CallerMemberName] string scenarioName = null) where T : IAggregate
        {
            aggregateId = aggregateId ?? "testid";
            factory = factory ?? (id => (T)DefaultCreateAggregate.Create(typeof (T), id));

            return new Aggregate.ScenarioBuilder<T>(factory, aggregateId, scenarioName);
        }

        public static class Aggregate
        {
            public interface IGiven<T> : IWhen<T> where T : IAggregate
            {
                IWhen<T> Given(params object[] events);
            }

            public interface IWhen<T> : IThen where T : IAggregate
            {
                IThen When(Expression<Func<T, Task>> when);

                IThen When(Expression<Action<T>> when);

                IThen When(Expression<Func<T>> when);
            }

            public interface IThen : IScenario
            {
                IThen Then(params object[] expectedEvents);

                IThen ThenNothingHappened();

                IThen ThenShouldThrow<TException>(Expression<Func<TException, bool>> isMatch = null) where TException : Exception;
            }

            internal class ScenarioBuilder<T> : IGiven<T> where T : IAggregate
            {
                private Func<string, T> _factory;
                private readonly string _aggregateId;
                private readonly string _name;

                private readonly Action<T> _runGiven;
                private Func<T, Task> _runWhen;
                private Action<T> _runThen;
                private Action<IAggregate> _afterGiven;

                private object[] _given;
                private LambdaExpression _when;
                private object[] _expect;
                private object _results;
                private bool _passed;
                private readonly Stopwatch _timer;

                public ScenarioBuilder(Func<string, T> factory, string aggregateId, string name)
                {
                    _factory = factory;
                    _aggregateId = aggregateId;
                    _name = name;
                    _runGiven = aggregate =>
                    {
                        using (var rehydrateAggregate = aggregate.BeginRehydrate())
                        {
                            foreach (var @event in _given ?? new object[0])
                            {
                                rehydrateAggregate.ApplyEvent(@event);
                            }
                        }

                        _afterGiven(aggregate);
                    };
                    _runWhen = _ =>
                    {
                        throw new ScenarioException("When not set.");
                    };

                    _timer = new Stopwatch();
                }

                public IWhen<T> Given(params object[] events)
                {
                    _given = events;
                    return this;
                }

                public IThen When(Expression<Func<T, Task>> when)
                {
                    _when = when;
                    _runWhen = aggregate => when.Compile()(aggregate); 
                    return this;
                }

                public IThen When(Expression<Action<T>> when)
                {
                    _when = when;
                    _runWhen = aggregate =>
                    {
                        when.Compile()(aggregate);
                        return Task.FromResult(true);
                    };
                    return this;
                }

                public IThen When(Expression<Func<T>> when)
                {
                    _when = when;
                    _factory = _ => when.Compile()();
                    _runWhen = _ => Task.FromResult(true);
                    _afterGiven = _ => { };
                    return this;
                }

                public IThen Then(params object[] expectedEvents)
                {
                    GuardThenNotSet();
                    _expect = expectedEvents;

                    _runThen = aggregate =>
                    {
                        var uncommittedEvents = new List<object>(aggregate.TakeUncommittedEvents().Cast<object>());
                        
                        _results = uncommittedEvents;
                        
                        if (false == uncommittedEvents.SequenceEqual(expectedEvents, MessageEqualityComparer.Instance))
                        {
                            throw new ScenarioException(
                                string.Format(
                                    "The ocurred events ({0}) did not equal the expected events ({1}).",
                                    uncommittedEvents.NicePrint()
                                        .Aggregate(new StringBuilder(), (builder, s) => builder.Append(s))
                                        .ToString(),
                                    _expect.NicePrint()
                                        .Aggregate(new StringBuilder(), (builder, s) => builder.Append(s))
                                        .ToString()));
                        }
                    };
                    return this;
                }

                public IThen ThenNothingHappened()
                {
                    GuardThenNotSet();
                    _expect = new object[0];

                    _runThen = aggregate =>
                    {
                        var uncommittedEvents = new List<object>(aggregate.TakeUncommittedEvents().Cast<object>());
                        
                        _results = uncommittedEvents;
                        
                        if (uncommittedEvents.Any())
                        {
                            throw new ScenarioException("No events were expected, yet some events occurred.");
                        }
                    };
                    return this;
                }

                public IThen ThenShouldThrow<TException>(Expression<Func<TException, bool>> isMatch = null) where TException : Exception
                {
                    GuardThenNotSet();
                    
                    _expect = isMatch != null ? new object[] {typeof(TException), isMatch} : new[] {typeof(TException)};

                    _runThen = _ => ((ScenarioResult)this).ThenShouldThrow(_results, isMatch);

                    return this;
                }

                public TaskAwaiter<ScenarioResult> GetAwaiter()
                {
                    IScenario scenario = this;

                    return scenario.Run().GetAwaiter();
                }

                void GuardThenNotSet()
                {
                    if (_runThen !=null) throw new InvalidOperationException("Then already set.");
                }

                string IScenario.Name
                {
                    get { return _name; }
                }

                async Task<ScenarioResult> IScenario.Run()
                {
                    try
                    {
                        _timer.Start();

                        try
                        {
                            T aggregate;

                            try
                            {
                                aggregate = _factory(_aggregateId);

                                _runGiven(aggregate);
                            }
                            catch(Exception ex)
                            {
                                _results = new ScenarioException(ex.Message);

                                return this;
                            }

                            try
                            {
                                await _runWhen(aggregate);
                            }
                            catch(Exception ex)
                            {
                                _results = ex;
                            }

                            if(_runThen == null)
                            {
                                throw new InvalidOperationException("Then not set.");
                            }

                            _runThen(aggregate);

                            _passed = true;
                        }
                        catch(Exception ex)
                        {
                            _results = ex;

                            return this;
                        }

                        _timer.Stop();

                        return this;
                    }
                    finally
                    {
                        _timer.Stop();
                    }
                }

                public static implicit operator ScenarioResult(ScenarioBuilder<T> builder)
                {
                    return new ScenarioResult(builder._name, builder._passed, builder._given, builder._when, builder._expect, builder._results, builder._timer.Elapsed);
                }
            }
        }
    }
}