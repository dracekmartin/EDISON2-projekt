using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Windows.MiniAppPickerWindow
{
    /// <summary>Jedna položka nabídky - skupina zůstává jen popiskem, seznam je plochý.</summary>
    public sealed record MiniAppChoice(MiniAppDescriptor Descriptor, string GroupTitle)
    {
        public string Title => Descriptor.Title;
    }

    /// <summary>Jedna volba umístění - popisek pro seznam a cíl pro <c>MiniAppManager.Open</c>.</summary>
    public sealed record MiniAppPlacementChoice(string Label, MiniAppPlacement Placement);

    /// <summary>
    /// Výběr miniaplikace z jednoúrovňového seznamu. Dřív se vybíralo z vnořeného submenu
    /// seskupeného per provider; seskupení tu zbylo jen jako popisek řádku.
    /// </summary>
    public class MiniAppPickerViewModel : EDViewModel
    {
        // Popisek pod-okna je výčet titulků jeho tabů; delší se ořízne, ať řádek neroste.
        private const int MaxListedTitles = 2;

        public MiniAppPickerViewModel(MiniAppCatalog catalog, IReadOnlyList<MiniAppTarget> targets)
            : this(EDServices.Current, catalog, targets) { }

        /// <summary>DI/test overload - vlastní <c>EDServices</c> místo <c>EDServices.Current</c>.</summary>
        public MiniAppPickerViewModel(EDServices services, MiniAppCatalog catalog, IReadOnlyList<MiniAppTarget> targets)
            : base(services)
        {
            // Pinned ven - co uživatel nezavře, není co otvírat (Plocha).
            Choices = [.. catalog.Groups.SelectMany(g => g.Apps.Where(a => !a.Pinned)
                                                               .Select(a => new MiniAppChoice(a, g.Title)))];
            Selected = Choices.FirstOrDefault();
            Placements = BuildPlacements(targets);
            SelectedPlacement = Placements.FirstOrDefault(p => p.Placement.Target?.IsActive == true)
                                ?? Placements.FirstOrDefault();
        }

        /// <summary>Pořadí = pořadí registrace providerů, uvnitř skupiny pořadí deskriptorů.</summary>
        public IReadOnlyList<MiniAppChoice> Choices { get; }

        public MiniAppChoice? Selected { get; set; }

        /// <summary>Nabídka umístění. Prázdná = není kam vybírat, panel vyplní celou plochu.</summary>
        public IReadOnlyList<MiniAppPlacementChoice> Placements { get; }

        /// <summary>False → sekce umístění se v okně vůbec nezobrazí.</summary>
        public bool HasPlacements => Placements.Count > 0;

        public MiniAppPlacementChoice? SelectedPlacement { get; set; }

        // Prázdné pod-okno vzniká jen jako poslední zbylé (AvalonDock ostatní prázdné odpojí),
        // takže bez obsazeného pod-okna je každá volba totéž co "vyplnit plochu" - nenabízet nic.
        private static IReadOnlyList<MiniAppPlacementChoice> BuildPlacements(IReadOnlyList<MiniAppTarget> targets)
        {
            var occupied = targets.Where(t => t.DocumentTitles.Count > 0).ToList();
            if (occupied.Count == 0) return [];
            return
            [
                .. occupied.Select(t => new MiniAppPlacementChoice(Describe(t), MiniAppPlacement.Into(t))),
                new("Nový sloupec vpravo", MiniAppPlacement.NewColumn),
                new("Nová řada dole", MiniAppPlacement.NewRow),
            ];
        }

        private static string Describe(MiniAppTarget target)
        {
            var titles = target.DocumentTitles;
            if (titles.Count <= MaxListedTitles) return string.Join(", ", titles);
            return $"{string.Join(", ", titles.Take(MaxListedTitles))} + {titles.Count - MaxListedTitles} další";
        }

        // Prázdný katalog nebo zrušený výběr nesmí dialog uzavřít jako potvrzený - volající
        // by pak otevíral null.
        protected override void OnOk()
        {
            if (Selected is null) return;
            base.OnOk();
        }

        // Okno se centruje na ownera, geometrie se nepersistuje (vzor ChangelogWindow) -
        // uložená pozice by se prala s centrováním nad maximalizovaným shellem.
        protected override string? PersistedFormId => null;
    }
}
