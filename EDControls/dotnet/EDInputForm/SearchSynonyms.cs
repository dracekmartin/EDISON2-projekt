namespace EDControls.EDInputForm;

/// <summary>
/// Slovník pro hledání ve formuláři: slovo, které uživatel napíše, a slova z popisků, která má najít navíc
/// („písmo" → Font, Velikost). Je to volba aplikace, ne formuláře: dosadí ho na svá okna přes
/// <see cref="EDInputForm.SearchSynonymsProperty"/> a formuláře v nich ho zdědí. Bez slovníku hledá
/// formulář jako dřív - celý výraz jako souvislou část popisku nebo názvu skupiny.
/// </summary>
/// <remarks>
/// Se slovníkem se hledá každé slovo zvlášť (všechna musí být v popisku nebo názvu skupiny pole) a slova od
/// pěti písmen bez poslední hlásky, aby „výška záhlaví" našla „Nejmenší výška" ve skupině Záhlaví.
/// Výchozí chování to není záměrně: synonyma dávají smysl jen v jedné oblasti (písmo = font ve formuláři
/// návrháře sestav, ve formuláři faktury ne).
/// </remarks>
public sealed class SearchSynonyms
{
    private readonly List<(string Kmen, string[] Hodnoty)> _slova;

    /// <param name="slovnik">Slovo uživatele → slova nebo spojení z popisků, která má najít navíc.
    /// Velikost písmen a diakritika nerozhodují.</param>
    public SearchSynonyms(IReadOnlyDictionary<string, string[]> slovnik)
    {
        _slova = [.. slovnik
            .Select(kv => (Stem(Normalize(kv.Key)), kv.Value.Select(Normalize).Where(h => h.Length > 0).ToArray()))
            .Where(s => s.Item1.Length > 0)];
    }

    /// <summary>Slova dotazu, normalizovaná.</summary>
    internal static string[] Words(string dotaz) =>
        Normalize(dotaz).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Splní normalizovaný text (popisek a skupina pole) všechna slova dotazu?</summary>
    internal bool Matches(string text, IReadOnlyList<string> slova) =>
        slova.All(slovo => Alternatives(slovo).Any(text.Contains));

    /// <summary>Co v textu splní jedno slovo dotazu: jeho kmen a synonyma.</summary>
    /// <remarks>Synonyma platí od tří písmen a i pro rozepsané slovo („pís" už najde Font), jinak by
    /// každé „p" nabídlo všechno, co v slovníku na „p" začíná.</remarks>
    internal IEnumerable<string> Alternatives(string slovo)
    {
        var kmen = Stem(slovo);
        yield return kmen;
        if (kmen.Length < 3) yield break;
        foreach (var (klic, hodnoty) in _slova)
            if (kmen.StartsWith(klic, StringComparison.Ordinal) || klic.StartsWith(kmen, StringComparison.Ordinal))
                foreach (var h in hodnoty) yield return h;
    }

    /// <summary>Slovo bez koncovky: od pěti písmen bez poslední hlásky (písma, písmo → písm).</summary>
    internal static string Stem(string slovo) => slovo.Length >= 5 ? slovo[..^1] : slovo;

    private static string Normalize(string s) => SearchNormalization.NormalizeForSearch(s).Trim();
}
