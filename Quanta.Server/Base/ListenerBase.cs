using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace Quanta
{
    //ICE specifies that TURN servers supercede STUN servers (so only TURN is needed)

    //Ephemeral ports 49152 to 65535

    public abstract class ListenerBase : IDisposable
    {
        //The minimum buffer size required is 288 bytes. 

        #region " Consts "

        private const int SO_PAUSE_ACCEPT = 12291;

        #endregion

        #region " Win32 "

        [DllImport("ws2_32.dll", EntryPoint = "setsockopt")]
        private static extern int SetSockOpt(
            IntPtr socket,
            SocketOptionLevel level,
            int optionName,
            ref int optionValue, //Pointer
            int optionLength
        );

        [DllImport("ws2_32.dll", EntryPoint = "WSAGetLastError")]
        private static extern int WSAGetLastError();

        #endregion

        #region " Properties "

        public bool Listening
        {
            get { return (_listening == 1 && _isDisposed == 0); }
        }

        public bool IsBound
        {
            get { return (_isBound == 1 && _isDisposed == 0); }
        }

        public bool IsDisposed
        {
            get { return (_isDisposed == 1); }
        }

        public object UserToken
        {
            get { return _userToken; }
            set { _userToken = value; }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        public Socket Socket
        {
            get { return _socket; }
        }

        #endregion

        #region " Members "

        private Socket _socket;
        private SocketAsyncEventArgs _acceptArgs;

        private int _doBind;
        private int _doListen;
        private int _doDispose;

        private int _isBound;
        private int _isDisposed;
        private int _listening;
        private int _initialized;

        private object _userToken;
        private IPEndPoint _localEndPoint;

        #endregion

        #region " Event Handlers "

        protected abstract void OnSocketConnected(Socket socket);

        protected abstract void OnExceptionThrown(Exception ex);

        protected abstract void OnBindingFailed();

        protected abstract void OnBindingCompleted();

        protected abstract void OnListeningFailed();

        protected abstract void OnListeningStarted();

        protected abstract void OnDisposed();
 
        private void OnExceptionThrownInternal(Exception ex)
        {
            if (_isDisposed == 1 || Interlocked.Exchange(ref _doDispose, 1) == 1)
            {
                return;
            }

            Dispose();

            OnExceptionThrown(ex);
        }

        #endregion

        #region " Bind "

        public void Bind()
        {
            Bind(new IPEndPoint(IPAddress.Loopback, 0));
        }

        public void Bind(ushort port)
        {
            Bind(new IPEndPoint(IPAddress.Loopback, port));
        }

        public void Bind(IPAddress address, ushort port)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            Bind(new IPEndPoint(address, port));
        }

        public void Bind(IPEndPoint localEndPoint)
        {
            if (localEndPoint == null)
            {
                throw new ArgumentNullException(nameof(localEndPoint));
            }

            if (_isDisposed == 1 || Interlocked.Exchange(ref _doBind, 1) == 1)
            {
                return;
            }

            try
            {
                _socket = CreateSocket();
                _socket.Bind(localEndPoint);

                _localEndPoint = (IPEndPoint)_socket.LocalEndPoint;
            }
            catch (Exception ex)
            {
                OnExceptionThrownInternal(ex);
                return;
            }

            Interlocked.Exchange(ref _isBound, 1);

            OnBindingCompleted();
        }

        #endregion

        #region " Listen "

        public void Listen()
        {
            Listen(5);
        }

        public void Listen(ushort backlog)
        {
            if (Interlocked.Exchange(ref _doListen, 1) == 1)
            {
                return;
            }

            Bind();

            if (_isDisposed == 1)
            {
                return;
            }

            try
            {
                _acceptArgs = new SocketAsyncEventArgs();
                _acceptArgs.Completed += Accept_Completed;

                _socket.Listen(backlog);
            }
            catch (Exception ex)
            {
                OnExceptionThrownInternal(ex);
                return;
            }

            Interlocked.Exchange(ref _listening, 1);

            OnListeningStarted();

            if (_isDisposed == 1)
            {
                return;
            }

            _socket.Poll(100, SelectMode.SelectRead);

            BeginAccept(_acceptArgs);
        }

        #endregion

        #region " Accept "

        private void BeginAccept(SocketAsyncEventArgs e)
        {
            bool pending = false;

            while (true)
            {
                try
                {
                    e.AcceptSocket = CreateSocket();

                    pending = _socket.AcceptAsync(e);
                }
                catch (Exception ex)
                {
                    OnExceptionThrownInternal(ex);
                    return;
                }

                if (pending || !EndAccept(_socket, e))
                {
                    return;
                }
            } 
        }

        private bool EndAccept(object sender, SocketAsyncEventArgs e)
        {
            Initialize();

            if (_isDisposed == 1)
            {
                return false;
            }

            Socket socket = e.AcceptSocket;

            if (e.SocketError == SocketError.Success)
            {
                OnSocketConnected(socket);
            }
            else if (e.SocketError == SocketError.ConnectionAborted || e.SocketError == SocketError.ConnectionReset)
            {
                socket.Close();
            }
            else
            {
                OnExceptionThrownInternal(new SocketException((int)e.SocketError));
                return false;
            }

            return true;
        }

        private void Accept_Completed(object sender, SocketAsyncEventArgs e)
        {
            if(EndAccept(sender, e))
            {
                BeginAccept(e);
            }
        }

        private void Initialize()
        {
            if (_isDisposed == 1 || Interlocked.Exchange(ref _initialized, 1) == 1)
            {
                return;
            }

            try
            {
                _localEndPoint = (IPEndPoint)_socket.LocalEndPoint;
            }
            catch (Exception ex)
            {
                OnExceptionThrownInternal(ex);
                return;
            }
        }

        #endregion

        #region " Pause / Resume "

        public void Pause()
        {
            try
            {
                SetSocketOption(_socket.Handle, SocketOptionLevel.Socket, SO_PAUSE_ACCEPT, 1);
            }
            catch (Exception ex)
            {
                OnExceptionThrownInternal(ex);
            }
        }

        public void Resume()
        {
            try
            {
                SetSocketOption(_socket.Handle, SocketOptionLevel.Socket, SO_PAUSE_ACCEPT, 0);
            }
            catch (Exception ex)
            {
                OnExceptionThrownInternal(ex);
            }
        }

        #endregion

        #region " Helpers "

        protected virtual Socket CreateSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        protected static void SetSocketOption(IntPtr handle, SocketOptionLevel level, int name, int value)
        {
            if (SetSockOpt(handle, level, name, ref value, 4) != 0)
            {
                throw new Win32Exception(WSAGetLastError());
            }
        }

        #endregion

        #region " IDisposable "

        private void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                if (disposing)
                {
                    _socket?.Close();
                    _acceptArgs?.Dispose();
                }

                if (_doBind == 1 && _isBound == 0)
                {
                    OnBindingFailed();
                }
                else if (_doListen == 1 && _listening == 0)
                {
                    OnListeningFailed();
                }

                OnDisposed();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

    }
}
