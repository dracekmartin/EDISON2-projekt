namespace EDControls.EDList
{
    public class ListColumn
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public ListColumnType Type { get; set; } = ListColumnType.Text;
        /// <summary>
        /// Formátovací řetězec. NUMBER: "N0", "N2". DATE/DATETIME: "dd.MM.yyyy" apod.
        /// </summary>
        public string Format { get; set; } = "";
        /// <summary>Šířka v pixelech. 0 = auto podle obsahu.</summary>
        public int Width { get; set; } = 0;
        public ListColumnAlign Align { get; set; } = ListColumnAlign.Auto;
        public bool Sortable { get; set; } = false;
        public bool Visible { get; set; } = true;
        /// <summary>Pole řádku s klíčem sdílené ikony (<c>EDIcons.*</c>), kterou textový sloupec ukáže před hodnotou;
        /// prázdné = bez ikony. Odliší druh řádku přímo u názvu (šablona M-line × vlastní).</summary>
        public string IconKey { get; set; } = "";
    }
}
