﻿namespace Cedar.Handlers
{
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Cedar.Annotations;
    using Cedar.Serialization;
    using Cedar.Serialization.Client;
    using EventStore.ClientAPI;

    public static class HandlerModuleExtensions
    {
        public static Task DispatchResolvedEvent(
            [NotNull] this IEnumerable<IHandlerResolver> handlerModules,
            ResolvedEvent resolvedEvent,
            [NotNull] ISerializer serializer,
            CancellationToken cancellationToken)
        {
            Guard.EnsureNotNull(handlerModules, "handlerModules");
            Guard.EnsureNotNull(resolvedEvent, "resolvedEvent");
            Guard.EnsureNotNull(serializer, "serializer");

            IDictionary<string, object> headers;
            var @event = serializer.DeserializeEventData(resolvedEvent, out headers);

            var methodInfo = typeof(HandlerModuleExtensions)
                .GetMethod("DispatchDomainEvent", BindingFlags.Static | BindingFlags.NonPublic);

            var genericMethod = methodInfo.MakeGenericMethod(@event.GetType());

            return (Task) genericMethod.Invoke(null, new[]
            {
                handlerModules, serializer, @event, headers, resolvedEvent, cancellationToken
            });
        }

        public static Task DispatchResolvedEvent(
            [NotNull] this IHandlerResolver handlerModule,
            ResolvedEvent resolvedEvent,
            [NotNull] ISerializer serializer,
            CancellationToken cancellationToken)
        {
            Guard.EnsureNotNull(handlerModule, "handlerModule");
            Guard.EnsureNotNull(resolvedEvent, "resolvedEvent");
            Guard.EnsureNotNull(serializer, "serializer");

            IDictionary<string, object> headers;
            var @event = serializer.DeserializeEventData(resolvedEvent, out headers);

            var methodInfo = typeof(HandlerModuleExtensions)
                .GetMethod("DispatchDomainEvent", BindingFlags.Static | BindingFlags.NonPublic);

            var genericMethod = methodInfo.MakeGenericMethod(@event.GetType());

            return (Task) genericMethod.Invoke(null, new[]
            {
                new[] {handlerModule}, serializer, @event, headers, resolvedEvent, cancellationToken
            });
        }

        [UsedImplicitly]
        private static Task DispatchDomainEvent<TDomainEvent>(
            IEnumerable<IHandlerResolver> handlerModules,
            [NotNull] ISerializer serializer,
            TDomainEvent domainEvent, 
            IDictionary<string, object> headers, 
            ResolvedEvent resolvedEvent,
            CancellationToken cancellationToken)
            where TDomainEvent : class
        {

            var message = GetEventStoreMessage.Create(domainEvent, headers, resolvedEvent);

            return handlerModules.Dispatch(message, cancellationToken);
        }
    }
}
