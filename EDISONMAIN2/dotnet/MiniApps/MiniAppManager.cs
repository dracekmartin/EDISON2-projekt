using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Otevírá, trackuje a zavírá mini-apps v docku. Multi-instance: tutéž mini-app jde otevřít
/// vícekrát, každá instance dostane unikátní <c>ContentId "{Id}#{n}"</c>. Záměrně samostatný
/// (nerozšiřuje <c>WindowNavigator</c> — viz MINIAPPS.md §5); shell ho volá explicitně.
/// </summary>
public sealed class MiniAppManager(IMiniAppHost host)
{
    private readonly List<MiniAppInstance> _open = [];
    private int _nextInstance = 1;

    public IReadOnlyList<MiniAppInstance> OpenInstances => _open;

    /// <summary>Vyrobí VM z deskriptoru, vloží panel do docku a začne ho trackovat.</summary>
    public MiniAppInstance Open(MiniAppDescriptor descriptor)
    {
        var vm = descriptor.CreateViewModel();
        var contentId = $"{descriptor.Id}#{_nextInstance++}";

        MiniAppInstance? instance = null;
        void OnRequestClose(bool? _) => instance?.Pane.Close();

        var pane = host.AddPane(contentId, vm.Title, vm.Icon, vm, onClosed: () =>
        {
            vm.RequestClose -= OnRequestClose;
            if (instance is not null) _open.Remove(instance);
            vm.OnClosed();
        });

        instance = new MiniAppInstance(descriptor.Id, contentId, vm, pane);
        vm.RequestClose += OnRequestClose;
        _open.Add(instance);
        vm.OnOpened();
        return instance;
    }

    /// <summary>Zavře všechny otevřené mini-apps (zavření shellu, přepnutí firmy).</summary>
    public void CloseAll()
    {
        foreach (var instance in _open.ToList())
            instance.Pane.Close();
    }
}
