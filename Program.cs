using NAudio.Wave;
using System.Runtime.InteropServices;

namespace AudioCodec
{
    public class Program
    {
        [DllImport("RtmPal.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern ulong RtcPalStartup();

        [DllImport("RtmPal.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void RtcPalCleanup();

        public const int FRAMESIZE = 20;

        private WavReader reader;
        private string filePath;

        private static MediaStreaming? acsMediaStreaming;

        public Program(string filePath)
        {
            this.reader = new WavReader(filePath);
            this.Start().Wait();
            //this.TestEncode(102);
            //this.filePath = filePath;
            //this.TestAudioEngineEcho();
        }

        static void Main(string[] args)
        {
            RtcPalStartup();
            //Program test = new Program("tone.wav");
            Program test = new Program("CantinaBand60-16000.wav");
            RtcPalCleanup();
        }

        public async Task Start()
        {
            acsMediaStreaming = new MediaStreaming();
            var roomId = "testing-codec";
            await acsMediaStreaming.Connect(roomId);
            
            //WaveFormat waveFormat = new WaveFormat(24000, 16, 1);
            //BufferedWaveProvider buffer = new BufferedWaveProvider(waveFormat)
            //{
            //    BufferDuration = TimeSpan.FromSeconds(60)
            //};
            
            //acsMediaStreaming.audioReceived += (sender, decodedPcmChunk) =>
            //{
            //    try
            //    {
            //        // Console.WriteLine($"Received audio packets for playback {decodedPcmChunk.Length}");
            //        buffer.AddSamples(decodedPcmChunk, 0, decodedPcmChunk.Length);
            //    }
            //    catch (Exception e)
            //    {
            //        Console.WriteLine($"Failed to add Samples with error {e}");
            //        buffer.ClearBuffer();
            //        buffer.AddSamples(decodedPcmChunk, 0, decodedPcmChunk.Length);
            //    }
            //};

            Thread pcmAudioThread = new Thread(new ThreadStart(this.SendAudioData));
            pcmAudioThread.Start();

            bool earlyExit = false;
            Console.CancelKeyPress += async (sender, eventArgs) =>
            {
                Console.WriteLine("Break detected, shutting down.");
                earlyExit = true;
                eventArgs.Cancel = true;
                await acsMediaStreaming.Disconnect();
                System.Environment.Exit(1);
            };

            Console.WriteLine("Ready. Hit CTRL-C to terminate");
            while (!earlyExit)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private void SendAudioData()
        {
            Thread.Sleep(500);

            // Send Wav file data
            ReadWavFramesAndSend();
        }

        private void ReadWavFramesAndSend()
        {
            int frameDurationMs = 20;

            int bytesPerMillisecond = reader.ByteRate / 1000;
            int frameSize = bytesPerMillisecond * frameDurationMs;

            byte[] buffer = new byte[frameSize];
            int bytesRead = 0;

            while (bytesRead < reader.AudioData!.Length)
            {
                // Send the audio frame
                Array.Copy(reader.AudioData, bytesRead, buffer, 0, frameSize);
                bytesRead += frameSize;
                acsMediaStreaming?.SendAudio(buffer);
            }
        }

        public void TestAudioEngineEcho()
        {
            AudioEngineSend aeSend = new AudioEngineSend(102);
            AudioEngineRecv aeRecv = new AudioEngineRecv(102);

            byte[] wavArray;
            int samplesInFrame;
            (wavArray, samplesInFrame) = WavDataToArray(reader ,16000);

            //int frames = 0;
            //ulong processStart = (ulong)Stopwatch.GetTimestamp();
            int sendTimestampSamples = 0;
            ulong recvTimestamp = 0;

            byte[] aeOutput = new byte[wavArray.Length];

            for (int i = 0; i < wavArray.Length; i += samplesInFrame * reader.NumberOfChannels * (reader.BitsPerSample / 8))
            {
                recvTimestamp += 10000 * 20;
                sendTimestampSamples += samplesInFrame;

                byte[] input = new byte[samplesInFrame * reader.NumberOfChannels * (reader.BitsPerSample / 8)];
                Array.Copy(wavArray, i, input, 0, samplesInFrame * reader.NumberOfChannels * (reader.BitsPerSample / 8));
                
                byte[] encodedPayload, redundantEncodedPayload;
                int encodedPayloadLength, redundantEncodedPayloadLength, payloadType, processFrameResult;
                (encodedPayload, encodedPayloadLength, redundantEncodedPayload, redundantEncodedPayloadLength, payloadType, processFrameResult) = aeSend.ProcessFrame(input);
                byte[] actualPayload = new byte[encodedPayloadLength];
                Array.Copy(encodedPayload, actualPayload, encodedPayloadLength);

                int pushFrameResult = aeRecv.PushFrame(actualPayload, (ulong)sendTimestampSamples, recvTimestamp, payloadType);
                if (pushFrameResult != 0)
                {
                    Console.WriteLine("AERecv PushFrame failed");
                }

                byte[] output;
                int outputLength;
                AudioEngineRecv.PCMInfo info = new AudioEngineRecv.PCMInfo();
                int pcmFrameCounter;
                (output, outputLength, info, pcmFrameCounter) = aeRecv.PullPCM();
                Array.Copy(output, 0, aeOutput, i, samplesInFrame * reader.NumberOfChannels * (reader.BitsPerSample / 8));
            }

            // Create the new wav
            WaveFormat waveFormat = new WaveFormat(16000, 16, 1);
            using (MemoryStream ms = new MemoryStream(aeOutput))
            {
                Random rand = new Random();
                using (WaveFileWriter wavFileWriter = new WaveFileWriter(rand.Next().ToString()+"aeOutput.wav", waveFormat))
                {
                    wavFileWriter.Write(aeOutput, 0, aeOutput.Length);
                }
            }

            aeSend.Destroy();
            aeRecv.Destroy();
        }


        public void TestEncode(int audioFormat)
        {
            Encoder encoder = new Encoder();
            encoder.Initialize(audioFormat, 20, reader.ByteRate * 8, 1000, reader.NumberOfChannels);

            Decoder decoder = new Decoder();
            decoder.Initialize(audioFormat, 20);

            int encoderSamplingRate = encoder.GetSamplingRate();
            int decoderSampllingRate = decoder.GetSamplingRate();

            if (!ResampleWavFile(encoderSamplingRate))
            {
                return;
            }

            byte[] wavArray;
            int samplesInFrame;
            (wavArray, samplesInFrame) = WavDataToArray(reader, encoderSamplingRate);

            byte[] decodeOutput = new byte[wavArray.Length];

            for (int i = 0; i < wavArray.Length; i += samplesInFrame * (reader.BitsPerSample / 8))
            {
                byte[] input = new byte[samplesInFrame * (reader.BitsPerSample / 8)];
                Array.Copy(wavArray, i, input, 0, samplesInFrame * (reader.BitsPerSample / 8));

                byte[] encoded, redeundantEncoded, decoded;
                int encodedLength, redundantEncodedLength, decodedLength;
                (encoded, encodedLength, redeundantEncoded, redundantEncodedLength) = encoder.Encode(input);

                (decoded, decodedLength) = decoder.Decode(new ArraySegment<byte>(encoded, 0, encodedLength).ToArray());

                Array.Copy(decoded, 0, decodeOutput, i, samplesInFrame * (reader.BitsPerSample / 8));
            }

            encoder.Destroy();
            decoder.Destroy();

            // Create the new wav
            WaveFormat waveFormat = new WaveFormat(16000, 16, 1);
            using (MemoryStream ms = new MemoryStream(decodeOutput))
            {
                using (WaveFileWriter wavFileWriter = new WaveFileWriter("output.wav", waveFormat))
                {
                    wavFileWriter.Write(decodeOutput, 0, decodeOutput.Length);
                }
            }
        }

        public (byte[] wavArray, int samplesInFrame) WavDataToArray(WavReader reader, int targetSampleRate, int packetTime = 20)
        {
            float fps = 1000.0f / packetTime;
            int samplesInFrame = (int)(targetSampleRate / fps);

            int bytesInFrame = samplesInFrame * reader.NumberOfChannels * reader.BitsPerSample / 8;

            int totalFrames = (int)Math.Floor((float)reader.AudioData!.Length / bytesInFrame);

            Console.WriteLine("The total frame count is " + totalFrames);

            byte[] wavArray = new byte[totalFrames * bytesInFrame];
            Array.Copy(reader.AudioData!, 0, wavArray, 0, totalFrames * bytesInFrame);

            return (wavArray, samplesInFrame);
        }

        public bool ResampleWavFile(int targetSamplingRate)
        {
            if (reader!.SampleRate == targetSamplingRate)
            {
                Console.WriteLine("WavFile already have the target sampling rate");
                return true;
            }

            if (reader!.SampleRate / 2 != targetSamplingRate)
            {
                Console.WriteLine("Only support downsampling by 2");
                return false;
            }

            Console.WriteLine("Need to downsample");
            return false;
        }
    }
}
