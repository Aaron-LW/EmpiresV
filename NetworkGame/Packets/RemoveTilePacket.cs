public class RemoveTilePacket : Packet
{
    public float X;
    public float Y;

    public RemoveTilePacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_REMOVE_TILE) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_REMOVE_TILE}");
        X = BitConverter.ToSingle(data[1..5]);
        Y = BitConverter.ToSingle(data[5..9]);
    }
    
    public override byte[] Serialize()
    {
        return [PacketId.PACKET_REMOVE_TILE, ..BitConverter.GetBytes(X), ..BitConverter.GetBytes(Y)];
    }
}