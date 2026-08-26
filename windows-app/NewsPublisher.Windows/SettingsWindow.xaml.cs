using System.Windows;

namespace KaragoPublisher.Windows;

public partial class SettingsWindow : Window
{
    private readonly WordPressClient client = new();

    public SettingsWindow()
    {
        InitializeComponent();
        DiafaneiaKey.Password = CredentialVault.Load("diafaneia").ApiKey;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CredentialVault.Save("diafaneia", DiafaneiaKey.Password.Trim());
            ConnectionStatus.Text = "✓ Το κλειδί αποθηκεύτηκε με ασφάλεια σε αυτό το PC.";
            MessageBox.Show("Αποθηκεύτηκε με ασφάλεια.", "News Publisher", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ConnectionStatus.Text = "❌ " + ex.Message;
            MessageBox.Show(ex.Message, "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = TestButton.IsEnabled = false;
        ConnectionStatus.Text = "Έλεγχος ασφαλούς σύνδεσης...";
        try
        {
            var status = await client.TestConnectionAsync("diafaneia", DiafaneiaKey.Password.Trim());
            CredentialVault.Save("diafaneia", DiafaneiaKey.Password.Trim());
            var right = status.CanPublish ? "Προσχέδια + Δημοσίευση" : "Μόνο προσχέδια";
            ConnectionStatus.Text = $"CONNECTED — το key αποθηκεύτηκε με ασφάλεια\nSite: {status.SiteName}\nΣυσκευή: {status.DeviceLabel}\nΔικαίωμα: {right}\nConnector: {status.Version}";
        }
        catch (Exception ex)
        {
            ConnectionStatus.Text = "CONNECTION FAILED\n" + ex.Message;
        }
        finally
        {
            SaveButton.IsEnabled = TestButton.IsEnabled = true;
        }
    }
}
