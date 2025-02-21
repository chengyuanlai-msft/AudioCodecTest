using System.Text;

namespace AudioCodec
{
    public class WavReader
    {
        public string? RIFFChunkId { get; set; } // should read "RIFF"
        public int RIFFChunkSize { get; set; } // should be the file size minus 8 bytes
        public string? WavFormat { get; set; } // should read "WAVE"

        public string? FMTChunkId { get; set; } // should read "fmt"
        public int FMTChunkSize { get; set; } //size of the format chunk, typically 16, 18, or 40 bytes, should be the format chunk size excluding ChunkId and ChunkSize field
        public short AudioFormat { get; set; } // format code, 1 for PCM
        public short NumberOfChannels { get; set; } // e.g. 1 for mono, 2 for stereo
        public int SampleRate { get; set; } // Number of samples per second
        public int ByteRate { get; set; } // Number of bytes per second, calculated as SampleRate * NumChannels * BitsPerSample / 8
        public short BlockAlign { get; set; } //Number of bytes for one sample including all channels, calculated as NumChannels * BitsPerSample / 8
        public short BitsPerSample { get; set; } // Number of bits per sample, typically 8, 16, 24, or 32 bits

        public string? DataChunkId { get; set; } // should read "data"
        public int DataChunkSize { get; set; } // calculated as NumTotalSamples * NumChannels * BitsPerSample / 8
        public byte[]? AudioData { get; set; }

        public WavReader(string filePath)
        {
            using (var file = File.Open(filePath, FileMode.Open))
            {
                BinaryReader reader = new BinaryReader(file);

                // Read RIFF header
                RIFFChunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                RIFFChunkSize = reader.ReadInt32();
                WavFormat = Encoding.ASCII.GetString(reader.ReadBytes(4));

                // Read fmt chunk
                FMTChunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                FMTChunkSize = reader.ReadInt32();
                AudioFormat = reader.ReadInt16();
                NumberOfChannels = reader.ReadInt16();
                SampleRate = reader.ReadInt32();
                ByteRate = reader.ReadInt32();
                BlockAlign = reader.ReadInt16();
                BitsPerSample = reader.ReadInt16();

                // Read data chunk
                DataChunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                DataChunkSize = reader.ReadInt32();
                AudioData = reader.ReadBytes(DataChunkSize);

                // Output some information
                Console.WriteLine($"RIFF Chunk ID: {RIFFChunkId}");
                Console.WriteLine($"Format: {WavFormat}");
                Console.WriteLine($"Audio Format: {AudioFormat}");
                Console.WriteLine($"Number of Channels: {NumberOfChannels}");
                Console.WriteLine($"Sample Rate: {SampleRate}");
                Console.WriteLine($"Byte Rate: {ByteRate}");
                Console.WriteLine($"Block Align: {BlockAlign}");
                Console.WriteLine($"Bits Per Sample: {BitsPerSample}");
                Console.WriteLine($"Data Chunk ID: {DataChunkId}");
                Console.WriteLine($"Data Chunk Size: {DataChunkSize}");
            }
        }
    }
}
