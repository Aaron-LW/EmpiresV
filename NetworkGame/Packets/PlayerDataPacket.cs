using System.Text;

public class PlayerDataPacket : Packet
{
    public PlayerData? PlayerData { get; set; }
    public byte Id { get; set; }

    public PlayerDataPacket(byte[]? data)
    {
        if (data == null) return;
        if (data[0] != PacketId.PACKET_PLAYERDATA) throw new InvalidDataException($"Invalid Packet Id; Got {data[0]} expected {PacketId.PACKET_PLAYERDATA}");
        bool host = data[1] == 0x01;
        Id = data[^1];
        string username = Encoding.UTF8.GetString(data[2..(data.Length - 2)]);

        PlayerData = new()
        {
            Username = username,
            Host = host,
        };
    }

    public override byte[] Serialize()
    {
        return [PacketId.PACKET_PLAYERDATA, PlayerData!.Host ? (byte)0x01 : (byte)0x00, ..Encoding.UTF8.GetBytes(PlayerData.Username), App.PlayerId];
    }
}