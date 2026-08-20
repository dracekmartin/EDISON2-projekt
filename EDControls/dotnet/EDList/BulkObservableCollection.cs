using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace EDControls.EDList
{
    /// <summary>
    /// Kolekce, která umí vyměnit celý obsah jediným <c>Reset</c> místo N+1 oznámení -
    /// u stovek řádků je rozdíl vidět jako škubnutí. <c>ICollectionView.DeferRefresh</c> to
    /// neřeší: odklad neodkládá <c>CollectionChanged</c>, takže mutace zdroje pod ním shodí
    /// <c>ListCollectionView</c> na currency.
    /// </summary>
    internal sealed class BulkObservableCollection<T> : ObservableCollection<T>
    {
        /// <summary>Nahradí obsah kolekce a pošle jediný <c>Reset</c>.</summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            CheckReentrancy();
            Items.Clear();
            foreach (var item in items) Items.Add(item);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
