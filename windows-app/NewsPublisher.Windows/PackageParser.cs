namespace KaragoPublisher.Windows;

public static class PackageParser
{
    public static ArticlePayload Parse(string? input)
    {
        var text = (input ?? "").Replace("\r\n", "\n");
        const string startMarker = "[KARAGO_ARTICLE_V1]", htmlMarker = "---HTML---", endMarker = "---END---", closeMarker = "[/KARAGO_ARTICLE_V1]";
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        var htmlStart = start < 0 ? -1 : text.IndexOf(htmlMarker, start + startMarker.Length, StringComparison.Ordinal);
        var end = htmlStart < 0 ? -1 : text.IndexOf(endMarker, htmlStart + htmlMarker.Length, StringComparison.Ordinal);
        var close = end < 0 ? -1 : text.IndexOf(closeMarker, end + endMarker.Length, StringComparison.Ordinal);
        if (start < 0 || htmlStart < 0 || end < htmlStart || close < end) throw new InvalidOperationException("Λείπουν οι δείκτες KARAGO_ARTICLE_V1 / HTML.");

        var values = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text[(start + startMarker.Length)..htmlStart].Trim().Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon > 0) values[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        string Get(string key) => values.TryGetValue(key, out var value) ? value.Trim() : "";
        List<string> List(string key) => Get(key).Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        var html = text[(htmlStart + htmlMarker.Length)..end];
        if (html.StartsWith('\n')) html = html[1..];
        if (html.EndsWith('\n')) html = html[..^1];

        var payload = new ArticlePayload {
            Site = Get("SITE").ToLowerInvariant(), RequestId = string.IsNullOrWhiteSpace(Get("REQUEST_ID")) ? Guid.NewGuid().ToString() : Get("REQUEST_ID"),
            Title = Get("TITLE"), Slug = Get("SLUG"), Excerpt = Get("EXCERPT"), Categories = List("CATEGORY"), Tags = List("TAGS"),
            TeamTag = Get("TEAM_TAG"), FocusKeyword = Get("FOCUS_KEYWORD"), SecondaryKeywords = List("SECONDARY_KEYWORDS"),
            SeoTitle = Get("SEO_TITLE"), MetaDescription = Get("META_DESCRIPTION"), Content = html
        };
        if (payload.Site != "diafaneia") throw new InvalidOperationException("SITE πρέπει να είναι diafaneia.");
        var required = new Dictionary<string,bool> {
            ["TITLE"] = payload.Title.Length > 0, ["SLUG"] = payload.Slug.Length > 0, ["CATEGORY"] = payload.Categories.Count > 0,
            ["TAGS"] = payload.Tags.Count > 0, ["EXCERPT"] = payload.Excerpt.Length > 0, ["FOCUS_KEYWORD"] = payload.FocusKeyword.Length > 0,
            ["SEO_TITLE"] = payload.SeoTitle.Length > 0, ["META_DESCRIPTION"] = payload.MetaDescription.Length > 0, ["HTML"] = payload.Content.Length > 0
        };
        var missing = required.FirstOrDefault(x => !x.Value).Key;
        if (missing != null) throw new InvalidOperationException($"Το {missing} είναι κενό.");
        if (payload.TeamTag.Length > 0) throw new InvalidOperationException("Το TEAM_TAG δεν χρησιμοποιείται στη Διαφάνεια και πρέπει να είναι κενό.");
        return payload;
    }
}
