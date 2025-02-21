using System.Runtime.InteropServices;

namespace AudioCodec
{
    public class Encoder
    {
        private string? encoderName;
        private IntPtr encoder;
        private int audioFormat;
        private int samplesInFrame;

        [DllImport("C:\\Users\\chengyuanlai\\source\\repos\\AudioCodecTest\\aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr encoder_construct();

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr encoder_destruct(IntPtr encoder);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int encoder_select(IntPtr encoder, int adspPayloadType, int bitrate, int ptime, int channels);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int encoder_get_sampling_rate(IntPtr encoder);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr get_current_encoder_name(IntPtr encoder);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int encoder_encode(IntPtr encoder, byte[] pcm, int pcmLength, byte[] encodedData, ref int encodedDataLength, byte[] redEncodedData, ref int redEncodedDataLength);

        public void Initialize(int audioFormat, int frameSize, int bitRate, int pTime, int numChannels)
        {
            this.encoder = encoder_construct();
            this.audioFormat = audioFormat;
            int result = select(audioFormat, bitRate, pTime, numChannels);
            this.samplesInFrame = getSamplesInFrame(frameSize);
        }

        public void Destroy()
        {
            encoder_destruct(this.encoder);
        }

        public (byte[], int, byte[], int) Encode(byte[] frameBuffer)
        {
            byte[] output = new byte[96000];
            int outputLength = 96000;
            byte[] redundantOutput = new byte[96000];
            int redundantOutputLength = 96000;

            int result = encoder_encode(this.encoder, frameBuffer, frameBuffer.Length, output, ref outputLength, redundantOutput, ref redundantOutputLength);
            return (output, outputLength, redundantOutput, redundantOutputLength);
        }

        public int GetSamplingRate()
        {
            return encoder_get_sampling_rate(this.encoder);
        }

        private int select(int audioFormat, int bitRate, int pTime, int numChannels)
        {
            int result = encoder_select(encoder, audioFormat, bitRate, pTime, numChannels);//, audioFormat.bitrate, audioFormat.pTime, audioFormat.channels);
            this.encoderName = getEncoderName();
            return result;
        }

        private int getSamplesInFrame(int frameSize = 20)
        {
            int fs = GetSamplingRate();
            float fps = 1000.0f / frameSize;
            return (int)Math.Round(fs / fps);
        }

        private string? getEncoderName()
        {
            IntPtr namePtr = get_current_encoder_name(encoder);

            if (namePtr == IntPtr.Zero)
            {
                Console.WriteLine("Encoder name pointer is null");
            }

            return Marshal.PtrToStringAnsi(namePtr);
        }
    }
}