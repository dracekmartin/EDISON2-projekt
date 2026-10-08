using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.Desktop;

/// <summary>
/// Miniaplikace „Plocha" - osobní launcher zástupců jako dlaždice. Data bere ze sdílené
/// <see cref="ModuleShortcutsService"/>, kam ★ ve stromu přidává moduly hlavní nabídky a agendy
/// samy tiskové sestavy a pohledy DS. Spuštění z dlaždice volá View přímo na službě
/// (<see cref="ModuleShortcutsService.RequestLaunch"/>, spouští jen klik bez tažení) a obsluhuje
/// ho shell (usage + overlay), miniaplikace launch logiku neduplikuje. Drag&amp;drop přidání/reorder viz View.
/// </summary>
public sealed class DesktopMiniAppViewModel : EDMiniAppViewModel
{
    public DesktopMiniAppViewModel(ModuleShortcutsService shortcuts) : this(EDServices.Current, shortcuts) { }

    public DesktopMiniAppViewModel(EDServices services, ModuleShortcutsService shortcuts) : base(services)
    {
        Shortcuts = shortcuts;
        RemoveCommand = Command<DesktopShortcut>(s => Shortcuts.Remove(s));
        ShowChangelogCommand = Command<DesktopShortcut>(s => Shortcuts.RequestChangelog(s));
        Shortcuts.Items.CollectionChanged += OnItemsChanged;
    }

    public override string Title => "Plocha";

    /// <summary>Sdílená služba - View ji používá i pro drop (přidání) a reorder dlaždic.</summary>
    public ModuleShortcutsService Shortcuts { get; }

    public ObservableCollection<DesktopShortcut> Items => Shortcuts.Items;

    /// <summary>Prázdno → View ukáže výzvu „přetáhněte sem moduly".</summary>
    public bool HasItems => Items.Count > 0;

    public ICommand RemoveCommand { get; }
    public ICommand ShowChangelogCommand { get; }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasItems));

    public override void OnClosed() => Shortcuts.Items.CollectionChanged -= OnItemsChanged;
}
