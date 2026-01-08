namespace api.Models;

public class ImageData
{
    public int Count { get; set; }
    public List<string> Urls { get; set; } = new();
    public int MainIndex { get; set; }
}
