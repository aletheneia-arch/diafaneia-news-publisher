using KaragoPublisher.Windows;

var passed = 0;
var failed = 0;

void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}

void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"expected '{expected}', got '{actual}'");
}

void True(bool value) { if (!value) throw new Exception("condition was false"); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("exception was not thrown"); }
int Occurrences(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;

string Article(string site = "diafaneia", string teamTag = "") => $"""
[KARAGO_ARTICLE_V1]
SITE: {site}
REQUEST_ID: req-123
TITLE: Δοκιμαστικό άρθρο
SLUG: dokimastiko-arthro
CATEGORY: Νέα|Πολιτική
TAGS: δοκιμή|είδηση
TEAM_TAG: {teamTag}
EXCERPT: Σύντομη περίληψη
FOCUS_KEYWORD: δοκιμή
SECONDARY_KEYWORDS: είδηση|άρθρο
SEO_TITLE: Δοκιμαστικό άρθρο SEO
META_DESCRIPTION: Περιγραφή για τη δοκιμή
---HTML---
<p>Περιεχόμενο άρθρου</p>
---END---
[/KARAGO_ARTICLE_V1]
""";

Test("parser accepts Diafaneia", () => Equal("diafaneia", PackageParser.Parse(Article()).Site));
Test("parser preserves REQUEST_ID", () => Equal("req-123", PackageParser.Parse(Article()).RequestId));
Test("parser extracts HTML", () => True(PackageParser.Parse(Article()).Content.Contains("Περιεχόμενο")));
Test("parser extracts multiple categories", () => Equal(2, PackageParser.Parse(Article()).Categories.Count));
Test("parser extracts multiple tags", () => Equal(2, PackageParser.Parse(Article()).Tags.Count));
Test("parser blocks TEAM_TAG", () => Throws(() => PackageParser.Parse(Article("diafaneia", "ΠΑΟΚ"))));
Test("parser blocks unknown site", () => Throws(() => PackageParser.Parse(Article("wrong"))));
Test("parser blocks missing markers", () => Throws(() => PackageParser.Parse("TITLE: test")));
Test("parser blocks missing required field", () => Throws(() => PackageParser.Parse(Article().Replace("TITLE: Δοκιμαστικό άρθρο", "TITLE:"))));

var categories = new[] {
    new WordPressCategory("diafaneia", 1, "Νέα", "nea", 0, 100),
    new WordPressCategory("diafaneia", 2, "Πολιτική", "politiki", 1, 80),
    new WordPressCategory("diafaneia", 3, "Οικονομία", "oikonomia", 0, 60),
    new WordPressCategory("diafaneia", 4, "Κόσμος", "kosmos", 0, 40),
    new WordPressCategory("diafaneia", 5, "Πολιτισμός", "politismos", 0, 20),
    new WordPressCategory("diafaneia", 6, "Υγεία", "ygeia", 0, 10),
    new WordPressCategory("diafaneia", 7, "Τεχνολογία", "technologia", 0, 5)
};

Test("category catalog accepts matching site", () => Equal(7, new CategoryCatalog("diafaneia", categories).All.Count));
Test("category catalog rejects wrong-site data", () => Throws(() => new CategoryCatalog("diafaneia", categories.Append(new("sportaki", 8, "Μπάλα", "mpala", 0, 1)))));
Test("frequent categories limited to six", () => Equal(6, new CategoryCatalog("diafaneia", categories).Frequent().Count));
Test("frequent categories ordered by use", () => Equal(1, new CategoryCatalog("diafaneia", categories).Frequent()[0].Id));
Test("suggestion matches Greek name", () => Equal(2, new CategoryCatalog("diafaneia", categories).Match(["Πολιτική"])[0].Id));
Test("suggestion matches slug", () => Equal(3, new CategoryCatalog("diafaneia", categories).Match(["oikonomia"])[0].Id));
Test("suggestion matches category id", () => Equal(4, new CategoryCatalog("diafaneia", categories).Match(["4"])[0].Id));
Test("search is accent-insensitive", () => Equal(3, new CategoryCatalog("diafaneia", categories).Search("οικονομια").Last().Id));
Test("search includes category parent", () => True(new CategoryCatalog("diafaneia", categories).Search("Πολιτική").Any(c => c.Id == 1)));
Test("hierarchy display identifies child", () => True(new CategoryCatalog("diafaneia", categories).DisplayName(categories[1]).StartsWith("— ")));
Test("multi-select keeps two categories", () => { var c = new CategoryCatalog("diafaneia", categories); c.Select(1, true); c.Select(3, true); Equal(2, c.SelectedIds.Count); });
Test("deselect removes category", () => { var c = new CategoryCatalog("diafaneia", categories); c.Select(1, true); c.Select(1, false); Equal(0, c.SelectedIds.Count); });
Test("preselect uses package suggestions", () => { var c = new CategoryCatalog("diafaneia", categories); c.Preselect(["Νέα", "politiki"]); Equal(2, c.SelectedIds.Count); });
Test("unknown selection is ignored", () => { var c = new CategoryCatalog("diafaneia", categories); c.Select(999, true); Equal(0, c.SelectedIds.Count); });
Test("compact view retains selected category", () => { var c = new CategoryCatalog("diafaneia", categories); c.Select(7, true); True(c.Compact([]).Any(x => x.Id == 7)); });

var defaultAds = AdvertisementSettings.CreateDefaults();
var articleHtml = "<p>Πρώτη παράγραφος</p>\n<p>Δεύτερη παράγραφος</p>";
Test("three default advertisements configured", () => Equal(3, defaultAds.Items.Count));
Test("first default advertisement is Mpainaktaris", () => True(defaultAds.Items[0].Html.Contains("MPAINAKTARIS")));
Test("second default advertisement is Verikoko", () => True(defaultAds.Items[1].Html.Contains("verikoko")));
Test("third default advertisement is Kato Porta", () => True(defaultAds.Items[2].Html.Contains("KATO-PORTA-OLIMPOI")));
Test("sequential first advertisement precedes article", () => True(AdvertisementComposer.Apply(articleHtml, defaultAds).StartsWith(defaultAds.Items[0].Html + "\n")));
Test("sequential remaining advertisements follow article", () => { var result = AdvertisementComposer.Apply(articleHtml, defaultAds); True(result.IndexOf(articleHtml, StringComparison.Ordinal) < result.IndexOf(defaultAds.Items[1].Html, StringComparison.Ordinal)); });
Test("all advertisements appear exactly once", () => { var result = AdvertisementComposer.Apply(articleHtml, defaultAds); foreach (var ad in defaultAds.Items) Equal(1, Occurrences(result, ad.Html)); });
Test("fixed reorder controls output order", () => { var settings = AdvertisementSettings.CreateDefaults(); settings.Items.Reverse(); var result = AdvertisementComposer.Apply(articleHtml, settings); True(result.StartsWith(settings.Items[0].Html)); });
Test("random mode keeps every advertisement once", () => { var settings = AdvertisementSettings.CreateDefaults(); settings.Mode = AdvertisementOrderMode.Random; var result = AdvertisementComposer.Apply(articleHtml, settings, new Random(42)); foreach (var ad in settings.Items) Equal(1, Occurrences(result, ad.Html)); });
Test("random mode preserves article exactly once", () => { var settings = AdvertisementSettings.CreateDefaults(); settings.Mode = AdvertisementOrderMode.Random; Equal(1, Occurrences(AdvertisementComposer.Apply(articleHtml, settings, new Random(7)), articleHtml)); });
Test("random mode differs from fixed order", () => { var settings = AdvertisementSettings.CreateDefaults(); var fixedResult = AdvertisementComposer.Apply(articleHtml, settings); settings.Mode = AdvertisementOrderMode.Random; var randomResult = AdvertisementComposer.Apply(articleHtml, settings, new Random(0)); True(randomResult != fixedResult); });
Test("random mode is stable for REQUEST_ID retries", () => { var settings = AdvertisementSettings.CreateDefaults(); settings.Mode = AdvertisementOrderMode.Random; Equal(AdvertisementComposer.ApplyForRequest(articleHtml, settings, "req-123"), AdvertisementComposer.ApplyForRequest(articleHtml, settings, "req-123")); });
Test("pre-existing advertisement is not duplicated", () => { var withAd = defaultAds.Items[0].Html + "\n" + articleHtml; var result = AdvertisementComposer.Apply(withAd, defaultAds); Equal(1, Occurrences(result, defaultAds.Items[0].Html)); });
Test("unlimited advertisement list is supported", () => { var settings = new AdvertisementSettings(); for (var i = 0; i < 25; i++) settings.Items.Add(new AdvertisementItem { Name = $"Ad {i}", Html = $"<img src=\"ad-{i}.jpg\" />" }); var result = AdvertisementComposer.Apply(articleHtml, settings); Equal(25, settings.Items.Count(ad => result.Contains(ad.Html))); });
Test("empty advertisement list leaves article unchanged", () => Equal(articleHtml, AdvertisementComposer.Apply(articleHtml, new AdvertisementSettings())));

Console.WriteLine($"TOTAL {passed + failed}: {passed} PASS, {failed} FAIL");
return failed == 0 ? 0 : 1;
