using System.Windows;
using EDControls;

namespace EDISONMAIN2;

/// <summary>
/// Geometrie oken E2E běhu jen v paměti, podle formID: dialog otevřený podruhé se vrátí na místo
/// a vývojářova geometrie v Caché se nečte ani nepřepíše. Obdoba <see cref="TransientLocalPrefs"/>.
/// </summary>
internal sealed class TransientWindowStateStore
{
    private readonly Dictionary<string, WindowGeometry> _saved = [];

    public IWindowStateStore For(string formId) => new Store(_saved, formId);

    private sealed class Store(Dictionary<string, WindowGeometry> saved, string formId) : IWindowStateStore
    {
        public WindowGeometry Load() => saved.GetValueOrDefault(formId);

        public void Save(Rect bounds, bool maximized) =>
            saved[formId] = new WindowGeometry(bounds.Left, bounds.Top, bounds.Width, bounds.Height, maximized);
    }
}
