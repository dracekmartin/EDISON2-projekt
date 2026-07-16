namespace EDISONMAIN2.MiniApps;

/// <summary>
/// DataObject formáty pro drag&drop modulů. In-process (nese přímo <c>ExecutableModule</c> instanci).
/// Dvě rozlišené operace: přetažení ze stromu (přidání) vs. přeuspořádání dlaždic v miniaplikaci.
/// </summary>
internal static class ModuleDrag
{
    /// <summary>Modul tažený ze stromu do miniaplikace Oblíbené → přidání.</summary>
    public const string FromTree = "EDISON.Module.FromTree";

    /// <summary>Dlaždice tažená uvnitř miniaplikace Oblíbené → přeuspořádání.</summary>
    public const string Reorder = "EDISON.Module.Reorder";
}
