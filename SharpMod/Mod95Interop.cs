using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMod {
    public partial class SoundFile {
        public uint Length => GetLength();

        public uint Position {
            get { return (mCurrentPattern * 64) + mRow; }
            set { SetCurrentPos(value); }
        }
    }
}
