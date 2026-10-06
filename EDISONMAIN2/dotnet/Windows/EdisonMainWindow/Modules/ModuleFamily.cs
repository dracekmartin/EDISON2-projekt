using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    class ModuleFamily : IModule, INotifyPropertyChanged
    {
        /// <summary>Název z dat stromu (hledání, cesta stromem). Dekorace sem nepatří.</summary>
        public string Name { get; set; } = "";

        /// <summary>Dekorace jen pro zobrazení (mlineOnly, agregovaný stav launchovatelnosti).</summary>
        public string DisplaySuffix { get; set; } = "";

        public string DisplayedName => Name + DisplaySuffix;

        /// <summary>
        /// Package ID (EDPackageEnum / EDMainPackageEnum) z legacy `id` atributu skupiny;
        /// "" = bez gate (syntetické složky, např. strom Oblíbených). Proti AccessTable se
        /// kontroluje vždy, proti Lic.Elements jen při <see cref="IsLicensed"/>.
        /// </summary>
        public string ElementId { get; init; } = "";

        /// <summary>True → složka podléhá licenční kontrole (legacy `licensed="1"` na skupině).</summary>
        public bool IsLicensed { get; init; }

        /// <summary>
        /// Legacy `mlineOnly="1"` na skupině: mimo doménu M-line je skrytá celá větev,
        /// v doméně dostane název suffix " [### M-line ###]".
        /// </summary>
        public bool MlineOnly { get; init; }

        private bool _isExpanded;

        /// <summary>
        /// Rozbalení uzlu ve stromu. TwoWay-bound na <c>TreeViewItem.IsExpanded</c> → odráží akci
        /// uživatele; observable, aby VM mohl persistovat změnu (per-user × IDFa).
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public ObservableCollection<IModule> NestedModules { get; set; } = [];

        /// <summary>
        /// Hex barva (<c>"#RRGGBB"</c>) pro Foreground rodiny ve stromu; "" = výchozí barva tématu.
        /// V autorovaných datech vždy "" - nastavuje ji runtime ModuleLaunchabilityCheck
        /// agregací stavu dětí (sbalená rodina pak signalizuje stub/broken bez rozbalení).
        /// </summary>
        public string Color { get; set; } = "";

        private bool _isFavorite;

        /// <summary>
        /// True když jsou VŠECHNY podřízené moduly oblíbené (agreguje FavoritesCoordinator přes RefreshFamilyFavoriteFlags).
        /// Řídí text kontextového menu rodiny (Přidat/Odebrat vše). Observable kvůli live aktualizaci.
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

        private bool _isVisibleBySearch = true;

        /// <summary>Viditelnost při vyhledávání - nastavuje VM (název matchne / má viditelného potomka).</summary>
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
