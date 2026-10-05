namespace EDControls.EDList
{
    public enum ListColumnType
    {
        Text,
        Number,
        Date,
        DateTime,
        Bool,
        /// <summary>Rezervováno: EDList ho zatím kreslí jako text (raw hodnota), kreslení až pro skutečného konzumenta.</summary>
        Image,
        Color,
        HoursMinutes,
        /// <summary>Rezervováno: EDList ho zatím kreslí jako text (raw kód), kreslení až pro skutečného konzumenta.</summary>
        Codebook,
    }
}
