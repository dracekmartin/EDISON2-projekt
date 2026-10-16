using System.ComponentModel;
using EDISONMAIN2.Modules;

namespace EDISONMAIN2.Desktop
{
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

        /// <summary>Název bez dekorací zobrazení - jde do blobu <c>Shortcuts</c> a launch payloadu agendy.</summary>
        public string Name { get; init; } = "";

        /// <summary>
        /// Text dlaždice - jednoznačný popisek modulu, tentýž jako ve Spuštěných a Používaných
        /// (na ploše chybí kontext stromu, krátký název sám je nejednoznačný). U položek mimo strom název zástupce.
        /// Odvozené ze stromu, nepersistuje se.
        /// </summary>
        public string DisplayedDistinctName { get; init; } = "";

        /// <summary>
        /// Cesta agendou do ToolTipu dlaždice. U položek, které strom nezná (sestavy, pohledy DS),
        /// padá na <see cref="DisplayedDistinctName"/> - prázdný ToolTip by byl jen prázdný rámeček.
        /// Odvozené ze stromu, nepersistuje se.
        /// </summary>
        public string TreePath { get; init; } = "";

        public string ExecutablePath { get; init; } = "";

        public string FormName { get; init; } = "";

        /// <summary>Legacy <c>InputProFrm</c> - parametr předaný formě jako <c>EDParamIn</c>.</summary>
        public string InputParam { get; init; } = "";

        /// <summary>
        /// Input, pod kterým položku zná server (<c>null</c> = <see cref="InputParam"/>). Liší se, když
        /// <see cref="ModuleShortcutsService.Resolve"/> přiřadil uložené položce jinou variantu ze stromu:
        /// Remove a Place by ji jinak na serveru minuly.
        /// </summary>
        public string? StoredInput { get; init; }

        /// <summary>Jen pro sestavy a pohledy DS; u ostatních druhů <c>null</c>.</summary>
        public ReportPayload? Report { get; init; }

        /// <summary>
        /// Sdílená instance uzlu stromu - neprázdná právě pro <see cref="ShortcutKind.MenuModule"/>,
        /// který se podařilo dohledat ve stromu. Díky sdílení se ★ ve stromu a dlaždice drží v sync.
        /// </summary>
        public ExecutableModule? Module { get; init; }

        /// <summary>
        /// Identita položky napříč druhy (dva různé druhy nad týmž modulem koexistují). Modul hlavní nabídky
        /// rozlišuje i variantu - Převody AD a MHD mají shodné ElementId i Id.
        /// </summary>
        public string Key => KeyOf(Kind, ElementId, Id, InputParam);

        /// <summary>Varianta modulu hlavní nabídky (<see cref="ExecutableModule.VariantKey"/>).</summary>
        public string VariantKey => ExecutableModule.VariantKeyOf(ElementId, InputParam);

        internal static string KeyOf(ShortcutKind kind, string elementId, string id, string inputParam) =>
            kind == ShortcutKind.MenuModule
                ? $"{kind}|{ExecutableModule.VariantKeyOf(elementId, inputParam)}"
                : $"{kind}|{elementId}|{id}";

        private int _col;
        private int _row;

        /// <summary>
        /// Sloupec dlaždice (0-based). Absolutní pozice, mřížka se nepřeskládává podle šířky -
        /// přesah řeší vodorovný scroll. Řídí <see cref="ModuleShortcutsService"/>; observable.
        /// </summary>
        public int Col
        {
            get => _col;
            set
            {
                if (_col == value) return;
                _col = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Col)));
            }
        }

        /// <summary>Řádek dlaždice (0-based); prázdné buňky = mezery.</summary>
        public int Row
        {
            get => _row;
            set
            {
                if (_row == value) return;
                _row = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Row)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Zástupce modulu hlavní nabídky (přidání přes ★ nebo tažením ze stromu).</summary>
        public static DesktopShortcut ForModule(ExecutableModule module) => new()
        {
            Kind = ShortcutKind.MenuModule,
            Id = module.ElementId,
            ElementId = module.ElementId,
            Name = module.Name,
            DisplayedDistinctName = module.DisplayedDistinctName,
            TreePath = module.TreePath,
            ExecutablePath = module.ExecutablePath,
            FormName = module.FormName,
            InputParam = module.InputParam,
            Module = module,
        };

        public DesktopShortcutDto ToDto() => new(Kind.ToString(), Id, ElementId, Name,
            ExecutablePath, FormName, StoredInput ?? InputParam, Col, Row, Report);
    }
}
