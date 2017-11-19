using System;
using System.Net.Sockets;

namespace Quanta
{
    public sealed partial class Listener
    {
        public sealed class SocketConnectedEventArgs : EventArgs
        {
            #region " Properties "

            public Socket Socket
            {
                get { return _socket; }
            }

            #endregion

            #region " Members "

            private Socket _socket;

            #endregion

            #region " Constructor "

            public SocketConnectedEventArgs(Socket socket)
            {
                _socket = socket;
            }

            #endregion

        }
    }
}
