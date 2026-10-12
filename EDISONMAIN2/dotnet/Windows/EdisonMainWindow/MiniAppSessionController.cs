using System;
using System.Collections.Generic;
using System.Linq;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    /// <summary>
    /// Čí je stav docku v <c>local.json</c>: stanice (Windows účet) × server × firma × uživatel EDISONu - jinak
    /// by na sdíleném terminálu jeden uživatel rozházel dock druhému. Soubory z dřívějška mají klíč jen IDFa.
    /// </summary>
    internal readonly record struct DockScope(string Server, int Port, string Idfa, string Login)
    {
        // Malá písmena: host i login se píšou v různé velikosti a pořád jde o tutéž session.
        public string Key => $"{Server}:{Port}|{Idfa}|{Login}".ToLowerInvariant();

        /// <summary>Klíč z doby, kdy byl stav docku jen per IDFa - čte se jako fallback, nezapisuje.</summary>
        public string LegacyKey => Idfa;
    }

    /// <summary>Rozměr panelu ve view: čte se při uložení layoutu, nastavuje při obnově.</summary>
    internal readonly record struct PanelSize(Func<double> Get, Action<double> Apply);

    /// <summary>
    /// Miniaplikace shellu napříč session: docking host, layout per <see cref="DockScope"/> v <see cref="ILocalPrefs"/>
    /// (spolu s šířkou stromu a výškou pruhu podpory) a výměna docku při přepnutí firmy nebo uživatele.
    /// </summary>
    internal sealed class MiniAppSessionController(ILocalPrefs prefs, MiniAppCatalog catalog)
    {
        // Manager žije až po AttachHost (potřebuje DockingManager z view) - do té doby null.
        private MiniAppManager? _manager;
        // Šířka stromu (ColumnDefinition) a výška pruhu podpory (RowDefinition) ve view.
        private PanelSize? _treeWidth;
        private PanelSize? _supportHeight;

        // Session, jejíž layout dock drží; null = dock vyprázdněný výměnou session. Persist jde sem,
        // ne na aktuální session: během rebuildu po přepnutí je Idfa už nová a dock prázdný, takže
        // zavření shellu (nebo force logout) by jí přepsalo uložený layout.
        private DockScope? _dockScope;

        /// <summary>Pod-okna docku, do kterých jde nová miniaplikace umístit (výběr v pickeru).</summary>
        internal IReadOnlyList<MiniAppTarget> Targets => _manager?.Targets ?? [];

        /// <summary>
        /// Napojí docking host a rozměry panelů z view a obnoví uložený layout session. Rozměry se persistují
        /// s layoutem, bez nich zůstane XAML default.
        /// </summary>
        internal void AttachHost(IMiniAppHost host, DockScope scope, PanelSize? treeWidth = null, PanelSize? supportHeight = null)
        {
            // Před prvním restore, který rozměry hned aplikuje.
            _treeWidth = treeWidth;
            _supportHeight = supportHeight;
            _manager = new MiniAppManager(host, catalog);
            Restore(scope);
        }

        internal void Open(MiniAppDescriptor descriptor, MiniAppPlacement? placement = null) =>
            _manager?.Open(descriptor, placement);

        // Plocha zůstává - zavřít ji nejde a prázdný dock nemá uživateli co dát.
        internal void CloseClosable() => _manager?.CloseClosable();

        /// <summary>Zavře vše (proběhne OnClosed úklid miniaplikací) a otevře výchozí sadu.</summary>
        internal void Reset()
        {
            _manager?.CloseAll();
            OpenDefault();
        }

        /// <summary>
        /// Při zavření shellu: zavře miniaplikace bez uložení (to dělá <see cref="Persist"/> předem) a odpojí host,
        /// takže pozdější <see cref="Restore"/> nic neotevře.
        /// </summary>
        internal void CloseAll()
        {
            // Shell se dá zavřít i uprostřed přepnutí (force logout během login dialogu), které pak dock obnovuje.
            _manager?.CloseAll();
            _manager = null;
        }

        // Připnuté miniaplikace (Plocha) v pořadí registrace; id zná jen jejich provider.
        private IEnumerable<MiniAppDescriptor> Pinned => catalog.Groups.SelectMany(g => g.Apps).Where(d => d.Pinned);

        // Výchozí rozložení: připnuté (dnes jen Plocha) přes celou plochu docku. Podpora tu
        // není - kanál IT → uživatelé nese fixní pruh v shellu, mimo dock.
        private void OpenDefault() => _manager?.OpenDefaultLayout([.. Pinned], bottomRow: null);

        // Připnuté musí být otevřené vždy - uložený layout je mít nemusí (pochází z verze, kde Plocha šla
        // zavřít). Bez ní by dock zůstal prázdný a uživatel by o dlaždice se zástupci přišel.
        private void EnsurePinnedOpen()
        {
            if (_manager is null) return;
            foreach (var pinned in Pinned)
                if (!_manager.OpenInstances.Any(i => i.DescriptorId == pinned.Id))
                    _manager.Open(pinned);
        }

        /// <summary>Uloží layout docku (a rozměry panelů) pod session, jejíž layout dock drží.</summary>
        internal void Persist()
        {
            if (_manager is null || _dockScope is not { } scope) return;
            // Best-effort: serializace layoutu nesmí zablokovat zavření shellu / přepnutí firmy.
            try
            {
                (prefs.MiniAppLayouts ??= new())[scope.Key] = _manager.SaveLayout();
                if (_treeWidth is { } tree)
                    (prefs.TreeWidths ??= new())[scope.Key] = tree.Get();
                if (_supportHeight is { } support)
                    (prefs.SupportStripHeights ??= new())[scope.Key] = support.Get();
                prefs.Save();
            }
            catch { }
        }

        /// <summary>Obnoví uložený layout session; bez něj (nebo s poškozeným) výchozí rozložení.</summary>
        internal void Restore(DockScope scope)
        {
            if (_manager is null) return;
            _dockScope = scope;
            // Rozměry panelů (nezávislé na tom, zda je uložený layout miniaplikací) - bez uložené
            // hodnoty zůstane XAML default. Aplikovat před RestoreLayout, ať AvalonDock
            // deserializuje do už správně velké plochy a nepřepočítává pane dvakrát.
            if (_treeWidth is { } tree && Find(prefs.TreeWidths, scope, out var width, out _) && width > 0)
                tree.Apply(width);
            if (_supportHeight is { } support && Find(prefs.SupportStripHeights, scope, out var height, out _) && height > 0)
                support.Apply(height);
            if (!Find(prefs.MiniAppLayouts, scope, out var xml, out var xmlKey) || string.IsNullOrWhiteSpace(xml))
            {
                // Žádný uložený layout (první spuštění / nová firma) → výchozí rozložení.
                OpenDefault();
                return;
            }
            try
            {
                _manager.RestoreLayout(xml);
                EnsurePinnedOpen();
            }
            catch
            {
                // Poškozený / nekompatibilní uložený layout nesmí shodit start shellu (letí z Loaded).
                // Zahodíme ho (i ten ze starého klíče); další uložení (close/switch) zapíše čistý.
                prefs.MiniAppLayouts?.Remove(xmlKey);
                prefs.Save();
                // Po zahozeném layoutu postavit výchozí, ať dock nezůstane prázdný. Vlastní guard -
                // stav docku po nezdařeném Deserialize je nedefinovaný a druhý pád by shodil start.
                try { OpenDefault(); }
                catch { }
            }
        }

        /// <summary>Výměna session: uloží layout session v docku a dock vyprázdní; obnovu dělá volající po rebuildu.</summary>
        internal void CloseSession()
        {
            if (_manager is null) return;
            Persist();
            _manager.CloseAll();
            _dockScope = null;
        }

        // Nový klíč, jinak starý per IDFa (soubor z doby před klíčem session) - po prvním uložení platí nový.
        private static bool Find<T>(Dictionary<string, T>? map, DockScope scope, out T value, out string key)
        {
            key = scope.Key;
            if (map is not null && map.TryGetValue(key, out value!)) return true;
            key = scope.LegacyKey;
            if (map is not null && map.TryGetValue(key, out value!)) return true;
            value = default!;
            return false;
        }
    }
}
