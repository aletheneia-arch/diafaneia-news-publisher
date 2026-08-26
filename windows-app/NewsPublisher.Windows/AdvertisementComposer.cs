using System.Security.Cryptography;
using System.Text;

namespace KaragoPublisher.Windows;

public static class AdvertisementComposer
{
    public static string ApplyForRequest(string content, AdvertisementSettings settings, string requestId)
    {
        if (settings.Mode != AdvertisementOrderMode.Random)
            return Apply(content, settings);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(requestId ?? ""));
        return Apply(content, settings, new Random(BitConverter.ToInt32(hash, 0)));
    }

    public static string Apply(string content, AdvertisementSettings settings, Random? random = null)
    {
        var article = (content ?? "").Trim();
        var advertisements = settings.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Html))
            .Select(item => item.Html.Trim())
            .ToList();

        if (advertisements.Count == 0) return article;

        // Reapplying or changing the order must never duplicate a configured advertisement.
        foreach (var advertisement in advertisements)
            article = article.Replace(advertisement, "", StringComparison.Ordinal).Trim();

        if (settings.Mode == AdvertisementOrderMode.Random)
        {
            var fixedOrder = advertisements.ToList();
            Shuffle(advertisements, random ?? Random.Shared);
            // "Random" must produce a genuinely different order when at least two ads exist.
            if (advertisements.Count > 1 && advertisements.SequenceEqual(fixedOrder, StringComparer.Ordinal))
            {
                var firstFixedItem = advertisements[0];
                advertisements.RemoveAt(0);
                advertisements.Add(firstFixedItem);
            }
        }

        var first = advertisements[0];
        if (advertisements.Count == 1)
            return string.IsNullOrWhiteSpace(article) ? first : first + "\n" + article;

        var remaining = string.Join("\n\n", advertisements.Skip(1));
        return string.IsNullOrWhiteSpace(article)
            ? string.Join("\n\n", advertisements)
            : first + "\n" + article + "\n\n" + remaining;
    }

    private static void Shuffle<T>(IList<T> items, Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
