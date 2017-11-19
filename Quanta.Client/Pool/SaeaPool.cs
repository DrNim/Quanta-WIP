using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Quanta
{

    internal sealed class SaeaPool
    {

        #region " Properties "

        public static SaeaPool Shared
        {
            get { return Volatile.Read(ref _shared) ?? EnsureSharedCreated(); }
        }

        #endregion

        #region " Members "

        private int _bufferSize;
        private int _itemCount;

        private int _timeout;

        Timer _timer;

        List<SaeaPage> _pages;
        ReaderWriterLockSlim _lock;

        private static SaeaPool _shared;
 
        #endregion

        #region " Constructor "

        private SaeaPool(int itemCount, int bufferSize, int timeout)
        {
            _itemCount = itemCount;
            _bufferSize = bufferSize;
            _timeout = timeout;

            _pages = new List<SaeaPage>();
            _lock = new ReaderWriterLockSlim();

            _timer = new Timer(TimerElapsed, null, 15000, 15000);
        }

        #endregion

        private static SaeaPool EnsureSharedCreated()
        {
            Interlocked.CompareExchange(ref _shared, Create(), null);

            return _shared;
        }

        public static SaeaPool Create()
        {
            return Create(48, ushort.MaxValue, 600000);
        }

        public static SaeaPool Create(int itemCount, int bufferSize, int timeout)
        {
            return new SaeaPool(itemCount, bufferSize, timeout);
        }

        public SaeaResource Rent()
        {
            _lock.EnterUpgradeableReadLock();

            try
            {
                SocketAsyncEventArgs item;

                while (true)
                {
                    for (int i = 0; i < _pages.Count; i++)
                    {
                        if (_pages[i].TryRent(out item))
                        { 
                            return new SaeaResource(this, item);
                        }
                    }

                    _lock.EnterWriteLock(); 
                    _pages.Add(SaeaPage.Create(_itemCount, _bufferSize)); 
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public void Return(SaeaResource resource)
        {
            resource.Dispose();
        }

        internal void Return(SocketAsyncEventArgs item)
        {
            _lock.EnterReadLock();

            try
            {
                for (int i = 0; i < _pages.Count; i++)
                {
                    if (_pages[i].Buffer == item.Buffer)
                    { 
                        _pages[i].Return(item);
                        return;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private void TimerElapsed(object state)
        {
            _lock.EnterUpgradeableReadLock();

            try
            {
                for (int i = _pages.Count - 1; i >= 0; i--)
                {
                    if (!_pages[i].Leasing && (unchecked((uint)Environment.TickCount) - _pages[i].LastAccess) > _timeout)
                    {
                        _lock.EnterWriteLock(); 
                        _pages.RemoveAt(i); 
                        _lock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

    }

}
