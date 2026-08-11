using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace EDControls.EDList
{
    /// <summary>
    /// Kolekce, která umí vyměnit celý obsah **jedním** oznámením o změně.
    /// <para>
    /// Plnění po jednom (<c>Clear</c> + N× <c>Add</c>) znamená N+1 přepočtů navázaného pohledu -
    /// u dvou set řádků je to vidět jako škubnutí, když načtení dosedne, zatímco uživatel píše
    /// do filtru. <c>ReplaceAll</c> zmutuje vnitřní seznam a pošle jediný <c>Reset</c>.
    /// </para>
    /// <para>
    /// ⚠️ Není to totéž co <c>ICollectionView.DeferRefresh</c>, který se na tohle zkoušel 4. 8. 2026
    /// a hned se vracel: odklad neodkládá <c>CollectionChanged</c>, takže mutace zdrojové kolekce
    /// pod ním shodí <c>ListCollectionView</c> na currency. Tady se žádný odklad nekoná - jen se
    /// místo N+1 oznámení pošle jedno, a <c>Reset</c> samotný posílá i dnešní <c>Clear</c>.
    /// </para>
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
