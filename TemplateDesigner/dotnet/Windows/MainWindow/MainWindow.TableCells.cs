using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

/// <summary>
/// Zadávání obsahu pevné tabulky **přímo na plátně** - dvojklik dovnitř, psaní do buňky, pohyb
/// klávesami, vkládání a mazání řádků a sloupců z místní nabídky.
///
/// Model je „objekt na stránce s excelovskými klávesami" (Word/PowerPoint), ne tabulkový procesor:
/// první klik vybere tabulku jako prvek, dvojklik teprve vejde dovnitř, Esc vrací o úroveň zpět.
/// Dokud je uživatel uvnitř, **klávesy patří buňce, ne prvku** - jinak by Delete při mazání překlepu
/// smazal celou tabulku.
/// </summary>
public partial class MainWindow
{
    // Tabulka, ve které jsme „uvnitř"; null = nejsme v žádné.
    private TableElement? _cellTable;
    private int _cellRow;
    private int _cellCol;

    /// <summary>Právě se píše do buňky (InlineEditBox je vidět) - odlišuje „vybraná buňka" od „píšu".</summary>
    private bool _cellEditing;
    private string? _cellEditOriginal;

    private bool InTableCells => _cellTable is not null;

    /// <summary>
    /// Poslední místo, kam se na prvek kliklo (mm relativně k prvku). Díky tomu F2 otevře **tu
    /// buňku, na kterou uživatel klikl**, ne pořád levou horní - klik prvek jen vybere, takže bez
    /// zapamatování by se ta informace ztratila.
    /// </summary>
    private (TemplateElement Element, float XMm, float YMm)? _lastElementClick;

    /// <summary>Zapamatuje místo kliknutí na prvek (volá se z mouse-down na plátně).</summary>
    private void RememberElementClick(TemplateElement element, float cx, float cy, float cw)
    {
        if (_canvas is null) return;
        var rect = _canvas.GetElementCanvasRect(element, cw);
        _lastElementClick = rect is null
            ? null
            : (element, (cx - rect.Value.Left) / _canvas.MmToPx, (cy - rect.Value.Top) / _canvas.MmToPx);
    }

    // ── Vstup a odchod ────────────────────────────────────────────────────────

    /// <summary>
    /// Vejde do tabulky (Alt+klik, F2 nebo místní nabídka). Vrací false pro jiné prvky, zamčenou
    /// nebo otočenou tabulku.
    /// <para>
    /// **Dvojklik sem vědomě nevede** - u všech ostatních prvků otevírá nastavení a tabulka by byla
    /// jediná výjimka (námitka Jana 12. 8.). Obsah se tedy otvírá klávesou F2, stejně jako u
    /// textového prvku, kde F2 taky znamená „uprav obsah na místě".
    /// </para>
    /// Vstupuje se do buňky, na kterou uživatel naposledy klikl; bez kliknutí do levé horní.
    /// </summary>
    private bool TryEnterTableCells(TemplateElement element, bool beginEdit = true)
    {
        if (_vm is null || _canvas is null) return false;
        if (element is not TableElement { Mode: TableMode.Fixed } table) return false;
        if (table.IsLocked || table.IsRotated) return false;

        var (rows, cols) = table.GridSize(_vm.State.CurrentPreviewData);
        if (rows == 0 || cols == 0) return false;

        var hit = _lastElementClick is { } click && ReferenceEquals(click.Element, table)
            ? table.HitTestCell(_vm.State.CurrentPreviewData, click.XMm, click.YMm)
            : null;
        _cellTable = table;
        _cellRow   = hit?.Row ?? 0;
        _cellCol   = hit?.Col ?? 0;

        // Vstup rovnou otevře buňku k psaní - F2 u textového prvku taky rovnou píše, ne že by
        // uživatel musel mačkat další klávesu. Výjimka je pravý klik: tam se buňka jen vybere,
        // protože uživatel míří na nabídku, ne na psaní.
        if (beginEdit) BeginCellEdit(replace: false);
        InvalidateCanvas();
        return true;
    }

    // ⚠️ Myší cesta dovnitř („druhý klik na už vybranou tabulku") tu **vědomě není** - zkusila se
    // 12. 8. a chovala se nepředvídatelně: první klik dvojkliku ji spustil dřív, než dorazil druhý,
    // takže na vybrané tabulce vlezl do buňky a na nevybrané otevřel nastavení. Odlišit obojí jde
    // jen čekáním na případný druhý klik, což by vstup do buňky pokaždé zdrželo. Dovnitř se tedy
    // chodí F2 nebo místní nabídkou.

    /// <summary>Odejde z tabulky ven na úroveň prvku. Rozepsaný text se potvrdí.</summary>
    private void ExitTableCells(bool commitEdit = true)
    {
        if (_cellEditing) EndCellEdit(commitEdit);
        _cellTable = null;
        InvalidateCanvas();
    }

    /// <summary>Klik jinam než do aktuální tabulky nás musí dostat ven - jinak by klávesy dál
    /// mířily do buňky prvku, který uživatel opustil.</summary>
    private void ExitTableCellsIfElsewhere(TemplateElement? hitElement)
    {
        if (InTableCells && !ReferenceEquals(hitElement, _cellTable)) ExitTableCells();
    }

    /// <summary>Klik uvnitř tabulky, ve které už jsme = přepnutí buňky (v tu chvíli se chová
    /// jako Excel - žádné další dvojkliky).</summary>
    private bool TrySelectCellAt(float cx, float cy, float cw)
    {
        if (_vm is null || _canvas is null || _cellTable is null) return false;

        var rect = _canvas.GetElementCanvasRect(_cellTable, cw);
        if (rect is null) return false;

        var hit = _cellTable.HitTestCell(_vm.State.CurrentPreviewData,
            (cx - rect.Value.Left) / _canvas.MmToPx,
            (cy - rect.Value.Top)  / _canvas.MmToPx);
        if (hit is null) return false;

        if (_cellEditing) EndCellEdit(commit: true);
        (_cellRow, _cellCol) = (hit.Value.Row, hit.Value.Col);
        InvalidateCanvas();
        return true;
    }

    // ── Psaní do buňky ────────────────────────────────────────────────────────

    /// <summary>Otevře textové pole přesně přes buňku. <paramref name="replace"/> = začít prázdný
    /// (uživatel začal psát), jinak se předvyplní stávající obsah (F2 nebo dvojklik).</summary>
    private void BeginCellEdit(bool replace)
    {
        if (_vm is null || _canvas is null || _cellTable is null) return;

        float cw = (float)skCanvas.ActualWidth;
        var elRect = _canvas.GetElementCanvasRect(_cellTable, cw);
        var cellMm = _cellTable.CellRectMm(_vm.State.CurrentPreviewData, _cellRow, _cellCol);
        if (elRect is null || cellMm is null) return;

        var cell = _cellTable.EnsureCell(_vm.State.CurrentPreviewData, _cellRow, _cellCol);
        if (cell is null) return;

        float px = _canvas.MmToPx;
        Canvas.SetLeft(InlineEditBox, elRect.Value.Left + cellMm.Value.Left * px);
        Canvas.SetTop (InlineEditBox, elRect.Value.Top  + cellMm.Value.Top  * px);
        InlineEditBox.Width  = cellMm.Value.Width  * px;
        InlineEditBox.Height = cellMm.Value.Height * px;

        // Písmo pole musí odpovídat tomu, co kreslí Skia - jinak text při psaní poskočí.
        var style = _cellTable.ResolveColumnDataParent();
        InlineEditBox.FontSize   = style.FontSize * (96f / 72f) * _vm.State.Zoom;
        InlineEditBox.FontFamily = new FontFamily(style.FontFamily);
        InlineEditBox.Text       = replace ? "" : cell.Content;
        InlineEditBox.Visibility = Visibility.Visible;

        _cellEditing      = true;
        _cellEditOriginal = cell.Content;

        InlineEditBox.Focus();
        InlineEditBox.CaretIndex = InlineEditBox.Text.Length;
        if (!replace) InlineEditBox.SelectAll();
    }

    /// <summary>Zavře textové pole; při potvrzení zapíše obsah přes příkaz, takže funguje Ctrl+Z.</summary>
    private void EndCellEdit(bool commit)
    {
        if (!_cellEditing || _vm is null || _cellTable is null) return;

        string newText = InlineEditBox.Text ?? "";
        string oldText = _cellEditOriginal ?? "";
        var cell = _cellTable.EnsureCell(_vm.State.CurrentPreviewData, _cellRow, _cellCol);

        _cellEditing      = false;
        _cellEditOriginal = null;
        InlineEditBox.Visibility = Visibility.Collapsed;
        HideSuggest();

        // Příkaz si **nesmí** držet instanci buňky: každá změna struktury (vložení řádku, tažení
        // hranice, vložení ze schránky) mřížku naklonuje, takže by zpětný krok psal do odpojené
        // buňky a tiše nic neudělal (nález revize 12. 8.). Adresuje se proto souřadnicí.
        if (commit && cell is not null && newText != oldText)
            _vm.Execute(CellContentCommand(_cellTable, _cellRow, _cellCol, oldText, newText));

        skCanvas.Focus();
        InvalidateCanvas();
    }

    // ── Klávesy ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Klávesy, když jsme uvnitř tabulky, ale zrovna nepíšeme. Vrací true = zpracováno, klávesa
    /// se nesmí pustit dál na globální zkratky (Delete by smazal prvek, šipky by hnuly tabulkou).
    /// </summary>
    private bool HandleTableCellsKey(KeyEventArgs e)
    {
        if (_vm is null || _cellTable is null || _cellEditing) return false;

        ClampCellSelection();
        if (_cellTable is null) return false;

        var (rows, cols) = _cellTable.GridSize(_vm.State.CurrentPreviewData);
        if (rows == 0 || cols == 0) { ExitTableCells(); return false; }

        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
        {
            case Key.Escape:
                ExitTableCells();
                return true;

            case Key.F2:
                BeginCellEdit(replace: false);
                return true;

            case Key.Delete:
            case Key.Back:
                ClearCell();
                return true;

            case Key.Tab:
                MoveCell(0, shift ? -1 : 1, rows, cols, wrap: true);
                return true;

            case Key.Enter:
                MoveCell(shift ? -1 : 1, 0, rows, cols);
                return true;

            // Schránka uvnitř tabulky patří buňkám, ne prvku - jinak by Ctrl+V uprostřed vyplňování
            // vložil kopii celé tabulky vedle.
            case Key.C when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                CopyCell();
                return true;

            case Key.X when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                CopyCell();
                ClearCell();
                return true;

            case Key.V when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                PasteIntoCells();
                return true;

            case Key.Up:    MoveCell(-1, 0, rows, cols); return true;
            case Key.Down:  MoveCell(1, 0, rows, cols);  return true;
            case Key.Left:  MoveCell(0, -1, rows, cols); return true;
            case Key.Right: MoveCell(0, 1, rows, cols);  return true;
        }

        return false;
    }

    /// <summary>
    /// Psaní na vybranou buňku ji rovnou přepíše (jako v Excelu). Chytá se to až na napsaném
    /// **znaku**, ne na klávese - jinak by se rozbila diakritika a rozložení klávesnice, protože
    /// z kódu klávesy se znak nedá spolehlivě odvodit.
    /// </summary>
    private void OnWindowTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!InTableCells || _cellEditing || e.Text.Length == 0 || char.IsControl(e.Text[0])) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;

        // ⚠️ Jen když píše uživatel do plátna. Bez téhle podmínky ukradne psaní každému poli
        // v panelu vlastností: režim buňky zůstává zapnutý i po potvrzení textu, takže první
        // napsaný znak by spadl do tabulky místo do pole (nález revize 12. 8.).
        if (!skCanvas.IsKeyboardFocusWithin) return;

        BeginCellEdit(replace: true);
        if (!_cellEditing) return;

        InlineEditBox.Text = e.Text;
        InlineEditBox.CaretIndex = e.Text.Length;
        e.Handled = true;
    }

    /// <summary>Klávesy v rozepsané buňce. Enter a Tab potvrdí a **posunou se dál** - to je ten
    /// excelovský pocit, kvůli kterému se dá tabulka vyplnit bez sahání na myš.</summary>
    private bool HandleCellEditKey(KeyEventArgs e)
    {
        if (!_cellEditing || _vm is null || _cellTable is null) return false;

        var (rows, cols) = _cellTable.GridSize(_vm.State.CurrentPreviewData);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        // Otevřená nabídka polí si bere šipky, Enter i Tab - jinak by Enter zavřel buňku místo
        // aby doplnil vybrané pole.
        if (SuggestOpen)
        {
            switch (e.Key)
            {
                case Key.Down:
                    CellFieldSuggestBox.SelectedIndex =
                        Math.Min(CellFieldSuggestBox.SelectedIndex + 1, CellFieldSuggestBox.Items.Count - 1);
                    return true;
                case Key.Up:
                    CellFieldSuggestBox.SelectedIndex = Math.Max(CellFieldSuggestBox.SelectedIndex - 1, 0);
                    return true;
                case Key.Enter:
                case Key.Tab:
                    CommitSuggest();
                    return true;
                case Key.Escape:
                    HideSuggest();   // první Esc zavře nabídku, teprve druhý zahodí úpravu buňky
                    return true;
            }
        }

        switch (e.Key)
        {
            case Key.Escape:
                EndCellEdit(commit: false);
                return true;

            case Key.Enter when !shift:
                EndCellEdit(commit: true);
                MoveCell(1, 0, rows, cols);
                return true;

            case Key.Tab:
                EndCellEdit(commit: true);
                MoveCell(0, shift ? -1 : 1, rows, cols, wrap: true);
                return true;
        }
        return false;
    }

    /// <summary>
    /// Posun po buňkách. Šipky a Enter drží svůj sloupec i řádek; **jen Tab** přeskakuje na konci
    /// řádku na začátek dalšího. Dřív se to počítalo přes jeden plochý index, takže šipka nahoru
    /// v prvním řádku skočila do jiného sloupce (nález revize 12. 8.).
    /// </summary>
    private void MoveCell(int dRow, int dCol, int rows, int cols, bool wrap = false)
    {
        if (wrap)
        {
            int index = Math.Clamp(_cellRow * cols + _cellCol + dCol, 0, rows * cols - 1);
            _cellRow = index / cols;
            _cellCol = index % cols;
        }
        else
        {
            _cellRow = Math.Clamp(_cellRow + dRow, 0, rows - 1);
            _cellCol = Math.Clamp(_cellCol + dCol, 0, cols - 1);
        }
        InvalidateCanvas();
    }

    /// <summary>Zápis obsahu buňky jako vratný příkaz - buňku dohledá podle souřadnice, takže
    /// funguje i po přeskládání mřížky.</summary>
    private SetPropertyCommand CellContentCommand(TableElement table, int row, int col,
        string oldText, string newText) =>
        new("Obsah buňky", oldText, newText, v =>
        {
            if (table.EnsureCell(_vm!.State.CurrentPreviewData, row, col) is { } c)
                c.Content = (string)(v ?? "");
        });

    /// <summary>
    /// Srovná vybranou buňku do mřížky. Ctrl+Z a Ctrl+Y jdou mimo náš kód, takže po vrácení
    /// vloženého řádku (nebo po smazání celé tabulky) ukazoval výběr mimo mřížku - zmizelo
    /// zvýraznění a psaní ani Delete nic nedělaly, dokud uživatel neklikl znovu (nález revize 12. 8.).
    /// </summary>
    private void ClampCellSelection()
    {
        if (_vm is null || _cellTable is null) return;

        bool vSablone = _vm.State.CurrentTemplate.Sections.Any(sec => sec.Elements.Contains(_cellTable));
        var (rows, cols) = _cellTable.GridSize(_vm.State.CurrentPreviewData);
        if (!vSablone || rows == 0 || cols == 0) { ExitTableCells(); return; }

        _cellRow = Math.Clamp(_cellRow, 0, rows - 1);
        _cellCol = Math.Clamp(_cellCol, 0, cols - 1);
    }

    private void ClearCell()
    {
        if (_vm is null || _cellTable is null) return;
        var cell = _cellTable.EnsureCell(_vm.State.CurrentPreviewData, _cellRow, _cellCol);
        if (cell is null || cell.Content.Length == 0) return;

        _vm.Execute(CellContentCommand(_cellTable, _cellRow, _cellCol, cell.Content, ""));
        InvalidateCanvas();
    }

    // ── Tažení hranic sloupců a řádků ─────────────────────────────────────────

    private enum DividerKind { Column, Row }

    /// <summary>Právě tažená hrana; null = netáhne se.</summary>
    private (DividerKind Kind, int Index)? _dividerDrag;

    /// <summary>Součet rozměrů dvojice u hrany, zjištěný na začátku tažení. Drží se konstantní,
    /// takže zbytek tabulky se nehne - kdyby se přepočítával průběžně, ujíždělo by to.</summary>
    private float _dividerPairSum;

    /// <summary>Odsazení začátku dvojice od horní/levé hrany prvku (mm) - taky se během tažení
    /// nemění, jinak by se rozměr počítal z rozhýbaného základu.</summary>
    private float _dividerPairStart;

    /// <summary>Myš se během tažení skutečně pohnula. Bez toho by pouhý klik na hranici zapsal
    /// prázdný krok do historie a označil šablonu za změněnou (nález revize 12. 8.).</summary>
    private bool _dividerMoved;
    private TableElement? _dividerTable;
    private TableElement.StructureSnapshot? _dividerBefore;

    /// <summary>
    /// Tabulka, jejíž hrany jde chytat: ta, ve které jsme uvnitř, **nebo prostě vybraná**.
    /// Kdyby to šlo jen uvnitř, musel by uživatel nejdřív Alt+kliknout (což rovnou otevře psaní)
    /// - rozměry přitom potřebuje srovnat dřív, než začne vyplňovat.
    /// </summary>
    private TableElement? DividerTable => _cellTable
        ?? (_vm?.State.SelectedElements is [TableElement { Mode: TableMode.Fixed } vybrana]
            && !vybrana.IsLocked && !vybrana.IsRotated ? vybrana : null);

    /// <summary>Jak blízko k hraně stačí být, aby se chytila (v px, ať to sedí na oko při každém
    /// přiblížení).</summary>
    private const float DividerHitPx = 4f;

    private bool InDividerDrag => _dividerDrag is not null;

    /// <summary>Hrana, kterou myš právě „drží" - podklad pro kurzor i pro začátek tažení.
    /// Svislá má přednost před vodorovnou (v rohu je pravděpodobnější, že uživatel míří na sloupec).</summary>
    private (DividerKind Kind, int Index)? DividerAt(float cx, float cy, float cw)
    {
        if (_vm is null || _canvas is null || DividerTable is not { } table) return null;

        var rect = _canvas.GetElementCanvasRect(table, cw);
        if (rect is null) return null;
        if (cx < rect.Value.Left || cx > rect.Value.Right) return null;
        if (cy < rect.Value.Top  || cy > rect.Value.Bottom) return null;

        float tolerance = DividerHitPx / _canvas.MmToPx;
        float xMm = (cx - rect.Value.Left) / _canvas.MmToPx;
        float yMm = (cy - rect.Value.Top)  / _canvas.MmToPx;
        var data = _vm.State.CurrentPreviewData;

        if (table.HitTestColumnDivider(data, xMm, tolerance) is { } col) return (DividerKind.Column, col);
        if (table.HitTestRowDivider(data, yMm, tolerance) is { } row)    return (DividerKind.Row, row);
        return null;
    }

    /// <summary>Začátek tažení hrany. Vrací false, když myš na hraně nestojí.</summary>
    private bool TryBeginDividerDrag(float cx, float cy, float cw)
    {
        if (DividerAt(cx, cy, cw) is not { } divider || DividerTable is not { } table) return false;

        if (_cellEditing) EndCellEdit(commit: true);

        var layout = table.ComputeLayout(_vm!.State.CurrentPreviewData);
        if (divider.Kind == DividerKind.Column)
        {
            _dividerPairStart = 0f;
            for (int i = 0; i < divider.Index; i++) _dividerPairStart += layout.ColumnWidths[i];
            _dividerPairSum = layout.ColumnWidths[divider.Index] + layout.ColumnWidths[divider.Index + 1];
        }
        else
        {
            _dividerPairStart = table.ShowHeader ? layout.HeaderHeightMm : 0f;
            for (int i = 0; i < divider.Index; i++) _dividerPairStart += layout.RowHeights[i];
            _dividerPairSum = layout.RowHeights[divider.Index] + layout.RowHeights[divider.Index + 1];
        }

        _dividerDrag   = divider;
        _dividerTable  = table;
        _dividerMoved  = false;
        _dividerBefore = table.CaptureStructure();
        skCanvas.CaptureMouse();
        return true;
    }

    /// <summary>Průběh tažení - rozměr je vzdálenost od začátku dvojice k myši; soused dostane
    /// zbytek, takže se zbytek tabulky nehne.</summary>
    private void UpdateDividerDrag(float cx, float cy, float cw)
    {
        if (_vm is null || _canvas is null || _dividerTable is not { } table
            || _dividerDrag is not { } drag) return;

        var rect = _canvas.GetElementCanvasRect(table, cw);
        if (rect is null) return;

        var data = _vm.State.CurrentPreviewData;
        _dividerMoved = true;

        if (drag.Kind == DividerKind.Column)
        {
            float sirka = (cx - rect.Value.Left) / _canvas.MmToPx - _dividerPairStart;
            table.SetColumnPair(data, drag.Index, sirka, _dividerPairSum);
            _vm.State.DragBadgeText = $"Šířka sloupce: " +
                $"{Math.Clamp(sirka, TableElement.MinColumnWidthMm, _dividerPairSum - TableElement.MinColumnWidthMm):0.#} mm";
        }
        else
        {
            float vyska = (cy - rect.Value.Top) / _canvas.MmToPx - _dividerPairStart;
            table.SetRowPair(data, drag.Index, vyska, _dividerPairSum);
            _vm.State.DragBadgeText = $"Výška řádku: " +
                $"{Math.Clamp(vyska, TableElement.MinRowHeightMm, _dividerPairSum - TableElement.MinRowHeightMm):0.#} mm";
        }

        InvalidateCanvas();
    }

    /// <summary>Konec tažení - výsledek jde přes příkaz, aby ho vrátil Ctrl+Z jedním krokem.</summary>
    private void EndDividerDrag(bool commit)
    {
        if (_vm is null || _dividerTable is not { } table || _dividerBefore is not { } before)
        {
            _dividerDrag  = null;
            _dividerTable = null;
            return;
        }

        var kind = _dividerDrag?.Kind ?? DividerKind.Column;
        bool moved = _dividerMoved;
        _dividerDrag   = null;
        _dividerTable  = null;
        _dividerBefore = null;
        _dividerMoved  = false;
        _vm.State.DragBadgeText = null;
        skCanvas.ReleaseMouseCapture();

        if (!commit || !moved) { table.RestoreStructure(before); InvalidateCanvas(); return; }

        var after = table.CaptureStructure();
        _vm.Execute(new SetPropertyCommand(
            kind == DividerKind.Column ? "Šířka sloupce" : "Výška řádku", before, after,
            v => table.RestoreStructure((TableElement.StructureSnapshot)v!)));
        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    // ── Našeptávač datových polí ──────────────────────────────────────────────

    /// <summary>Nabídka je otevřená - pak jí patří šipky a Enter, ne pohybu po buňkách.</summary>
    private bool SuggestOpen => CellFieldSuggestBox.Visibility == Visibility.Visible;

    /// <summary>Rozepsaný název pole: pozice „{", od které se filtruje. -1 = nabídka se netýká.</summary>
    private int _suggestTokenStart = -1;

    /// <summary>
    /// Doplňujeme právě vybrané pole do textu. Zápis do <c>Text</c> spustí <c>TextChanged</c>
    /// **synchronně**, tedy ještě než se posune kurzor - bez tohohle příznaku by se nabídka
    /// přepočítávala nad nekonzistentním stavem (kurzor před závorkou → záporná délka, pád).
    /// </summary>
    private bool _suggestApplying;

    /// <summary>
    /// Po každé změně textu zjistí, jestli kurzor stojí uvnitř nedopsaného <c>{POLE</c>, a podle
    /// toho nabídku otevře a přefiltruje. Filtruje se podle **názvu i popisku**, protože uživatel
    /// zná pole spíš pod českým popiskem než pod klíčem.
    /// </summary>
    private void InlineEditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suggestApplying) return;
        if (!_cellEditing || _vm is null) { HideSuggest(); return; }

        if (PlaceholderToken.Typing(InlineEditBox.Text, InlineEditBox.CaretIndex) is not var (open, prefix))
        {
            HideSuggest();
            return;
        }

        var items = _vm.PlaceholderFieldItems()
            .Where(i => prefix.Length == 0
                        || i.Key.Contains(prefix, StringComparison.OrdinalIgnoreCase)
                        || i.Label.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();

        if (items.Count == 0) { HideSuggest(); return; }

        _suggestTokenStart = open;
        CellFieldSuggestBox.ItemsSource = items;
        CellFieldSuggestBox.SelectedIndex = 0;

        // Pod editovanou buňkou; overlay má stejné souřadnice jako plátno.
        Canvas.SetLeft(CellFieldSuggestBox, Canvas.GetLeft(InlineEditBox));
        Canvas.SetTop (CellFieldSuggestBox, Canvas.GetTop(InlineEditBox) + InlineEditBox.Height);
        CellFieldSuggestBox.Visibility = Visibility.Visible;
    }

    private void HideSuggest()
    {
        _suggestTokenStart = -1;
        CellFieldSuggestBox.Visibility = Visibility.Collapsed;
    }

    /// <summary>Doplní vybrané pole i s uzavírací závorkou, ať uživatel nemusí psát „}".</summary>
    private void CommitSuggest()
    {
        if (!SuggestOpen || CellFieldSuggestBox.SelectedItem is not EDControls.ListItem item) return;

        string text = InlineEditBox.Text ?? "";
        int caret = Math.Clamp(InlineEditBox.CaretIndex, 0, text.Length);
        int start = Math.Clamp(_suggestTokenStart, 0, caret);

        // Uzavírací závorku nepřidávat dvakrát, když už ji uživatel napsal.
        bool zavorkaZa = caret < text.Length && text[caret] == '}';
        string nahrada = "{" + item.Key + (zavorkaZa ? "" : "}");

        HideSuggest();
        _suggestApplying = true;
        try
        {
            InlineEditBox.Text = text[..start] + nahrada + text[caret..];
            InlineEditBox.CaretIndex = start + nahrada.Length + (zavorkaZa ? 1 : 0);
        }
        finally { _suggestApplying = false; }
    }

    private void CellFieldSuggestBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CommitSuggest();
        InlineEditBox.Focus();
    }

    // ── Schránka ──────────────────────────────────────────────────────────────

    private void CopyCell()
    {
        if (_vm is null || _cellTable is null) return;
        var cell = _cellTable.EnsureCell(_vm.State.CurrentPreviewData, _cellRow, _cellCol);
        if (cell is not null) System.Windows.Clipboard.SetText(cell.Content);
    }

    /// <summary>
    /// Vloží blok ze schránky od vybrané buňky - typicky nakopírovaný kus Excelu. Obsah takových
    /// tabulek mají lidé často hotový v Excelu; bez tohohle by ho přepisovali ručně buňku po buňce.
    /// Tabulka se dorovná, aby se nic tiše neztratilo.
    /// </summary>
    private void PasteIntoCells()
    {
        if (_vm is null || _cellTable is null) return;

        string text = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : "";
        if (string.IsNullOrEmpty(text)) return;

        var block = TableElement.ParseClipboardBlock(text);
        if (block.Count == 0) return;

        int row = _cellRow, col = _cellCol;
        EditStructure("Vložení ze schránky",
            t => t.PasteBlock(_vm.State.CurrentPreviewData, row, col, block));
    }

    // ── Řádky a sloupce ───────────────────────────────────────────────────────

    /// <summary>Položky místní nabídky pro tabulku, ve které jsme uvnitř. Vrací false, když
    /// nejsme v tabulce - volající pak postaví běžné menu prvku.</summary>
    private bool TryBuildCellContextMenu(ContextMenu menu, float cx, float cy, float cw)
    {
        if (_vm is null || _cellTable is null) return false;
        TrySelectCellAt(cx, cy, cw);

        var table = _cellTable;
        void Add(string header, Action action) =>
            menu.Items.Add(new MenuItem
            {
                Header  = header,
                Command = new EDControls.EDViewModel.RelayCommand(action)
            });

        // Vložení nad / vlevo posune stávající obsah o jednu dál, takže s ním musí jít i výběr -
        // jinak by uživateli „uskočil" na nový prázdný řádek (nahlášeno 12. 8.).
        Add("Vložit řádek nad",  () => EditStructure("Vložení řádku", t => t.InsertRow(_cellRow), rowDelta: 1));
        Add("Vložit řádek pod",  () => EditStructure("Vložení řádku", t => t.InsertRow(_cellRow + 1)));
        Add("Smazat řádek",      () => EditStructure("Smazání řádku", t => t.RemoveRow(_cellRow)));
        menu.Items.Add(new Separator());

        int modelCol = table.ModelColumnIndex(_vm.State.CurrentPreviewData, _cellCol);
        Add("Vložit sloupec vlevo",  () => EditStructure("Vložení sloupce", t => t.InsertColumn(modelCol), colDelta: 1));
        Add("Vložit sloupec vpravo", () => EditStructure("Vložení sloupce", t => t.InsertColumn(modelCol + 1)));
        Add("Smazat sloupec",        () => EditStructure("Smazání sloupce", t => t.RemoveColumn(modelCol)));
        menu.Items.Add(new Separator());
        Add("Kopírovat buňku (Ctrl+C)", CopyCell);
        Add("Vložit ze schránky (Ctrl+V)", PasteIntoCells);
        Add("Vymazat obsah buňky (Del)", ClearCell);

        // „Vlastnosti tabulky" tu vědomě nejsou - nabídka pokračuje standardními položkami prvku
        // (Vyjmout, Kopírovat, Smazat, Vrstva, Vlastnosti), společnými pro všechny prvky.
        return true;
    }

    /// <summary>
    /// Undoable změna struktury. Řádky, sloupce i výška se vrací jedním snímkem - dělat z každé
    /// z nich vlastní příkaz by znamenalo, že Ctrl+Z vrátí půl operace.
    /// </summary>
    /// <param name="rowDelta">Posun výběru, když se obsah odsunul (vložení nad / vlevo).</param>
    private void EditStructure(string label, Action<TableElement> change, int rowDelta = 0, int colDelta = 0)
    {
        if (_vm is null || _cellTable is null) return;
        var table = _cellTable;

        var before = table.CaptureStructure();
        change(table);
        var after = table.CaptureStructure();

        _vm.Execute(new SetPropertyCommand(label, before, after,
            v => table.RestoreStructure((TableElement.StructureSnapshot)v!)));

        // Výběr jde s obsahem (viz rowDelta); po smazání může skončit mimo mřížku.
        var (rows, cols) = table.GridSize(_vm.State.CurrentPreviewData);
        _cellRow = Math.Clamp(_cellRow + rowDelta, 0, Math.Max(0, rows - 1));
        _cellCol = Math.Clamp(_cellCol + colDelta, 0, Math.Max(0, cols - 1));

        UpdatePropertiesPanel();
        InvalidateCanvas();
    }

    // ── Kreslení ──────────────────────────────────────────────────────────────

    /// <summary>Zvýraznění vybrané buňky a rámeček kolem tabulky, ve které jsme uvnitř. Bez toho
    /// by uživatel nepoznal, jestli klávesy míří do buňky, nebo na prvek.</summary>
    private void DrawTableCellOverlay(SKCanvas canvas, float cw)
    {
        if (_vm is null || _canvas is null || _cellTable is null || _cellEditing) return;

        ClampCellSelection();
        if (_cellTable is null) return;

        var elRect = _canvas.GetElementCanvasRect(_cellTable, cw);
        var cellMm = _cellTable.CellRectMm(_vm.State.CurrentPreviewData, _cellRow, _cellCol);
        if (elRect is null || cellMm is null) return;

        float px = _canvas.MmToPx;
        var cellRect = new SKRect(
            elRect.Value.Left + cellMm.Value.Left  * px,
            elRect.Value.Top  + cellMm.Value.Top   * px,
            elRect.Value.Left + cellMm.Value.Right * px,
            elRect.Value.Top  + cellMm.Value.Bottom * px);

        using var fill   = new SKPaint { Color = new SKColor(0x222563EB) };
        using var border = new SKPaint
        {
            Color = new SKColor(0xFF2563EB), IsStroke = true, StrokeWidth = 2f, IsAntialias = true
        };
        canvas.DrawRect(cellRect, fill);
        canvas.DrawRect(cellRect, border);

        using var tableBorder = new SKPaint
        {
            Color = new SKColor(0x802563EB), IsStroke = true, StrokeWidth = 1f, IsAntialias = true
        };
        canvas.DrawRect(elRect.Value, tableBorder);
    }
}
