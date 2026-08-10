using System.Collections.ObjectModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Sdílený zdroj pravdy pro plochu (dlaždice zástupců) - používá ho miniaplikace „Plocha"
    /// i strom (★ nad moduly hlavní nabídky). Plocha je nadmnožina oblíbených: drží položky všech
    /// <see cref="ShortcutKind"/>, ★ umí jen <see cref="ShortcutKind.MenuModule"/>.
    /// Čistě in-memory: členství + pozice (<see cref="DesktopShortcut.Slot"/>) + <b>sdílené</b>
    /// <see cref="ExecutableModule"/> instance (tytéž jako ve stromu → flip <c>IsFavorite</c> se
    /// projeví všude). Persistenci a spouštění NEřeší - obsluhuje <c>EdisonMainViewModel</c> přes
    /// <see cref="LaunchRequested"/>, <c>Items.CollectionChanged</c> a <see cref="SlotsChanged"/>.
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

        /// <summary>Položky plochy v pořadí připnutí. Pozici dlaždice drží <see cref="DesktopShortcut.Slot"/>.</summary>
        public ObservableCollection<DesktopShortcut> Items { get; } = [];

        /// <summary>
        /// Miniaplikace si vyžádá spuštění položky; obsluhuje <c>EdisonMainViewModel</c>
        /// (usage + overlay + <c>WindowNavigator</c>) - launch logika není duplikovaná.
        /// </summary>
        public event Action<DesktopShortcut>? LaunchRequested;

        /// <summary>Změna pozice dlaždice (<see cref="PlaceAt"/>) - shell persistuje.</summary>
        public event Action? SlotsChanged;

        public void RequestLaunch(DesktopShortcut shortcut) => LaunchRequested?.Invoke(shortcut);

        /// <summary>
        /// Re-inicializace po (pře)stavbě stromů (login, přepnutí firmy). Moduly hlavní nabídky se
        /// dohledávají v aktuálních stromech (sdílené instance, ne kopie); ostatní druhy si nesou
        /// vše v uložené položce. Chybějící <see cref="DesktopShortcutDto.Slot"/> → pořadí.
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

            // 1) roztřídit na zobrazitelné a jen přenášené (skryté) položky
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

            // 2) sloty: nejdřív rezervuj pozice skrytých položek, ať je zobrazené nepřeberou - jinak
            // by po návratu licence seděly dvě dlaždice na sobě (mřížka kolize neřeší).
            var used = new HashSet<int>(_unresolved.Select(d => d.Slot ?? -1));
            int order = 0;
            foreach (var (dto, kind, module) in shown)
            {
                int slot = dto.Slot ?? order;
                while (!used.Add(slot)) slot++;

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
                    Slot = slot,
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
            shortcut.Slot = NextFreeSlot();
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
        /// Umístí dlaždici na daný slot (mřížka s mezerami). Když je slot obsazený, prohodí ho
        /// s dosavadní pozicí přesouvané dlaždice (swap).
        /// </summary>
        public void PlaceAt(DesktopShortcut shortcut, int slot)
        {
            // Cílovou dlaždici hledej v kolekci podle Key (jako Remove) - předaná instance může být
            // z doby před posledním Seed a zápis do ní by se nikam neprojevil.
            if (slot < 0 || Items.FirstOrDefault(i => i.Key == shortcut.Key) is not { } target
                || target.Slot == slot) return;
            if (Items.FirstOrDefault(i => i.Slot == slot && i.Key != target.Key) is { } occupant)
                occupant.Slot = target.Slot;
            target.Slot = slot;
            SlotsChanged?.Invoke();
        }

        /// <summary>
        /// Nejnižší volný slot (kompaktní zaplnění mezer při přidání). Počítá i pozice skrytých
        /// položek - ty se ukládají se svým slotem a po návratu by kolidovaly.
        /// </summary>
        private int NextFreeSlot()
        {
            var used = Items.Select(i => i.Slot).Concat(_unresolved.Select(d => d.Slot ?? -1)).ToHashSet();
            int i = 0;
            while (used.Contains(i)) i++;
            return i;
        }
    }
}
