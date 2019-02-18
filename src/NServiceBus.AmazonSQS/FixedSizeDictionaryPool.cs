namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    class FixedSizeDictionaryPool
    {
        ConcurrentBag<Dictionary<string, string>> pool = new ConcurrentBag<Dictionary<string, string>>();

        public Lease Rent(Dictionary<string, string> fillWith = null)
        {
            if (!pool.TryTake(out var rent))
            {
                rent = new Dictionary<string, string>(16);
            }
            else
            {
                rent.Clear();
            }

            if (fillWith != null)
            {
                foreach (var kvp in fillWith)
                {
                    rent.Add(kvp.Key, kvp.Value);
                }
            }

            return new Lease(rent, this);
        }

        void Return(Dictionary<string, string> dictionary)
        {
            dictionary.Clear();
            pool.Add(dictionary);
        }

        public struct Lease : IDisposable
        {
            Dictionary<string, string> rented;
            FixedSizeDictionaryPool pool;

            public Lease(Dictionary<string, string> rented, FixedSizeDictionaryPool pool)
            {
                this.pool = pool;
                this.rented = rented;
            }

            public void Dispose()
            {
                pool.Return(rented);
            }

            public static implicit operator Dictionary<string, string>(Lease lease)
            {
                return lease.rented;
            }
        }
    }
}