using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Waimea.Channel;
using Waimea.Channel.Messages.Common;
using Waimea.Channel.Messages.ToBackend;
using Waimea.Channel.Messages.ToFrontend;

namespace AudioCodec
{
    public class MediaStreaming
    {
        // WB thigns
        private const string WB_ORIGIN = "https://alphasandbox.dev.waimeabae.com";
        private const uint AUDIO_FEED_VIEW_ID = 1;
        private FrontendChannel? channel = null;
        // custom private FeedId roomFeedId = new FeedId() { Name = "direct", Params = { ["topic"] = "topic_name" } };
        private FeedId roomFeedId = new FeedId() { Name = "room" };

        private TaskCompletionSource backendDisconnected = new TaskCompletionSource();
        private ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // Received audio

        public event EventHandler<byte[]> audioReceived;
        public event EventHandler<string> frameReceived;

        //JitterBuffer jitterBuffer;
        //private OpusDotNet.OpusDecoder decoder = new OpusDotNet.OpusDecoder(24000, 1);
        private AudioEngineRecv aeRecv = new AudioEngineRecv(102);
        private int sendTimestampSamples = 0;
        private ulong recvTimestamp = 0;
        // Send Audio

        //private OpusEncoder encoder = new OpusEncoder(24000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        private AudioEngineSend aeSend = new AudioEngineSend(102);
        private Queue<byte[]> _buffer;
        private int _maxBufferSize;
        private object _lockObj = new object();
        ulong nextAudioTimestamp = (ulong)DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        Thread _playbackThread;
        

        private static async Task ActivateSession(string endpointId, string secretToken, string roomId)
        {
            var client = new HttpClient();
            var body = new
            {
                endpointId,
                secretToken,
                profile = "videoconf",
                roomId,
                appEntityId = "*"
            };
            var content = JsonContent.Create(body);
            var result = await client.PostAsync(new Uri(WB_ORIGIN + "/samples/app_server/activate"), content).ConfigureAwait(true);
            Console.WriteLine($"Activation completed: {result.StatusCode}");

            content.Dispose();
            client.Dispose();
        }

        public async Task Connect(string roomId)
        {
            // audio send
            _maxBufferSize = 50000;
            _buffer = new Queue<byte[]>(50000);
            //Thread _playbackThread = new Thread(Playback);
            //_playbackThread.Start();

            // audio receive
            //jitterBuffer = new JitterBuffer(50000);
            //jitterBuffer.processPacket += (sender, eventArgs) =>
            //{
            //    var packet = eventArgs;
            //    var pcmBuffer = new byte[960];
            //    // 24000*2 number of bytes / sec and devide that with 50 for the 960^^
            //    //Buffer.BlockCopy(packet, 0, pcmBuffer, 0, packet.Length);
            //    var decodedSamples = decoder.Decode(packet, packet.Length, pcmBuffer, pcmBuffer.Length);
            //    OnAudioReceivedEvent(pcmBuffer);
            //};

            var backendConnected = new TaskCompletionSource();
            var counter = 0;

            channel = new FrontendChannel(Str0mBackendFactory.Create, (message, attachment) =>
            {
                switch (message.MessageCase)
                {
                    case ToFrontendMessage.MessageOneofCase.ConnectionMetadata:
                        {
                            var metadata = message.ConnectionMetadata;
                            _ = ActivateSession(metadata.EndpointId, metadata.SecretToken, roomId);
                            backendConnected.SetResult();
                            //jitterBuffer.Start();
                            break;
                        }
                    case ToFrontendMessage.MessageOneofCase.ConnectionStateChange:
                        {
                            var stateChanged = message.ConnectionStateChange;
                            switch (stateChanged.State)
                            {
                                case ConnectionState.Connected:
                                    Console.WriteLine("Connected to WaimeaBay.");
                                    break;
                                case ConnectionState.Disconnected:
                                    Console.WriteLine("Disconnected from WaimeaBay.");
                                    //jitterBuffer.Stop();
                                    backendDisconnected.SetResult();
                                    break;
                                default:
                                    break;
                            }

                            break;
                        }
                    case ToFrontendMessage.MessageOneofCase.AddInboundMedia:
                            Console.WriteLine($"Inbound media was added {message}");
                        break;
                    case ToFrontendMessage.MessageOneofCase.RemoveInboundMedia:
                        Console.WriteLine($"Inbound media was removed {message}");
                        break;
                    case ToFrontendMessage.MessageOneofCase.InboundMedia:
                        {
                            Console.WriteLine($"Inbound media received {message} and {attachment}");

                            if (attachment != null)
                            {
                                var attachmentInBytes = attachment.ToBytes();

                                if (attachmentInBytes.Length > 0)
                                {
                                    recvTimestamp += 10000 * 20;
                                    sendTimestampSamples += 320;
                                    aeRecv.PushFrame(attachmentInBytes, (ulong)sendTimestampSamples, recvTimestamp);
                                }
                                attachment.Release();
                            }

                            break;
                        }
                    default:
                        Console.WriteLine($"Received Default {message}");
                        break;
                }
            }, loggerFactory.CreateLogger<FrontendChannel>(), LogLevel.Debug);

            channel.Send(new()
            {
                AddFeedView = new()
                {
                    FeedViewId = AUDIO_FEED_VIEW_ID,
                    FeedId = roomFeedId,
                    AudioConfig = new()
                    {
                        MaxStreams = 1,
                        ReflectionMode = ReflectionMode.None,
                    },
                    DataConfigs = { new FeedViewDataConfig() { PayloadType = 13, MaxStreams = 1 } }
                }
            });

            channel.Send(new()
            {
                Connect = new()
                {
                    FrontdoorOrigin = WB_ORIGIN
                }
            });

            channel.Send(new()
            {
                AddOutboundMedia = new()
                {
                    SourceId = AUDIO_FEED_VIEW_ID,
                    AppEntityId = new AppEntityId("ACSCalling").ToByteString(),
                    MediaType = new() { Audio = new() },
                    AttachmentFormat = AttachmentFormat.Encoded,
                    FeedIds = { roomFeedId }
                },
            });

            Console.WriteLine("Waiting for Connect.");

            await backendConnected.Task.ConfigureAwait(false);
        }

        protected virtual void OnAudioReceivedEvent(byte[] decodedBuff)
        {
            audioReceived?.Invoke(this, decodedBuff);
        }

        public void SendAudio(byte[] pcmData)
        {
            lock (_lockObj)
            {
                if (_buffer.Count >= _maxBufferSize)
                {
                    Console.WriteLine("SendAudio Buffer full, discarding packet");
                    return;
                }

                byte[] encodedPayload, redundantEncodedPayload;
                int encodedPayloadLength, redundantEncodedPayloadLength, payloadType, processFrameResult;
                (encodedPayload, encodedPayloadLength, redundantEncodedPayload, redundantEncodedPayloadLength, payloadType, processFrameResult) = aeSend.ProcessFrame(pcmData);
                byte[] actualPayload = new byte[encodedPayloadLength];
                Array.Copy(encodedPayload, actualPayload, encodedPayloadLength);

                _buffer.Enqueue(actualPayload);
            }
        }

        private void Playback()
        {
            while (true)
            {
                byte[] output;
                int outputLength, pcmFrameCounter;
                AudioEngineRecv.PCMInfo info = new AudioEngineRecv.PCMInfo();

                lock (_lockObj)
                {
                    (output, outputLength, info, pcmFrameCounter) = aeRecv.PullPCM();
                }
                if (outputLength > 0 && output[0] != 0)
                {
                    ProcessPacket(output);
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private void ProcessPacket(byte[] packet)
        {
            OnAudioReceivedEvent(packet);
        }

        public async Task Disconnect()
        {
            Console.WriteLine("Disconnecting.");
            channel?.Dispose();

            await backendDisconnected.Task.ConfigureAwait(false);
            loggerFactory.Dispose();
            _playbackThread?.Join();

            Console.WriteLine("Shutdown complete");
        }
    }
}