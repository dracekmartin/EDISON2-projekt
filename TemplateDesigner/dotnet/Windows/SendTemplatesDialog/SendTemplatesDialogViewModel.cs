using System.ComponentModel;
using System.Runtime.CompilerServices;
using EDControls.EDViewModel;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// Okno „Odeslat jiné firmě" (`B189`, R9): uživatel zaškrtne vlastní šablony firmy a firmy, napíše zprávu a server
/// šablony pošle přes sdílení dat ISYCloudu. Šablony se vybírají tady, ne v okně šablon - tam dvojklik otevírá
/// a akce pracují s jednou šablonou (rozhodnutí Jan 3. 11.).
/// </summary>
public sealed class SendTemplatesDialogViewModel : EDViewModel
{
    private readonly ITemplateSharing _sharing;

    public IReadOnlyList<SelectableItem> Templates { get; }

    private IReadOnlyList<SelectableItem> _recipients = [];
    public IReadOnlyList<SelectableItem> VisibleRecipients => Filter(_recipients, _recipientFilter);

    private string _recipientFilter = "";
    /// <summary>Hledání firmy podle názvu, zákazníka nebo zkratky (IdFa).</summary>
    public string RecipientFilter
    {
        get => _recipientFilter;
        set { if (Set(ref _recipientFilter, value ?? "")) OnPropertyChanged(nameof(VisibleRecipients)); }
    }

    private string _message = "";
    public string Message
    {
        get => _message;
        set => Set(ref _message, value ?? "");
    }

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    private string _recipientsError = "";
    /// <summary>Proč se firmy nenačetly (ISYCloud nedostupný); prázdné = načetly se.</summary>
    public string RecipientsError
    {
        get => _recipientsError;
        private set { if (Set(ref _recipientsError, value)) OnPropertyChanged(nameof(HasRecipientsError)); }
    }

    public bool HasRecipientsError => RecipientsError != "";

    private bool _isSending;

    /// <summary>Co odejde: „2 šablony, 1 firma"; uživatel to vidí u tlačítka Odeslat.</summary>
    public string Summary =>
        $"{Pocet(Checked(Templates).Count, "šablona", "šablony", "šablon")}, " +
        $"{Pocet(Checked(_recipients).Count, "firma", "firmy", "firem")}";

    public AsyncRelayCommand SendCommand { get; }

    /// <param name="templates">Vlastní šablony firmy (Id, název, agenda); šablony M-line poslat nejde.</param>
    /// <param name="preselected">Šablona vybraná v okně šablon, ta je předem zaškrtnutá.</param>
    public SendTemplatesDialogViewModel(ITemplateSharing sharing, IEnumerable<(Guid Id, string Name, string Agenda)> templates,
        Guid? preselected)
    {
        _sharing = sharing;
        Templates = [.. templates
            .OrderBy(t => t.Agenda, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new SelectableItem(t.Id.ToString(), t.Name, t.Agenda, OnCheckedChanged, t.Id == preselected))];
        SendCommand = CommandAsync(SendAsync, CanSend);
    }

    public bool CanSend() => !_isSending && Checked(Templates).Count > 0 && Checked(_recipients).Count > 0;

    /// <summary>Načte firmy, kterým jde poslat. Nedostupný ISYCloud ukáže důvod v okně místo seznamu.</summary>
    public async Task LoadAsync()
    {
        try
        {
            var firmy = await _sharing.GetRecipientsAsync();
            _recipients = [.. firmy.Select(f => new SelectableItem(f.IdFa, f.Name, f.Customer, OnCheckedChanged))];
            RecipientsError = _recipients.Count == 0 ? "Žádná jiná firma nemá sdílení dat povolené." : "";
        }
        catch (CacheTemplateException ex)
        {
            RecipientsError = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(VisibleRecipients));
            OnPropertyChanged(nameof(Summary));
        }
    }

    private async Task SendAsync()
    {
        _isSending = true;
        try
        {
            var ids = Checked(Templates).Select(t => Guid.Parse(t.Key)).ToList();
            var firmy = Checked(_recipients);
            var vysledek = await _sharing.SendTemplatesAsync(ids, [.. firmy.Select(f => f.Key)], Message.Trim());
            PopupDisplayer.Info(SentText(vysledek.Templates, firmy.Select(f => f.Title).ToList()));
            CloseDialog(true);
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
        finally { _isSending = false; }
    }

    /// <summary>Potvrzení odeslání: kolik šablon a komu; příjemce je uvidí v okně šablon a přijde mu e-mail.</summary>
    internal static string SentText(int templates, IReadOnlyList<string> firms) =>
        $"Odesláno: {Pocet(templates, "šablona", "šablony", "šablon")} pro {string.Join(", ", firms)}. " +
        "Příjemce je najde v okně šablon a přijde mu o tom e-mail.";

    internal static IReadOnlyList<SelectableItem> Filter(IReadOnlyList<SelectableItem> items, string filter)
    {
        var slova = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return slova.Length == 0 ? items
            : [.. items.Where(i => slova.All(s => i.Matches(s)))];
    }

    private static List<SelectableItem> Checked(IEnumerable<SelectableItem> items) => [.. items.Where(i => i.IsChecked)];

    internal static string Pocet(int n, string jedna, string dve, string vic) =>
        $"{n} {(n == 1 ? jedna : n is >= 2 and <= 4 ? dve : vic)}";

    private void OnCheckedChanged()
    {
        OnPropertyChanged(nameof(Summary));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>Zaškrtávací položka okna pro odeslání: šablona (název, agenda) nebo firma (název, zákazník).</summary>
public sealed class SelectableItem(string key, string title, string detail, Action onCheckedChanged, bool isChecked = false)
    : INotifyPropertyChanged
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public string Detail { get; } = detail;

    private bool _isChecked = isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged();
            onCheckedChanged();
        }
    }

    public bool Matches(string word) =>
        Title.Contains(word, StringComparison.CurrentCultureIgnoreCase)
        || Detail.Contains(word, StringComparison.CurrentCultureIgnoreCase)
        || Key.Contains(word, StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
