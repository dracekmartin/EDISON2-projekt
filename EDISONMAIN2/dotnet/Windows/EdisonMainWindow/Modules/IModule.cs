namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public interface IModule
    {
        string DisplayedName { get; }

        /// <summary>Hex barva (<c>"#RRGGBB"</c>) pro Foreground uzlu ve stromu; "" = výchozí barva tématu.</summary>
        string Color { get; }
    }
}
