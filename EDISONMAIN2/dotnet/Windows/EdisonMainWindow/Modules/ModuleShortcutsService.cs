using System.Collections.ObjectModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Sdílený zdroj pravdy pro oblíbené moduly — používá ho strom (hvězdička ★) i dockovatelná
    /// miniaplikace „Oblíbené" (dlaždice). Čistě in-memory: členství + pořadí + <b>sdílené</b>
    /// <see cref="ExecutableModule"/> instance (tytéž jako ve stromu → flip <c>IsFavorite</c> se
    /// projeví všude). Persistenci a spouštění NEřeší — to obsluhuje <c>EdisonMainViewModel</c>
    /// přes <see cref="LaunchRequested"/> a <c>Favorites.CollectionChanged</c>.
    /// </summary>
    public sealed class ModuleShortcutsService
    {
        private IReadOnlyList<ExecutableModule> _all = [];

        /// <summary>Oblíbené v pořadí připnutí (uspořádané). Instance sdílené se stromem.</summary>
        public ObservableCollection<ExecutableModule> Favorites { get; } = [];

        /// <summary>
        /// Miniaplikace si vyžádá spuštění modulu; obsluhuje <c>EdisonMainViewModel</c>
        /// (usage + overlay + <c>WindowNavigator</c>) — launch logika není duplikovaná.
        /// </summary>
        public event Action<ExecutableModule>? LaunchRequested;

        public void RequestLaunch(ExecutableModule module) => LaunchRequested?.Invoke(module);

        /// <summary>
        /// Re-inicializace po (pře)stavbě stromů (login, přepnutí firmy) — zachová pořadí z uložených
        /// ID. Bere instance z aktuálních stromů, ať jsou sdílené (ne kopie).
        /// </summary>
        public void Seed(IEnumerable<ExecutableModule> allExecutables, IEnumerable<string> favoriteIdsInOrder)
        {
            _all = allExecutables.ToList();
            foreach (var m in Favorites) m.IsFavorite = false;
            Favorites.Clear();
            foreach (var id in favoriteIdsInOrder)
                if (!Contains(id) && Resolve(id) is { } module)
                {
                    module.IsFavorite = true;
                    Favorites.Add(module);
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
            Favorites.Add(module);
        }

        public void Remove(ExecutableModule module)
        {
            if (Favorites.FirstOrDefault(f => f.ElementId == module.ElementId) is not { } existing) return;
            existing.IsFavorite = false;
            Favorites.Remove(existing);
        }

        public void Move(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || newIndex < 0 || oldIndex >= Favorites.Count || newIndex >= Favorites.Count) return;
            Favorites.Move(oldIndex, newIndex);
        }
    }
}
