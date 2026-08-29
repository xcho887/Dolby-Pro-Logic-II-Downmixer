using System;

namespace AudioDownmixerGUI
{
    internal class ReadOnlySpan<T>
    {
        private byte[] outBytes;
        private int v;
        private int length;

        public ReadOnlySpan(byte[] outBytes, int v, int length)
        {
            this.outBytes = outBytes;
            this.v = v;
            this.length = length;
        }

        internal object Cast<T1, T2>()
        {
            throw new NotImplementedException();
        }
    }
}