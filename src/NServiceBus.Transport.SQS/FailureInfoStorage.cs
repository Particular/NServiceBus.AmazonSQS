namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;

    // The data structure has fixed maximum size. When the data structure reaches its maximum size,
    // the least recently used (LRU) message processing failure is removed from the storage.
    class FailureInfoStorage
    {
        public FailureInfoStorage(int maxElements)
        {
            this.maxElements = maxElements;
        }

        public void RecordFailureInfoForMessage(string messageId)
        {
            lock (lockObject)
            {
                if (failureInfoPerMessage.TryGetValue(messageId, out var node))
                {
                    // We have seen this message before, just update the counter
                    node.Attempts++;

                    // Maintain invariant: leastRecentlyUsedMessages.First contains the LRU item.
                    leastRecentlyUsedMessages.Remove(node.LeastRecentlyUsedEntry);
                    leastRecentlyUsedMessages.AddLast(node.LeastRecentlyUsedEntry);
                }
                else
                {
                    if (failureInfoPerMessage.Count == maxElements)
                    {
                        // We have reached the maximum allowed capacity. Remove the LRU item.
                        var leastRecentlyUsedEntry = leastRecentlyUsedMessages.First;
                        failureInfoPerMessage.Remove(leastRecentlyUsedEntry.Value);
                        leastRecentlyUsedMessages.RemoveFirst();
                    }

                    var newNode = new FailureInfoNode(messageId, 1);

                    failureInfoPerMessage[messageId] = newNode;

                    // Maintain invariant: leastRecentlyUsedMessages.First contains the LRU item.
                    leastRecentlyUsedMessages.AddLast(newNode.LeastRecentlyUsedEntry);
                }
            }
        }

        public bool TryGetFailureInfoForMessage(string messageId, out int attempts)
        {
            lock (lockObject)
            {
                if (failureInfoPerMessage.TryGetValue(messageId, out var node))
                {
                    attempts = node.Attempts;
                    return true;
                }
                attempts = 0;
                return false;
            }
        }

        readonly Dictionary<string, FailureInfoNode> failureInfoPerMessage = new Dictionary<string, FailureInfoNode>();
        readonly LinkedList<string> leastRecentlyUsedMessages = new LinkedList<string>();
        readonly object lockObject = new object();
        readonly int maxElements;

        class FailureInfoNode
        {
            public FailureInfoNode(string messageId, int attempts)
            {
                Attempts = attempts;
                LeastRecentlyUsedEntry = new LinkedListNode<string>(messageId);
            }

            public int Attempts { get; set; }

            public LinkedListNode<string> LeastRecentlyUsedEntry { get; }
        }
    }
}