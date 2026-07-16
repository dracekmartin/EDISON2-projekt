using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Otevírá, trackuje a zavírá mini-apps v docku. Multi-instance: tutéž mini-app jde otevřít
/// vícekrát, každá instance dostane unikátní <c>ContentId "{Id}#{n}"</c>. Záměrně samostatný
/// (nerozšiřuje <c>WindowNavigator</c> — viz MINIAPPS.md §5); shell ho volá explicitně.
/// </summary>
public sealed class MiniAppManager(IMiniAppHost host, MiniAppCatalog catalog)
{
    private readonly List<MiniAppInstance> _open = [];
    private int _nextInstance = 1;

    public IReadOnlyList<MiniAppInstance> OpenInstances => _open;

    /// <summary>Vyvolá se po každém otevření i zavření mini-app (shell sleduje prázdnotu docku).</summary>
    public event Action? InstancesChanged;

    /// <summary>Obnoví všechny otevřené obnovitelné mini-apps. Vrací názvy (titulky) obnovených.</summary>
    public IReadOnlyList<string> RefreshAll()
    {
        var refreshed = new List<string>();
        foreach (var instance in _open.ToList())
            if (instance.ViewModel is IRefreshableMiniApp refreshable)
            {
                refreshable.Refresh();
                refreshed.Add(instance.ViewModel.Title);
            }
        return refreshed;
    }

    /// <summary>Vyrobí VM z deskriptoru, vloží panel do docku a začne ho trackovat.</summary>
    /// <param name="newColumn">true → panel jde do nového sloupce vedle stávajících (jinak jako tab).</param>
    /// <param name="newRow">true → panel jde do nové řady přes celou šířku pod sloupci.</param>
    public MiniAppInstance Open(MiniAppDescriptor descriptor, bool newColumn = false, bool newRow = false)
    {
        var vm = descriptor.CreateViewModel();
        var contentId = $"{descriptor.Id}#{_nextInstance++}";
        return Mount(descriptor.Id, contentId, vm,
            addPane: onClosed => host.AddPane(contentId, vm.Title, vm.Icon, vm, onClosed, newColumn, newRow));
    }

    /// <summary>
    /// Otevře sadu mini-apps vedle sebe do sloupců — první do stávajícího pane, další každá do
    /// nového sloupce.
    /// </summary>
    public IReadOnlyList<MiniAppInstance> OpenColumns(IEnumerable<MiniAppDescriptor> descriptors)
    {
        var opened = new List<MiniAppInstance>();
        foreach (var descriptor in descriptors)
            opened.Add(Open(descriptor, newColumn: opened.Count > 0));
        return opened;
    }

    /// <summary>
    /// Výchozí layout: čistý skeleton → sloupce nahoře + volitelná řada přes celou šířku dole
    /// (např. 3 web widgety nahoře, Oblíbené dole — mirror starého shellu).
    /// </summary>
    public void OpenDefaultLayout(IEnumerable<MiniAppDescriptor> columns, MiniAppDescriptor? bottomRow)
    {
        host.ResetToDefaultSkeleton();
        OpenColumns(columns);
        if (bottomRow is not null) Open(bottomRow, newRow: true);
    }

    /// <summary>Zavře všechny otevřené mini-apps (zavření shellu, přepnutí session).</summary>
    public void CloseAll()
    {
        foreach (var instance in _open.ToList())
            instance.Pane.Close();
    }

    /// <summary>Serializuje aktuální layout docku (k uložení do LocalPrefs per IDFa).</summary>
    public string SaveLayout() => host.SaveLayout();

    /// <summary>
    /// Obnoví layout — pro každý uložený panel najde podle <c>ContentId</c> prefixu descriptor
    /// v katalogu a vyrobí novou instanci VM (s výchozím stavem). Neznámé descriptory se zahodí.
    /// </summary>
    public void RestoreLayout(string layoutXml) => host.RestoreLayout(layoutXml, ResolveRestored);

    private RestoredContent? ResolveRestored(string contentId, IMiniAppPane pane)
    {
        var descriptor = catalog.Find(DescriptorIdOf(contentId));
        if (descriptor is null) return null;

        var vm = descriptor.CreateViewModel();
        Action? onClosed = null;
        Mount(descriptor.Id, contentId, vm, addPane: oc => { onClosed = oc; return pane; });
        BumpInstanceCounter(contentId);
        return new RestoredContent(vm, onClosed!);
    }

    // Sdílené jádro pro Open i restore: vytvoří instanci, napojí RequestClose + tracking + OnOpened.
    // addPane dostane onClosed handler a vrátí handle panelu (Open ho založí, restore reuse-uje existující).
    private MiniAppInstance Mount(string descriptorId, string contentId, EDMiniAppViewModel vm,
        Func<Action, IMiniAppPane> addPane)
    {
        MiniAppInstance? instance = null;
        void OnRequestClose(bool? _) => instance!.Pane.Close();
        void OnClosed()
        {
            vm.RequestClose -= OnRequestClose;
            if (instance is not null) _open.Remove(instance);
            vm.OnClosed();
            InstancesChanged?.Invoke();
        }

        var pane = addPane(OnClosed);
        instance = new MiniAppInstance(descriptorId, contentId, vm, pane);
        vm.RequestClose += OnRequestClose;
        _open.Add(instance);
        if (vm is IMiniAppRefreshController controller) controller.AttachRefresher(RefreshAll);
        vm.OnOpened();
        InstancesChanged?.Invoke();
        return instance;
    }

    private static string DescriptorIdOf(string contentId)
    {
        int hash = contentId.IndexOf('#');
        return hash < 0 ? contentId : contentId[..hash];
    }

    // Po obnově layoutu posuň counter za nejvyšší obnovený suffix, ať nové Open nekoliduje s ContentId.
    private void BumpInstanceCounter(string contentId)
    {
        int hash = contentId.IndexOf('#');
        if (hash >= 0 && int.TryParse(contentId[(hash + 1)..], out int n) && n >= _nextInstance)
            _nextInstance = n + 1;
    }
}

/// <summary>
/// Řídicí mini-app, která ovládá hromadný refresh ostatních. <see cref="MiniAppManager"/> jí při
/// mountu předá svůj <c>RefreshAll</c> (vrací názvy obnovených) — žádná závislost na EDServices.
/// </summary>
public interface IMiniAppRefreshController
{
    void AttachRefresher(Func<IReadOnlyList<string>> refreshAll);
}
