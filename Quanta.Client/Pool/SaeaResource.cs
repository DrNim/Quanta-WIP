using System;
using System.Net.Sockets;

namespace Quanta
{
    internal sealed class SaeaResource : IDisposable
    {

        #region " Properties "

        public byte[] Buffer
        {
            get
            {
                return _buffer;
            }
        }

        public int Offset
        {
            get
            {
                return _offset;
            }
        }

        public int Count
        {
            get
            {
                return _count;
            }
        }

        public SocketAsyncEventArgs EventArgs
        {
            get { return _eventArgs; }
        }

        #endregion

        #region " Members "

        private byte[] _buffer;
        private int _offset;
        private int _count;

        private bool _disposed;

        private SaeaPool _pool;
        private SocketAsyncEventArgs _eventArgs;

        #endregion

        #region " Constructor "

        public SaeaResource(SaeaPool pool, SocketAsyncEventArgs eventArgs)
        {
            _pool = pool;
            _eventArgs = eventArgs;
            _buffer = eventArgs.Buffer;
            _offset = eventArgs.Offset;
            _count = eventArgs.Count;
        }

        #endregion

        #region " IDisposable "

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _eventArgs.UserToken = null;

            if (_eventArgs.Count != _count || _eventArgs.Offset != _offset)
            {
                _eventArgs.SetBuffer(_offset, _count);
            }

            _pool.Return(_eventArgs);
        }

        ~SaeaResource()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

    }

}
