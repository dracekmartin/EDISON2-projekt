using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EDControls.EDViewModel;

public class EDViewModel : INotifyPropertyChanged
{
    protected readonly EDServices _services;

    /// <summary>
    /// Sdílená MLRest instance ze shared <see cref="EDServices"/>. Pouze composition-root
    /// VM (<c>EdisonMainViewModel</c>) zapisuje do <c>services.MLRest</c> přímo.
    /// </summary>
    public IMLRest? MLRest => _services.MLRest;

    /// <summary>Navigator pro otevírání oken — <c>WindowNavigator.Show(...)</c> / <c>ShowDialog(...)</c>.</summary>
    protected IWindowNavigator WindowNavigator => _services.WindowNavigator;

    /// <summary>
    /// Služba pro popupy — vždy cílí na aktivní okno jako owner.
    /// Použití: <c>PopupDisplayer.Confirm(...)</c> nebo <c>PopupDisplayer.Info(...)</c> z VM.
    /// </summary>
    public IPopupDisplayer PopupDisplayer => _services.PopupDisplayer;

    /// <summary>
    /// Pokud true, <c>EDFooter</c> si vyžádá potvrzení před zavřením přes Storno/Zavřít.
    /// Nastavuj v konstruktoru pro okna s neuloženými daty.
    /// </summary>
    public bool ConfirmOnCancel { get; protected set; }

    /// <summary>
    /// Akce dostupné v okně (Přidat, Upravit, Smazat, …). <c>EDToolbar</c> a <c>EDFooter</c>
    /// je čtou automaticky. Nastav v konstruktoru subclass.
    /// </summary>
    public IReadOnlyList<EDAction>? Actions { get; protected set; }

    private bool _isActionInProgress;
    public bool IsActionInProgress
    {
        get => _isActionInProgress;
        private set => Set(ref _isActionInProgress, value);
    }

    /// <summary>
    /// Nastaví <see cref="IsActionInProgress"/> na true a vrátí scope, který ho resetuje při Dispose.
    /// Použití: <c>using var _ = ActionInProgress();</c>
    /// </summary>
    public IDisposable ActionInProgress()
    {
        IsActionInProgress = true;
        return new ActionScope(this);
    }

    private sealed class ActionScope(EDViewModel vm) : IDisposable
    {
        public void Dispose() => vm.IsActionInProgress = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// VM žádá zavření okna. <c>true</c> = potvrzeno (OK), <c>false</c> = zrušeno, <c>null</c> = jen zavřít.
    /// <c>EDFooter</c> se na ni přihlásí automaticky když detekuje <c>EDViewModel</c> jako DataContext.
    /// </summary>
    public event Action<bool?>? RequestClose;

    /// <summary>
    /// Bound na tlačítko Ok v <c>EDFooter</c> (FooterMode = OkCancel). Override <see cref="OnOk"/>
    /// pro vlastní validaci nebo logiku před zavřením.
    /// </summary>
    public ICommand OkCommand { get; }

    /// <summary>
    /// Standardní CRUD commandy. Override <see cref="OnAdd"/> / <see cref="OnEdit"/> /
    /// <see cref="OnDelete"/> v subclass. Napoj na akce přes <c>StandardActions.Add()</c> / Edit / Delete.
    /// </summary>
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }

    /// <summary>
    /// Bezparametrický konstruktor — čte z <see cref="EDServices.Current"/>.
    /// Používá se pro okna vytvářená přes <see cref="WindowNavigator"/>.
    /// </summary>
    public EDViewModel() : this(EDServices.Current) { }

    /// <summary>
    /// Explicitní konstruktor pro DI. Testy předávají mocky; composition-root VM
    /// předává <see cref="EDServices"/> z bootstrapu.
    /// </summary>
    public EDViewModel(EDServices services)
    {
        _services = services;
        OkCommand     = Command(OnOk);
        AddCommand    = Command(OnAdd);
        EditCommand   = Command(OnEdit);
        DeleteCommand = Command(OnDelete);
    }

    /// <summary>
    /// FormID pro persistenci stavu okna přes <see cref="WindowStatePersistence"/>.
    /// Default odvozuje z názvu VM: <c>EdisonMainViewModel</c> → <c>"EDISON2.EdisonMainWindow"</c>.
    /// Override na <c>null</c> v subclass pro opt-out.
    /// </summary>
    protected virtual string? PersistedFormId
    {
        get
        {
            var name = GetType().Name;
            if (name.EndsWith("ViewModel"))
                name = name[..^"ViewModel".Length] + "Window";
            return $"EDISON2.{name}";
        }
    }

    /// <summary>
    /// Volat **před** <c>window.Show()</c> / <c>ShowDialog()</c> aby se klávesové zkratky
    /// a perzistence stavu okna napojily na <c>SourceInitialized</c> ještě před prvním paintem
    /// (jinak by se zobrazila XAML default velikost a pak teprve restored — flicker).
    /// <see cref="WindowNavigator"/> volá automaticky pro Show/ShowDialog;
    /// composition-root v App.xaml.cs musí volat ručně pro shell okno.
    /// </summary>
    public void AttachToWindow(Window window)
    {
        RegisterKeyBindings(window);
        if (PersistedFormId is { } formId)
            WindowStatePersistence.Attach(window, formId);
    }

    /// <summary>
    /// Vytvoří <see cref="RelayCommand"/> s napojeným <see cref="PopupDisplayer"/> pro výjimky.
    /// Používej místo <c>new RelayCommand(...)</c> ve VM subclass.
    /// </summary>
    protected RelayCommand Command(Action execute, Func<bool>? canExecute = null) =>
        new(execute, canExecute, PopupDisplayer);

    /// <inheritdoc cref="Command(Action, Func{bool}?)"/>
    protected RelayCommand<T> Command<T>(Action<T> execute, Func<T, bool>? canExecute = null) =>
        new(execute, canExecute, PopupDisplayer);

    /// <summary>
    /// Aplikační přístup uživatele pro <paramref name="elementId"/> (string ID z legacy
    /// EDISFormEnum / EDPackageEnum / EDMainPackageEnum). Pokud <c>AccessTable</c> nebyl
    /// loadnut (testy, pre-login UI), vrací <c>true</c> — degrade gracefully.
    /// </summary>
    protected bool HasAccess(string elementId) =>
        _services.AccessTable?.Contains(elementId) ?? true;

    /// <summary>
    /// Vytvoří <see cref="AsyncRelayCommand"/> s napojeným <see cref="PopupDisplayer"/> pro výjimky.
    /// Pro <c>async Task</c> handlery — vyhni se <c>async void</c>. Pojmenování
    /// <c>CommandAsync</c> (ne overload <c>Command</c>) drží overload resolution jasné.
    /// </summary>
    protected AsyncRelayCommand CommandAsync(Func<Task> execute, Func<bool>? canExecute = null) =>
        new(execute, canExecute, PopupDisplayer);

    private void RegisterKeyBindings(Window window)
    {
        window.InputBindings.Add(new KeyBinding(OkCommand, Key.Return, ModifierKeys.Control));

        if (Actions == null) return;
        foreach (var action in Actions.Where(a => a.Key != Key.None))
            window.InputBindings.Add(new KeyBinding(action.Command, action.Key, action.Modifiers));
    }

    /// <summary>
    /// Volá se po kliknutí na Ok. Override pro vlastní validaci. Volej <c>base.OnOk()</c>
    /// (nebo <c>CloseDialog(true)</c>) pro potvrzení a zavření.
    /// </summary>
    protected virtual void OnOk() => CloseDialog(true);

    protected virtual void OnAdd() { }
    protected virtual void OnEdit() { }
    protected virtual void OnDelete() { }

    /// <summary>
    /// Továrna pro standardní akce. Použití:
    /// <c>Actions = [StandardActions.Add(), StandardActions.Edit(), StandardActions.Delete()];</c>
    /// </summary>
    protected StandardActionsAccessor StandardActions => new(this);

    public readonly struct StandardActionsAccessor(EDViewModel vm)
    {
        public EDAction Add()    => Add(vm.AddCommand);
        public EDAction Edit()   => Edit(vm.EditCommand);
        public EDAction Delete() => Delete(vm.DeleteCommand);

        public EDAction Add(ICommand command) =>
            new(FindString("EDStrings.Add") ?? "Přidat",
                command,
                FindImage("EDIcons.Add"),
                Key.N, ModifierKeys.Control);

        public EDAction Edit(ICommand command) =>
            new(FindString("EDStrings.Edit") ?? "Upravit",
                command,
                FindImage("EDIcons.Edit"),
                Key.E, ModifierKeys.Control);

        public EDAction Delete(ICommand command) =>
            new(FindString("EDStrings.Delete") ?? "Smazat",
                command,
                FindImage("EDIcons.Delete"),
                Key.D, ModifierKeys.Control);

        private static string? FindString(string key) =>
            Application.Current?.TryFindResource(key) as string;

        private static ImageSource? FindImage(string key) =>
            Application.Current?.TryFindResource(key) as ImageSource;
    }

    /// <summary>Žádá o zavření okna s daným dialog result.</summary>
    protected void CloseDialog(bool? result = null) => RequestClose?.Invoke(result);

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(property);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
