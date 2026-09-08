namespace EDTesting;

/// <summary>
/// Spuštění akce na STA threadu - WPF controly jdou konstruovat jen tam, xUnit běží na MTA.
/// Výjimka z akce se přehodí volajícímu, aby assert selhal v testu, ne na cizím threadu.
/// </summary>
public static class Sta
{
    public static void Run(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
