namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Linq;
    using System.Threading;

    class RoundRobinBuffer<T> : IDisposable
    {
        T[] items;
        int index;
        int max;

        public RoundRobinBuffer(T[] items)
        {
            this.items = items;
            index = 0;
            max = items.Length;
        }

        public T GetNext()
        {
            var next = unchecked((uint)Interlocked.Increment(ref index));
            return items[(int)next % max];
        }

        public void Dispose()
        {
            foreach (var disposable in items.OfType<IDisposable>())
            {
                disposable.Dispose();
            }
        }
    }
}