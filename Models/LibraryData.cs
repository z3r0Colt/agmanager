namespace AgApp.Models;

public class LibraryData
{
    public int SchemaVersion { get; set; } = 0;
    public List<InstalledGame> Games { get; set; } = new();
}
