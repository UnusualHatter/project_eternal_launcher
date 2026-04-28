namespace LauncherTF2.Models;

public class NewsItem
{
    public string Title { get; set; } = "";
    public string Contents { get; set; } = "";
    public DateTime Date { get; set; }
    public string Url { get; set; } = "";
    public string FeedLabel { get; set; } = "";

    public string DateFormatted => Date.ToString("MMM dd, yyyy");
}
