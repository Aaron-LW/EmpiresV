public class StartGamePacket : Packet 
{
    public int Seed { get; set; }

    public StartGamePacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_START_GAME) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_START_GAME}");
        Seed = BitConverter.ToInt32(data[1..data.Length]);
    }

    public override byte[] Serialize()
    {
        return [PacketId.PACKET_START_GAME, ..BitConverter.GetBytes(Seed)];
    }
}