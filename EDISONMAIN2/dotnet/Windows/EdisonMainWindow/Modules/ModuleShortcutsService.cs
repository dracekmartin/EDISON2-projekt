using System.Collections.ObjectModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Sdílený zdroj pravdy pro plochu (dlaždice zástupců) - používá ho miniaplikace „Plocha"
    /// i strom (★ nad moduly hlavní nabídky). Plocha je nadmnožina oblíbených: drží položky všech
    /// <see cref="ShortcutKind"/>, ★ umí jen <see cref="ShortcutKind.MenuModule"/>.
    /// Čistě in-memory: členství + pozice (<see cref="DesktopShortcut.Col"/>/<see cref="DesktopShortcut.Row"/>) + <b>sdílené</b>
    /// <see cref="ExecutableModule"/> instance (tytéž jako ve stromu → flip <c>IsFavorite</c> se
    /// projeví všude). Persistenci a spouštění NEřeší - obsluhuje <c>EdisonMainViewModel</c> přes
    /// <see cref="LaunchRequested"/>, <see cref="ChangelogRequested"/>, <c>Items.CollectionChanged</c>
    /// a <see cref="PositionsChanged"/>.
    /// </summary>
    public sealed class ModuleShortcutsService
    {
        private IReadOnlyList<ExecutableModule> _all = [];

        // Cesta agendou do ToolTipu dlaždice. Delegát (ne mapa) - klíč `exe|forma|param` zná
        // EdisonMainViewModel, plocha ho nemusí umět skládat podruhé.
        private Func<ExecutableModule, string>? _treePathOf;

        // Uložené položky, které v aktuálním stromě neexistují (odebraná licence, hidden, mlineOnly).
        // Nezobrazují se, ale musí se uložit zpátky - jinak by je první změna členství tiše smazala.
        private readonly List<DesktopShortcutDto> _unresolved = [];

        /// <summary>Položky plochy v pořadí připnutí. Pozici dlaždice drží <c>Col</c>/<c>Row</c>.</summary>
        public ObservableCollection<DesktopShortcut> Items { get; } = [];

        /// <summary>
        /// Šířka mřížky pro <b>přidělování</b> pozice nově přidaným položkám (★ ve stromu, zápis
        /// agendy do Caché) - rozložení samo je absolutní a tohle nečte. Miniaplikace ji dorovnává
        /// podle své šířky; default platí, dokud plocha není otevřená.
        /// </summary>
        public int GridColumns { get; set; } = 6;

        /// <summary>
        /// Miniaplikace si vyžádá spuštění položky; obsluhuje <c>EdisonMainViewModel</c>
        /// (usage + overlay + <c>WindowNavigator</c>) - launch logika není duplikovaná.
        /// </summary>
        public event Action<DesktopShortcut>? LaunchRequested;

        /// <summary>
        /// Miniaplikace si vyžádá novinky položky; obsluhuje <c>EdisonMainViewModel</c>
        /// (vynucené zobrazení mimo auto-popup).
        /// </summary>
        public event Action<DesktopShortcut>? ChangelogRequested;

        /// <summary>Změna pozice dlaždice (<see cref="PlaceAt"/>) - shell persistuje.</summary>
        public event Action? PositionsChanged;

        public void RequestLaunch(DesktopShortcut shortcut) => LaunchRequested?.Invoke(shortcut);

        public void RequestChangelog(DesktopShortcut shortcut) => ChangelogRequested?.Invoke(shortcut);

        /// <summary>
        /// Re-inicializace po (pře)stavbě stromů (login, přepnutí firmy). Moduly hlavní nabídky se
        /// dohledávají v aktuálních stromech (sdílené instance, ne kopie); ostatní druhy si nesou
        /// vše v uložené položce. Chybějící pozice (<c>Col</c>/<c>Row</c>) → z pořadí.
        /// </summary>
        public void Seed(IEnumerable<ExecutableModule> allExecutables, IEnumerable<DesktopShortcutDto> stored,
            Func<ExecutableModule, string>? treePathOf = null)
        {
            _all = allExecutables.ToList();
            _treePathOf = treePathOf;
            foreach (var item in Items)
                if (item.Module is { } module) module.IsFavorite = false;
            Items.Clear();
            _unresolved.Clear();

            var shown = new List<(DesktopShortcutDto Dto, ShortcutKind Kind, ExecutableModule? Module)>();
            var keys = new HashSet<string>();
            foreach (var dto in stored)
            {
                // Uznáváme jen jméno člena: Enum.TryParse bere i číselné stringy ("0" → MenuModule,
                // "7" → nedefinovaná hodnota), což by u numericky serializovaného legacy ItemType
                // přeznačilo uložená data. Neznámý druh se jen přenese dál (novější verze, ruční edit).
                if (!Enum.IsDefined(typeof(ShortcutKind), dto.Kind))
                {
                    _unresolved.Add(dto);
                    continue;
                }
                var kind = Enum.Parse<ShortcutKind>(dto.Kind);

                var module = kind == ShortcutKind.MenuModule ? Resolve(dto.ElementId) : null;
                if (kind == ShortcutKind.MenuModule && module is null)
                {
                    _unresolved.Add(dto);
                    continue;
                }
                if (!keys.Add($"{kind}|{dto.ElementId}|{dto.Id}")) continue;
                shown.Add((dto, kind, module));
            }

            // 2) pozice: nejdřív rezervuj pozice skrytých položek, ať je zobrazené nepřeberou - jinak
            // by po návratu licence seděly dvě dlaždice na sobě (mřížka kolize neřeší).
            var used = new HashSet<(int Col, int Row)>(_unresolved.Select(CellOf).OfType<(int, int)>());
            int order = 0;
            foreach (var (dto, kind, module) in shown)
            {
                var cell = CellOf(dto) ?? CellOfOrder(order);
                while (!used.Add(cell)) cell = Next(cell);

                if (module is not null) module.IsFavorite = true;
                Items.Add(new DesktopShortcut
                {
                    Kind = kind,
                    Id = dto.Id,
                    ElementId = dto.ElementId,
                    DisplayedName = module?.DisplayedName ?? dto.Name,
                    DisplayedLongName = module?.DisplayedLongName ?? dto.Name,
                    TreePath = TreePathOf(module, module?.DisplayedLongName ?? dto.Name),
                    ExecutablePath = module?.ExecutablePath ?? dto.Exe,
                    FormName = module?.FormName ?? dto.Form,
                    InputParam = module?.InputParam ?? dto.Input,
                    Report = dto.Report,
                    Module = module,
                    Col = cell.Col,
                    Row = cell.Row,
                });
                order++;
            }
        }

        /// <summary>Vše k uložení: zobrazené položky + uložené, které aktuální strom nezná.</summary>
        public IEnumerable<DesktopShortcutDto> Persistable() =>
            Items.Select(i => i.ToDto()).Concat(_unresolved);

        /// <summary>Je modul hlavní nabídky na ploše? (řídí ★ ve stromu)</summary>
        public bool Contains(string elementId) =>
            Items.Any(i => i.Kind == ShortcutKind.MenuModule && i.ElementId == elementId);

        /// <summary>Najde instanci modulu podle ElementId napříč všemi stromy (pro DnD drop).</summary>
        public ExecutableModule? Resolve(string elementId) =>
            _all.FirstOrDefault(e => e.ElementId == elementId);

        // Cesta agendou pro ToolTip dlaždice; položka mimo strom (sestavy) padá na svůj název.
        private string TreePathOf(ExecutableModule? module, string fallback)
        {
            var path = module is null || _treePathOf is null ? "" : _treePathOf(module);
            return path.Length > 0 ? path : fallback;
        }

        /// <summary>Přidá modul hlavní nabídky na plochu (idempotentní).</summary>
        public void Add(ExecutableModule module)
        {
            if (module.ElementId.Length == 0 || Contains(module.ElementId)) return;
            module.IsFavorite = true;
            var shortcut = DesktopShortcut.ForModule(module, TreePathOf(module, module.DisplayedLongName));
            (shortcut.Col, shortcut.Row) = NextFreeCell();
            Items.Add(shortcut);
        }

        /// <summary>Odebere z plochy zástupce daného modulu hlavní nabídky.</summary>
        public void Remove(ExecutableModule module)
        {
            if (Items.FirstOrDefault(i => i.Kind == ShortcutKind.MenuModule && i.ElementId == module.ElementId)
                is not { } existing) return;
            Remove(existing);
        }

        public void Remove(DesktopShortcut shortcut)
        {
            if (Items.FirstOrDefault(i => i.Key == shortcut.Key) is not { } existing) return;
            if (existing.Module is { } module) module.IsFavorite = false;
            Items.Remove(existing);
        }

        /// <summary>
        /// Umístí dlaždici na danou buňku (mřížka s mezerami). Když je buňka obsazená, prohodí ji
        /// s dosavadní pozicí přesouvané dlaždice (swap).
        /// </summary>
        public void PlaceAt(DesktopShortcut shortcut, int col, int row)
        {
            // Cílovou dlaždici hledej v kolekci podle Key (jako Remove) - předaná instance může být
            // z doby před posledním Seed a zápis do ní by se nikam neprojevil.
            if (col < 0 || row < 0 || Items.FirstOrDefault(i => i.Key == shortcut.Key) is not { } target
                || (target.Col == col && target.Row == row)) return;
            if (Items.FirstOrDefault(i => i.Col == col && i.Row == row && i.Key != target.Key) is { } occupant)
                (occupant.Col, occupant.Row) = (target.Col, target.Row);
            (target.Col, target.Row) = (col, row);
            PositionsChanged?.Invoke();
        }

        /// <summary>
        /// Nejnižší volná buňka row-major v rámci <see cref="GridColumns"/> (kompaktní zaplnění
        /// mezer při přidání). Počítá i pozice skrytých položek - ty se ukládají se svou pozicí
        /// a po návratu by kolidovaly.
        /// </summary>
        private (int Col, int Row) NextFreeCell()
        {
            var used = Items.Select(i => (i.Col, i.Row)).Concat(_unresolved.Select(CellOf).OfType<(int, int)>())
                .ToHashSet();
            var cell = (Col: 0, Row: 0);
            while (used.Contains(cell)) cell = Next(cell);
            return cell;
        }

        // Uložená pozice jen když ji položka nese celou - půlka souřadnice je horší než pořadí.
        private static (int Col, int Row)? CellOf(DesktopShortcutDto dto) =>
            dto is { Col: { } c, Row: { } r } && c >= 0 && r >= 0 ? (c, r) : null;

        private (int Col, int Row) CellOfOrder(int order) =>
            (order % Math.Max(1, GridColumns), order / Math.Max(1, GridColumns));

        private (int Col, int Row) Next((int Col, int Row) cell) =>
            cell.Col + 1 < Math.Max(1, GridColumns) ? (cell.Col + 1, cell.Row) : (0, cell.Row + 1);
    }
}
