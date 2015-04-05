namespace Cedar.EventSourcing
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class MsSqlEventStore: IEventStore
    {
        public Task AppendToStream(string streamId, int expectedVersion, IEnumerable<NewStreamEvent> events)
        {
            throw new System.NotImplementedException();
        }

        public Task DeleteStream(string streamId, int expectedVersion = ExpectedVersion.Any, bool hardDelete = true)
        {
            throw new System.NotImplementedException();
        }

        public Task<AllEventsPage> ReadAll(string checkpoint, int maxCount, ReadDirection direction = ReadDirection.Forward)
        {
            throw new System.NotImplementedException();
        }

        public Task<StreamEventsPage> ReadStream(string streamId, int start, int count, ReadDirection direction = ReadDirection.Forward)
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }
    }
}