using ALWA.SYS;

namespace EDControls.EDViewModel;

/// <summary>
/// Base ViewModel for windows that communicate with the Caché backend via MLRest.
/// Use plain EDViewModel for windows that don't need server access.
/// </summary>
public class EDDataViewModel(MLRest mlRest) : EDViewModel
{
    public MLRest MLRest { get; } = mlRest;
}
