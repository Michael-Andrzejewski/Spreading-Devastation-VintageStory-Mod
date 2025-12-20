using ProtoBuf;

namespace SpreadingDevastation.Network
{
    /// <summary>
    /// Network packet sent from server to client containing fog configuration.
    /// </summary>
    [ProtoContract]
    public class FogConfigPacket
    {
        [ProtoMember(1)]
        public bool Enabled = true;

        [ProtoMember(2)]
        public float ColorR = 0.55f;

        [ProtoMember(3)]
        public float ColorG = 0.25f;

        [ProtoMember(4)]
        public float ColorB = 0.15f;

        [ProtoMember(5)]
        public float Density = 0.004f;

        [ProtoMember(6)]
        public float Min = 0.15f;

        [ProtoMember(7)]
        public float ColorWeight = 0.7f;

        [ProtoMember(8)]
        public float DensityWeight = 0.5f;

        [ProtoMember(9)]
        public float MinWeight = 0.6f;

        [ProtoMember(10)]
        public float TransitionSpeed = 0.5f;
    }
}
