using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner;

public class AiTemplateAssistantDialogViewModel : EDViewModel
{
    private readonly AnthropicApiClient  _apiClient;
    private readonly IDataSchemaProvider _schemaProvider;

    private string _description      = "";
    private string _apiKey           = "";
    private string _selectedAgenda   = "";
    private bool   _isBusy;

    public string Description    { get => _description;    set => Set(ref _description,    value); }
    public string ApiKey         { get => _apiKey;         set => Set(ref _apiKey,         value); }
    public string SelectedAgenda { get => _selectedAgenda; set => Set(ref _selectedAgenda, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set { Set(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); }
    }
    public bool IsNotBusy => !IsBusy;

    /// <summary>Vygenerovaná šablona — čtena volajícím po ShowDialog() == true.</summary>
    public Template? GeneratedTemplate { get; private set; }

    public AsyncRelayCommand GenerateCommand { get; }

    public AiTemplateAssistantDialogViewModel(string? defaultApiKey, IDataSchemaProvider schemaProvider)
    {
        _apiClient      = new AnthropicApiClient();
        _schemaProvider = schemaProvider;
        ApiKey          = defaultApiKey ?? "";

        GenerateCommand = CommandAsync(GenerateAsync);
    }

    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            PopupDisplayer.Info("Zadejte popis šablony, kterou chcete vygenerovat.");
            return;
        }
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            PopupDisplayer.Info("Zadejte Anthropic API klíč nebo jej nastavte v appsettings.json.");
            return;
        }

        IsBusy = true;
        try
        {
            var agendaCode = string.IsNullOrEmpty(SelectedAgenda) ? null : SelectedAgenda;
            var schema     = agendaCode is not null ? _schemaProvider.GetSchema(agendaCode) : null;

            GeneratedTemplate = await _apiClient.GenerateTemplateAsync(Description, agendaCode, schema, ApiKey);

            if (GeneratedTemplate is not null)
            {
                PopupDisplayer.Info("Šablona byla úspěšně vygenerována a bude otevřena v designeru.");
                CloseDialog(true);
            }
            else
            {
                PopupDisplayer.Info("AI vrátila prázdnou odpověď. Zkuste upravit popis nebo zkusit znovu.");
            }
        }
        catch (Exception ex)
        {
            PopupDisplayer.Error(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
