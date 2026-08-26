using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KaragoPublisher.Windows;

public static class AdvertisementSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KARAGO",
        "NewsPublisher");

    private static string SettingsFile => Path.Combine(Folder, "advertisements.json");

    public static AdvertisementSettings Load()
    {
        if (!File.Exists(SettingsFile))
        {
            var defaults = AdvertisementSettings.CreateDefaults();
            Save(defaults);
            return defaults;
        }

        var settings = JsonSerializer.Deserialize<AdvertisementSettings>(File.ReadAllText(SettingsFile), Options)
            ?? throw new InvalidOperationException("Το αρχείο ρυθμίσεων διαφημίσεων είναι κενό ή κατεστραμμένο.");
        settings.Items ??= [];
        Validate(settings);
        return settings;
    }

    public static void Save(AdvertisementSettings settings)
    {
        Validate(settings);
        Directory.CreateDirectory(Folder);
        var temporaryFile = SettingsFile + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(settings, Options));
        File.Move(temporaryFile, SettingsFile, true);
    }

    private static void Validate(AdvertisementSettings settings)
    {
        if (!Enum.IsDefined(settings.Mode))
            throw new InvalidOperationException("Άγνωστη λειτουργία σειράς διαφημίσεων.");
        if (settings.Items.Count > 1000)
            throw new InvalidOperationException("Υπάρχουν υπερβολικά πολλές διαφημίσεις στο αρχείο ρυθμίσεων.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in settings.Items)
        {
            item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim();
            item.Name = item.Name?.Trim() ?? "";
            item.Html = item.Html?.Trim() ?? "";
            if (item.Name.Length == 0 || item.Html.Length == 0)
                throw new InvalidOperationException("Κάθε διαφήμιση χρειάζεται όνομα και κώδικα HTML.");
            if (!ids.Add(item.Id))
                throw new InvalidOperationException("Βρέθηκε διπλό αναγνωριστικό διαφήμισης.");
        }
    }
}
