using ProtoBuf;
using Vintagestory.API.MathTools;

namespace SpreadingDevastation.Models
{
    /// <summary>
    /// Tracks a block that has been devastated and will eventually regenerate.
    /// </summary>
    [ProtoContract]
    public class RegrowingBlocks
    {
        [ProtoMember(1)]
        public BlockPos Pos;

        [ProtoMember(2)]
        public string Out;

        [ProtoMember(3)]
        public double LastTime;
    }
}
