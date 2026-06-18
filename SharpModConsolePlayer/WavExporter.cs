using System.Text;
using PrettyConsole;
using SharpMod;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer {
    internal static class WavExporter {
        private const int BufferLength = 6000;

        internal static int Export(SoundFile sf, string outPath, int sampleRate, int bitDepth, int channels) {
            int blockAlign = channels * (bitDepth / 8);
            uint totalPositions = sf.PositionCount;

            Console.WriteLineInterpolated($"{Yellow}Exporting{Default} to {Cyan}{outPath}{Default}");
            Console.WriteLineInterpolated($"  {DarkGray}{sampleRate} Hz, {bitDepth}-bit, {(channels == 2 ? "stereo" : "mono")}{Default}");

            using FileStream fs = File.Create(outPath);
            using BinaryWriter bw = new(fs);

            WriteHeader(bw, sampleRate, bitDepth, channels, dataSize: 0);

            byte[] buffer = new byte[BufferLength];
            long bytesWritten = 0;
            int zeroReads = 0;

            while(true) {
                uint read = sf.Read(buffer, (uint)buffer.Length);
                if(read == 0) {
                    if(++zeroReads >= 2) break;
                    continue;
                }
                zeroReads = 0;
                bw.Write(buffer, 0, (int)read);
                bytesWritten += read;
                if(sf.Position >= totalPositions) break;
            }

            bw.Flush();
            fs.Position = 4;
            bw.Write((uint)(bytesWritten + 36));
            fs.Position = 40;
            bw.Write((uint)bytesWritten);

            double seconds = bytesWritten / (double)(sampleRate * blockAlign);
            Console.WriteLineInterpolated($"  {Green}Wrote{Default} {White}{bytesWritten:N0}{Default} bytes ({White}{seconds:F2}{Default} s)");
            return 0;
        }

        private static void WriteHeader(BinaryWriter bw, int rate, int bits, int channels, uint dataSize) {
            int byteRate = rate * channels * (bits / 8);
            short blockAlign = (short)(channels * (bits / 8));
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(dataSize + 36u);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16u);
            bw.Write((short)1); // PCM
            bw.Write((short)channels);
            bw.Write((uint)rate);
            bw.Write((uint)byteRate);
            bw.Write(blockAlign);
            bw.Write((short)bits);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);
        }
    }
}
