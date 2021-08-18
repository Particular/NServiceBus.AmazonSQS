namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;

    partial class RateLimiter
    {
        class SizeConstrainedStack<T> : LinkedList<T>
        {
            public SizeConstrainedStack(int maxSize)
            {
                this.maxSize = maxSize;
            }

            public void Push(T item)
            {
                AddFirst(item);

                if (Count > maxSize)
                {
                    RemoveLast();
                }
            }

            readonly int maxSize;
        }
    }
}