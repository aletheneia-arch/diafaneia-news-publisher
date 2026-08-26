from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "windows-app" / "NewsPublisher.Windows"
PLUGIN_PATH = ROOT / "wordpress-plugin" / "karago-diafaneia-publisher-connector" / "karago-diafaneia-publisher-connector.php"
OLD_BRIDGE_PATH = ROOT / "wordpress-plugin" / "news-publisher-bridge" / "news-publisher-bridge.php"

PLUGIN = PLUGIN_PATH.read_text(encoding="utf-8")
OLD_BRIDGE = OLD_BRIDGE_PATH.read_text(encoding="utf-8")
CLIENT = (APP / "WordPressClient.cs").read_text(encoding="utf-8")
PARSER = (APP / "PackageParser.cs").read_text(encoding="utf-8")
MAIN = (APP / "MainWindow.xaml.cs").read_text(encoding="utf-8")
MAIN_XAML = (APP / "MainWindow.xaml").read_text(encoding="utf-8")
SETTINGS = (APP / "SettingsWindow.xaml.cs").read_text(encoding="utf-8")
SETTINGS_XAML = (APP / "SettingsWindow.xaml").read_text(encoding="utf-8")
CREDENTIALS = (APP / "CredentialVault.cs").read_text(encoding="utf-8")
CACHE = (APP / "CategoryCache.cs").read_text(encoding="utf-8")
CATEGORIES = (APP / "CategoryCatalog.cs").read_text(encoding="utf-8")
AD_MODELS = (APP / "AdvertisementModels.cs").read_text(encoding="utf-8")
AD_STORE = (APP / "AdvertisementSettingsStore.cs").read_text(encoding="utf-8")
AD_COMPOSER = (APP / "AdvertisementComposer.cs").read_text(encoding="utf-8")
AD_WINDOW = (APP / "AdsWindow.xaml.cs").read_text(encoding="utf-8")
PROMPT = (ROOT / "PROMPT_CHATGPT_DIAFANEIA.txt").read_text(encoding="utf-8")
WORKFLOW = (ROOT / ".github" / "workflows" / "build-windows.yml").read_text(encoding="utf-8")
PROJECT = (APP / "NewsPublisher.Windows.csproj").read_text(encoding="utf-8")

checks = []


def check(name, condition):
    if not condition:
        raise AssertionError(name)
    checks.append(name)
    print("PASS", name)


def balanced_source(text):
    pairs = {')': '(', ']': '[', '}': '{'}
    stack = []
    i = 0
    quote = None
    line_comment = False
    block_comment = False
    while i < len(text):
        c = text[i]
        n = text[i + 1] if i + 1 < len(text) else ''
        if line_comment:
            if c == '\n':
                line_comment = False
            i += 1
            continue
        if block_comment:
            if c == '*' and n == '/':
                block_comment = False
                i += 2
            else:
                i += 1
            continue
        if quote:
            if c == '\\':
                i += 2
                continue
            if c == quote:
                quote = None
            i += 1
            continue
        if c == '/' and n == '/':
            line_comment = True
            i += 2
            continue
        if c == '/' and n == '*':
            block_comment = True
            i += 2
            continue
        if c in "'\"":
            quote = c
        elif c in "([{":
            stack.append(c)
        elif c in ")]}":
            if not stack or stack.pop() != pairs[c]:
                return False
        i += 1
    return not stack and quote is None and not block_comment


# Existing app baseline and UI.
check("Windows project targets .NET 8 WPF", "net8.0-windows" in PROJECT and "<UseWPF>true</UseWPF>" in PROJECT)
check("Windows version is 1.2.0", all(value in PROJECT for value in ["<Version>1.2.0</Version>", "<AssemblyVersion>1.2.0.0</AssemblyVersion>"]))
check("Main window version is 1.2.0", "News Publisher 1.2.0" in MAIN_XAML)
check("Diafaneia HTTPS endpoint fixed", '"https://diafaneia.eu"' in CLIENT and "http://diafaneia" not in CLIENT)
check("parser accepts only Diafaneia", 'payload.Site != "diafaneia"' in PARSER)
check("TEAM_TAG blocked by Windows parser", "TEAM_TAG δεν χρησιμοποιείται" in PARSER)
check("KARAGO package markers preserved", all(x in PARSER for x in ["[KARAGO_ARTICLE_V1]", "---HTML---", "---END---"]))
check("real categories use Connector terms", "karago-diafaneia/v1/terms" in CLIENT and "/wp-json/wp/v2/categories" not in CLIENT)
check("category fields parent and count consumed", all(x in CLIENT for x in ['"parent"', '"count"']))
check("category site isolation", "Απορρίφθηκαν κατηγορίες άλλου site" in CATEGORIES)
check("separate Diafaneia category cache", "NewsPublisher" in CACHE and 'site != "diafaneia"' in CACHE)
check("Smart Category Picker frequent categories", "Frequent(" in CATEGORIES and "FrequentPanel" in MAIN)
check("Smart Category Picker search", "Search(" in CATEGORIES and "CategorySearch" in MAIN)
check("Smart Category Picker multi-select", "SelectedIds" in CATEGORIES and "CheckBox" in MAIN)
check("zero category submission blocked", "SelectedIds.Count == 0" in MAIN)
check("optional featured image remains", "ChooseImage_Click" in MAIN and "ReadImageAsync" in CLIENT and 'featured_image' in CLIENT)
check("Draft and Publish remain", 'SubmitAsync("draft")' in MAIN and 'SubmitAsync("publish")' in MAIN)
check("Yoast request mapping remains", all(x in CLIENT for x in ['SeoPlugin = "yoast"', '"seo_title"', '"meta_description"', '"focus_keyphrase"']))

# New Windows Connector transport and credential security.
check("status endpoint configured", "karago-diafaneia/v1/status" in CLIENT)
check("publish endpoint configured", "karago-diafaneia/v1/publish" in CLIENT)
check("Connector API key header configured", 'HeaderName = "X-KARAGO-Key"' in CLIENT and "TryAddWithoutValidation(HeaderName, apiKey)" in CLIENT)
check("Basic authentication removed", "AuthenticationHeaderValue(\"Basic\"" not in CLIENT and "Authorization" not in CLIENT)
check("Application Password input removed", "DiafaneiaUser" not in SETTINGS_XAML and "DiafaneiaPass" not in SETTINGS_XAML and "WordPress username" not in SETTINGS_XAML)
check("Connector key UI present", "KARAGO Connector API Key" in SETTINGS_XAML)
check("connection test uses status", "TestConnectionAsync" in SETTINGS and "CONNECTED" in SETTINGS and "CONNECTION FAILED" in SETTINGS)
check("successful connection test saves key securely", "TestConnectionAsync" in SETTINGS and "CredentialVault.Save" in SETTINGS)
check("credentials loaded only from Windows vault", "CredentialVault.Load(site).ApiKey" in CLIENT)
check("Windows Credential Manager write", "CredWriteW" in CREDENTIALS)
check("Windows Credential Manager read", "CredReadW" in CREDENTIALS)
check("new independent credential target", "KARAGO.NewsPublisher.Connector" in CREDENTIALS)
check("credentials restricted to Diafaneia", 'site != "diafaneia"' in CREDENTIALS)
check("device key format validated locally", "^kdia_[a-f0-9]{16}_[A-Za-z0-9]{48}$" in CREDENTIALS and "^kdia_[a-f0-9]{16}_[A-Za-z0-9]{48}$" in CLIENT)
check("redirect following disabled", "AllowAutoRedirect = false" in CLIENT)
check("wrong-site response validation", "ValidateIdentity" in CLIENT and "NormalizeHost" in CLIENT)
check("safe GET retries only", "SafeGetAsync" in CLIENT and "client.PostAsync" in CLIENT and "PostAsync(PublishPath" in CLIENT)
check("POST has no automatic retry loop", CLIENT.count("PostAsync(PublishPath") == 1 and "for (var attempt" in CLIENT)
check("timeout preserves REQUEST_ID guidance", "TIMEOUT" in CLIENT and "ίδιο REQUEST_ID" in CLIENT)
check("auth failure disables submission despite cache", "connectionVerified = false" in MAIN and "var enabled = connectionVerified" in MAIN)
check("draft-only device disables Publish UI", "connectorCanPublish" in MAIN and "PublishButton.IsEnabled = enabled && connectorCanPublish" in MAIN)
check("401 handling present", "HttpStatusCode.Unauthorized" in CLIENT)
check("403 handling present", "HttpStatusCode.Forbidden" in CLIENT)
check("404 handling present", "HttpStatusCode.NotFound" in CLIENT)
check("409 handling present", "HttpStatusCode.Conflict" in CLIENT)
check("413 handling present", "StatusCode == 413" in CLIENT)
check("429 handling present", "HttpStatusCode.TooManyRequests" in CLIENT)
check("5xx handling present", ">= 500" in CLIENT)
check("API key redacted from errors", "[REDACTED]" in CLIENT and "Redact(" in CLIENT)
check("random ad order stable across retries", "ApplyForRequest" in AD_COMPOSER and "SHA256.HashData" in AD_COMPOSER and "ApplyForRequest" in MAIN)

# Existing advertisement functionality.
check("three supplied advertisements are defaults", all(value in AD_MODELS for value in ["MPAINAKTARIS", "verikoko-1-840x508", "KATO-PORTA-OLIMPOI"]))
check("advertisement list is not fixed to three", "List<AdvertisementItem>" in AD_MODELS and "Items.Add" in AD_WINDOW)
check("advertisements can be edited", "SaveItem_Click" in AD_WINDOW and ".Html = html" in AD_WINDOW)
check("advertisements can be deleted", "Delete_Click" in AD_WINDOW and "Items.RemoveAt" in AD_WINDOW)
check("advertisements can be reordered", "MoveSelected" in AD_WINDOW and "Items.Insert" in AD_WINDOW)
check("sequential and random modes available", "AdvertisementOrderMode.Sequential" in AD_WINDOW and "AdvertisementOrderMode.Random" in AD_WINDOW)
check("random mode uses every advertisement once", "Shuffle(advertisements" in AD_COMPOSER and "advertisements.Skip(1)" in AD_COMPOSER)
check("first advertisement precedes article", 'first + "\\n" + article' in AD_COMPOSER)
check("remaining advertisements follow article", 'article + "\\n\\n" + remaining' in AD_COMPOSER)
check("reapplication prevents duplicate advertisements", "article.Replace(advertisement" in AD_COMPOSER)
check("advertisement settings persist in LocalAppData", "LocalApplicationData" in AD_STORE and "advertisements.json" in AD_STORE)
check("advertisement settings save atomically", 'SettingsFile + ".tmp"' in AD_STORE and "File.Move(temporaryFile, SettingsFile, true)" in AD_STORE)

# Independent WordPress Connector identity, multi-device administration and auth.
check("new plugin name and version", "Plugin Name: KARAGO Diafaneia Publisher Connector" in PLUGIN and "Version: 1.0.0" in PLUGIN)
check("new namespace is independent", "karago-diafaneia/v1" in PLUGIN and "karago/v1" not in PLUGIN and "karago-news/v1" not in PLUGIN)
check("new database names are independent", "karago_diafaneia_devices" in PLUGIN and "karago_diafaneia_requests" in PLUGIN)
check("device key IDs are unique", "UNIQUE KEY key_id" in PLUGIN)
check("up to 100 active devices supported", "MAX_ACTIVE_KEYS = 100" in PLUGIN)
check("device labels stored", "device_label" in PLUGIN and "label varchar(100)" in PLUGIN)
check("device key keeps a valid WordPress author", "author_user_id" in PLUGIN and "post_author" in PLUGIN and "user_can($author, 'edit_posts')" in PLUGIN)
check("draft-only and publish scopes", all(x in PLUGIN for x in ["'draft'", "'publish'", "karago_publish_scope_required"]))
check("individual device revocation", "karago_diafaneia_revoke_device" in PLUGIN and "revoked_at IS NULL" in PLUGIN)
check("last-used device audit", "last_used_at" in PLUGIN)
check("admin capability required", PLUGIN.count("current_user_can('manage_options')") >= 3)
check("admin nonces required", "check_admin_referer('karago_diafaneia_create_device')" in PLUGIN and "check_admin_referer('karago_diafaneia_revoke_device_'" in PLUGIN)
check("full API key generated with strong randomness", "random_bytes(8)" in PLUGIN and "wp_generate_password(48, false, false)" in PLUGIN)
check("only API key hash stored", "wp_hash_password($raw_key)" in PLUGIN and "key_hash varchar(255)" in PLUGIN and "raw_key varchar" not in PLUGIN)
check("full key not persisted in transient", "NEW_KEY_TRANSIENT" not in PLUGIN and "set_transient(self::NEW_KEY" not in PLUGIN)
check("server verifies hashed key", "wp_check_password($provided" in PLUGIN)
check("revoked keys fail closed", "WHERE key_id = %s AND revoked_at IS NULL" in PLUGIN)
check("rate limiting implemented per key", "karago_rate_limited" in PLUGIN and "retry_after" in PLUGIN and "_p' : '_r'" in PLUGIN)

# REST contract and site isolation.
check("status route exists", "'/status'" in PLUGIN and "'ready' => (bool) $can_edit" in PLUGIN)
check("terms route exists", "'/terms'" in PLUGIN and "'pagination' => false" in PLUGIN)
check("publish route exists", "'/publish'" in PLUGIN and "WP_REST_Server::CREATABLE" in PLUGIN)
check("authentication header exact", "HEADER_NAME = 'X-KARAGO-Key'" in PLUGIN and "get_header('x-karago-key')" in PLUGIN)
check("status exposes device scope without key", all(x in PLUGIN for x in ["'device' => array(", "'label'", "'scope'", "'capabilities' => array("]))
check("terms use real WordPress taxonomies", "get_terms(array(" in PLUGIN and "'taxonomy' => 'category'" in PLUGIN and "'taxonomy' => 'post_tag'" in PLUGIN)
check("terms include category hierarchy and frequency", "'parent' => (int) $term->parent" in PLUGIN and "'count' => (int) $term->count" in PLUGIN)
check("wrong-site host lock", "EXPECTED_HOST = 'diafaneia.eu'" in PLUGIN and "karago_wrong_site" in PLUGIN)
check("publish requires SITE diafaneia", "karago_invalid_site" in PLUGIN and "SITE πρέπει να είναι diafaneia" in PLUGIN)
check("HTTPS configuration enforced", "karago_https_required" in PLUGIN and "is_https_configured" in PLUGIN)
check("TEAM_TAG rejected", "karago_team_tag_not_supported" in PLUGIN)
check("strict Draft or Publish only", "karago_invalid_status" in PLUGIN and "array('draft', 'publish')" in PLUGIN)

# Publish payload, images and Yoast.
check("strict category ID validation", "get_term($id, 'category')" in PLUGIN and "karago_unknown_category" in PLUGIN)
check("tag names resolved or created", "normalize_tag_names" in PLUGIN and "wp_insert_term($name, 'post_tag')" in PLUGIN)
check("featured image accepted", "get_param('featured_image')" in PLUGIN and "set_post_thumbnail" in PLUGIN)
check("inline images accepted", "get_param('inline_images')" in PLUGIN and "KARAGO_INLINE_IMAGE" in PLUGIN)
check("image count and size limits", "MAX_INLINE_IMAGES = 20" in PLUGIN and "MAX_IMAGE_BYTES = 20971520" in PLUGIN and "MAX_TOTAL_IMAGE_BYTES = 52428800" in PLUGIN)
check("base64 decoded strictly", "base64_decode($image['data_base64'], true)" in PLUGIN)
check("actual image type verified", "wp_check_filetype_and_ext" in PLUGIN and "wp_getimagesize" in PLUGIN and "actual_mime" in PLUGIN)
check("image types allowlisted", all(value in PLUGIN for value in ["image/jpeg", "image/png", "image/webp", "image/gif"]))
check("image alt text supported", "alt_text" in PLUGIN and "_wp_attachment_image_alt" in PLUGIN)
check("failed image/post cleanup", "delete_attachments" in PLUGIN and "wp_delete_post($post_id, true)" in PLUGIN)
check("Yoast SEO title mapping", "_yoast_wpseo_title" in PLUGIN)
check("Yoast meta description mapping", "_yoast_wpseo_metadesc" in PLUGIN)
check("Yoast focus keyphrase mapping", "_yoast_wpseo_focuskw" in PLUGIN)
check("non-Yoast SEO rejected", "karago_invalid_seo_plugin" in PLUGIN)
check("content uses explicit allowlist", "wp_kses_allowed_html('post')" in PLUGIN and "wp_kses(" in PLUGIN and "kses_remove_filters" not in PLUGIN)

# Atomic idempotency across every device.
check("REQUEST_ID table primary key", "PRIMARY KEY  (request_id)" in PLUGIN)
check("payload hash stored", "payload_hash char(64)" in PLUGIN and "hash('sha256'" in PLUGIN)
check("atomic request reservation", "INSERT IGNORE INTO" in PLUGIN and "state, post_id, key_id" in PLUGIN)
check("same ID different payload conflicts", "hash_equals" in PLUGIN and "karago_request_id_conflict" in PLUGIN)
check("simultaneous request returns 409", "karago_request_in_progress" in PLUGIN and "'retry_after' => 5" in PLUGIN)
check("completed request returns duplicate", "duplicate_post_id" in PLUGIN and "'duplicate' => (bool) $duplicate" in PLUGIN)
check("post carries REQUEST_ID recovery meta", "META_REQUEST_ID" in PLUGIN and "find_post_by_request_id" in PLUGIN)
check("failed identical request may safely retry", "state = 'processing'" in PLUGIN and "state = 'failed'" in PLUGIN)
check("stale processing recovery guarded", "PROCESSING_STALE_SECONDS = 600" in PLUGIN and "updated_at < %s" in PLUGIN)

# Coexistence and regression protection.
check("old bridge file preserved", "Plugin Name: News Publisher Bridge for Diafaneia" in OLD_BRIDGE and "Version: 1.0.0" in OLD_BRIDGE)
check("old bridge REQUEST_ID protection preserved", "find_existing_request" in OLD_BRIDGE and "META_REQUEST_ID" in OLD_BRIDGE)
check("old bridge Yoast mapping preserved", all(x in OLD_BRIDGE for x in ["_yoast_wpseo_title", "_yoast_wpseo_metadesc", "_yoast_wpseo_focuskw"]))
check("old bridge wrong-site protection preserved", "karago_wrong_site" in OLD_BRIDGE)
check("new and old REST namespaces coexist", "karago-diafaneia/v1" in PLUGIN and "karago-news/v1" in OLD_BRIDGE)
check("prompt uses Diafaneia site key", "SITE: diafaneia" in PROMPT)
check("prompt keeps TEAM_TAG empty", "TEAM_TAG:\n" in PROMPT)
check("prompt uses pipe separators", "χωρίζονται αποκλειστικά με: |" in PROMPT)
check("prompt requires WordPress HTML without H1", "χωρίς H1" in PROMPT and "---HTML---" in PROMPT)
check("prompt keeps agreed word range", "400–700" in PROMPT and "300 λέξεις" in PROMPT)
check("GitHub Actions builds Windows 1.2.0", "windows-latest" in WORKFLOW and "dotnet publish" in WORKFLOW and "News-Publisher-Diafaneia-Windows-1.2.0" in WORKFLOW)
check("GitHub Actions packages new connector", "KARAGO_DIAFANEIA_PUBLISHER_CONNECTOR_1.0.0.zip" in WORKFLOW)

# Parseable structure and secret scan.
ET.parse(APP / "MainWindow.xaml")
ET.parse(APP / "SettingsWindow.xaml")
check("WPF XAML is well formed", True)
check("PHP delimiters are balanced", balanced_source(PLUGIN))
check("changed C# delimiters are balanced", all(balanced_source(source) for source in [CLIENT, CREDENTIALS, SETTINGS, MAIN, AD_COMPOSER]))
combined = "\n".join([CLIENT, CREDENTIALS, PLUGIN, SETTINGS])
check("no real-looking connector key embedded", not re.search(r"kdia_[a-f0-9]{16}_[A-Za-z0-9]{48}", combined))
check("no obvious embedded passwords", not re.search(r"(?i)(password|api[_ -]?key|secret)\s*[:=]\s*[\"'](?!\s|\$|\{|\[)[^\"']{12,}[\"']", combined))
check("no credential logging", not re.search(r"(?i)(error_log|console\.write|debug\.write|trace\.write).*?(api.?key|provided|raw_key)", combined))

print(f"Static acceptance checks passed: {len(checks)}")
