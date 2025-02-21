using System.Runtime.InteropServices;

namespace AudioCodec
{
    public class Decoder
    {
        private string? decoderName;
        private IntPtr decoder;
        private int audioFormat;
        private int samplesInFrame;

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr decoder_construct();

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr decoder_destruct(IntPtr encoder);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int decoder_select(IntPtr decoder, int adspPayloadType);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int decoder_get_sampling_rate(IntPtr decoder);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr get_current_decoder_name(IntPtr decoder);

        [DllImport("aetest.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int decoder_decode(IntPtr decoder, byte[] encodedData, int encodedDataLength, byte[] pcm, ref int pcmLength);

        public void Initialize(int audioFormat, int frameSize)
        {
            this.decoder = decoder_construct();
            this.audioFormat = audioFormat;
            int result = select(audioFormat);
            this.samplesInFrame = getSamplesInFrame(frameSize);
        }

        public void Destroy()
        {
            decoder_destruct(this.decoder);
        }

        public (byte[], int) Decode(byte[] frameBuffer)
        {
            int inputLength = frameBuffer.Length;

            byte[] output = new byte[this.samplesInFrame * 2];
            int outputLength = this.samplesInFrame * 2;

            int result = decoder_decode(this.decoder, frameBuffer, frameBuffer.Length, output, ref outputLength);
            return (output, outputLength);
        }

        private int select(int audioFormat)
        {
            int result = decoder_select(decoder, audioFormat);
            this.decoderName = getDecoderName();
            return result;
        }

        private int getSamplesInFrame(int frameSize = 20)
        {
            int fs = GetSamplingRate();
            float fps = 1000.0f / frameSize;
            return (int)Math.Round(fs / fps);
        }

        private string? getDecoderName()
        {
            IntPtr namePtr = get_current_decoder_name(decoder);

            if (namePtr == IntPtr.Zero)
            {
                Console.WriteLine("Encoder name pointer is null");
            }

            return Marshal.PtrToStringAnsi(namePtr);
        }

        public int GetSamplingRate()
        {
            return decoder_get_sampling_rate(this.decoder);
        }
    }
}
