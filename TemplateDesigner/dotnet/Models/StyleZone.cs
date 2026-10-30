namespace TemplateDesigner.Models;

/// <summary>
/// Textová část prvku a styl, se kterým se vytiskne: <paramref name="Path"/> je cesta vlastnosti stylu
/// na prvku (<c>Style</c>, <c>HeaderStyle</c>, <c>MinuteStyle</c>…, tak ji znají pole formuláře),
/// <paramref name="Style"/> vyřešená kaskáda včetně zapečených výchozích a výchozího písma šablony.
/// </summary>
public sealed record StyleZone(string Path, ResolvedTextStyle Style);
