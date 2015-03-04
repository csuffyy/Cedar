﻿namespace Cedar.Testing
{
    using System;
    using System.Threading.Tasks;
    using Cedar.Domain;
    using FluentAssertions;
    using Xunit;

    public class AggregateTests
    {
        private class SomethingHappened
        {
            public override string ToString()
            {
                return "Something happened.";
            }
        }

        private class Aggregate : AggregateBase
        {
            private int _something = 0;

            public Aggregate(string id) : base(id)
            {
                
            }
            
            void Apply(SomethingHappened e)
            {
                _something++;
            }

            public void DoSomething()
            {
                RaiseEvent(new SomethingHappened());
            }

        }

        private class BuggyAggregate : AggregateBase
        {
            public BuggyAggregate(string id) : base(id)
            {}

            public void DoSomething()
            {
                throw new InvalidOperationException();
            }
        }

        private class ReallyBuggyAggregate : AggregateBase
        {
            public ReallyBuggyAggregate(string id)
                : base(id)
            {
                throw new InvalidOperationException();
            }

            public void DoSomething()
            {
            }
        }

        private class ConstructorBehaviorAggregate : AggregateBase
        {
            public ConstructorBehaviorAggregate(Guid id)
                : base(id.ToString())
            {
                RaiseEvent(new SomethingHappened());
            }

            protected ConstructorBehaviorAggregate(string id) : base(id)
            {}

            void Apply(SomethingHappened e) { }
        }

        [Fact]
        public async Task a_passing_aggregate_scenario_should()
        {
            var result = await Scenario.ForAggregate(id => new Aggregate(id))
                .Given(new SomethingHappened())
                .When(a => a.DoSomething())
                .Then(new SomethingHappened());

            result.Passed.Should().BeTrue();
        }

        [Fact]
        public async Task a_passing_aggregate_with_events_raised_in_the_constructor_should()
        {
            var result = await Scenario.ForAggregate<ConstructorBehaviorAggregate>()
                .When(() => new ConstructorBehaviorAggregate(Guid.Empty))
                .Then(new SomethingHappened());

            result.Passed.Should().BeTrue();
        }

        [Fact]
        public async Task a_passing_aggregate_scenario_with_no_given_should()
        {
            var result = await Scenario.ForAggregate(id => new Aggregate(id))
                .When(a => a.DoSomething())
                .Then(new SomethingHappened());

            result.Passed.Should().BeTrue();
        }

        [Fact]
        public async Task an_aggregate_throwing_an_exception_should()
        {
            var result = await Scenario.ForAggregate(id => new BuggyAggregate(id))
                .When(a => a.DoSomething())
                .Then(new SomethingHappened());

            result.Passed.Should().BeFalse();
            result.Results.Should().BeOfType<ScenarioException>();
        }


        [Fact]
        public async Task an_aggregate_throwing_an_exception_in_its_constructor_should()
        {
            var result = await Scenario.ForAggregate(id => new ReallyBuggyAggregate(id))
                .When(a => a.DoSomething())
                .Then(new SomethingHappened());

            result.Passed.Should().BeFalse();
            result.Results.Should().BeOfType<ScenarioException>();
        }


        [Fact]
        public async Task an_aggregate_throwing_an_expected_exception_should()
        {
            var result = await Scenario.ForAggregate(id => new BuggyAggregate(id))
                .When(a => a.DoSomething())
                .ThenShouldThrow<InvalidOperationException>();

            result.Passed.Should().BeTrue();
            result.Results.Should().BeOfType<InvalidOperationException>();
        }
    }
}
