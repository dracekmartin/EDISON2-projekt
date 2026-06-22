using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    class ModuleFamily : IModule, INotifyPropertyChanged
    {
        public string DisplayedName { get; set; } = "";

        public bool IsExpanded { get; set; } = false;

        public ObservableCollection<IModule> NestedModules { get; set; } = [];

        /// <summary>
        /// Hex barva (<c>"#RRGGBB"</c>) pro Foreground rodiny ve stromu; "" = výchozí barva tématu.
        /// V autorovaných datech vždy "" — nastavuje ji runtime ModuleLaunchabilityCheck
        /// agregací stavu dětí (sbalená rodina pak signalizuje stub/broken bez rozbalení).
        /// </summary>
        public string Color { get; set; } = "";

        private bool _isFavorite;

        /// <summary>
        /// True když jsou VŠECHNY podřízené moduly oblíbené (agreguje VM přes RefreshFamilyFavoriteFlags).
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

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
