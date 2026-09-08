using System.Text.Json.Serialization;

namespace KaragoPublisher.Windows;

public sealed class ArticlePayload
{
    [JsonPropertyName("site")] public string Site { get; set; } = "";
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = Guid.NewGuid().ToString();
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("slug")] public string Slug { get; set; } = "";
    [JsonPropertyName("excerpt")] public string Excerpt { get; set; } = "";
    [JsonPropertyName("categories")] public List<string> Categories { get; set; } = [];
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
    [JsonPropertyName("secondary_keywords")] public List<string> SecondaryKeywords { get; set; } = [];
    [JsonPropertyName("team_tag")] public string TeamTag { get; set; } = "";
    [JsonPropertyName("seo_title")] public string SeoTitle { get; set; } = "";
    [JsonPropertyName("meta_description")] public string MetaDescription { get; set; } = "";
    [JsonPropertyName("focus_keyword")] public string FocusKeyword { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "draft";

    public ArticlePayload Copy() => new()
    {
        Site = Site,
        RequestId = RequestId,
        Title = Title,
        Slug = Slug,
        Excerpt = Excerpt,
        Categories = Categories.ToList(),
        Tags = Tags.ToList(),
        SecondaryKeywords = SecondaryKeywords.ToList(),
        TeamTag = TeamTag,
        SeoTitle = SeoTitle,
        MetaDescription = MetaDescription,
        FocusKeyword = FocusKeyword,
        Content = Content,
        Status = Status
    };
}

public sealed record WordPressCategory(string Site, int Id, string Name, string Slug, int Parent, int Count);
public sealed record ConnectorCredentials(string ApiKey);
public sealed record ConnectorStatus(bool Ready, string Version, string SiteName, string DeviceLabel, string Scope, bool CanPublish);
public sealed record PublishResult(int PostId, string Status, string EditLink, string Link, bool Duplicate);

public sealed class ConnectorImage
{
    private string name = "";
    private string mimeType = "";
    private string dataBase64 = "";

    [JsonPropertyName("name")] public string Name { get => name; set => name = value ?? ""; }
    [JsonPropertyName("mime_type")] public string MimeType { get => mimeType; set => mimeType = value ?? ""; }
    [JsonPropertyName("data_base64")]
    public string DataBase64
    {
        get => dataBase64;
        set
        {
            var raw = value ?? "";
            byte[] bytes;
            try { bytes = Convert.FromBase64String(raw); }
            catch (FormatException) { throw new InvalidOperationException("Η featured image είναι κατεστραμμένη."); }

            var actualMime = DetectActualMime(bytes)
                ?? throw new InvalidOperationException("Η featured image δεν είναι έγκυρο JPEG, PNG, WebP ή GIF.");

            mimeType = actualMime;
            name = NormalizeName(name, actualMime);
            dataBase64 = raw;
        }
    }
    [JsonPropertyName("alt_text")] public string AltText { get; set; } = "";

    internal static string? DetectActualMime(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff) return "image/jpeg";
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4e && data[3] == 0x47
            && data[4] == 0x0d && data[5] == 0x0a && data[6] == 0x1a && data[7] == 0x0a) return "image/png";
        if (data.Length >= 6 && data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F'
            && data[3] == (byte)'8' && (data[4] == (byte)'7' || data[4] == (byte)'9') && data[5] == (byte)'a') return "image/gif";
        if (data.Length >= 12 && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F'
            && data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P') return "image/webp";
        return null;
    }

    internal static string NormalizeName(string? originalName, string mime)
    {
        var clean = string.IsNullOrWhiteSpace(originalName) ? "featured-image" : Path.GetFileName(originalName.Trim());
        var baseName = Path.GetFileNameWithoutExtension(clean);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "featured-image";
        var extension = mime switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };
        return baseName + extension;
    }
}
