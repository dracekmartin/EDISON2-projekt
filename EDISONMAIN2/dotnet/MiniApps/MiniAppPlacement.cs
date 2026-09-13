namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Existující pod-okno docku (skupina tabů) jako cíl pro vložení další mini-app.
/// <see cref="Token"/> je neprůhledný klíč hosta - <c>AvalonDockHost</c> v něm veze svůj
/// <c>LayoutDocumentPane</c>, aby AvalonDock typy neprosákly do manageru ani do pickeru.
/// </summary>
public sealed record MiniAppTarget(object Token, IReadOnlyList<string> DocumentTitles, bool IsActive);

public enum MiniAppPlacementKind
{
    /// <summary>Do prvního (levého) pod-okna; když žádné není, vznikne a vyplní celou plochu.</summary>
    Fill,
    NewColumn,
    NewRow,
    IntoTarget,
}

/// <summary>Kam vložit nový panel. Nahrazuje dřívější dvojici <c>bool newColumn, newRow</c>.</summary>
public sealed record MiniAppPlacement(MiniAppPlacementKind Kind, MiniAppTarget? Target = null)
{
    public static MiniAppPlacement Fill { get; } = new(MiniAppPlacementKind.Fill);
    public static MiniAppPlacement NewColumn { get; } = new(MiniAppPlacementKind.NewColumn);
    public static MiniAppPlacement NewRow { get; } = new(MiniAppPlacementKind.NewRow);

    public static MiniAppPlacement Into(MiniAppTarget target) => new(MiniAppPlacementKind.IntoTarget, target);
}
