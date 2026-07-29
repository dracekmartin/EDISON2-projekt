namespace TemplateDesigner.Models;

/// <summary>
/// Field ID plochých polí kolekce ZASTAVKY - jednotný datový/sloupcový kontrakt sdílený mezi
/// <see cref="Elements.StopListElement"/> a DynamicTableElement nad zdrojem ZASTAVKY. Agenda
/// (<c>ZastavkoveJrAd</c>/<c>ZastavkoveJrMhd</c>) vystavuje přes GetSchema identickou sadu (AD ≡ MHD)
/// a StopList je používá přímo jako <c>StopColumn.Key</c> → sloupce StopListu jsou 1:1 s poli, která
/// nabízí DynamicTable. Uživatel tak v podmíněném renderu / počítaných polích píše stejné ID bez
/// ohledu na to, kterou cestou zastávky zobrazuje.
///
/// <para>Hodnoty se persistují do JSON šablony (drátový klíč i <c>StopColumn.Key</c>) - <b>neměnit bez
/// migrace</b>. Zamčeno testem <c>StopFieldsTests</c>.</para>
/// </summary>
public static class StopFields
{
    public const string StopName   = "NAZEV_ZASTAVKY";    // název zastávky (+ inline značky/šipka = render special StopListu)
    public const string TravelTime = "CAS_JIZDY";         // čas jízdy (příp. rozvinut na varianty tras)
    public const string Poradi     = "PORADI";            // pořadí zastávky (zobrazovací čítač)
    public const string JeAktualni = "AKTUALNI_ZASTAVKA"; // příznak aktuální (nástupní) zastávky
    public const string Zone       = "ZONY";              // tarifní zóny (ploché „1/2")
    public const string Nastupiste = "NASTUPISTE";        // označník zastávky (AD: J-věta #3; MHD: Oznacnik) - UI label „Označník"
    public const string NastupisteSpoju = "NASTUPISTE_SPOJU"; // označníky dle spojů (AD: W-trasa pozice 7, fallback J-věta) - CSV dle četnosti, první = nejčetnější

    /// <summary>Interní join-klíč zastávky (<c>ODJEZDY.ZASTAVKA_ID</c>) - je v datech, ale NE ve schématu
    /// ani jako sloupec (uživatel s ním nepracuje).</summary>
    public const string Id = "ID";

    /// <summary>Prefix runtime variantních časových sub-sloupců (<c>"CAS_JIZDY#0"</c>, <c>"CAS_JIZDY#1"</c>, …),
    /// generovaných za běhu při více variantách jízdních dob. Nepersistuje se v default sloupcích.</summary>
    public const string TravelTimeVariantPrefix = "CAS_JIZDY#";
}
