namespace EDControls.EDInputForm
{
    public abstract class Item
    {
        /// <summary>
        /// 1-based sequential number assigned by Caché across all items (groups and fields).
        /// </summary>
        public int Number { get; set; }
    }
}
