using System.ComponentModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Druh položky plochy. Zrcadlí legacy <c>DesktopShortcutItemType</c>
    /// (<c>EDCORE/EDFormInterfaceLic.vb</c>) - plocha je nadmnožina oblíbených: ★ ve stromu umí
    /// jen <see cref="MenuModule"/>, ostatní druhy vznikají v samotném modulu („umístit na plochu").
    /// </summary>
    public enum ShortcutKind
    {
        /// <summary>Modul hlavní nabídky (legacy FormularHlavniNabidky) - jediný druh, který dnes umí ★.</summary>
        MenuModule,
        /// <summary>Forma balíku mimo hlavní nabídku (legacy FormularDanehoBaliku).</summary>
        PackageForm,
        /// <summary>Tisková sestava bez výběru parametrů (legacy SestavaTiskova, CallType=Procedura).</summary>
        PrintReport,
        /// <summary>Sestava s výběrem parametrů / uloženou předvolbou (legacy SestavaVyberova).</summary>
        SelectReport,
        /// <summary>Uložený pohled datového skladu (legacy PohledDatovehoSkladu).</summary>
        DwhView,
    }

    /// <summary>
    /// Doplňkový payload sestav a pohledů DS - 1:1 legacy <c>ReportShortcut</c>
    /// (<c>EDLIBRARY/clsForm.vb</c>). Server z něj dohledá uložené parametry tisku
    /// (<c>GetIndexGlobaluPodleUlozenychParametru</c>), proto se pole nesmí přeskládat ani slučovat.
    /// </summary>
    public record ReportPayload(
        string Agenda = "",
        string Oblast = "",
        string TypSestavy = "",
        string Sestava = "",
        string Predvolba = "",
        bool JeVerejny = false);

    /// <summary>
    /// Položka plochy (dlaždice). <see cref="Key"/> je ekvivalent legacy čtveřice
    /// <c>(Form, ID, CallType, ItemType)</c> - <c>CallType</c> je odvoditelný z <see cref="Kind"/>.
    /// Záměrně obyčejná třída (ne record): porovnává se přes <see cref="Key"/>, hodnotová rovnost
    /// by u dvou různých dlaždic téhož modulu mátla.
    /// </summary>
    public sealed class DesktopShortcut : INotifyPropertyChanged
    {
        public ShortcutKind Kind { get; init; } = ShortcutKind.MenuModule;

        /// <summary>ID položky v rámci hostitelské formy; u <see cref="ShortcutKind.MenuModule"/> == <see cref="ElementId"/>.</summary>
        public string Id { get; init; } = "";

        /// <summary>Hostitelská forma (legacy <c>Form</c>) = numerické EDISFormEnum id; zároveň klíč licenčního gate.</summary>
        public string ElementId { get; init; } = "";

        public string DisplayedName { get; init; } = "";

        public string ExecutablePath { get; init; } = "";

        public string FormName { get; init; } = "";

        /// <summary>Legacy <c>InputProFrm</c> - parametr předaný formě jako <c>EDParamIn</c>.</summary>
        public string InputParam { get; init; } = "";

        /// <summary>Jen pro sestavy a pohledy DS; u ostatních druhů <c>null</c>.</summary>
        public ReportPayload? Report { get; init; }

        /// <summary>
        /// Sdílená instance uzlu stromu - neprázdná právě pro <see cref="ShortcutKind.MenuModule"/>,
        /// který se podařilo dohledat ve stromu. Díky sdílení se ★ ve stromu a dlaždice drží v sync.
        /// </summary>
        public ExecutableModule? Module { get; init; }

        /// <summary>Identita položky napříč druhy (dva různé druhy nad týmž modulem koexistují).</summary>
        public string Key => $"{Kind}|{ElementId}|{Id}";

        private int _slot;

        /// <summary>
        /// Pozice dlaždice v mřížce (0-based, prázdné sloty = mezery). Řídí
        /// <see cref="ModuleShortcutsService"/>; observable - změna přeskládá mřížku.
        /// </summary>
        public int Slot
        {
            get => _slot;
            set
            {
                if (_slot == value) return;
                _slot = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Slot)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Zástupce modulu hlavní nabídky (přidání přes ★ nebo tažením ze stromu).</summary>
        public static DesktopShortcut ForModule(ExecutableModule module) => new()
        {
            Kind = ShortcutKind.MenuModule,
            Id = module.ElementId,
            ElementId = module.ElementId,
            DisplayedName = module.DisplayedName,
            ExecutablePath = module.ExecutablePath,
            FormName = module.FormName,
            InputParam = module.InputParam,
            Module = module,
        };

        public DesktopShortcutDto ToDto() => new(Kind.ToString(), Id, ElementId, DisplayedName,
            ExecutablePath, FormName, InputParam, Slot, Report);
    }

    /// <summary>
    /// Serializovaná podoba položky plochy (blob <c>Shortcuts</c> ve scope <c>EDISONMAIN2</c>).
    /// <c>Kind</c> je string, ne enum - přeskládání <see cref="ShortcutKind"/> nesmí přeznačit uložená data.
    /// Chybějící <c>Slot</c> = doplní se z pořadí.
    /// </summary>
    public record DesktopShortcutDto(
        string Kind = nameof(ShortcutKind.MenuModule),
        string Id = "",
        string ElementId = "",
        string Name = "",
        string Exe = "",
        string Form = "",
        string Input = "",
        int? Slot = null,
        ReportPayload? Report = null);
}
