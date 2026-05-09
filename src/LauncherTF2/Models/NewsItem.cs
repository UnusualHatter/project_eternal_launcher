namespace LauncherTF2.Models;

public class NewsItem
{
    public string Title { get; set; } = "";
    public string Contents { get; set; } = "";
    public DateTime Date { get; set; }
    public string Url { get; set; } = "";
    public string FeedLabel { get; set; } = "";

    /// <summary>
    /// First image URL extracted from the article HTML, or a TF2 placeholder
    /// when the article didn't include any. Always non-null so the UI never
    /// has to deal with broken layout.
    /// </summary>
    public string ImageUrl { get; set; } = "";

    public string DateFormatted => Date.ToString("MMM dd, yyyy");
}
