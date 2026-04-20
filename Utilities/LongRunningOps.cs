using System;
using System.Threading;

namespace VANTAGE.Utilities
{
    // Tracks long-running operations (Submit Week, snapshot deletes, etc.) so
    // MainWindow can warn the user before closing the app mid-operation.
    //
    // Usage:
    //   using (LongRunningOps.Begin())
    //   {
    //       // work that must not be interrupted by app close
    //   }
    public static class LongRunningOps
    {
        private static int _count;

        public static bool IsRunning => Volatile.Read(ref _count) > 0;

        public static IDisposable Begin() => new Scope();

        private sealed class Scope : IDisposable
        {
            private int _disposed;

            public Scope() => Interlocked.Increment(ref _count);

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                    Interlocked.Decrement(ref _count);
            }
        }
    }
}
