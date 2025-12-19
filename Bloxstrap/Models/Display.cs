public class DisplayMode
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public string DisplayName => $"{Width}x{Height} @ {RefreshRate}Hz";

    public override string ToString() => DisplayName;
}
