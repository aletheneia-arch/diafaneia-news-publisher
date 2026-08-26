using System.Windows;
using System.Windows.Controls;

namespace KaragoPublisher.Windows;

public partial class AdsWindow : Window
{
    private readonly AdvertisementSettings settings;

    public AdsWindow()
    {
        InitializeComponent();
        settings = AdvertisementSettingsStore.Load();
        SequentialRadio.IsChecked = settings.Mode == AdvertisementOrderMode.Sequential;
        RandomRadio.IsChecked = settings.Mode == AdvertisementOrderMode.Random;
        RefreshList(settings.Items.Count > 0 ? 0 : -1);
    }

    private void RefreshList(int selectedIndex)
    {
        AdsList.ItemsSource = null;
        AdsList.ItemsSource = settings.Items;
        AdsList.SelectedIndex = selectedIndex >= 0 && selectedIndex < settings.Items.Count ? selectedIndex : -1;
        if (AdsList.SelectedIndex < 0)
        {
            NameBox.Clear();
            HtmlBox.Clear();
        }
        StatusText.Text = $"Σύνολο διαφημίσεων: {settings.Items.Count}";
    }

    private void AdsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AdsList.SelectedItem is not AdvertisementItem item) return;
        NameBox.Text = item.Name;
        HtmlBox.Text = item.Html;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        AdsList.SelectedIndex = -1;
        NameBox.Clear();
        HtmlBox.Clear();
        NameBox.Focus();
        StatusText.Text = "Συμπληρώστε όνομα και HTML και πατήστε Αποθήκευση διαφήμισης.";
    }

    private void SaveItem_Click(object sender, RoutedEventArgs e)
    {
        if (!CommitEditor()) return;
        StatusText.Text = "Η αλλαγή κρατήθηκε. Πατήστε Αποθήκευση και κλείσιμο για μόνιμη αποθήκευση.";
    }

    private bool CommitEditor()
    {
        var name = NameBox.Text.Trim();
        var html = HtmlBox.Text.Trim();
        if (name.Length == 0 || html.Length == 0)
        {
            MessageBox.Show("Χρειάζονται όνομα και κώδικας HTML.", "Διαφήμιση", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var index = AdsList.SelectedIndex;
        if (index >= 0)
        {
            settings.Items[index].Name = name;
            settings.Items[index].Html = html;
        }
        else
        {
            settings.Items.Add(new AdvertisementItem { Name = name, Html = html });
            index = settings.Items.Count - 1;
        }
        RefreshList(index);
        return true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var index = AdsList.SelectedIndex;
        if (index < 0) return;
        if (MessageBox.Show($"Να διαγραφεί η διαφήμιση «{settings.Items[index].Name}»;", "Επιβεβαίωση", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        settings.Items.RemoveAt(index);
        RefreshList(Math.Min(index, settings.Items.Count - 1));
    }

    private void Up_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void Down_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int offset)
    {
        var index = AdsList.SelectedIndex;
        var target = index + offset;
        if (index < 0 || target < 0 || target >= settings.Items.Count) return;
        var item = settings.Items[index];
        settings.Items.RemoveAt(index);
        settings.Items.Insert(target, item);
        RefreshList(target);
    }

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((NameBox.Text.Length > 0 || HtmlBox.Text.Length > 0) && !CommitEditor()) return;
            settings.Mode = RandomRadio.IsChecked == true ? AdvertisementOrderMode.Random : AdvertisementOrderMode.Sequential;
            AdvertisementSettingsStore.Save(settings);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Σφάλμα αποθήκευσης", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
