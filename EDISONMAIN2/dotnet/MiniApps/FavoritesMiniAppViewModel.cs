using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Miniaplikace „Oblíbené" — osobní launcher připnutých modulů jako dlaždice. Data bere ze sdílené
/// <see cref="ModuleShortcutsService"/> (týž zdroj jako hvězdička ★ ve stromu → živá synchronizace).
/// Spuštění deleguje na službu (<see cref="ModuleShortcutsService.RequestLaunch"/>) → obsluhuje shell
/// (usage + overlay), miniaplikace launch logiku neduplikuje. Drag&drop přidání/reorder viz View.
/// </summary>
public sealed class FavoritesMiniAppViewModel : EDMiniAppViewModel
{
    public FavoritesMiniAppViewModel(ModuleShortcutsService shortcuts) : this(EDServices.Current, shortcuts) { }

    public FavoritesMiniAppViewModel(EDServices services, ModuleShortcutsService shortcuts) : base(services)
    {
        Shortcuts = shortcuts;
        LaunchCommand = Command<ExecutableModule>(m => Shortcuts.RequestLaunch(m));
        RemoveCommand = Command<ExecutableModule>(m => Shortcuts.Remove(m));
        Shortcuts.Favorites.CollectionChanged += OnFavoritesChanged;
    }

    public override string Title => "Oblíbené";

    /// <summary>Sdílená služba — View ji používá i pro drop (přidání) a reorder oblíbených.</summary>
    public ModuleShortcutsService Shortcuts { get; }

    public ObservableCollection<ExecutableModule> Favorites => Shortcuts.Favorites;

    /// <summary>Prázdno → View ukáže výzvu „přetáhněte sem moduly".</summary>
    public bool HasFavorites => Favorites.Count > 0;

    public ICommand LaunchCommand { get; }
    public ICommand RemoveCommand { get; }

    private void OnFavoritesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasFavorites));

    public override void OnClosed() => Shortcuts.Favorites.CollectionChanged -= OnFavoritesChanged;
}
