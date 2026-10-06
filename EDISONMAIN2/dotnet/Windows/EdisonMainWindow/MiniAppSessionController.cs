using System;
using System.Collections.Generic;
using System.Linq;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    /// <summary>
    /// Miniaplikace shellu napříč session: docking host, layout per IDFa v <see cref="ILocalPrefs"/>
    /// (spolu s šířkou stromu a výškou pruhu podpory) a výměna docku při přepnutí firmy nebo uživatele.
    /// </summary>
    internal sealed class MiniAppSessionController(ILocalPrefs prefs, MiniAppCatalog catalog)
    {
        // Manager žije až po AttachHost (potřebuje DockingManager z view) - do té doby null.
        private MiniAppManager? _manager;
        // Přístup k šířce stromu (ColumnDefinition ve view) - registruje code-behind přes AttachTreeLayout.
        private Func<double>? _getTreeWidth;
        private Action<double>? _applyTreeWidth;
        // Výška pruhu podpory (RowDefinition ve view) - registruje code-behind přes AttachSupportLayout.
        private Func<double>? _getSupportHeight;
        private Action<double>? _applySupportHeight;

        // Firma, jejíž layout dock drží; null = dock vyprázdněný výměnou session. Persist jde sem,
        // ne na aktuální Idfa: během rebuildu po přepnutí je Idfa už nová a dock prázdný, takže
        // zavření shellu (nebo force logout) by jí přepsalo uložený layout.
        private string? _dockIdfa;

        /// <summary>Pod-okna docku, do kterých jde nová miniaplikace umístit (výběr v pickeru).</summary>
        internal IReadOnlyList<MiniAppTarget> Targets => _manager?.Targets ?? [];

        /// <summary>Napojí docking host z view a obnoví uložený layout firmy.</summary>
        internal void AttachHost(IMiniAppHost host, string idfa)
        {
            _manager = new MiniAppManager(host, catalog);
            Restore(idfa);
        }

        /// <summary>
        /// Přístup k šířce stromu z view. Volat <b>před</b> <see cref="AttachHost"/> - ten spouští
        /// první restore, který už šířku aplikuje.
        /// </summary>
        internal void AttachTreeLayout(Func<double> getWidth, Action<double> applyWidth)
        {
            _getTreeWidth = getWidth;
            _applyTreeWidth = applyWidth;
        }

        /// <summary>
        /// Přístup k výšce pruhu podpory z view. Volat <b>před</b> <see cref="AttachHost"/> - ten
        /// spouští první restore, který už výšku aplikuje.
        /// </summary>
        internal void AttachSupportLayout(Func<double> getHeight, Action<double> applyHeight)
        {
            _getSupportHeight = getHeight;
            _applySupportHeight = applyHeight;
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

        /// <summary>Při zavření shellu: zavře miniaplikace bez uložení (to dělá <see cref="Persist"/> předem).</summary>
        internal void CloseAll() => _manager?.CloseAll();

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

        /// <summary>Uloží layout docku (a rozměry panelů) pod firmu, jejíž layout dock drží.</summary>
        internal void Persist()
        {
            if (_manager is null || _dockIdfa is not { } idfa) return;
            // Best-effort: serializace layoutu nesmí zablokovat zavření shellu / přepnutí firmy.
            try
            {
                (prefs.MiniAppLayouts ??= new())[idfa] = _manager.SaveLayout();
                if (_getTreeWidth is not null)
                    (prefs.TreeWidths ??= new())[idfa] = _getTreeWidth();
                if (_getSupportHeight is not null)
                    (prefs.SupportStripHeights ??= new())[idfa] = _getSupportHeight();
                prefs.Save();
            }
            catch { }
        }

        /// <summary>Obnoví uložený layout firmy; bez něj (nebo s poškozeným) výchozí rozložení.</summary>
        internal void Restore(string idfa)
        {
            if (_manager is null) return;
            _dockIdfa = idfa;
            // Rozměry panelů (nezávislé na tom, zda je uložený layout miniaplikací) - bez uložené
            // hodnoty zůstane XAML default. Aplikovat před RestoreLayout, ať AvalonDock
            // deserializuje do už správně velké plochy a nepřepočítává pane dvakrát.
            if (_applyTreeWidth is not null && prefs.TreeWidths?.TryGetValue(idfa, out var width) == true && width > 0)
                _applyTreeWidth(width);
            if (_applySupportHeight is not null && prefs.SupportStripHeights?.TryGetValue(idfa, out var height) == true && height > 0)
                _applySupportHeight(height);
            if (prefs.MiniAppLayouts?.TryGetValue(idfa, out var xml) != true || string.IsNullOrWhiteSpace(xml))
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
                // Zahodíme ho; další uložení (close/switch) zapíše čistý.
                prefs.MiniAppLayouts?.Remove(idfa);
                prefs.Save();
                // Po zahozeném layoutu postavit výchozí, ať dock nezůstane prázdný. Vlastní guard -
                // stav docku po nezdařeném Deserialize je nedefinovaný a druhý pád by shodil start.
                try { OpenDefault(); }
                catch { }
            }
        }

        /// <summary>Výměna session: uloží layout firmy v docku a dock vyprázdní; obnovu dělá volající po rebuildu.</summary>
        internal void CloseSession()
        {
            if (_manager is null) return;
            Persist();
            _manager.CloseAll();
            _dockIdfa = null;
        }
    }
}
