namespace EDControls.EDList
{
    public enum ListColumnType
    {
        Text,
        Number,
        Date,
        DateTime,
        Bool,
        /// <summary>Ikona 16 px; hodnota = klíč sdílené ikony (<c>EDIcons.*</c>), prázdná = bez ikony.</summary>
        Image,
        Color,
        HoursMinutes,
        /// <summary>Rezervováno: EDList ho zatím kreslí jako text (raw kód), kreslení až pro skutečného konzumenta.</summary>
        Codebook,
    }
}
