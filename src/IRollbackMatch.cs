using System;

namespace IdolShowdown
{
    public interface IRollbackMatch
    {
        public int FrameNumber { get; set; }
        public bool MatchEnded { get; }

        public byte[] ToBytes();
        public void FromBytes(byte[] bytes);

        public void GameUpdate(bool rollbackFrame);
    }
}
