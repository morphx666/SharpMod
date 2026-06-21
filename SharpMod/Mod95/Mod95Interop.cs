namespace SharpMod {
    public partial class SoundFile {
        public string Title { get => title; }
        public string TrackerName { get => trackerName; }
        public uint RestartPos { get; private set; }
        public Types Type { get; private set; }
        public uint Rate { get; }
        public uint ActiveChannels { get; private set; }
        public uint ActiveSamples { get; private set; }
        public uint MusicSpeed { get; private set; }
        public uint MusicTempo { get; private set; }
        public uint SpeedCount { get; private set; }
        public uint BufferCount { get; private set; }
        public uint Pattern { get; private set; }
        public uint CurrentPattern { get; private set; }
        public uint NextPattern { get; private set; }
        public uint Row { get; private set; }
        public bool Is16Bit { get; }
        public bool IsStereo { get; }
        public bool Loop { get; }
        public uint Length { get; private set; }
        public uint AverageTempo { get; private set; }
        public bool IsValid { get; private set; }
        public ModInstrument[] Instruments => instruments;
        public ModChannel[] Channels => channels;
        public byte[] Order => order;
        public byte[][] Patterns => patterns;

        public uint Position {
            get { return (CurrentPattern * 64) + Row; }
            set { SetCurrentPos(value); }
        }

        public uint PositionCount => GetTotalPos();

        public void ToggleMute(uint channelIndex) {
            if(channelIndex >= ActiveChannels) return;
            channels[channelIndex].Muted = !channels[channelIndex].Muted;
        }
    }
}