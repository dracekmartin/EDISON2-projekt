using System.Windows.Input;
using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Ukázková plná-MVVM mini-app (dev placeholder): vlastní VM + View + DataTemplate. Demonstruje
/// <c>Command(...)</c>, <c>Set</c>/<c>OnPropertyChanged</c> a nezávislý stav per instance
/// (otevři vícekrát → každé počítadlo počítá zvlášť). Vzor pro reálné agendové mini-apps.
/// </summary>
public sealed class DevCounterMiniAppViewModel : EDMiniAppViewModel
{
    public DevCounterMiniAppViewModel() : this(EDServices.Current) { }

    public DevCounterMiniAppViewModel(EDServices services) : base(services)
    {
        IncrementCommand = Command(() => Count++);
        ResetCommand = Command(() => Count = 0);
    }

    public override string Title => "Počítadlo";

    private int _count;
    public int Count { get => _count; private set => Set(ref _count, value); }

    public ICommand IncrementCommand { get; }
    public ICommand ResetCommand { get; }
}
