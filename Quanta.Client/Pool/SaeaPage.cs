using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Quanta
{
    internal sealed class SaeaPage
    {

        #region " Properties "

        public byte[] Buffer
        {
            get { return _buffer; }
        }

        public bool Leasing
        {
            get { return _itemCount > _items.Count; }
        }

        public uint LastAccess
        {
            get { return _lastAccess; }
        }

        #endregion

        #region " Members "

        private byte[] _buffer;

        private int _itemCount;
        private uint _lastAccess;

        private ConcurrentStack<SocketAsyncEventArgs> _items;

        #endregion

        #region " Constructor "

        private SaeaPage(byte[] buffer, SocketAsyncEventArgs[] items)
        {
            _buffer = buffer;
            _itemCount = items.Length;

            _items = new ConcurrentStack<SocketAsyncEventArgs>(items);
        }

        #endregion

        public static SaeaPage Create(int itemCount, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize * itemCount];

            SocketAsyncEventArgs[] items = new SocketAsyncEventArgs[itemCount];

            for (int i = 0; i < itemCount; i++)
            {
                items[i] = new SocketAsyncEventArgs();
                items[i].SetBuffer(buffer, i * bufferSize, bufferSize); 
            }

            return new SaeaPage(buffer, items);
        }

        public bool TryRent(out SocketAsyncEventArgs item)
        {
            if (_items.TryPop(out item))
            {
                _lastAccess = unchecked((uint)Environment.TickCount);

                return true;
            }

            return false;
        }

        public void Return(SocketAsyncEventArgs item)
        {
            _lastAccess = unchecked((uint)Environment.TickCount);

            _items.Push(item);
        }

    }

}
