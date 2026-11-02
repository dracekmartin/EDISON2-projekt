namespace TemplateDesigner.Models;

/// <summary>
/// Parametry sestavy, které čte nebo porovnává kód návrháře: jména a popisky voleb. Definuje je agenda na serveru
/// (<c>GetParameters</c>) a popisky voleb jsou <b>zamčené</b> - ukládá se zobrazený text (<c>ux-conventions §8</c>).
/// Do 30. 10. je měl kód rozepsané na pěti místech; že je agendy opravdu posílají a nikde jinde v kódu nejsou
/// napevno, hlídá <c>ParametrySestavyTests</c>.
/// </summary>
public static class ParametrySestavy
{
    // ── Minuty odjezdů ──
    public const string CasovaOsa = "CASOVA_OSA";
    public const string CasovaOsaJenSOdjezdy = "Jen hodiny s odjezdy";
    public const string CasovaOsaSouvisla = "Souvislá od první do poslední";
    public const string CasovaOsaVlastniRozsah = "Vlastní rozsah";
    public const string HodinyOd = "HODINY_OD";
    public const string HodinyDo = "HODINY_DO";
    public const string RoztahnoutNaVysku = "ROZTAHNOUT_NA_VYSKU";

    /// <summary>Rozdělení sloupců vývěsky (zastávkový JŘ) a linkového JŘ - stejná mechanika, jiný parametr.</summary>
    public const string RozdeleniSloupcu = "ROZDELENI_SLOUPCU";
    public const string DeleniSloupcu = "DELENI_SLOUPCU";
    public const string AutomatickyDleLinky = "Automaticky (dle linky)";
    /// <summary>Ukazatel, že si uživatel sloupce upravil - ručně vybrat nejde.</summary>
    public const string VlastniRozlozeni = "Vlastní rozložení";

    // ── Seznam zastávek ──
    public const string TiskSipky = "TISK_SIPKY";
    public const string TiskProjetychZastavek = "TISK_PROJETYCH_ZAST";
    public const string ZdrojStanoviste = "ZDROJ_STANOVISTE";
    public const string ZdrojStanovisteSloupek = "Sloupek";

    // ── Poznámky ──
    public const string PismenaVariant = "PISMENA_VARIANT";
    public const string SchovatVRozsahu = "SCHOVAT_V_ROZSAHU";
    public const string SchovatAutomaticke = "Automatické (rozdíly tras)";
    public const string SchovatOboji = "Obojí";

    // ── Tabulka spojů ──
    public const string PocetStranSestavy = "POCET_STRAN_SESTAVY";
}
