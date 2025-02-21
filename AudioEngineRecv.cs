using System.Runtime.InteropServices;

namespace AudioCodec
{
    public class AudioEngineRecv
    {
        private IntPtr aeRecv;
        private ulong timestamp;
        private int sequenceNumber;

        private int pcmFrameCounter;

        [StructLayout(LayoutKind.Sequential)]
        public struct PCMInfo
        {
            public ulong rtpSrcTimestamp;
            public ulong rtpTimestamp;
            public ulong NTPTimestamp;

            public float averageEnergy;
            public int comfortNoise;
            public int concealedAudio;
            public int compressedAudio;
            public int stretchedAudio;
            public int unmodifiedAudio;

            public int bitsPerSample;
            public int channels;
            public int samplesPerSec;
        }

        public enum AudioRecvInfoInt { FECDistance = 1 }

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr AERecvConstruct();

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int AERecvInit(IntPtr aeRecv, int payloadType, int bitsPerSample, int channels, int samplesPerSec, ulong timestamp);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int AERecvPushFrame(IntPtr asRecv, byte[] encodedPayload, int encodedPayloadLength, int markerbit, int payloadType,
                                int sequenceNumber, ulong sendTimestampSamples, ulong recvTimestamp100ns, int isRedPacket,
                                int redPacketMainSequenceNumber, int redPacketTimestamplOffsetSamples);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int AERecvPullPCM(IntPtr asRecv, ulong timestamp, byte[] pcmOutput, ref int pcmOutputLength, ref PCMInfo pcmInfo);


        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int AERecvPullInfoInt(IntPtr aeRecv, AudioRecvInfoInt info, ref int value);


        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void AERecvDestruct(IntPtr aeRecv);

        public AudioEngineRecv(int payloadType = 104, int channels = 1, int samplesPerSeconds = 16000, ulong? timestamp = null)
        {
            this.timestamp = 0;
            this.sequenceNumber = 0;
            this.pcmFrameCounter = 0;
            this.aeRecv = AERecvConstruct();

            if (timestamp == null)
            {
                this.timestamp = 0;
            }
            else
            {
                this.timestamp = (ulong)timestamp;
            }

            int result = AERecvInit(this.aeRecv, payloadType, 16, 1, samplesPerSeconds, this.timestamp);
            if (result != 0)
            {
                Console.WriteLine("AERecv init failed!");
            }
        }

        public int PushFrame(byte[] encodedPayload, ulong sendTimestampSamples, ulong recvTimestamp, int payloadType = 104, bool markerBit = false,
                    bool isRedPacket = false, int redPacketMainSequenceNumber = 0, int redPacketTimestampOffsetSamples = 0, int? sequenceNumber = null)
        {
            if (sequenceNumber == null)
            {
                this.sequenceNumber += 1;
            }
            else
            {
                this.sequenceNumber = (int)sequenceNumber;
            }

            return AERecvPushFrame(this.aeRecv, encodedPayload, encodedPayload.Length, markerBit ? 1 : 0, payloadType, this.sequenceNumber,
                            sendTimestampSamples, recvTimestamp, isRedPacket ? 1 : 0, redPacketMainSequenceNumber, redPacketTimestampOffsetSamples);
        }

        public (byte[] output, int outputLength, PCMInfo pcmInfo, int pcmFrameCounter) PullPCM()
        {
            this.timestamp += 10000 * 20;
            int outputLength = 96000*2;
            byte[] output = new byte[outputLength];
            PCMInfo pcmInfo = new PCMInfo();

            int result = AERecvPullPCM(this.aeRecv, this.timestamp, output, ref outputLength, ref pcmInfo);
            this.pcmFrameCounter += 1;
            return (output, outputLength, pcmInfo, this.pcmFrameCounter);
        }

        public int PullInfoInt()
        {
            int value = 0;
            int result = AERecvPullInfoInt(this.aeRecv, AudioRecvInfoInt.FECDistance, ref value);
            return value;
        }

        public void Destroy()
        {
            AERecvDestruct(this.aeRecv);
        }
    }
}
