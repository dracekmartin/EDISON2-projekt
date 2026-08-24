using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.Windows.MiniAppPickerWindow
{
    /// <summary>Jedna položka nabídky - skupina zůstává jen popiskem, seznam je plochý.</summary>
    public sealed record MiniAppChoice(MiniAppDescriptor Descriptor, string GroupTitle)
    {
        public string Title => Descriptor.Title;
    }

    /// <summary>
    /// Výběr miniaplikace z jednoúrovňového seznamu. Dřív se vybíralo z vnořeného submenu
    /// seskupeného per provider; seskupení tu zbylo jen jako popisek řádku.
    /// </summary>
    public class MiniAppPickerViewModel : EDViewModel
    {
        public MiniAppPickerViewModel(MiniAppCatalog catalog) : this(EDServices.Current, catalog) { }

        /// <summary>DI/test overload - vlastní <c>EDServices</c> místo <c>EDServices.Current</c>.</summary>
        public MiniAppPickerViewModel(EDServices services, MiniAppCatalog catalog) : base(services)
        {
            Choices = [.. catalog.Groups.SelectMany(g => g.Apps.Select(a => new MiniAppChoice(a, g.Title)))];
            Selected = Choices.FirstOrDefault();
        }

        /// <summary>Pořadí = pořadí registrace providerů, uvnitř skupiny pořadí deskriptorů.</summary>
        public IReadOnlyList<MiniAppChoice> Choices { get; }

        public MiniAppChoice? Selected { get; set; }

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
