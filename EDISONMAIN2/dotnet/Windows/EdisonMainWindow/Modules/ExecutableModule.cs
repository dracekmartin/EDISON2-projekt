using System.ComponentModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public record ExecutableModule : IModule, INotifyPropertyChanged
    {
        public string DisplayedName { get; init; } = "";

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
        /// Hex barva (<c>"#RRGGBB"</c>) pro Foreground položky ve stromu. Prázdný / null = výchozí
        /// barva tématu. Konvence: <c>"#FFA500"</c> (oranžová) = autorský stub bez vyplněné
        /// <c>.exe</c>; <c>"#FF0000"</c> (červená) = exe vyplněné, ale soubor chybí.
        /// </summary>
        public string Color { get; init; } = "";

        private bool _isFavorite;

        /// <summary>
        /// Jediná mutable/observable property — uživatelovo „oblíbené" označení. Přepíná se in-place
        /// (hvězdička), takže nevyžaduje rebuild stromu. Instance se sdílí mezi hlavním stromem,
        /// záložkou Oblíbené a žebříčkem Používané → přepnutí jednou se projeví všude.
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

        private int _favoriteSlot;

        /// <summary>
        /// Pozice dlaždice v mřížce miniaplikace Oblíbené (0-based slot; sloupce dle šířky panelu,
        /// prázdné sloty = mezery). Řídí <see cref="Modules.ModuleShortcutsService"/>. Relevantní jen
        /// když <see cref="IsFavorite"/>. Observable — změna přeskládá mřížku (attached Slot binding).
        /// </summary>
        public int FavoriteSlot
        {
            get => _favoriteSlot;
            set
            {
                if (_favoriteSlot == value) return;
                _favoriteSlot = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FavoriteSlot)));
            }
        }

        private bool _isStartupWindow;

        /// <summary>
        /// Uživatelův výchozí okno (per IDFa) — shell ho po loginu automaticky otevře a ve
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

        /// <summary>Viditelnost při vyhledávání ve stromu — nastavuje VM podle shody názvu.</summary>
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
