using System.Text.Json;
using System.IO;

namespace KaragoPublisher.Windows;

public static class CategoryCache
{
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KARAGO", "NewsPublisher");
    private static string FileFor(string site) { Validate(site); return Path.Combine(Folder, $"categories-{site}.json"); }
    public static void Save(string site, IReadOnlyList<WordPressCategory> categories) { Validate(site); if (categories.Any(c => c.Site != site)) throw new InvalidOperationException("Λάθος site cache."); Directory.CreateDirectory(Folder); File.WriteAllText(FileFor(site), JsonSerializer.Serialize(new CacheDocument(site, DateTimeOffset.UtcNow, categories))); }
    public static IReadOnlyList<WordPressCategory> Load(string site) { Validate(site); var path = FileFor(site); if (!File.Exists(path)) return []; var doc = JsonSerializer.Deserialize<CacheDocument>(File.ReadAllText(path)); if (doc?.Site != site) throw new InvalidOperationException("Απορρίφθηκε cache άλλου site."); return doc.Categories; }
    private static void Validate(string site) { if (site != "diafaneia") throw new InvalidOperationException("Άγνωστο cache site."); }
    private sealed record CacheDocument(string Site, DateTimeOffset SavedAt, IReadOnlyList<WordPressCategory> Categories);
}
