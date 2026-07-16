using System.Collections.ObjectModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Sdílený zdroj pravdy pro oblíbené moduly — používá ho strom (hvězdička ★) i dockovatelná
    /// miniaplikace „Oblíbené" (dlaždice). Čistě in-memory: členství + <b>sdílené</b>
    /// <see cref="ExecutableModule"/> instance (tytéž jako ve stromu → flip <c>IsFavorite</c> se
    /// projeví všude) + pozice dlaždic (<see cref="ExecutableModule.FavoriteSlot"/>, mřížka s mezerami).
    /// Persistenci a spouštění NEřeší — to obsluhuje <c>EdisonMainViewModel</c> přes
    /// <see cref="LaunchRequested"/>, <c>Favorites.CollectionChanged</c> a <see cref="SlotsChanged"/>.
    /// </summary>
    public sealed class ModuleShortcutsService
    {
        private IReadOnlyList<ExecutableModule> _all = [];

        /// <summary>Oblíbené (členství). Pozice dlaždice drží <see cref="ExecutableModule.FavoriteSlot"/>.</summary>
        public ObservableCollection<ExecutableModule> Favorites { get; } = [];

        /// <summary>
        /// Miniaplikace si vyžádá spuštění modulu; obsluhuje <c>EdisonMainViewModel</c>
        /// (usage + overlay + <c>WindowNavigator</c>) — launch logika není duplikovaná.
        /// </summary>
        public event Action<ExecutableModule>? LaunchRequested;

        /// <summary>Změna pozice dlaždice (<see cref="PlaceAt"/>) — shell persistuje sloty.</summary>
        public event Action? SlotsChanged;

        public void RequestLaunch(ExecutableModule module) => LaunchRequested?.Invoke(module);

        /// <summary>
        /// Re-inicializace po (pře)stavbě stromů (login, přepnutí firmy). Bere instance z aktuálních
        /// stromů (sdílené, ne kopie). Sloty z <paramref name="slotsByElementId"/>; chybějící → pořadí.
        /// </summary>
        public void Seed(IEnumerable<ExecutableModule> allExecutables, IEnumerable<string> favoriteIdsInOrder,
                         IReadOnlyDictionary<string, int>? slotsByElementId = null)
        {
            _all = allExecutables.ToList();
            foreach (var m in Favorites) m.IsFavorite = false;
            Favorites.Clear();
            int order = 0;
            foreach (var id in favoriteIdsInOrder)
                if (!Contains(id) && Resolve(id) is { } module)
                {
                    module.IsFavorite = true;
                    module.FavoriteSlot = slotsByElementId?.GetValueOrDefault(id) ?? order;
                    Favorites.Add(module);
                    order++;
                }
        }

        public bool Contains(string elementId) => Favorites.Any(f => f.ElementId == elementId);

        /// <summary>Najde instanci modulu podle ElementId napříč všemi stromy (pro DnD drop).</summary>
        public ExecutableModule? Resolve(string elementId) =>
            _all.FirstOrDefault(e => e.ElementId == elementId);

        public void Add(ExecutableModule module)
        {
            if (module.ElementId.Length == 0 || Contains(module.ElementId)) return;
            module.IsFavorite = true;
            module.FavoriteSlot = NextFreeSlot();
            Favorites.Add(module);
        }

        public void Remove(ExecutableModule module)
        {
            if (Favorites.FirstOrDefault(f => f.ElementId == module.ElementId) is not { } existing) return;
            existing.IsFavorite = false;
            Favorites.Remove(existing);
        }

        /// <summary>
        /// Umístí dlaždici na daný slot (mřížka s mezerami). Když je slot obsazený, prohodí ho
        /// s dosavadní pozicí přesouvané dlaždice (swap).
        /// </summary>
        public void PlaceAt(ExecutableModule module, int slot)
        {
            if (slot < 0 || !Contains(module.ElementId) || module.FavoriteSlot == slot) return;
            if (Favorites.FirstOrDefault(f => f.FavoriteSlot == slot && f != module) is { } occupant)
                occupant.FavoriteSlot = module.FavoriteSlot;
            module.FavoriteSlot = slot;
            SlotsChanged?.Invoke();
        }

        /// <summary>Nejnižší volný slot (kompaktní zaplnění mezer při přidání).</summary>
        private int NextFreeSlot()
        {
            var used = Favorites.Select(f => f.FavoriteSlot).ToHashSet();
            int i = 0;
            while (used.Contains(i)) i++;
            return i;
        }
    }
}
