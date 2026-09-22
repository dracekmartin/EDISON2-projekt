using System.Windows.Controls;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    /// <summary>
    /// Drží plochu miniaplikací nad minimem, když si ji vezme strom modulů nebo pruh podpory.
    /// Obojí jsou post-layout korekce: GridSplitter meze star řádku/sloupce u fixní px velikosti
    /// souseda obchází a layoutové MinWidth/MinHeight na star definici by přetékalo z okna.
    /// </summary>
    internal static class ShellSplitLimits
    {
        /// <summary>
        /// Nastaví strop výšky pruhu podpory tak, aby ploše zbylo <paramref name="minDockHeight"/>.
        /// <paramref name="availableHeight"/> musí být **přidělená** výška star řádku, ve kterém
        /// sloupec sedí, ne výška sloupce samotného.
        /// </summary>
        public static void ApplySupportCeiling(RowDefinition supportRow, RowDefinition splitterRow,
                                               double availableHeight, double minDockHeight)
        {
            // Strop, ne clamp po dragu: GridSplitter mez řádku respektuje, takže tažení zastaví.
            // Sráží jen render - Height zůstane uživatelova, takže se pruh po zvětšení okna sám vrátí.
            if (availableHeight <= 0) return;    // před prvním layoutem není proti čemu měřit
            // Vnořený Grid ani součet jeho řádků se měřit nedají: při uložené výšce větší než okno
            // layout přetéká a ActualHeight vrací požadovaný rozměr včetně přetečení. Totéž dělič.
            double splitter = splitterRow.Height.IsAbsolute ? splitterRow.Height.Value : splitterRow.ActualHeight;
            supportRow.MaxHeight = Math.Max(supportRow.MinHeight, availableHeight - splitter - minDockHeight);
        }

        /// <summary>
        /// Když cokoli (drag splitteru, zúžení okna) zmenší plochu pod <paramref name="minDockWidth"/>,
        /// o schodek zúží strom (až po jeho vlastní <see cref="ColumnDefinition.MinWidth"/>).
        /// </summary>
        /// <remarks>
        /// Destruktivní (přepíše uloženou šířku), takže až po úvodním layoutu - jinak zašlape restore.
        /// Strop jako u výšky tady nejde: strom je star sloupec, počítal by se ze sebe.
        /// </remarks>
        public static void ClampTreeWidth(ColumnDefinition treeColumn, double dockWidth, double minDockWidth)
        {
            double deficit = minDockWidth - dockWidth;
            if (deficit <= 0) return;
            double target = Math.Max(treeColumn.MinWidth, treeColumn.ActualWidth - deficit);
            if (target < treeColumn.ActualWidth)
                treeColumn.Width = new System.Windows.GridLength(target);
        }
    }
}
