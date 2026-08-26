using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace KaragoPublisher.Windows;

public partial class MainWindow : Window
{
    private readonly WordPressClient client = new();
    private ArticlePayload? payload;
    private CategoryCatalog? catalog;
    private List<string> suggestions = [];
    private string? imagePath;
    private bool allOpen;
    private bool connectionVerified;
    private bool connectorCanPublish;

    public MainWindow()
    {
        InitializeComponent();
        UpdateAdStatus();
    }

    private void Paste_Click(object sender, RoutedEventArgs e) { if (Clipboard.ContainsText()) PackageBox.Text = Clipboard.GetText(); ParsePackage(); }
    private void Parse_Click(object sender, RoutedEventArgs e) => ParsePackage();
    private void Settings_Click(object sender, RoutedEventArgs e) { new SettingsWindow { Owner = this }.ShowDialog(); }
    private void Ads_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            new AdsWindow { Owner = this }.ShowDialog();
            UpdateAdStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Σφάλμα διαφημίσεων", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void Clear_Click(object sender, RoutedEventArgs e) { PackageBox.Clear(); payload = null; catalog = null; suggestions.Clear(); imagePath = null; ResetUi(); }

    private async void ParsePackage()
    {
        try {
            payload = PackageParser.Parse(PackageBox.Text); suggestions = payload.Categories.ToList(); catalog = null; ResetCategoryUi();
            SiteText.Text = "Site: " + payload.Site.ToUpperInvariant();
            var adSettings = AdvertisementSettingsStore.Load();
            SummaryText.Text = $"Τίτλος: {payload.Title}\nΚατηγορία πακέτου: {string.Join(", ", suggestions)}\nTags: {payload.Tags.Count}\nYoast SEO + HTML: ✓\nΔιαφημίσεις: {adSettings.Items.Count}, {(adSettings.Mode == AdvertisementOrderMode.Random ? "τυχαία σειρά" : "σταθερή σειρά")}";
            ResultText.Text = "Το πακέτο είναι έγκυρο. Φορτώνω τις πραγματικές κατηγορίες..."; await LoadCategoriesAsync(false);
        } catch (Exception ex) { payload = null; catalog = null; ResetUi(); ResultText.Text = "❌ " + ex.Message; }
    }

    private async Task LoadCategoriesAsync(bool manual)
    {
        if (payload == null) return; var site = payload.Site; connectionVerified = false; connectorCanPublish = false; UpdateButtons(); CategoryStatus.Text = manual ? "Ανανέωση κατηγοριών..." : "Φόρτωση κατηγοριών...";
        if (!manual) try { var cache = CategoryCache.Load(site); if (cache.Count > 0) ApplyCategories(site, cache, "Ασφαλής cache — γίνεται ανανέωση..."); } catch { }
        try {
            var connectorStatus = await client.TestConnectionAsync(site);
            var fresh = await client.GetCategoriesAsync(site); if (fresh.Count == 0) throw new InvalidOperationException("Το WordPress δεν επέστρεψε κατηγορίες.");
            if (payload?.Site != site) return; connectionVerified = true; connectorCanPublish = connectorStatus.CanPublish; CategoryCache.Save(site, fresh); ApplyCategories(site, fresh, $"Φορτώθηκαν {fresh.Count} πραγματικές κατηγορίες. Δικαίωμα: {(connectorCanPublish ? "Draft + Publish" : "μόνο Draft")}."); ResultText.Text = "Έλεγξε τις τελικές κατηγορίες.";
        } catch (Exception ex) {
            connectionVerified = false; connectorCanPublish = false;
            if (catalog?.Site == site) CategoryStatus.Text = "Αποτυχία ανανέωσης — χρησιμοποιείται ασφαλής cache.\n" + ex.Message;
            else { catalog = null; CategoryStatus.Text = "Σφάλμα φόρτωσης: " + ex.Message; ResultText.Text = "❌ Δεν επιτρέπεται αποστολή χωρίς επαληθευμένες κατηγορίες."; }
            UpdateButtons();
        }
    }

    private void ApplyCategories(string site, IReadOnlyList<WordPressCategory> categories, string status)
    {
        var previous = catalog?.Site == site ? catalog.SelectedIds.ToList() : [];
        var next = new CategoryCatalog(site, categories); if (catalog == null) next.Preselect(suggestions); else foreach (var id in previous) next.Select(id, true);
        catalog = next; CategoryStatus.Text = status; ShowAllButton.IsEnabled = true; RenderCategories();
    }

    private void RenderCategories()
    {
        FrequentPanel.Children.Clear(); if (catalog == null) { UpdateButtons(); return; }
        foreach (var category in catalog.Compact(suggestions)) FrequentPanel.Children.Add(Check(category, false));
        RenderAll(); RenderFinal();
    }
    private CheckBox Check(WordPressCategory category, bool hierarchy)
    {
        var box = new CheckBox { Content = $"{(hierarchy ? catalog!.DisplayName(category) : category.Name)}  ({category.Count})", IsChecked = catalog!.SelectedIds.Contains(category.Id), Tag = category.Id };
        box.Click += (_, _) => { catalog.Select(category.Id, box.IsChecked == true); RenderCategories(); }; return box;
    }
    private void RenderAll() { AllPanel.Children.Clear(); if (!allOpen || catalog == null) return; foreach (var c in catalog.Search(CategorySearch.Text)) AllPanel.Children.Add(Check(c, true)); }
    private void RenderFinal() { FinalCategoriesText.Text = catalog == null || catalog.SelectedIds.Count == 0 ? "Επιλεγμένες κατηγορίες: —\nΕπίλεξε τουλάχιστον μία κατηγορία." : "Επιλεγμένες κατηγορίες:\n" + string.Join("\n", catalog.Selected().Select(c => "✓ " + c.Name)); UpdateButtons(); }
    private void UpdateButtons() { var enabled = connectionVerified && payload != null && catalog?.Site == payload.Site && catalog.SelectedIds.Count > 0; DraftButton.IsEnabled = enabled; PublishButton.IsEnabled = enabled && connectorCanPublish; }

    private async void RefreshCategories_Click(object sender, RoutedEventArgs e) => await LoadCategoriesAsync(true);
    private void ShowAll_Click(object sender, RoutedEventArgs e) { allOpen = !allOpen; AllArea.Visibility = allOpen ? Visibility.Visible : Visibility.Collapsed; ShowAllButton.Content = allOpen ? "Απόκρυψη κατηγοριών ▲" : "Όλες οι κατηγορίες ▼"; RenderAll(); }
    private void CategorySearch_TextChanged(object sender, TextChangedEventArgs e) => RenderAll();
    private void ChooseImage_Click(object sender, RoutedEventArgs e) { var dialog = new OpenFileDialog { Filter = "Εικόνες|*.jpg;*.jpeg;*.png;*.webp;*.gif" }; if (dialog.ShowDialog() == true) { imagePath = dialog.FileName; ImageText.Text = "Featured image: " + Path.GetFileName(imagePath); } }
    private async void Draft_Click(object sender, RoutedEventArgs e) => await SubmitAsync("draft");
    private async void Publish_Click(object sender, RoutedEventArgs e) { if (MessageBox.Show($"Να δημοσιευτεί τώρα στο {payload?.Site.ToUpperInvariant()};", "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await SubmitAsync("publish"); }

    private async Task SubmitAsync(string status)
    {
        if (payload == null || catalog?.Site != payload.Site || catalog.SelectedIds.Count == 0) return;
        DraftButton.IsEnabled = PublishButton.IsEnabled = false;
        try {
            ResultText.Text = "Προσθήκη διαφημίσεων και αποστολή...";
            var adSettings = AdvertisementSettingsStore.Load();
            var outbound = payload.Copy();
            outbound.Categories = catalog.SelectedIds.Select(x => x.ToString()).ToList();
            outbound.Content = AdvertisementComposer.ApplyForRequest(payload.Content, adSettings, payload.RequestId);
            ResultText.Text = imagePath == null ? "Αποστολή μέσω του ασφαλούς Connector..." : "Προετοιμασία featured image και ασφαλής αποστολή...";
            var result = await client.PublishAsync(outbound, status, imagePath); ResultText.Text = $"✅ ΕΤΟΙΜΟ\nPost ID: {result.PostId}\nStatus: {result.Status}\nΔιαφημίσεις: {adSettings.Items.Count} ({(adSettings.Mode == AdvertisementOrderMode.Random ? "τυχαία" : "σταθερή")} σειρά)\n{(result.Duplicate ? "Ίδιο REQUEST_ID — δεν δημιουργήθηκε duplicate.\n" : "")}Edit: {result.EditLink}\nLink: {result.Link}";
        } catch (Exception ex) { ResultText.Text = "❌ ΣΦΑΛΜΑ\n" + ex.Message; }
        finally { UpdateButtons(); }
    }

    private void ResetCategoryUi() { FrequentPanel.Children.Clear(); AllPanel.Children.Clear(); ShowAllButton.IsEnabled = false; allOpen = false; connectionVerified = false; connectorCanPublish = false; AllArea.Visibility = Visibility.Collapsed; CategorySearch.Clear(); CategoryStatus.Text = "Φόρτωση κατηγοριών..."; FinalCategoriesText.Text = "Επιλεγμένες κατηγορίες: —"; UpdateButtons(); }
    private void ResetUi() { SiteText.Text = "Site: —"; SummaryText.Text = "Δεν έχει γίνει έλεγχος ακόμη."; ResultText.Text = ""; ImageText.Text = "Featured image: καμία"; ResetCategoryUi(); }

    private void UpdateAdStatus()
    {
        try
        {
            var settings = AdvertisementSettingsStore.Load();
            AdStatusText.Text = $"Διαφημίσεις: {settings.Items.Count} — {(settings.Mode == AdvertisementOrderMode.Random ? "τυχαία σειρά" : "σταθερή σειρά")}";
        }
        catch (Exception ex)
        {
            AdStatusText.Text = "Σφάλμα ρυθμίσεων διαφημίσεων: " + ex.Message;
        }
    }
}
