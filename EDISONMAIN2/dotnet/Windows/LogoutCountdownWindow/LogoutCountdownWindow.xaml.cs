using System;
using System.Windows;
using System.Windows.Threading;
// Alias proti ambient System.Drawing (UseWindowsForms je zapnuté pro WinForms color picker).
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace EDISONMAIN2.Windows.LogoutCountdownWindow
{
    /// <summary>
    /// Non-modální top-most odpočet před vynuceným ukončením (cooperative logout).
    /// Informativní - bez možnosti zrušit (server rozhodl). Po vypršení zavolá <c>onExpired</c>.
    /// Countdown timer je inherentně view-driven, proto v code-behind.
    /// </summary>
    public partial class LogoutCountdownWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly Action _onExpired;
        private readonly CountdownState _state;

        public LogoutCountdownWindow(int seconds, Action onExpired)
        {
            _onExpired = onExpired;
            _state = new CountdownState(seconds);
            InitializeComponent();
            UpdateText();
            // Až po layoutu jsou ActualWidth/Height známé (SizeToContent).
            Loaded += (_, _) => PositionBottomRight();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }

        private void Tick()
        {
            if (_state.Tick())
            {
                _timer.Stop();
                Close();
                _onExpired();
                return;
            }
            UpdateText();
        }

        private void UpdateText() => CountdownText.Text = _state.Text;

        // Okno nemá vlastníka, monitor shellu se proto hledá přes hlavní okno aplikace.
        private void PositionBottomRight()
        {
            var topLeft = BottomRight(EDControls.MonitorPlacement.FallbackWorkArea(this), new Size(ActualWidth, ActualHeight));
            Left = topLeft.X;
            Top = topLeft.Y;
        }

        internal static Point BottomRight(Rect workArea, Size window) =>
            new(workArea.Right - window.Width - EdgeGap, workArea.Bottom - window.Height - EdgeGap);

        private const double EdgeGap = 16;
    }
}
