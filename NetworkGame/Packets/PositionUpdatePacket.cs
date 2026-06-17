public class PositionUpdatePacket : Packet
{
    public float X { get; set; }
    public float Y { get; set; }

    public PositionUpdatePacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_UPDATE_POS) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_UPDATE_POS}");
        X = BitConverter.ToSingle(data[1..5]);
        Y = BitConverter.ToSingle(data[5..9]);
    }

    public override byte[] Serialize()
    {
        return [PacketId.PACKET_UPDATE_POS, ..BitConverter.GetBytes(X), ..BitConverter.GetBytes(Y)];
    }
}