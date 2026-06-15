namespace EDControls.MiniApps;

/// <summary>
/// Agreguje <see cref="MiniAppDescriptor"/> ze všech registrovaných <see cref="IMiniAppProvider"/>.
/// Lookup podle <see cref="MiniAppDescriptor.Id"/> (prefix <c>ContentId</c> při obnově layoutu).
/// </summary>
public sealed class MiniAppCatalog
{
    private readonly Dictionary<string, MiniAppDescriptor> _byId;

    private MiniAppCatalog(Dictionary<string, MiniAppDescriptor> byId) => _byId = byId;

    /// <summary>
    /// Posbírá deskriptory ze všech providerů. <see cref="MiniAppDescriptor.Id"/> musí být unikátní
    /// napříč všemi providery — duplicita je chyba registrace, vyhodí výjimku (fail fast).
    /// </summary>
    public static MiniAppCatalog Build(IEnumerable<IMiniAppProvider> providers)
    {
        var byId = new Dictionary<string, MiniAppDescriptor>();
        foreach (var provider in providers)
            foreach (var descriptor in provider.GetMiniApps())
                if (!byId.TryAdd(descriptor.Id, descriptor))
                    throw new InvalidOperationException(
                        $"Duplicitní MiniAppDescriptor.Id \"{descriptor.Id}\" — Id musí být unikátní napříč všemi providery.");
        return new MiniAppCatalog(byId);
    }

    /// <summary>Všechny registrované mini-apps.</summary>
    public IReadOnlyCollection<MiniAppDescriptor> All => _byId.Values;

    /// <summary>Najde deskriptor podle Id, nebo null když není registrovaný.</summary>
    public MiniAppDescriptor? Find(string id) => _byId.GetValueOrDefault(id);
}
