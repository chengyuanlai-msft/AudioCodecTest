using System.Runtime.InteropServices;

namespace AudioCodec
{
    public class AudioEngineSend
    {
        private IntPtr aeSend;
        private ulong timestamp;
        private int pTime;

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr AESendConstruct();

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int Init(IntPtr aeSend, int payloadType, int bitrate, int pTime,
                                int bitsPerSample, int channels, int samplesPerSec, ulong timestamp);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int ProcessFrame(IntPtr asSend, byte[] pcmInput, int pcmInputLength,
                                int silentFrame, ulong currentTimeStamp100ns, byte[] encodedPayload, ref int encodedPayloadLength,
                                byte[] encodedRedundantPayload, ref int encodedRedundantPayloadLength, ref int payloadType);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void AESendDestruct(IntPtr asSend);

        public AudioEngineSend(int payloadType = 104, int bitrate = 36000, int pTime = 20, int channels = 1, int samplePerSeconds = 16000, ulong? timestamp = null)
        {
            aeSend = AESendConstruct();
            this.timestamp = (ulong)(timestamp == null ? 0 : timestamp);
            this.pTime = pTime;
            int result = Init(aeSend, payloadType, bitrate, pTime, 16, channels, samplePerSeconds, this.timestamp);
            if (result != 0)
            {
                Console.WriteLine("AESend init failed!");
            }
        }

        public (byte[], int, byte[], int, int, int) ProcessFrame(byte[] frameBuffer, bool silentFrame = false, ulong? timestamp = null)
        {
            if (timestamp == null)
            {
                this.timestamp += 10000 * (ulong)this.pTime;
            }
            else
            {
                this.timestamp = (ulong)timestamp;
            }

            byte[] output = new byte[96000];
            int outputLength = 96000;
            byte[] redundantOutput = new byte[96000];
            int redundantOutputLength = 96000;
            int payloadType = 0;

            int result = ProcessFrame(this.aeSend, frameBuffer, frameBuffer.Length, silentFrame ? 1 : 0, this.timestamp,
                                output, ref outputLength, redundantOutput, ref redundantOutputLength, ref payloadType);

            return (output, outputLength, redundantOutput, redundantOutputLength, payloadType, result);
        }

        public void Destroy()
        {
            AESendDestruct(this.aeSend);
        }
    }
}
