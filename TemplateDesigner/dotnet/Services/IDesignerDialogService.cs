namespace TemplateDesigner.Services;

/// <summary>
/// Abstrakce View vrstvy pro ViewModel (pravidlo R4 - ViewModel bez WPF referencí): modální dialogy,
/// souborové dialogy a systémová schránka. Drží `Window`/`ShowDialog`/`OpenFileDialog`/`Clipboard`
/// mimo <see cref="TemplateDesignerViewModel"/> → VM je unit-testovatelný (mock této služby).
/// Implementace žije ve View vrstvě (<see cref="WpfDesignerDialogService"/>, drží owner okno).
///
/// Dialogové ViewModely (`*DialogViewModel`) staví volající VM sám (VM→VM, žádné WPF); tato služba
/// je jen zobrazí v okně a vrátí výsledek (true = OK, false = zrušeno). Výstupní hodnoty čte volající
/// z předaného dialogového VM (`SelectedInstances`, `ChosenCode`, …).
/// </summary>
public interface IDesignerDialogService
{
    /// <summary>Zobrazí modální dialog nad daným dialogovým VM. Vrátí true = potvrzeno, false = zrušeno.</summary>
    bool ShowInstancePicker(InstancePickerDialogViewModel vm);

    bool ShowChangeAgenda(ChangeAgendaDialogViewModel vm);

    bool ShowCacheTemplatePicker(CacheTemplatePickerDialogViewModel vm);

    bool ShowSaveToCache(SaveToCacheDialogViewModel vm);

    /// <summary>
    /// Rychlý formulář nad libovolným objektem podle EDInputForm schématu (rozměr nové tabulky).
    /// Po potvrzení jsou hodnoty zapsané do <paramref name="target"/>. Vrátí false = zrušeno.
    /// </summary>
    bool ShowQuickForm(object target, string cacheClass, string title);

    /// <summary>Otevírací souborový dialog. Vrátí vybranou cestu, nebo null při zrušení.</summary>
    string? PickOpenFile(string title, string filter);

    /// <summary>Ukládací souborový dialog. Vrátí vybranou cestu, nebo null při zrušení.</summary>
    string? PickSaveFile(string title, string filter, string? fileName, string? defaultExt = null);

    /// <summary>Zkopíruje text do systémové schránky.</summary>
    void SetClipboardText(string text);
}
