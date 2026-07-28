using SkiaSharp;

namespace TemplateDesigner.Designer;

/// <summary>
/// Barva plochy pod stránkou - jediné místo, odkud ji berou <b>oba</b> SkiaSharp canvasy
/// (návrhář i náhled) i XAML kolem nich (token <c>EDTheme.Bg.Canvas</c>).
///
/// <para>Proč settable a ne konstanta: plocha musí sedět s pozadím, které kreslí WPF kolem
/// canvasu - jinak vznikne šev na hranici plátna/náhledu. Naplňuje ji View z theme tokenu
/// (WPF stranu drží <c>MainWindow</c>), aby tato vrstva nepotřebovala referenci na WPF.</para>
///
/// <para>Papír stránky, pravítka a chrome návrháře <b>záměrně</b> zůstávají pevné - papír je
/// bílý, protože se tiskne na bílý; tmavý režim mění jen plochu kolem něj.</para>
/// </summary>
public static class CanvasTheme
{
    /// <summary>Plocha pod/kolem stránky. Default = světlý motiv (kdyby View nic nenastavil).</summary>
    public static SKColor Background { get; set; } = new(0xFFE4E4E4);
}
