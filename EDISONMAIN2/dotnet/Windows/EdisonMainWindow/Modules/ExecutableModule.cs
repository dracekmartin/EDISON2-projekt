using System.ComponentModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public record ExecutableModule : IModule, INotifyPropertyChanged
    {
        /// <summary>Název z dat stromu: hledání, cesta stromem i uložený zástupce. Dekorace sem nepatří.</summary>
        public string Name { get; init; } = "";

        /// <summary>Dekorace jen pro zobrazení (mlineOnly, stav launchovatelnosti).</summary>
        public string DisplaySuffix { get; init; } = "";

        public string DisplayedName => Name + DisplaySuffix;

        /// <summary>
        /// Disambiguovaný název (legacy `nameEx`) - krátký název je ve stromu nejednoznačný
        /// (dvoje „Zaměstnanci", troje „Události"). <c>""</c> = legacy data ho nemají.
        /// Ke zobrazení používej <see cref="DisplayedLongName"/>.
        /// </summary>
        public string LongName { get; init; } = "";

        /// <summary>Dlouhý název pro plochu a menu spuštěných modulů; fallback krátký název.</summary>
        public string DisplayedLongName => LongName.Length > 0 ? LongName : DisplayedName;

        public const string PathSeparator = " › ";

        /// <summary>Názvy nadřazených rodin („Ekonomický systém › Servis"); <c>""</c> = modul v kořeni stromu.</summary>
        public string FamilyPath { get; init; } = "";

        /// <summary>Cesta stromem včetně listu, bez dekorací zobrazení.</summary>
        public string TreePath => FamilyPath.Length == 0 ? Name : FamilyPath + PathSeparator + Name;

        /// <summary>
        /// Název rozlišitelný bez kontextu stromu (seznam Používané): dlouhý název, jinak cesta stromem
        /// s dekorací listu (mlineOnly nemá barvu, bez suffixu by značka interního nástroje zmizela).
        /// Pravidlo výběru jako <c>EdisonMainViewModel.ResolveRunningLabel</c>.
        /// </summary>
        public string DisplayedDistinctName => LongName.Length > 0 ? LongName : TreePath + DisplaySuffix;

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
        /// na ploše (<see cref="DesktopShortcut.Module"/>) → přepnutí jednou se projeví všude.
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
