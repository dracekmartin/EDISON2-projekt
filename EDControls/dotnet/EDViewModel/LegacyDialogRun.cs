namespace EDControls.EDViewModel;

/// <summary>
/// Legacy dialog spuštěný mimo job object shellu (<see cref="IWindowNavigator.StartLegacyDetachedDialog"/>).
/// <see cref="WindowShown"/> doběhne s prvním oknem procesu (nebo po timeoutu, nebo koncem procesu) - do té doby
/// drží volající overlay „Akce probíhá". <see cref="Exited"/> doběhne koncem procesu.
/// </summary>
public sealed record LegacyDialogRun(Task WindowShown, Task Exited);
