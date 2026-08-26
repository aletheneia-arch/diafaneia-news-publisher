using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KaragoPublisher.Windows;

public sealed class WordPressClient
{
    private const string SiteKey = "diafaneia";
    private const string SiteUrl = "https://diafaneia.eu";
    private const string HeaderName = "X-KARAGO-Key";
    private const string StatusPath = "/wp-json/karago-diafaneia/v1/status";
    private const string TermsPath = "/wp-json/karago-diafaneia/v1/terms";
    private const string PublishPath = "/wp-json/karago-diafaneia/v1/publish";
    private const long MaximumImageBytes = 20L * 1024L * 1024L;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ConnectorStatus> TestConnectionAsync(string site, string? apiKeyOverride = null, CancellationToken token = default)
    {
        var session = CreateClient(site, apiKeyOverride);
        using var client = session.Client;
        var responseData = await SafeGetAsync(client, StatusPath, session.ApiKey, token);
        using var document = ParseJson(responseData.Text, session.ApiKey, "status");
        var root = document.RootElement;
        ValidateIdentity(root);

        var ready = root.TryGetProperty("ready", out var readyValue) && readyValue.ValueKind == JsonValueKind.True;
        if (!ready) throw new InvalidOperationException("CONNECTION FAILED: Ο Connector δεν δήλωσε ready=true.");
        var version = Text(root, "version");
        var siteName = Text(root, "site_name");
        var deviceLabel = root.TryGetProperty("device", out var device) ? Text(device, "label") : "";
        var scope = root.TryGetProperty("device", out device) ? Text(device, "scope") : "";
        var canPublish = root.TryGetProperty("capabilities", out var capabilities)
            && capabilities.TryGetProperty("publish", out var publish)
            && publish.ValueKind == JsonValueKind.True;
        return new ConnectorStatus(true, version, siteName, deviceLabel, scope, canPublish);
    }

    public async Task<IReadOnlyList<WordPressCategory>> GetCategoriesAsync(string site, CancellationToken token = default)
    {
        var session = CreateClient(site);
        using var client = session.Client;
        var responseData = await SafeGetAsync(client, TermsPath, session.ApiKey, token);
        using var document = ParseJson(responseData.Text, session.ApiKey, "terms");
        var root = document.RootElement;
        if (Text(root, "site") != SiteKey) throw new InvalidOperationException("CONNECTION FAILED: wrong-site response από τον Connector.");
        if (!root.TryGetProperty("categories", out var items) || items.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Ο Connector δεν επέστρεψε έγκυρη λίστα κατηγοριών.");

        var categories = new List<WordPressCategory>();
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idValue) || !idValue.TryGetInt32(out var id) || id <= 0) continue;
            var parent = item.TryGetProperty("parent", out var parentValue) && parentValue.TryGetInt32(out var parsedParent) ? parsedParent : 0;
            var count = item.TryGetProperty("count", out var countValue) && countValue.TryGetInt32(out var parsedCount) ? parsedCount : 0;
            categories.Add(new WordPressCategory(site, id, Text(item, "name"), Text(item, "slug"), parent, count));
        }
        return categories;
    }

    public async Task<PublishResult> PublishAsync(ArticlePayload payload, string status, string? featuredImagePath, CancellationToken token = default)
    {
        if (payload.Site != SiteKey) throw new InvalidOperationException("Άγνωστο ή λανθασμένο site.");
        if (status != "draft" && status != "publish") throw new InvalidOperationException("Το status πρέπει να είναι draft ή publish.");
        var categoryIds = new List<int>();
        foreach (var category in payload.Categories)
        {
            if (!int.TryParse(category, out var id) || id <= 0) throw new InvalidOperationException("Οι κατηγορίες πρέπει να είναι επαληθευμένα WordPress IDs.");
            categoryIds.Add(id);
        }
        if (categoryIds.Count == 0) throw new InvalidOperationException("Επίλεξε τουλάχιστον μία πραγματική κατηγορία.");

        var featuredImage = string.IsNullOrWhiteSpace(featuredImagePath)
            ? null
            : await ReadImageAsync(featuredImagePath, payload.Title, token);
        var outbound = new ConnectorPublishRequest
        {
            Site = SiteKey,
            RequestId = payload.RequestId,
            Status = status,
            Title = payload.Title,
            Content = payload.Content,
            Excerpt = payload.Excerpt,
            Slug = payload.Slug,
            Categories = categoryIds.Distinct().ToList(),
            TagNames = payload.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            FeaturedImage = featuredImage,
            InlineImages = [],
            SeoPlugin = "yoast",
            SeoTitle = payload.SeoTitle,
            MetaDescription = payload.MetaDescription,
            FocusKeyphrase = payload.FocusKeyword
        };

        var session = CreateClient(payload.Site);
        using var client = session.Client;
        using var body = new StringContent(JsonSerializer.Serialize(outbound, JsonOptions), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(PublishPath, body, token);
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            throw new InvalidOperationException("TIMEOUT: Το αποτέλεσμα δεν είναι βέβαιο. Πάτησε ξανά το ίδιο κουμπί χωρίς να αλλάξεις το πακέτο· το ίδιο REQUEST_ID αποτρέπει δεύτερο άρθρο.");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("CONNECTION FAILED: Δεν ήταν δυνατή η ασφαλής σύνδεση με τον Connector.");
        }
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(token);
            EnsureSuccess(response, text, session.ApiKey, "publish");
            using var document = ParseJson(text, session.ApiKey, "publish");
            var root = document.RootElement;
            if (Text(root, "site") != SiteKey) throw new InvalidOperationException("CONNECTION FAILED: wrong-site publish response.");
            if (!root.TryGetProperty("post_id", out var postValue) || !postValue.TryGetInt32(out var postId) || postId <= 0)
                throw new InvalidOperationException("Ο Connector δεν επέστρεψε έγκυρο WordPress post ID.");
            var link = Text(root, "url");
            if (link.Length == 0) link = Text(root, "link");
            return new PublishResult(
                postId,
                Text(root, "status"),
                Text(root, "edit_link"),
                link,
                root.TryGetProperty("duplicate", out var duplicate) && duplicate.ValueKind == JsonValueKind.True);
        }
    }

    private static (HttpClient Client, string ApiKey) CreateClient(string site, string? apiKeyOverride = null)
    {
        if (site != SiteKey) throw new InvalidOperationException("Άγνωστο site.");
        var baseUri = new Uri(SiteUrl, UriKind.Absolute);
        if (baseUri.Scheme != Uri.UriSchemeHttps || NormalizeHost(baseUri.Host) != "diafaneia.eu")
            throw new InvalidOperationException("Η σύνδεση επιτρέπεται μόνο στο https://diafaneia.eu.");

        var apiKey = string.IsNullOrWhiteSpace(apiKeyOverride) ? CredentialVault.Load(site).ApiKey.Trim() : apiKeyOverride.Trim();
        if (apiKey.Length == 0) throw new InvalidOperationException("Δεν έχει αποθηκευτεί KARAGO Connector API key για αυτό το PC.");
        if (!Regex.IsMatch(apiKey, "^kdia_[a-f0-9]{16}_[A-Za-z0-9]{48}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Το αποθηκευμένο KARAGO Connector API key δεν έχει έγκυρη μορφή.");

        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        var client = new HttpClient(handler)
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(120)
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderName, apiKey);
        return (client, apiKey);
    }

    private static async Task<(string Text, HttpStatusCode Status)> SafeGetAsync(HttpClient client, string path, string apiKey, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(path, token);
            }
            catch (TaskCanceledException) when (!token.IsCancellationRequested)
            {
                if (attempt == 0) { await Task.Delay(500, token); continue; }
                throw new InvalidOperationException("CONNECTION FAILED: timeout κατά την επικοινωνία με τον Connector.");
            }
            catch (HttpRequestException)
            {
                if (attempt == 0) { await Task.Delay(500, token); continue; }
                throw new InvalidOperationException("CONNECTION FAILED: Δεν ήταν δυνατή η ασφαλής σύνδεση με τον Connector.");
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(token);
                if (attempt == 0 && IsSafeGetRetry(response.StatusCode))
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(750);
                    if (delay > TimeSpan.FromSeconds(3)) delay = TimeSpan.FromSeconds(3);
                    await Task.Delay(delay, token);
                    continue;
                }
                EnsureSuccess(response, text, apiKey, "GET");
                return (text, response.StatusCode);
            }
        }
    }

    private static bool IsSafeGetRetry(HttpStatusCode status) => status == HttpStatusCode.TooManyRequests || (int)status is 500 or 502 or 503 or 504;

    private static void ValidateIdentity(JsonElement root)
    {
        if (Text(root, "site") != SiteKey) throw new InvalidOperationException("CONNECTION FAILED: Ο Connector απάντησε για λάθος site.");
        if (!Uri.TryCreate(Text(root, "site_url"), UriKind.Absolute, out var siteUri)
            || siteUri.Scheme != Uri.UriSchemeHttps
            || NormalizeHost(siteUri.Host) != "diafaneia.eu")
            throw new InvalidOperationException("CONNECTION FAILED: Η ταυτότητα του site δεν είναι https://diafaneia.eu.");
    }

    private static async Task<ConnectorImage> ReadImageAsync(string path, string alt, CancellationToken token)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new InvalidOperationException("Η επιλεγμένη featured image δεν βρέθηκε.");
        if (info.Length <= 0 || info.Length > MaximumImageBytes) throw new InvalidOperationException("Η featured image πρέπει να είναι έως 20 MB.");
        var mime = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => throw new InvalidOperationException("Υποστηρίζονται μόνο JPEG, PNG, WebP και GIF εικόνες.")
        };
        var bytes = await File.ReadAllBytesAsync(path, token);
        return new ConnectorImage
        {
            Name = Path.GetFileName(path),
            MimeType = mime,
            DataBase64 = Convert.ToBase64String(bytes),
            AltText = alt
        };
    }

    private static JsonDocument ParseJson(string text, string apiKey, string operation)
    {
        try { return JsonDocument.Parse(text); }
        catch (JsonException) { throw new InvalidOperationException($"Ο Connector επέστρεψε μη έγκυρη JSON απάντηση στο {operation}: {Redact(text, apiKey)}"); }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string text, string apiKey, string operation)
    {
        if ((int)response.StatusCode is >= 200 and <= 299) return;
        var serverMessage = "";
        var code = "";
        try
        {
            using var error = JsonDocument.Parse(text);
            serverMessage = Text(error.RootElement, "message");
            code = Text(error.RootElement, "code");
        }
        catch (JsonException) { }
        serverMessage = Redact(serverMessage, apiKey);
        var detail = serverMessage.Length > 0 ? $" — {serverMessage}" : "";
        var suffix = code.Length > 0 ? $" ({code})" : "";
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "CONNECTION FAILED: Το API key λείπει, είναι λάθος ή έχει ανακληθεί.",
            HttpStatusCode.Forbidden => "CONNECTION FAILED: Το κλειδί δεν έχει το απαιτούμενο δικαίωμα ή απορρίφθηκε από τον έλεγχο ασφαλείας.",
            HttpStatusCode.NotFound => "CONNECTION FAILED: Δεν βρέθηκε ο KARAGO Diafaneia Connector ή το endpoint.",
            HttpStatusCode.Conflict => "Ασφαλής διακοπή: σύγκρουση REQUEST_ID, λάθος site ή ίδιο αίτημα ήδη σε εξέλιξη.",
            HttpStatusCode.TooManyRequests => "Πάρα πολλά αιτήματα. Περίμενε λίγο και ξαναδοκίμασε.",
            _ when (int)response.StatusCode == 413 => "Το άρθρο ή η εικόνα υπερβαίνει το επιτρεπόμενο μέγεθος.",
            _ when (int)response.StatusCode >= 500 => "Προσωρινό σφάλμα του Connector. Για δημοσίευση ξαναχρησιμοποίησε ακριβώς το ίδιο REQUEST_ID.",
            _ => $"Αποτυχία {operation}: HTTP {(int)response.StatusCode}."
        };
        throw new InvalidOperationException(message + suffix + detail);
    }

    private static string Redact(string? text, string apiKey)
    {
        var safe = (text ?? "").Replace(apiKey, "[REDACTED]", StringComparison.Ordinal);
        safe = Regex.Replace(safe, "kdia_[a-f0-9]{16}_[A-Za-z0-9]{48}", "[REDACTED]", RegexOptions.CultureInvariant);
        return safe.Length <= 1500 ? safe : safe[..1500] + "…";
    }

    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string NormalizeHost(string host) => host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..].ToLowerInvariant() : host.ToLowerInvariant();

    private sealed class ConnectorPublishRequest
    {
        [JsonPropertyName("site")] public string Site { get; set; } = "";
        [JsonPropertyName("request_id")] public string RequestId { get; set; } = "";
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
        [JsonPropertyName("excerpt")] public string Excerpt { get; set; } = "";
        [JsonPropertyName("slug")] public string Slug { get; set; } = "";
        [JsonPropertyName("categories")] public List<int> Categories { get; set; } = [];
        [JsonPropertyName("tag_names")] public List<string> TagNames { get; set; } = [];
        [JsonPropertyName("featured_image")] public ConnectorImage? FeaturedImage { get; set; }
        [JsonPropertyName("inline_images")] public List<object> InlineImages { get; set; } = [];
        [JsonPropertyName("seo_plugin")] public string SeoPlugin { get; set; } = "yoast";
        [JsonPropertyName("seo_title")] public string SeoTitle { get; set; } = "";
        [JsonPropertyName("meta_description")] public string MetaDescription { get; set; } = "";
        [JsonPropertyName("focus_keyphrase")] public string FocusKeyphrase { get; set; } = "";
    }
}
