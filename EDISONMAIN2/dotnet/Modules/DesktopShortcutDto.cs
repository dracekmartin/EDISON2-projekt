namespace EDISONMAIN2.Modules
{
    /// <summary>
    /// Druh položky plochy. Zrcadlí legacy <c>DesktopShortcutItemType</c>
    /// (<c>EDCORE/EDFormInterfaceLic.vb</c>) - plocha je nadmnožina oblíbených: ★ ve stromu umí
    /// jen <see cref="MenuModule"/>, ostatní druhy vznikají v samotném modulu („umístit na plochu").
    /// </summary>
    public enum ShortcutKind
    {
        /// <summary>Modul hlavní nabídky (legacy FormularHlavniNabidky) - jediný druh, který dnes umí ★.</summary>
        MenuModule,
        /// <summary>Forma balíku mimo hlavní nabídku (legacy FormularDanehoBaliku).</summary>
        PackageForm,
        /// <summary>Tisková sestava bez výběru parametrů (legacy SestavaTiskova, CallType=Procedura).</summary>
        PrintReport,
        /// <summary>Sestava s výběrem parametrů / uloženou předvolbou (legacy SestavaVyberova).</summary>
        SelectReport,
        /// <summary>Uložený pohled datového skladu (legacy PohledDatovehoSkladu).</summary>
        DwhView,
    }

    /// <summary>
    /// Doplňkový payload sestav a pohledů DS - 1:1 legacy <c>ReportShortcut</c>
    /// (<c>EDLIBRARY/clsForm.vb</c>). Server z něj dohledá uložené parametry tisku
    /// (<c>GetIndexGlobaluPodleUlozenychParametru</c>), proto se pole nesmí přeskládat ani slučovat.
    /// </summary>
    public record ReportPayload(
        string Agenda = "",
        string Oblast = "",
        string TypSestavy = "",
        string Sestava = "",
        string Predvolba = "",
        bool JeVerejny = false);

    /// <summary>
    /// Serializovaná podoba položky plochy (blob <c>Shortcuts</c> ve scope <c>EDISONMAIN2</c>).
    /// <c>Kind</c> je string, ne enum - přeskládání <see cref="ShortcutKind"/> nesmí přeznačit uložená data.
    /// Chybějící <c>Col</c>/<c>Row</c> = doplní se z pořadí.
    /// </summary>
    public record DesktopShortcutDto(
        string Kind = nameof(ShortcutKind.MenuModule),
        string Id = "",
        string ElementId = "",
        string Name = "",
        string Exe = "",
        string Form = "",
        string Input = "",
        int? Col = null,
        int? Row = null,
        ReportPayload? Report = null);
}
