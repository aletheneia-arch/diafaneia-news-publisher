namespace KaragoPublisher.Windows;

public enum AdvertisementOrderMode
{
    Sequential,
    Random
}

public sealed class AdvertisementItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Νέα διαφήμιση";
    public string Html { get; set; } = "";
}

public sealed class AdvertisementSettings
{
    public AdvertisementOrderMode Mode { get; set; } = AdvertisementOrderMode.Sequential;
    public List<AdvertisementItem> Items { get; set; } = [];

    public static AdvertisementSettings CreateDefaults() => new()
    {
        Mode = AdvertisementOrderMode.Sequential,
        Items =
        [
            new AdvertisementItem
            {
                Name = "Μπαινάκταρης — Ταβέρνα Λ. Αιγαίου",
                Html = """<img class="aligncenter" src="https://diafaneia.eu/wp-content/uploads/2026/08/MPAINAKTARIS-TAVERNA-L.-AIGAIOY-560x840.jpeg" />"""
            },
            new AdvertisementItem
            {
                Name = "Βερίκοκο",
                Html = """<img src="https://diafaneia.eu/wp-content/uploads/2025/05/verikoko-1-840x508.jpeg" />"""
            },
            new AdvertisementItem
            {
                Name = "Κάτω Πόρτα — Ολύμποι",
                Html = """<img src="https://diafaneia.eu/wp-content/uploads/2026/07/%CE%9A%CE%91%CE%A4%CE%A9-%CE%A0%CE%9F%CE%A1%CE%A4%CE%91-%CE%9F%CE%9B%CE%A5%CE%9C%CE%A0%CE%9F%CE%99-KATO-PORTA-OLIMPOI-840x840.jpg" />"""
            }
        ]
    };
}
