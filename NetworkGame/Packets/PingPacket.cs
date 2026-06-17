using System.Text;

public class PingPacket : Packet
{
    public string Message { get; set; } = "";

    public PingPacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_PING) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_PING}");
        Message = Encoding.UTF8.GetString(data[1..(data.Length - 1)]);
    }

    public override byte[] Serialize()
    {
        return [PacketId.PACKET_PING, ..Encoding.UTF8.GetBytes(Message)];
    }
}