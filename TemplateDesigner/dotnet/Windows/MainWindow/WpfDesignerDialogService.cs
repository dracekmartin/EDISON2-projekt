using System.Windows;
using Microsoft.Win32;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// WPF implementace <see cref="IDesignerDialogService"/> — jediné místo, kde se pro hlavní VM otevírají
/// okna, souborové dialogy a schránka. Drží owner okno (nastavení <c>Owner</c> pro modální dialogy).
/// Žádná logika navíc: mapuje dialogový VM na jeho okno a vrací výsledek.
/// </summary>
internal sealed class WpfDesignerDialogService(Window owner) : IDesignerDialogService
{
    public bool ShowInstancePicker(InstancePickerDialogViewModel vm) =>
        new InstancePickerDialog(vm) { Owner = owner }.ShowDialog() == true;

    public bool ShowChangeAgenda(ChangeAgendaDialogViewModel vm) =>
        new ChangeAgendaDialog(vm) { Owner = owner }.ShowDialog() == true;

    public bool ShowCacheTemplatePicker(CacheTemplatePickerDialogViewModel vm) =>
        new CacheTemplatePickerDialog(vm) { Owner = owner }.ShowDialog() == true;

    public bool ShowSaveToCache(SaveToCacheDialogViewModel vm) =>
        new SaveToCacheDialog(vm) { Owner = owner }.ShowDialog() == true;

    public string? PickOpenFile(string title, string filter)
    {
        var dlg = new OpenFileDialog { Title = title, Filter = filter };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickSaveFile(string title, string filter, string? fileName, string? defaultExt = null)
    {
        var dlg = new SaveFileDialog { Title = title, Filter = filter };
        if (fileName is not null) dlg.FileName = fileName;
        if (defaultExt is not null) dlg.DefaultExt = defaultExt;
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public void SetClipboardText(string text) => System.Windows.Clipboard.SetText(text);
}
