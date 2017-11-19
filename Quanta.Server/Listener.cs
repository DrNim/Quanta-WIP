using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Quanta
{
    //TODO: OnSocketConnected and OnSocketConnectedInternal should be swapped..
    //TODO: Rename SocketConnectedInternal to HandleSocketConnected, move to Helpers region
    //TODO: Same as above, with ExceptionThrownInternal

    //props: lastacceptime, listentime, etc..

    public sealed partial class Listener : ListenerBase
    {

        #region " Consts "

        private const int TCP_TIMESTAMPS = 10;

        #endregion

        #region " Events "

        public event EventHandler<SocketConnectedEventArgs> SocketConnected;
        public event EventHandler<ExceptionThrownEventArgs> ExceptionThrown;
        public event EventHandler BindingFailed;
        public event EventHandler BindingCompleted;
        public event EventHandler ListeningFailed;
        public event EventHandler ListeningStarted;
        public event EventHandler Disposed;

        #endregion

        #region " Event Handlers "

        protected override void OnSocketConnected(Socket socket)
        {
            Task.Run(() => OnSocketConnectedInternal(socket));
        }

        private void OnSocketConnectedInternal(Socket socket)
        {
            if (!Listening)
            {
                return;
            }

            EventHandler<SocketConnectedEventArgs> handler = SocketConnected;

            handler?.Invoke(this, new SocketConnectedEventArgs(socket));
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

        protected override void OnListeningFailed()
        {
            EventHandler handler = ListeningFailed;

            handler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnListeningStarted()
        {
            EventHandler handler = ListeningStarted;

            handler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDisposed()
        {
            EventHandler handler = Disposed;

            handler?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region " Members "

        private bool _noDelay;
        private bool _exclusiveAddressUse;
        private bool _useTimestamps;

        private IPProtectionLevel _protectionLevel;

        #endregion

        #region " Constructor "

        private Listener(Options options) : base()
        {
            _noDelay = options.NoDelay;
            _exclusiveAddressUse = options.ExclusiveAddressUse;
            _useTimestamps = options.UseTimestamps;
            _protectionLevel = options.ProtectionLevel;
        }

        #endregion

        #region " Factory "

        public static Listener Create()
        {
            return Create(new Options());
        }

        public static Listener Create(Options options)
        {
            return new Listener(options);
        }

        #endregion

        #region " Helpers "

        protected override Socket CreateSocket()
        {
            Socket socket = base.CreateSocket();
            socket.LingerState = new LingerOption(true, 0);
            socket.ExclusiveAddressUse = _exclusiveAddressUse;
            socket.NoDelay = _noDelay;

            socket.SetIPProtectionLevel(_protectionLevel);

            SetSocketOption(socket.Handle, SocketOptionLevel.Tcp, TCP_TIMESTAMPS, _useTimestamps ? 1 : 0);

            return socket;
        }

        #endregion

    }
}
