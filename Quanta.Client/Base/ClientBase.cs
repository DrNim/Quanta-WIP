using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace Quanta
{
    //NOTE: BeginSend should not be called until all pending EndSend operations on the returned SocketAsyncEventArgs have raised SendCompleted, otherwise
    //      data corruption could occur (e.g. writing over outgoing data before the transfer has completed).

    //NOTE: We need a AsyncEventArg pool to prevent deadlocks and resource exaustion (in both send and receive); for this same reason it is important we only use
    //      a single AsyncEventArg per send / recv - otherwise scenarios could arrise in which a few clients consume all available resources and starve other clients

    //NOTE: At the protocol level, after data has been received it should be processed into it's individual packets and raise notifications on new threads (one for
    //      each receiver - this way we do not congest bandwidth if a receiver is slow to process the data (such as decoding)

    public abstract partial class ClientBase
    {

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
        private static extern int WsaGetLastError();

        #endregion

        #region " Properties "

        public bool Connected
        {
            get { return (_connected == 1 && _isDisposed == 0); }
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

        public IPEndPoint RemoteEndPoint
        {
            get { return _remoteEndPoint; }
        }

        public Socket Socket
        {
            get { return _socket; }
        }

        public bool Inherited
        {
            get { return _inherited; }
        }

        protected EventHandler<SocketAsyncEventArgs> SendCompleted
        {
            get { return Send_Completed; }
        }

        protected EventHandler<SocketAsyncEventArgs> ReceiveCompleted
        {
            get { return Receive_Completed; }
        }

        #endregion

        #region " Members "

        private Socket _socket;
        private SocketAsyncEventArgs _connectArgs;

        private SocketAsyncEventArgs _sendArgs;
        private SocketAsyncEventArgs _receiveArgs;

        private int _doBind;
        private int _doConnect;
        private int _doDispose;

        private int _isBound;
        private int _isDisposed;
        private int _connected;

        private object _userToken;

        private bool _inherited;

        private IPEndPoint _localEndPoint;
        private IPEndPoint _remoteEndPoint;

        #endregion

        #region " Event Handlers "

        protected abstract void OnSendCompleted(SocketAsyncEventArgs e);

        protected abstract void OnReceiveCompleted(SocketAsyncEventArgs e);

        protected abstract void OnSendFailed(SocketAsyncEventArgs e);

        protected abstract void OnReceiveFailed(SocketAsyncEventArgs e);

        protected abstract void OnExceptionThrown(Exception ex);

        protected abstract void OnBindingFailed();

        protected abstract void OnBindingCompleted();

        protected abstract void OnConnectionFailed();

        protected abstract void OnConnectionEstablished();

        protected abstract void OnDisposed();
 
        #endregion

        #region " Constructor "

        protected ClientBase(Socket socket)
        {
            if (socket != null)
            {
                _socket = socket;
                _inherited = true;
                _isBound = 1;
            }
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

            if (_inherited)
            {
                throw new InvalidOperationException();
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
                HandleException(ex);
                return;
            }

            Interlocked.Exchange(ref _isBound, 1);

            OnBindingCompleted();
        }

        #endregion

        #region " Connect "

        public void Connect(IPAddress address, ushort port)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            Connect(new IPEndPoint(address, port));
        }

        public void Connect(IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            if (_inherited)
            {
                throw new InvalidOperationException();
            }

            if (Interlocked.Exchange(ref _doConnect, 1) == 1)
            {
                return;
            }

            Bind();

            if (_isDisposed == 1)
            {
                return;
            }

            bool pending = false;

            try
            {
                _connectArgs = new SocketAsyncEventArgs();
                _connectArgs.RemoteEndPoint = remoteEndPoint;
                _connectArgs.Completed += Connect_Completed;

                pending = _socket.ConnectAsync(_connectArgs);
            }
            catch (Exception ex)
            {
                HandleException(ex);
                return;
            }

            if (!pending)
            {
                Connect_Completed(_socket, _connectArgs);
            }
        }

        private void Connect_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (_isDisposed == 1)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                HandleException(new SocketException((int)e.SocketError));
                return;
            }

            try
            {
                _connectArgs.Dispose();
            }
            catch (Exception ex)
            {
                HandleException(ex);
                return;
            }

            InitializeInternal();
        }

        protected void Initialize()
        {
            if (!_inherited)
            {
                throw new InvalidOperationException();
            }

            InitializeInternal();
        }

        private void InitializeInternal()
        {
            try
            {
                _localEndPoint = (IPEndPoint)_socket.LocalEndPoint;
                _remoteEndPoint = (IPEndPoint)_socket.RemoteEndPoint;
            }
            catch (Exception ex)
            {
                HandleException(ex);
                return;
            }

            Interlocked.Exchange(ref _connected, 1);

            OnConnectionEstablished();

            if (_isDisposed == 1)
            {
                return;
            }

            BeginReceive();
        }

        #endregion

        #region " Receive "

        private void BeginReceive()
        {
            bool pending = false;

            while (true)
            {
                SocketAsyncEventArgs eventArgs = GetReceiveArgs();

                try
                {
                    pending = _socket.ReceiveAsync(eventArgs);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                    return;
                }

                if (pending || !EndReceive(_socket, eventArgs))
                {
                    return;
                }
            }
        }

        private bool EndReceive(object sender, SocketAsyncEventArgs e)
        {
            bool success = false;

            try
            {
                if (_isDisposed == 1)
                {
                    return false;
                }

                if (e.BytesTransferred <= 0)
                {
                    Dispose();
                    return false;
                }

                if (e.SocketError != SocketError.Success)
                {
                    HandleException(new SocketException((int)e.SocketError));
                    return false;
                }

                success = true;
                OnReceiveCompleted(e);

                if (_isDisposed == 1)
                {
                    return false;
                }

                return true;
            }
            finally
            {
                if (!success)
                {
                    OnReceiveFailed(e);
                }
            }
        }

        private void Receive_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (EndReceive(sender, e))
            {
                BeginReceive();
            }
        }

        #endregion

        #region " Send "

        internal SocketAsyncEventArgs BeginSend()
        {
            return GetSendArgs();
        }

        internal void EndSend(SocketAsyncEventArgs eventArgs, int bytesWritten)
        {
            bool pending = false;

            try
            {
                eventArgs.SetBuffer(eventArgs.Buffer, eventArgs.Offset, bytesWritten);

                pending = _socket.SendAsync(eventArgs);
            }
            catch (Exception ex)
            {
                HandleException(ex);
                OnSendFailed(eventArgs);
                return;
            }

            if (!pending)
            {
                Send_Completed(_socket, eventArgs);
            }
        }

        private void Send_Completed(object sender, SocketAsyncEventArgs e)
        {
            bool success = false;

            try
            {
                if (_isDisposed == 1)
                {
                    return;
                }

                if (e.SocketError != SocketError.Success)
                {
                    HandleException(new SocketException((int)e.SocketError));
                    return;
                }

                success = true;
                OnSendCompleted(e);
            }
            finally
            {
                if (!success)
                {
                    OnSendFailed(e);
                }
            }
        }

        #endregion

        #region " Helpers "

        internal void HandleException(Exception ex)
        {
            if (_isDisposed == 1 || Interlocked.Exchange(ref _doDispose, 1) == 1)
            {
                return;
            }

            Dispose();

            OnExceptionThrown(ex);
        }

        protected virtual SocketAsyncEventArgs GetSendArgs()
        {
            if (_sendArgs == null)
            {
                _sendArgs = new SocketAsyncEventArgs();
                _sendArgs.Completed += SendCompleted;
                _sendArgs.SetBuffer(new byte[ushort.MaxValue], 0, ushort.MaxValue);
            }

            return _sendArgs;
        }

        protected virtual SocketAsyncEventArgs GetReceiveArgs()
        {
            if (_receiveArgs == null)
            {
                _receiveArgs = new SocketAsyncEventArgs();
                _receiveArgs.Completed += ReceiveCompleted;
                _receiveArgs.SetBuffer(new byte[ushort.MaxValue], 0, ushort.MaxValue);
            }

            return _receiveArgs;
        }

        protected virtual Socket CreateSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        protected static void SetSocketOption(IntPtr handle, SocketOptionLevel level, int name, int value)
        {
            if (SetSockOpt(handle, level, name, ref value, 4) != 0)
            {
                throw new Win32Exception(WsaGetLastError());
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
                    _connectArgs?.Dispose();
                }

                if (_doBind == 1 && _isBound == 0)
                {
                    OnBindingFailed();
                }
                else if (_doConnect == 1 && _connected == 0)
                {
                    OnConnectionFailed();
                }

                OnDisposed();
            }
        }

        public virtual void Dispose()
        {
            Dispose(true);
        }

        #endregion

    }

}
