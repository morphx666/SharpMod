using NAudio.Wave;

namespace SharpModPlayer {
    public class CustomBufferProvider : IWaveProvider {
        private readonly FillBuffer fb;
        private readonly WaveFormat mWaveFormat;

        public delegate int FillBuffer(byte[] buffer);

        WaveFormat IWaveProvider.WaveFormat => mWaveFormat;

        public CustomBufferProvider(FillBuffer bufferFiller, int sampleRate, int bitDepth, int channels) {
            mWaveFormat = new WaveFormat(sampleRate, bitDepth, channels);
            fb = bufferFiller;
        }

        public int Read(byte[] buffer, int offset, int count) {
            int n = fb.Invoke(buffer);
            return n;
        }
    }
}
