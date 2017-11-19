using System;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

//NOTE: SSL operates on chunks of 16384 (Tls records), perhaps a separate memory pool for these would be ideal (instead of allocating 64KB and only using part of it.)

namespace Quanta
{
    //SIO_IDEAL_SEND_BACKLOG_QUERY

    //NOTE: This is important because we don't want consumers of 16-200 bytes locking up EventArgs
    //Ethernet V2 MTU = 1500, arbitrary for our purposes but better than some random value without rhyme or reason

    //Use a Queue so an in-use EventArg can be returned to the back of the line while it's being used

    //Once the reference counter is set to 0, reset to the original offset

    //NOTE: Pooling should apply to byte buffers, not SocketEventArgs; SendArgs should be retreived from a pool but this may not make sense
    //for receive operations since they may wait an indeterminate period of time. However, in low bandwidth scenarios it may not be an issue
    //given the use of a keep-alive messaging system (though perhaps this could cause client disconnects?).

    //ClientBase should enforce an internal ConnectionTimeout via a Task?

    //DisconnectTimeout default = 2000? 
    //PingTimeout = 500

    //{"The IAsyncResult object was not returned from the corresponding asynchronous method on this class.\r\nParameter name: asyncResult"}


    //TODO: Seperate this into Client / Server 'nodes', this way the protocol knows which should initiate communications (especially in hole punch cases.)
    public sealed partial class Client : ClientBase
    {

        #region " Consts "

        private const int TCP_MAXRT = 5;
        private const int TCP_TIMESTAMPS = 10;

        #endregion

        #region " Properties "

        //NOTE: Because of limitations in rebinding with ExclusiveAddressUse set, all Clients should have a Parent / Listener property in case
        //      they need to be closed to restart the listener.

        public Socket Parent
        {
            get { return _parent; }
        }

        /// <summary>
        /// Defines the duration in milliseconds between keep-alive packets, also known as pings.
        /// </summary>
        public int PingTimeout
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Defines the timeout in milliseconds before a connection is considered to have been disconnected.
        /// </summary>
        public int DisconnectTimeout
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The maximum sampled round trip time, in milliseconds.
        /// </summary>
        public int MaximumPing
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The minimum sampled round trip time, in milliseconds.
        /// </summary>
        public int MinimumPing
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The round trip time, in milliseconds.
        /// </summary>
        public int Ping
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The last time that data was received on this connection.
        /// </summary>
        public DateTime LastReceiveTime
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The last time that data was sent on this connection.
        /// </summary>
        public DateTime LastWriteTime
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The time that the network connection was established.
        /// </summary>
        public DateTime ConnectionTime
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The time that the network connection was authenticated.
        /// </summary>
        public DateTime AuthenticationTime
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The total number of bytes received on this connection.
        /// </summary>
        public long BytesReceived
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The total number of bytes sent on this connection.
        /// </summary>
        public long BytesWritten
        {
            get { throw new NotImplementedException(); }
        }

        //public string ServerName
        //{
        //    get { return _serverName; }
        //    set { _serverName = value; }
        //}

        //public X509Certificate ServerCertificate
        //{
        //    get { return _serverCertificate; }
        //    set { _serverCertificate = value; }
        //}

        //public X509CertificateCollection ClientCertificates
        //{
        //    get { return _clientCertificates; }
        //    set { _clientCertificates = value; }
        //}

        //public ECCertificate Certificate
        //{
        //    get { return _certificate; }
        //    set { _certificate = value; }
        //}

        public bool IsAuthority
        {
            get { return _isAuthority; }
        }

        public bool IsAuthenticated
        {
            get { return _isAuthenticated; }
        }

        #endregion

        #region " Events "

        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<ExceptionThrownEventArgs> ExceptionThrown;
        public event EventHandler Authenticated;
        public event EventHandler BindingFailed;
        public event EventHandler BindingCompleted;
        public event EventHandler ConnectionFailed;
        public event EventHandler ConnectionEstablished;
        //public event EventHandler PingCompleted;
        public event EventHandler Disposed;

        #endregion

        #region " Members "

        private bool _isAuthenticated;
        private bool _isAuthority;
        private bool _noDelay;
        private bool _exclusiveAddressUse;
        private bool _useTimestamps;

        private int _connectionTimeout;

        //private ECCertificate _certificate;

        //private Peer _peer;

        //private string _serverName;
        //private X509Certificate _serverCertificate;
        //private X509CertificateCollection _clientCertificates;

        private IPProtectionLevel _protectionLevel;

        private Socket _parent;

        //private SslStream _sslStream;
        //private ConcurrentQueue<SocketAsyncEventArgs> _receiveQueue;
        //private CancellationTokenSource _tokenSource;
        //private SemaphoreSlim _semaphore;

        //private byte[] _sslBuffer;

        #endregion

        #region " Event Handlers "

        protected override void OnSendFailed(SocketAsyncEventArgs e)
        {
            e.Completed -= SendCompleted;

            ReturnEventArgs(e);
        }

        protected override void OnSendCompleted(SocketAsyncEventArgs e)
        {
            e.Completed -= SendCompleted;

            ReturnEventArgs(e);
        }

        protected override void OnReceiveFailed(SocketAsyncEventArgs e)
        {
            e.Completed -= ReceiveCompleted;

            ReturnEventArgs(e);
        }

        protected override void OnReceiveCompleted(SocketAsyncEventArgs e)
        {
            e.Completed -= ReceiveCompleted;

            //_peer.Decode(e.Buffer, e.Offset, e.BytesTransferred);

            //_receiveQueue.Enqueue(e);
            //_semaphore.Release();
        }

        protected override void OnExceptionThrown(Exception ex)
        {
            EventHandler<ExceptionThrownEventArgs> handler = ExceptionThrown;

            handler?.Invoke(this, new ExceptionThrownEventArgs(ex));
        }

        protected override void OnBindingFailed()
        {
            EventHandler handler = BindingFailed;

            handler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnBindingCompleted()
        {
            EventHandler handler = BindingCompleted;

            handler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnConnectionFailed()
        {
            EventHandler handler = ConnectionFailed;

            handler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnConnectionEstablished()
        {
            EventHandler handler = ConnectionEstablished;

            handler?.Invoke(this, EventArgs.Empty);

            //if (_isAuthority)
            //{
            //    _peer = new ServerPeer(this, 20);
            //}
            //else
            //{
            //    _peer = new ClientPeer(this);
            //}

            //_peer.Initialize();

            //BeginAuthentication();
        }

        private void OnAuthenticated()
        {
            _isAuthenticated = true;

            EventHandler handler = Authenticated;

            handler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDisposed()
        {
            EventHandler handler = Disposed;

            handler?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region " Constructor "

        private Client(Socket parent, Socket socket, Options options, bool isAuthority) : base(socket)
        {
            _parent = parent;
            _isAuthority = isAuthority;

            if (options != null)
            {
                _noDelay = options.NoDelay;
                _exclusiveAddressUse = options.ExclusiveAddressUse;
                _useTimestamps = options.UseTimestamps;
                _connectionTimeout = options.ConnectionTimeout;
                _protectionLevel = options.ProtectionLevel;
            }
        }

        #endregion

        //#region " SSL "

        //private void BeginAuthentication()
        //{
        //    ClientStream stream = new ClientStream(this);

        //    _semaphore = new SemaphoreSlim(0);
        //    _tokenSource = new CancellationTokenSource();
        //    _receiveQueue = new ConcurrentQueue<SocketAsyncEventArgs>();

        //    _sslBuffer = new byte[16384];
        //    _sslStream = new SslStream(stream, false, ValidateRemoteCertificate, SelectLocalCertificate, EncryptionPolicy.RequireEncryption);

        //    if (_isServer)
        //    {
        //        _sslStream.BeginAuthenticateAsServer(_serverCertificate, false, SslProtocols.Tls12, false, EndAuthentication, null);
        //    }
        //    else
        //    {
        //        _sslStream.BeginAuthenticateAsClient(_serverName, _clientCertificates, SslProtocols.Tls12, false, EndAuthentication, null);
        //    }
        //}

        //private void EndAuthentication(IAsyncResult r)
        //{
        //    if (_isServer)
        //    {
        //        _sslStream.EndAuthenticateAsServer(r);
        //    }
        //    else
        //    {
        //        _sslStream.EndAuthenticateAsClient(r);
        //    }

        //    if (!_sslStream.IsEncrypted || !_sslStream.IsSigned || _sslStream.CipherStrength < 128)
        //    {
        //        throw new CryptographicException();
        //    }

        //    OnAuthenticated();
        //    BeginRead();
        //}

        //private void BeginRead()
        //{
        //    _sslStream.BeginRead(_sslBuffer, 0, _sslBuffer.Length, EndRead, null);
        //}

        //private void EndRead(IAsyncResult r)
        //{
        //    int bytesRead = _sslStream.EndRead(r);

        //    EventHandler<DataReceivedEventArgs> handler = DataReceived;

        //    handler?.Invoke(this, new DataReceivedEventArgs(_sslBuffer, 0, bytesRead));

        //    BeginRead();
        //}

        //private static bool ValidateRemoteCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        //{
        //    return true;

        //    //if (sslPolicyErrors == SslPolicyErrors.None)
        //    //{
        //    //    return true;
        //    //}

        //    //return false;
        //}

        //private static X509Certificate SelectLocalCertificate(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        //{
        //    if (acceptableIssuers != null && acceptableIssuers.Length > 0 && localCertificates != null && localCertificates.Count > 0)
        //    {
        //        foreach (X509Certificate certificate in localCertificates)
        //        {
        //            if (Array.IndexOf(acceptableIssuers, certificate.Issuer) != -1)
        //            {
        //                return certificate;
        //            }
        //        }
        //    }

        //    if (localCertificates != null && localCertificates.Count > 0)
        //    {
        //        return localCertificates[0];
        //    }

        //    return null;
        //}

        //#endregion

        #region " Send "

        public void Send(byte[] buffer)
        {
            Send(buffer, 0, buffer.Length);
        }

        public void Send(byte[] buffer, int offset, int count)
        {
            if (!_isAuthenticated)
            {
                throw new InvalidOperationException("Client is not authenticated.");
            }

            //_peer.Encode(buffer, offset, count);
        }

        #endregion

        #region " Factory "

        internal void HandleAuthenticated()
        {
            OnAuthenticated();
        }

        internal void HandleDataReceived(byte[] buffer, int offset, int count)
        {
            EventHandler<DataReceivedEventArgs> handler = DataReceived;
            handler?.Invoke(this, new DataReceivedEventArgs(buffer, offset, count));
        }

        //TODO: This should have an overload that does not expect an authority (false by default)
        public static Client Create(bool isAuthority)
        {
            return Create(isAuthority, new Options());
        }

        public static Client Create(bool isAuthority, Options options)
        {
            return new Client(null, null, options, isAuthority);
        }

        //TODO: When will this ever be called in which the client will not be an authority?
        public static Client BeginCreate(Socket parent, Socket socket, bool isAuthority)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            return new Client(parent, socket, null, isAuthority);
        }

        public static void EndCreate(Client client)
        {
            client.Initialize();
        }

        #endregion

        #region " Helpers "

        //internal bool Read(out SocketAsyncEventArgs data)
        //{
        //    _semaphore.Wait(_tokenSource.Token);

        //    return _receiveQueue.TryDequeue(out data);
        //}

        internal void ReturnEventArgs(SocketAsyncEventArgs eventArgs)
        {
            ((SaeaResource)eventArgs.UserToken).Dispose();
        }

        protected override SocketAsyncEventArgs GetReceiveArgs()
        {
            SaeaResource resource = SaeaPool.Shared.Rent();
            resource.EventArgs.UserToken = resource;
            resource.EventArgs.Completed += ReceiveCompleted;

            return resource.EventArgs;
        }

        protected override SocketAsyncEventArgs GetSendArgs()
        {
            SaeaResource resource = SaeaPool.Shared.Rent();
            resource.EventArgs.UserToken = resource;
            resource.EventArgs.Completed += SendCompleted;

            return resource.EventArgs;
        }

        protected override Socket CreateSocket()
        {
            Socket socket = base.CreateSocket();
            socket.LingerState = new LingerOption(true, 0);
            socket.ExclusiveAddressUse = _exclusiveAddressUse;
            socket.NoDelay = _noDelay;

            socket.SetIPProtectionLevel(_protectionLevel);

            SetSocketOption(socket.Handle, SocketOptionLevel.Tcp, TCP_MAXRT, _connectionTimeout);
            SetSocketOption(socket.Handle, SocketOptionLevel.Tcp, TCP_TIMESTAMPS, _useTimestamps ? 1 : 0);

            return socket;
        }

        #endregion

        //#region " IDispose "

        //public override void Dispose()
        //{
        //    base.Dispose();

        //    _tokenSource?.Cancel(); //TODO: NullReferenceException is occuring here

        //    //TODO: Empty _receiveQueue and release objects

        //    _sslStream?.Dispose();
        //    _semaphore?.Dispose();
        //    _tokenSource?.Dispose();
        //}

        //#endregion

    }
}
