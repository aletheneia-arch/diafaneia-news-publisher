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
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("mime_type")] public string MimeType { get; set; } = "";
    [JsonPropertyName("data_base64")] public string DataBase64 { get; set; } = "";
    [JsonPropertyName("alt_text")] public string AltText { get; set; } = "";
}
