namespace EDISONMAIN2.Windows.LogoutCountdownWindow
{
    /// <summary>Stav odpočtu odděleně od DispatcherTimeru - vypršení se ohlásí právě jednou.</summary>
    internal sealed class CountdownState(int seconds)
    {
        public int SecondsLeft { get; private set; } = seconds;

        public string Text => $"Aplikace bude ukončena za {SecondsLeft} s.";

        /// <summary>Jeden tik; true právě při přechodu do vypršení.</summary>
        public bool Tick()
        {
            if (SecondsLeft <= 0) return false;
            SecondsLeft--;
            return SecondsLeft <= 0;
        }
    }
}
