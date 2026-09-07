namespace AgApp.Models;

public class Game
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string CoverUrl { get; set; } = "";
    public string PageUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Size { get; set; } = "";
    public string Developer { get; set; } = "";
    public List<DownloadLink> DownloadLinks { get; set; } = new();
    public List<string> Screenshots { get; set; } = new();
}

public class DownloadLink
{
    public string Text { get; set; } = "";
    public string Url { get; set; } = "";
}
