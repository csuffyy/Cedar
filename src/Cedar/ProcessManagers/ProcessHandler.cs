namespace Cedar.ProcessManagers
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Cedar.Annotations;
    using Cedar.Handlers;
    using Cedar.ProcessManagers.Messages;
    using Cedar.ProcessManagers.Persistence;
    using CuttingEdge.Conditions;

    public static class ProcessHandler
    {
        public static ProcessHandler<TProcess, TCheckpoint> For<TProcess, TCheckpoint>(
            DispatchCommand dispatchCommand,
            IProcessManagerCheckpointRepository<TCheckpoint> checkpointRepository,
            IProcessManagerFactory processManagerFactory = null,
            ProcessHandler<TProcess, TCheckpoint>.BuildProcessManagerId buildProcessId = null) 
            where TProcess : IProcessManager 
            where TCheckpoint : IComparable<string>
        {
            return new ProcessHandler<TProcess, TCheckpoint>(
                dispatchCommand,
                checkpointRepository,
                processManagerFactory,
                buildProcessId);
        }
    }

    public class ProcessHandler<TProcess,TCheckpoint> : IHandlerResolver where TProcess : IProcessManager where TCheckpoint : IComparable<string>
    {
        public static readonly BuildProcessManagerId DefaultBuildProcessManagerId =
            correlationId => typeof(TProcess).Name + "-" + correlationId;

        public delegate string BuildProcessManagerId(string correlationId);

        private readonly IList<Pipe<object>> _pipes;
        private readonly ProcessManagerDispatcher _dispatcher;

        internal ProcessHandler(
            DispatchCommand dispatchCommand,
            IProcessManagerCheckpointRepository<TCheckpoint> checkpointRepository,
            IProcessManagerFactory processManagerFactory = null,
            BuildProcessManagerId buildProcessId = null)
        {
            Condition.Requires(dispatchCommand, "dispatchCommand").IsNotNull();
            Condition.Requires(checkpointRepository, "checkpointRepository").IsNotNull();

            _pipes = new List<Pipe<object>>();
            _dispatcher = new ProcessManagerDispatcher(dispatchCommand, checkpointRepository, processManagerFactory, buildProcessId);
            CorrelateBy<ProcessCompleted>(message => message.DomainEvent.CorrelationId)
                .CorrelateBy<CheckpointReached>(message => message.DomainEvent.CorrelationId);
        }

        public ProcessHandler<TProcess, TCheckpoint> CorrelateBy<TMessage>(
            Func<EventMessage<TMessage>, string> getCorrelationId) where TMessage : class
        {
            _dispatcher.CorrelateBy(getCorrelationId);

            return this;
        }

        public ProcessHandler<TProcess, TCheckpoint> Pipe(Pipe<object> pipe)
        {
            _pipes.Add(pipe);

            return this;
        }

        public IHandlerResolver BuildHandlerResolver()
        {
            return new HandlerResolver(_dispatcher.Aggregate(new HandlerModule(), HandleMessageType));
        }

        private HandlerModule HandleMessageType(HandlerModule module, Type messageType)
        {
            return (HandlerModule)GetType()
                .GetMethod("BuildHandler", BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(messageType)
                .Invoke(this, new object[] { module });
        }

        [UsedImplicitly]
        private HandlerModule BuildHandler<TMessage>(HandlerModule module)
            where TMessage : class
        {
            _pipes.Select(pipe => Delegate.CreateDelegate(typeof(Pipe<TMessage>), pipe.Method) as Pipe<TMessage>)
                .Aggregate(module.For<TMessage>(), (builder, pipe) => builder.Pipe(pipe))
                .Handle(_dispatcher.Dispatch);

            return module;
        }

        public IEnumerable<Handler<TMessage>> ResolveAll<TMessage>() where TMessage : class
        {
            return _dispatcher.ResolveAll<TMessage>();
        }

        private class CheckpointedProcess
        {
            public readonly TCheckpoint Checkpoint;
            public readonly TProcess Process;

            public CheckpointedProcess(TProcess process, TCheckpoint checkpoint)
            {
                Process = process;
                Checkpoint = checkpoint;
            }
        }

        private class ProcessManagerDispatcher : IHandlerResolver, IEnumerable<Type>
        {
            private readonly IProcessManagerCheckpointRepository<TCheckpoint> _checkpointRepository;
            private readonly DispatchCommand _dispatchCommand;
            private readonly IProcessManagerFactory _processManagerFactory;
            private readonly BuildProcessManagerId _buildProcessId;
            private readonly IDictionary<Type, Func<object, string>> _byCorrelationId;
            private readonly ConcurrentDictionary<string, CheckpointedProcess> _activeProcesses;

            public ProcessManagerDispatcher(
                DispatchCommand dispatchCommand,
                IProcessManagerCheckpointRepository<TCheckpoint> checkpointRepository,
                IProcessManagerFactory processManagerFactory = null,
                BuildProcessManagerId buildProcessId = null)
            {
                _checkpointRepository = checkpointRepository;
                _dispatchCommand = dispatchCommand;
                _processManagerFactory = processManagerFactory ?? new DefaultProcessManagerFactory();
                _buildProcessId = buildProcessId ?? DefaultBuildProcessManagerId;
                _byCorrelationId = new Dictionary<Type, Func<object, string>>();
                _activeProcesses = new ConcurrentDictionary<string, CheckpointedProcess>();
            }

            public IEnumerable<Handler<TMessage>> ResolveAll<TMessage>() where TMessage : class
            {
                if(false == typeof(EventMessage).IsAssignableFrom(typeof(TMessage))
                   || false == _byCorrelationId.ContainsKey(typeof(TMessage)))
                {
                    yield break;
                }
                yield return async (message, ct) =>
                {
                    Func<object, string> getCorrelationId;
                    if(false == _byCorrelationId.TryGetValue(typeof(TMessage), out getCorrelationId))
                    {
                        return;
                    }

                    var domainEventMessage = (message as EventMessage);

                    var correlationId = getCorrelationId(message);

                    var checkpointedProcess = await GetProcess(correlationId, ct);

                    var process = checkpointedProcess.Process;
                    var checkpoint = checkpointedProcess.Checkpoint;

                    process.Inbox.OnNext(domainEventMessage);

                    if(checkpoint.CompareTo(domainEventMessage.CheckpointToken) >= 0)
                    {
                        return;
                    }

                    var commands = process.Commands.ToList();

                    if(false == commands.Any())
                    {
                        return;
                    }

                    await Task.WhenAll(commands.Select(command => _dispatchCommand(command, ct)));

                    await _checkpointRepository.SaveCheckpointToken(process, domainEventMessage.CheckpointToken, ct);
                };
            }

            private async Task<CheckpointedProcess> GetProcess(string correlationId, CancellationToken ct)
            {
                CheckpointedProcess checkpointedProcess;
                if(false == _activeProcesses.TryGetValue(correlationId, out checkpointedProcess))
                {
                    var process = (TProcess) _processManagerFactory
                        .Build(typeof(TProcess), _buildProcessId(correlationId), correlationId);

                    var checkpoint = await _checkpointRepository.GetCheckpoint(process.Id, ct);

                    process.Events.OfType<ProcessCompleted>()
                        .Subscribe(async e =>
                        {
                            CheckpointedProcess _;
                            _activeProcesses.TryRemove(e.ProcessId, out _);
                            await _checkpointRepository.MarkProcessCompleted(e, ct);
                            process.Dispose();
                        });

                    checkpointedProcess = new CheckpointedProcess(process, checkpoint);

                    _activeProcesses.TryAdd(process.Id, checkpointedProcess);
                }
                return checkpointedProcess;
            }

            public IEnumerator<Type> GetEnumerator()
            {
                return _byCorrelationId.Keys.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void CorrelateBy<TMessage>(
                Func<EventMessage<TMessage>, string> getCorrelationId) where TMessage : class
            {
                _byCorrelationId.Add(typeof(EventMessage<TMessage>),
                    message => getCorrelationId((EventMessage<TMessage>) message));
            }
        }
    }
}