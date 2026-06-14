using System;
using System.Windows;
using System.Windows.Threading;

namespace EDISONMAIN2.Windows.LogoutCountdownWindow
{
    /// <summary>
    /// Non-modální top-most odpočet před vynuceným ukončením (cooperative logout, feature #2).
    /// Informativní — bez možnosti zrušit (server rozhodl). Po vypršení zavolá <c>onExpired</c>.
    /// Countdown timer je inherentně view-driven, proto v code-behind.
    /// </summary>
    public partial class LogoutCountdownWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly Action _onExpired;
        private int _secondsLeft;

        public LogoutCountdownWindow(int seconds, Action onExpired)
        {
            _onExpired = onExpired;
            _secondsLeft = seconds;
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
            _secondsLeft--;
            if (_secondsLeft <= 0)
            {
                _timer.Stop();
                Close();
                _onExpired();
                return;
            }
            UpdateText();
        }

        private void UpdateText() =>
            CountdownText.Text = $"Aplikace bude ukončena za {_secondsLeft} s.";

        private void PositionBottomRight()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 16;
            Top = area.Bottom - ActualHeight - 16;
        }
    }
}
