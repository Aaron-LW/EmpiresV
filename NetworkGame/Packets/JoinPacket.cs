using System.Text;

public class JoinPacket : Packet
{
    public string? Username;

    public JoinPacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_JOIN) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_JOIN}");
        Username = Encoding.UTF8.GetString(data[1..(data.Length - 1)]);
    }

    public override byte[] Serialize()
    {
        return [PacketId.PACKET_JOIN, ..Encoding.UTF8.GetBytes(Username!)];
    }
}