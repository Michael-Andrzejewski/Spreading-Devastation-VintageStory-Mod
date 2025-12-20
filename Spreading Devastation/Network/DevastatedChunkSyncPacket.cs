using System.Collections.Generic;
using ProtoBuf;

namespace SpreadingDevastation.Network
{
    /// <summary>
    /// Network packet sent from server to client containing devastated chunk positions.
    /// Used for client-side fog/sky effects when player is in devastated areas.
    /// </summary>
    [ProtoContract]
    public class DevastatedChunkSyncPacket
    {
        /// <summary>
        /// List of chunk X coordinates that are devastated near the player.
        /// </summary>
        [ProtoMember(1)]
        public List<int> ChunkXs = new List<int>();

        /// <summary>
        /// List of chunk Z coordinates that are devastated near the player.
        /// Indices correspond to ChunkXs.
        /// </summary>
        [ProtoMember(2)]
        public List<int> ChunkZs = new List<int>();
    }
}
