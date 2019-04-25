using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMod {
    public partial class SoundFile {
        public string Title { get => Instruments[0].Name; }
        public uint Type { get; }
        public uint Rate { get; }
        public uint ActiveChannels { get; }
        public uint ActiveSamples { get; }
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
        public uint Length => GetLength();

        public uint Position {
            get { return (CurrentPattern * 64) + Row; }
            set { SetCurrentPos(value); }
        }
    }
}
