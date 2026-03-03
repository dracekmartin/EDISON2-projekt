namespace EDControls.EDList
{
    public class ListColumn
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public EListColumnType Type { get; set; } = EListColumnType.Text;
        /// <summary>
        /// Formátovací řetězec. NUMBER: "N0", "N2". DATE/DATETIME: "dd.MM.yyyy" apod.
        /// </summary>
        public string Format { get; set; } = "";
        /// <summary>Šířka v pixelech. 0 = auto podle obsahu.</summary>
        public int Width { get; set; } = 0;
        public EListColumnAlign Align { get; set; } = EListColumnAlign.Auto;
        public bool Sortable { get; set; } = false;
        public bool Visible { get; set; } = true;
        public bool IsLink { get; set; } = false;
    }
}
