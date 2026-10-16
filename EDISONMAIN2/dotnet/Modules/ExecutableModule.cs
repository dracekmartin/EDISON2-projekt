using System.ComponentModel;

namespace EDISONMAIN2.Modules
{
    /// <summary>
    /// Spustitelný uzel stromu. Třída s referenční rovností, ne record: instance se sdílí mezi stromy,
    /// Oblíbenými, Používanými i dlaždicí a nese pozorovatelný stav, takže dva shodně vyplněné uzly
    /// nesmí splynout (kontejnery WPF, výběr, <c>List.Remove</c>).
    /// </summary>
    public sealed class ExecutableModule : IModule, INotifyPropertyChanged
    {
        public ExecutableModule() { }

        /// <summary>
        /// Kopie uzlu pro dekoraci (filtr, launchovatelnost); změny doplní initializer
        /// (<c>new ExecutableModule(exe) { Color = ... }</c>). Odběratelé <see cref="PropertyChanged"/> se nekopírují.
        /// </summary>
        public ExecutableModule(ExecutableModule source)
        {
            Name = source.Name;
            DisplaySuffix = source.DisplaySuffix;
            LongName = source.LongName;
            FamilyPath = source.FamilyPath;
            DistinctName = source.DistinctName;
            ExecutablePath = source.ExecutablePath;
            FormName = source.FormName;
            ElementId = source.ElementId;
            IsLicensed = source.IsLicensed;
            InputParam = source.InputParam;
            Hidden = source.Hidden;
            MlineOnly = source.MlineOnly;
            Color = source.Color;
            _isFavorite = source._isFavorite;
            _isStartupWindow = source._isStartupWindow;
            _isVisibleBySearch = source._isVisibleBySearch;
        }

        /// <summary>Název z dat stromu: hledání, cesta stromem i uložený zástupce. Dekorace sem nepatří.</summary>
        public string Name { get; init; } = "";

        /// <summary>Dekorace jen pro zobrazení (mlineOnly, stav launchovatelnosti).</summary>
        public string DisplaySuffix { get; init; } = "";

        public string DisplayedName => Name + DisplaySuffix;

        /// <summary>
        /// Disambiguovaný název (legacy `nameEx`) - krátký název je ve stromu nejednoznačný
        /// (dvoje „Zaměstnanci", troje „Události"). <c>""</c> = legacy data ho nemají.
        /// Ke zobrazení používej <see cref="DisplayedDistinctName"/>.
        /// </summary>
        public string LongName { get; init; } = "";

        public const string PathSeparator = " › ";

        /// <summary>Názvy nadřazených rodin („Ekonomický systém › Servis"); <c>""</c> = modul v kořeni stromu.</summary>
        public string FamilyPath { get; init; } = "";

        /// <summary>Cesta stromem včetně listu, bez dekorací zobrazení.</summary>
        public string TreePath => FamilyPath.Length == 0 ? Name : FamilyPath + PathSeparator + Name;

        /// <summary>
        /// Popisek jednoznačný v celém načteném stromu (<c>ModuleTreeState.BuildModuleLabels</c>), tentýž
        /// jako v menu Spuštěné moduly. Přiděluje ho <c>ModuleTreeState.Load</c>; <c>""</c> = uzel mimo load nebo bez exe.
        /// </summary>
        public string DistinctName { get; internal set; } = "";

        /// <summary>
        /// Název rozlišitelný bez kontextu stromu (Používané, dlaždice plochy) s dekorací listu (mlineOnly nemá
        /// barvu, bez suffixu by značka interního nástroje zmizela). Uzel mimo load padá na cestu stromem.
        /// </summary>
        public string DisplayedDistinctName => (DistinctName.Length > 0 ? DistinctName : TreePath) + DisplaySuffix;

        public string ExecutablePath { get; init; } = "";

        public string FormName { get; init; } = "";

        /// <summary>Numerické ID z EDISFormEnum (string). Klíč pro licenční gate; jinak unikátní identifikátor uzlu.</summary>
        public string ElementId { get; init; } = "";

        /// <summary>Když true, <see cref="ElementId"/> se kontroluje proti <c>Lic.Elements</c>; jinak modul vidí každý.</summary>
        public bool IsLicensed { get; init; } = false;

        /// <summary>
        /// Volitelný launch parametr předaný formě jako <c>EDParamIn</c> (string).
        /// Legacy navbar <c>input="AdminMode=1"</c> ekvivalent. Default <c>""</c> = forma použije svůj default.
        /// </summary>
        public string InputParam { get; init; } = "";

        /// <summary>
        /// Klíč varianty pro používanost, plochu a Oblíbené: varianty téže formy sdílí <see cref="ElementId"/>
        /// a liší se parametrem (Převody AD/MHD, Události agd=...). Bez parametru holé ElementId, uložené záznamy platí dál.
        /// </summary>
        public string VariantKey => VariantKeyOf(ElementId, InputParam);

        internal static string VariantKeyOf(string elementId, string inputParam) =>
            inputParam.Length == 0 ? elementId : elementId + "|" + inputParam;

        /// <summary>Když true, modul se ve stromu nezobrazí. Legacy `hidden="1"` ekvivalent.</summary>
        public bool Hidden { get; init; } = false;

        /// <summary>
        /// Legacy `mlineOnly="1"`: mimo doménu M-line se modul nezobrazí, v doméně dostane
        /// suffix " [### M-line ###]" (interní M-line nástroje na zákaznických stanicích).
        /// </summary>
        public bool MlineOnly { get; init; } = false;

        /// <summary>
        /// Hex barva pro Foreground položky ve stromu. Prázdný / null = výchozí barva tématu.
        /// Diagnostické barvy launchovatelnosti nastavuje <see cref="ModuleLaunchabilityCheck"/>
        /// z theme tokenů <c>EDTheme.Module.Stub</c> (stub bez .exe) / <c>EDTheme.Module.Broken</c>
        /// (exe vyplněné, ale soubor chybí).
        /// </summary>
        public string Color { get; init; } = "";

        private bool _isFavorite;

        /// <summary>
        /// Uživatelovo „oblíbené" označení (★). Přepíná se in-place, takže nevyžaduje rebuild stromu.
        /// Instance se sdílí mezi hlavním stromem, záložkou Oblíbené, žebříčkem Používané i dlaždicí
        /// na ploše (<c>DesktopShortcut.Module</c>) → přepnutí jednou se projeví všude.
        /// </summary>
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value) return;
                _isFavorite = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
            }
        }

        private bool _isStartupWindow;

        /// <summary>
        /// Uživatelův výchozí okno (per IDFa) - shell ho po loginu automaticky otevře a ve
        /// stromu ho značí „(výchozí)". Přepíná se in-place (jako <see cref="IsFavorite"/>);
        /// instance se sdílí mezi stromy → značka se projeví všude.
        /// </summary>
        public bool IsStartupWindow
        {
            get => _isStartupWindow;
            set
            {
                if (_isStartupWindow == value) return;
                _isStartupWindow = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsStartupWindow)));
            }
        }

        private bool _isVisibleBySearch = true;

        /// <summary>Viditelnost při vyhledávání ve stromu - nastavuje VM podle shody názvu.</summary>
        public bool IsVisibleBySearch
        {
            get => _isVisibleBySearch;
            set
            {
                if (_isVisibleBySearch == value) return;
                _isVisibleBySearch = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisibleBySearch)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
