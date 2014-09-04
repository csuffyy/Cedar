﻿namespace Cedar.Testing
{
    using System;
    using System.Linq.Expressions;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public static partial class Scenario
    {
        public static Any.Given<T> For<T>([CallerMemberName] string scenarioName = null)
        {
            return new Any.ScenarioBuilder<T>(scenarioName);
        }

        public static class Any
        {
            public interface Given<T> : When<T>
            {
                When<T> Given(T instance);
            }

            public interface When<T> : Then<T>
            {
                Then<T> When(Expression<Func<T, T>> when);
                Then<T> When(Expression<Func<T, Task<T>>> when);
            }

            public interface Then<T> : IScenario
            {
                Then<T> ThenShouldEqual(T other);
                Then<T> ThenShouldThrow<TException>(Func<TException, bool> isMatch = null) where TException : Exception;
            }

            internal class ScenarioBuilder<T> : Given<T>
            {
                private readonly string _name;
                
                private readonly Func<T> _runGiven;
                private readonly Func<T, Task<T>> _runWhen;
                private Action<T> _runThen = _ => { };
                private T _given;
                private Expression<Func<T, Task<T>>> _when;
                private T _expect;
                private Exception _occurredException;
                private bool passed;

                public ScenarioBuilder(string name)
                {
                    _name = name;

                    _runGiven = () => _given;
                    _runWhen = instance => _when.Compile()(instance);
                }

                public When<T> Given(T instance)
                {
                    _given = instance;

                    return this;
                }

                public Then<T> When(Expression<Func<T, T>> when)
                {
                    /*var parameter = Expression.Parameter(typeof (T), "instance");
                    Expression body = Expression.Invoke(, parameter);
                    return When(Expression.Lambda<Func<T, Task<
                        T>>>(body, parameter));*/

                   // return When(async instance => await instance);

                    throw new NotImplementedException();
                }

                public Then<T> When(Expression<Func<T, Task<T>>> when)
                {
                    _when = when;

                    return this;
                }

                public Then<T> ThenShouldEqual(T other)
                {
                    _runThen = instance =>
                    {
                        if (false == instance.Equals(other))
                        {
                            throw new ScenarioException(this, String.Format("{0} was expected to equal {1}.", instance, other));
                        }
                    };
                    return this;
                }

                public Then<T> ThenShouldThrow<TException>(Func<TException, bool> isMatch = null)
                    where TException : Exception
                {
                    isMatch = isMatch ?? (_ => true);

                    _runThen = _ => ((ScenarioResult)this).AssertExceptionMatches(_occurredException, isMatch);

                    return this;
                }

                public TaskAwaiter<ScenarioResult> GetAwaiter()
                {
                    IScenario scenario = this;

                    return scenario.Run().GetAwaiter();
                }

                string IScenario.Name
                {
                    get { return _name; }
                }



                async Task<ScenarioResult> IScenario.Run()
                {
                    _given = _runGiven();

                    try
                    {
                        _expect = await _runWhen(_given);
                    }
                    catch (Exception ex)
                    {
                        _occurredException = ex;
                    }

                    _runThen(_expect);

                    passed = true;

                    return this;
                }

                public static implicit operator ScenarioResult(ScenarioBuilder<T> builder)
                {
                    return new ScenarioResult(builder._name, builder.passed, builder._given, builder._when, builder._expect, builder._occurredException);
                }
            }
        }
    }
}