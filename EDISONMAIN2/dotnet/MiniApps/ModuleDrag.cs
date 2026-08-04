namespace EDISONMAIN2.MiniApps;

/// <summary>
/// DataObject formáty pro drag&drop na plochu. In-process (nese přímo instance).
/// Dvě rozlišené operace: přetažení modulu ze stromu (přidání) vs. přeuspořádání dlaždic.
/// </summary>
internal static class ModuleDrag
{
    /// <summary>Modul (<c>ExecutableModule</c>) tažený ze stromu na plochu → přidání.</summary>
    public const string FromTree = "EDISON.Module.FromTree";

    /// <summary>Dlaždice (<c>Shortcut</c>) tažená uvnitř plochy → přeuspořádání.</summary>
    public const string ReorderTile = "EDISON.Shortcut.Reorder";
}
