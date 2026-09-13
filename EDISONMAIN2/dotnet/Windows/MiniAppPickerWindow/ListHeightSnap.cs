using System.Windows;
using ListBox = System.Windows.Controls.ListBox;
using ItemsPresenter = System.Windows.Controls.ItemsPresenter;
using System.Windows.Media;
using System.Windows.Threading;

namespace EDISONMAIN2.Windows.MiniAppPickerWindow
{
    /// <summary>
    /// Sráží výšku seznamu na celé položky. Bez toho seznam končí uprostřed položky - z posledního
    /// řádku zbyde pár pixelů a vypadá jako prázdná neklikatelná položka.
    /// </summary>
    // Aliasy místo `using System.Windows.Controls` - WPF markup temp projekt (_wpftmp) importuje
    // i System.Windows.Forms, takže holý `ListBox` je tam dvojznačný (CS0104).
    internal static class ListHeightSnap
    {
        /// <summary>
        /// Napojí srážení na změny velikosti <paramref name="owner"/> (okna, které dává seznamu prostor). <paramref name="cap"/> je strop
        /// výšky (<c>PositiveInfinity</c> = jen tolik, kolik dá rodič).
        /// </summary>
        /// <remarks>
        /// Poslouchá okno, ne seznam: srážení mění výšku seznamu, takže na jeho vlastním
        /// <c>SizeChanged</c> by se zacyklilo a seznam by viditelně kmital.
        /// </remarks>
        public static void Attach(FrameworkElement owner, ListBox list, double cap)
        {
            void Refresh()
            {
                // Reset na strop, ať se seznam po zvětšení okna smí zase roztáhnout - jinak by držel
                // sraženou výšku z předchozího kola a už nikdy nevyrostl.
                list.MaxHeight = cap;
                // Priorita Loaded = ještě před vykreslením snímku, takže reset není vidět.
                list.Dispatcher.BeginInvoke(new Action(() => Apply(list, cap)), DispatcherPriority.Loaded);
            }
            owner.Loaded += (_, _) => Refresh();
            owner.SizeChanged += (_, _) => Refresh();
        }

        /// <summary>Srazí <see cref="FrameworkElement.MaxHeight"/> na celé položky. Vrací true, když zabralo.</summary>
        public static bool Apply(ListBox list, double cap)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(0) is not FrameworkElement { ActualHeight: > 0 } item ||
                FindVisualChild<ItemsPresenter>(list) is not { ActualHeight: > 0 } presenter)
                return false;
            double whole = Math.Floor(presenter.ActualHeight / item.ActualHeight) * item.ActualHeight;
            if (whole <= 0 || whole >= presenter.ActualHeight) return false;
            // Chrome = rámeček + padding seznamu; presenter je uvnitř nich.
            list.MaxHeight = Math.Min(cap, whole + (list.ActualHeight - presenter.ActualHeight));
            return true;
        }

        private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) return match;
                if (FindVisualChild<T>(child) is { } nested) return nested;
            }
            return null;
        }
    }
}
