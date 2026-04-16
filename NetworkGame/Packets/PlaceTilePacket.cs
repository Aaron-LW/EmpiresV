public class PlaceTilePacket : Packet
{
    public required string TextureName { get; set; }
    public required float X { get; set; }
    public required float Y { get; set; }
}