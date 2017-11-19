using System;

namespace Quanta
{
    public sealed partial class Client
    {
        public sealed class DataReceivedEventArgs : EventArgs
        {
            #region " Properties "

            public byte[] Buffer
            {
                get { return _buffer; }
            }

            public int Count
            {
                get { return _count; }
            }

            public int Offset
            {
                get { return _offset; }
            }

            #endregion

            #region " Members "

            private int _count;
            private int _offset; 

            private byte[] _buffer;

            #endregion

            #region " Constructor "

            public DataReceivedEventArgs(byte[] buffer, int offset, int count)
            {
                _buffer = buffer;
                _offset = offset;
                _count = count;
            }

            #endregion
        }

    }
}
