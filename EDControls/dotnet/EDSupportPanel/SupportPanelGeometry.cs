namespace EDControls.EDSupportPanel;

/// <summary>Umístění okna panelu (DIP, obrazovkové souřadnice).</summary>
internal readonly record struct SupportPanelPlacement(double Left, double Top, double Width);

/// <summary>
/// Výpočet pozice vysouvacího panelu bez WPF závislostí. Panel je okno u pravého okraje
/// klientské oblasti ownera, které podle <c>slideOffset</c> roste **doleva** - obsah
/// (úchyt vlevo, karta vpravo) zůstává zarovnaný vlevo a hranice okna ho klipuje, takže
/// v zavřeném stavu je vidět jen úchyt a karta se odkrývá směrem doleva.
/// </summary>
internal static class SupportPanelGeometry
{
    /// <summary>Šířka úchytu - jediné, co je vidět v zavřeném stavu.</summary>
    internal const double TabWidth = 12;

    /// <summary>Šířka karty s tlačítky (bez úchytu).</summary>
    internal const double CardWidth = 170;

    /// <summary>
    /// Vejde se panel do klientské oblasti ownera? Karta žije v samostatném okně, takže by
    /// u menšího okna přečnívala mimo jeho hranu (přes footer s tlačítky) - tam panel radši
    /// nepřipojíme vůbec, než abychom kreslili kartu mimo dialog.
    /// </summary>
    internal static bool Fits(double clientWidth, double clientHeight, double panelHeight) =>
        clientWidth >= TabWidth + CardWidth && clientHeight >= panelHeight;

    internal static SupportPanelPlacement Compute(
        double clientLeft, double clientTop, double clientWidth, double clientHeight,
        double panelHeight, double slideOffset)
    {
        var offset = Math.Clamp(slideOffset, 0, CardWidth);
        var width = Math.Min(TabWidth + offset, Math.Max(clientWidth, TabWidth));
        var left = clientLeft + clientWidth - width;

        // Svisle na střed, ale nikdy pod spodní hranou klientské oblasti (owner se může
        // zmenšit pod výšku karty i po připojení panelu - resize, změna fontu).
        var top = clientTop + Math.Max(0, Math.Min(
            (clientHeight - panelHeight) / 2,
            clientHeight - panelHeight));

        return new SupportPanelPlacement(Left: left, Top: top, Width: width);
    }
}
