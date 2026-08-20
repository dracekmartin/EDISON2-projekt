namespace EDControls.MiniApps;

/// <summary>
/// Agreguje <see cref="MiniAppDescriptor"/> ze všech registrovaných <see cref="IMiniAppProvider"/>.
/// Lookup podle <see cref="MiniAppDescriptor.Id"/> (prefix <c>ContentId</c> při obnově layoutu).
/// </summary>
public sealed class MiniAppCatalog
{
    private readonly Dictionary<string, MiniAppDescriptor> _byId;
    private readonly List<MiniAppGroup> _groups;

    private MiniAppCatalog(Dictionary<string, MiniAppDescriptor> byId, List<MiniAppGroup> groups)
        => (_byId, _groups) = (byId, groups);

    /// <summary>
    /// Posbírá deskriptory ze všech providerů. <see cref="MiniAppDescriptor.Id"/> musí být unikátní
    /// napříč všemi providery - duplicita je chyba registrace, vyhodí výjimku (fail fast).
    /// </summary>
    public static MiniAppCatalog Build(IEnumerable<IMiniAppProvider> providers)
    {
        var byId = new Dictionary<string, MiniAppDescriptor>();
        var groups = new List<MiniAppGroup>();
        foreach (var provider in providers)
        {
            var apps = provider.GetMiniApps().ToList();
            foreach (var descriptor in apps)
                if (!byId.TryAdd(descriptor.Id, descriptor))
                    throw new InvalidOperationException(
                        $"Duplicitní MiniAppDescriptor.Id \"{descriptor.Id}\" - Id musí být unikátní napříč všemi providery.");
            if (apps.Count > 0)
                groups.Add(new MiniAppGroup(provider.GroupTitle, apps));
        }
        return new MiniAppCatalog(byId, groups);
    }

    /// <summary>Všechny registrované mini-apps.</summary>
    public IReadOnlyCollection<MiniAppDescriptor> All => _byId.Values;

    /// <summary>Skupiny per provider, pořadí = pořadí registrace. Provider bez mini-apps skupinu netvoří.</summary>
    public IReadOnlyList<MiniAppGroup> Groups => _groups;

    /// <summary>Najde deskriptor podle Id, nebo null když není registrovaný.</summary>
    public MiniAppDescriptor? Find(string id) => _byId.GetValueOrDefault(id);
}
