namespace Cedar.GetEventStore
{
    using System;
    using EnsureThat;

    public static class StringExtensions
    {
        public static string FormatStreamIdWithBucket(this string streamId, string bucketId = null)
        {
            Ensure.That(streamId, "streamId").IsNotNullOrWhiteSpace();

            bucketId = string.IsNullOrWhiteSpace(bucketId) ? "default" : bucketId;

            return string.Format("[{0}].{1}", bucketId, streamId);
        }

        public static string FormatStreamNameWithoutBucket(this string streamId)
        {
            Ensure.That(streamId, "streamId").IsNotNullOrWhiteSpace();

            var split = streamId.Split(new[] {'.'}, 2);

            if(split.Length < 2)
            {
                throw new ArgumentException(string.Format("Expected {0} to be prefixed with a bucket.", streamId), "streamId");
            }

            return split[1];
        }
    }
}