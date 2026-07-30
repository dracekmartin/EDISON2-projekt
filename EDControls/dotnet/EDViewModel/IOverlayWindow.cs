namespace EDControls.EDViewModel;

/// <summary>
/// Marker pro **owned okna kreslená přes ownera** (panel Podpora, overlay „Akce probíhá").
/// Jsou to top-level okna, takže se bez markeru chovají jako okno modulu: objevila by se
/// v menu „Spuštěné moduly" a <see cref="WindowNavigator.CloseAll"/> by je zavřel — u panelu
/// nevratně, protože se k oknu připojuje jen jednou.
/// </summary>
internal interface IOverlayWindow;
