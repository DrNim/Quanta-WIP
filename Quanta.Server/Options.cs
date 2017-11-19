using System.Net.Sockets;

namespace Quanta
{
    public sealed partial class Listener
    {

        public sealed class Options
        {

            #region " Properties "

            public bool ExclusiveAddressUse
            {
                get { return _exclusiveAddressUse; }
                set { _exclusiveAddressUse = value; }
            }

            public bool NoDelay
            {
                get { return _noDelay; }
                set { _noDelay = value; }
            } 

            public bool UseTimestamps
            {
                get { return _useTimestamps; }
                set { _useTimestamps = value; }
            }

            public IPProtectionLevel ProtectionLevel
            {
                get { return _protectionLevel; }
                set { _protectionLevel = value; }
            }

            #endregion

            #region " Members "

            private bool _noDelay; 
            private bool _exclusiveAddressUse;
            private bool _useTimestamps;

            private IPProtectionLevel _protectionLevel;

            #endregion 

            #region " Constructor "

            public Options()
            {
                _noDelay = true;
                _useTimestamps = true;
                _exclusiveAddressUse = true;
                _protectionLevel = IPProtectionLevel.Unrestricted;
            }

            #endregion

        }

    }
}
