using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner;

public partial class AiTemplateAssistantDialog : Window
{
    private readonly AnthropicApiClient _apiClient;
    private readonly IDataSchemaProvider _schemaProvider;
    public Template? GeneratedTemplate { get; private set; }

    public AiTemplateAssistantDialog(string? defaultApiKey, IDataSchemaProvider schemaProvider)
    {
        InitializeComponent();
        _apiClient = new AnthropicApiClient();
        _schemaProvider = schemaProvider;

        if (!string.IsNullOrEmpty(defaultApiKey))
            ApiKeyTextBox.Text = defaultApiKey;
    }

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        var description = DescriptionTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            ShowValidationError("Zadejte popis šablony, kterou chcete vygenerovat.", "Chybí popis");
            DescriptionTextBox.Focus();
            return;
        }

        var apiKey = ApiKeyTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ShowValidationError("Zadejte Anthropic API klíč nebo jej nastavte v appsettings.json.", "Chybí API klíč");
            ApiKeyTextBox.Focus();
            return;
        }

        SetBusyState(true);

        try
        {
            var agendaCode = (AgendaComboBox.SelectedItem as ComboBoxItem)?.Tag as string;

            // Načíst schema pokud je agenda vybraná
            DataSchema? schema = null;
            if (!string.IsNullOrEmpty(agendaCode))
            {
                schema = _schemaProvider.GetSchema(agendaCode);
            }

            GeneratedTemplate = await _apiClient.GenerateTemplateAsync(description, agendaCode, schema, apiKey);

            if (GeneratedTemplate is not null)
            {
                MessageBox.Show("Šablona byla úspěšně vygenerována a bude otevřena v designeru.",
                    "Úspěch", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("AI vrátila prázdnou odpověď. Zkuste upravit popis nebo zkusit znovu.",
                    "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chyba při generování šablony:\n\n{ex.Message}",
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private void SetBusyState(bool isBusy)
    {
        BtnGenerate.IsEnabled = !isBusy;
        BtnCancel.IsEnabled = !isBusy;
        ProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void ShowValidationError(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
