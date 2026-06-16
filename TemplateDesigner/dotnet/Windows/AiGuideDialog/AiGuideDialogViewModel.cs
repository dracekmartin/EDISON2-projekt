using System.Collections.ObjectModel;
using System.Text;
using EDControls.EDViewModel;
using TemplateDesigner.Config;
using TemplateDesigner.Services;

namespace TemplateDesigner.Windows.AiGuideDialog;

/// <summary>Živý kontext pro průvodce — čte se z hlavního VM při každém dotazu.</summary>
public readonly record struct AiGuideContext(
    string? AgendaCode, string? AgendaName, string? SelectedElementType, int SectionCount);

/// <summary>Jedna zpráva v chatu s průvodcem.</summary>
public class AiGuideMessage
{
    public required bool IsUser { get; init; }
    public required string Text { get; init; }
    public string Sender => IsUser ? "Vy" : "Pomocník";
}

/// <summary>
/// Pasivní AI průvodce aplikací — žije jako záložka v levém panelu (non-modal).
/// Neměňuje šablonu, jen radí kde co je a jak na to. Znalosti ze <see cref="CapabilityCatalog"/>.
/// Kontext (agenda, vybraný element, počet sekcí) se čte živě při každém dotazu přes
/// <paramref name="contextProvider"/>. Konverzace je session-scoped (po restartu se vyčistí).
/// Model: Haiku.
/// </summary>
public class AiGuidePanelViewModel : EDViewModel
{
    private const string Model = "claude-haiku-4-5";

    private readonly Func<AiGuideContext> _contextProvider;

    public ObservableCollection<AiGuideMessage> Messages { get; } = [];

    private string _input = "";
    public string Input { get => _input; set => Set(ref _input, value); }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { Set(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); }
    }
    public bool IsNotBusy => !IsBusy;

    public AsyncRelayCommand AskCommand { get; }
    public RelayCommand ClearCommand { get; }

    /// <summary>Signál pro view, aby odscrolloval na poslední zprávu.</summary>
    public Action? RequestScrollToEnd { get; set; }

    public AiGuidePanelViewModel(Func<AiGuideContext> contextProvider)
    {
        _contextProvider = contextProvider;
        AskCommand  = CommandAsync(AskAsync, () => IsNotBusy);
        ClearCommand = Command(Clear);
    }

    private void Clear()
    {
        Messages.Clear();
        Input = "";
    }

    private async Task AskAsync()
    {
        var question = Input.Trim();
        if (question.Length == 0) return;

        var apiKey = AppSettings.Current?.AiAssistant.AnthropicApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            PopupDisplayer.Info("Chybí Anthropic API klíč. Nastav ho v templatedesigner.appsettings.local.json.");
            return;
        }

        Messages.Add(new AiGuideMessage { IsUser = true, Text = question });
        Input = "";
        RequestScrollToEnd?.Invoke();

        IsBusy = true;
        try
        {
            var ctx = _contextProvider();
            var systemPrompt = CapabilityCatalog.BuildSystemPrompt(
                ctx.AgendaCode, ctx.AgendaName, ctx.SelectedElementType, ctx.SectionCount);

            var answer = await AnthropicApiClient.CallApiAsync(
                apiKey, systemPrompt, BuildConversationPrompt(), Model, maxTokens: 1024, temperature: 0.2);

            Messages.Add(new AiGuideMessage
            {
                IsUser = false,
                Text = string.IsNullOrWhiteSpace(answer)
                    ? "Nepodařilo se odpovědět, zkus dotaz zopakovat."
                    : answer.Trim()
            });
            RequestScrollToEnd?.Invoke();
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

    /// <summary>
    /// Sestaví user prompt z konverzace (CallApiAsync bere jeden text, ne seznam zpráv).
    /// Historie udrží kontext doptávání.
    /// </summary>
    private string BuildConversationPrompt()
    {
        var sb = new StringBuilder();
        foreach (var m in Messages)
            sb.AppendLine($"{(m.IsUser ? "Uživatel" : "Průvodce")}: {m.Text}");
        sb.AppendLine("Průvodce:");
        return sb.ToString();
    }
}
