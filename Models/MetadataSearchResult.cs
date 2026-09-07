namespace AgApp.Models;

public class MetadataSearchResult
{
    public int RawgId { get; set; }
    public string Title { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public int Released { get; set; }
    public string Genres { get; set; } = "";
    public double Rating { get; set; }
}
