using ProtoBuf;
using Vintagestory.API.MathTools;

namespace SpreadingDevastation.Models
{
    /// <summary>
    /// Represents a block that has "bled" outside its source chunk into a non-devastated chunk.
    /// These blocks have a limited infection budget and can only spread a few more times.
    /// </summary>
    [ProtoContract]
    public class BleedBlock
    {
        [ProtoMember(1)]
        public BlockPos Pos;

        [ProtoMember(2)]
        public int RemainingSpread; // How many more blocks this can infect (decrements with each spread)
    }
}
