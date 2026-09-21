using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

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

    /// <summary>Kotva výběru - buňka, ve které výběr začal. Obdélník se dopočítá proti aktivní
    /// buňce (<see cref="_cellRow"/>/<see cref="_cellCol"/>), takže rozšiřování kotvu nechává být.</summary>
    private int _cellAnchorRow;
    private int _cellAnchorCol;

    /// <summary>Právě se táhne výběr myší.</summary>
    private bool _cellRangeDrag;

    private bool InCellRangeDrag => _cellRangeDrag && InTableCells;

    private CellRange CellSelection => new(_cellAnchorRow, _cellAnchorCol, _cellRow, _cellCol);

    /// <summary>Přepne výběr; aktivní buňka je vždy ta, kam se píše a podle které jdou řádky a sloupce.</summary>
    private void SetCellSelection(CellRange range)
    {
        _cellAnchorRow = range.AnchorRow;
        _cellAnchorCol = range.AnchorCol;
        _cellRow       = range.ActiveRow;
        _cellCol       = range.ActiveCol;
    }

    /// <summary>Právě se píše do buňky (InlineEditBox je vidět) - odlišuje „vybraná buňka" od „píšu".</summary>
    private bool _cellEditing;
    private string? _cellEditOriginal;

    private bool InTableCells => _cellTable is not null;

    /// <summary>
    /// Klávesy patří vybrané buňce. **Nerozhoduje o tom fokus plátna** - ten mu vezme i kliknutí
    /// na tlačítko formátovací lišty, takže po ztučnění se do buňky nedalo psát, přestože zůstala
    /// zvýrazněná (nález Jan 17. 8.). Rozhoduje, jestli si klávesy nenárokuje něco, kam se opravdu
    /// píše: panel vlastností, formulář prvku nebo jiné textové pole (třeba filtr stromu).
    /// </summary>
    private bool CellKeysActive =>
        InTableCells
        && !InlinePropertiesPanel.IsKeyboardFocusWithin
        && !InlineElementForm.IsKeyboardFocusWithin
        && Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase
        && Keyboard.FocusedElement is not ComboBox;

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
        SetCellSelection(CellRange.Single(hit?.Row ?? 0, hit?.Col ?? 0));

        // Vstup rovnou otevře buňku k psaní - F2 u textového prvku taky rovnou píše, ne že by
        // uživatel musel mačkat další klávesu. Výjimka je pravý klik: tam se buňka jen vybere,
        // protože uživatel míří na nabídku, ne na psaní.
        // Panel a lišta se přestaví **před** otevřením buňky k psaní - stavba panelu jinak přijde
        // až po `InlineEditBox.Focus()` a fokus se z rozepsané buňky ztratí (nález Jan 17. 8.).
        SyncFormatToolbar();
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
        SyncFormatToolbar();
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

        // Shift rozšíří výběr od kotvy (jako v Excelu), obyčejný klik ho smrskne na jednu buňku
        // a zároveň začne tažení - výběr oblasti tak vznikne přirozeným tahem, bez modifikátoru.
        SetCellSelection(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
            ? CellSelection.ExtendTo(hit.Value.Row, hit.Value.Col)
            : CellRange.MoveTo(hit.Value.Row, hit.Value.Col));
        _cellRangeDrag = true;

        SyncFormatToolbar();
        InvalidateCanvas();
        return true;
    }

    /// <summary>Buňka pod pravým klikem: uvnitř výběru se výběr zachová, mimo něj se přesune.</summary>
    private void SelectForContextMenu(float cx, float cy, float cw)
    {
        if (_vm is null || _canvas is null || _cellTable is null) return;
        var rect = _canvas.GetElementCanvasRect(_cellTable, cw);
        if (rect is null) return;

        var hit = _cellTable.HitTestCell(_vm.State.CurrentPreviewData,
            (cx - rect.Value.Left) / _canvas.MmToPx,
            (cy - rect.Value.Top)  / _canvas.MmToPx);
        if (hit is null || CellSelection.Contains(hit.Value.Row, hit.Value.Col)) return;

        TrySelectCellAt(cx, cy, cw);
    }

    /// <summary>Tažení výběru - kotva zůstává, aktivní buňka jde za myší. Mimo mřížku se drží
    /// poslední trefená buňka, ať výběr při vyjetí z tabulky neposkakuje.</summary>
    private void UpdateCellRangeDrag(float cx, float cy, float cw)
    {
        if (_vm is null || _canvas is null || _cellTable is null) return;
        var rect = _canvas.GetElementCanvasRect(_cellTable, cw);
        if (rect is null) return;

        var hit = _cellTable.HitTestCell(_vm.State.CurrentPreviewData,
            (cx - rect.Value.Left) / _canvas.MmToPx,
            (cy - rect.Value.Top)  / _canvas.MmToPx);
        if (hit is null || (hit.Value.Row == _cellRow && hit.Value.Col == _cellCol)) return;

        SetCellSelection(CellSelection.ExtendTo(hit.Value.Row, hit.Value.Col));
        SyncFormatToolbar();
        InvalidateCanvas();
    }

    private void EndCellRangeDrag() => _cellRangeDrag = false;

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

        // Písmo pole musí odpovídat tomu, co kreslí Skia - jinak text při psaní poskočí. Bere se
        // vyřešený styl **té buňky** (ne celého sloupce), protože buňka může mít vlastní písmo.
        var style = CellFormatting?.ActiveStyle() ?? _cellTable.ResolveColumnDataParent();
        InlineEditBox.FontSize   = style.FontSize * (96f / 72f) * _vm.State.Zoom;
        InlineEditBox.FontFamily = new FontFamily(style.FontFamily);
        InlineEditBox.FontWeight = style.Bold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal;
        InlineEditBox.FontStyle  = style.Italic ? FontStyles.Italic : FontStyles.Normal;
        InlineEditBox.Text       = replace ? "" : cell.Content;
        InlineEditBox.Visibility = Visibility.Visible;

        _cellEditing      = true;
        _cellEditOriginal = cell.Content;

        InlineEditBox.Focus();
        InlineEditBox.CaretIndex = InlineEditBox.Text.Length;
        if (!replace) InlineEditBox.SelectAll();

        // Fokus se pod probíhajícím kliknutím nemusí udržet - plátno si ho po dokončení obsluhy
        // myši bere zpět a napsané znaky by spadly do prázdna. Proto ještě jednou, až vstup doběhne.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_cellEditing && !InlineEditBox.IsKeyboardFocusWithin) InlineEditBox.Focus();
        }), System.Windows.Threading.DispatcherPriority.Input);
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
    /// Zkratky formátování buňky (Ctrl+B/I/U) - platí i **během psaní** do buňky, jako ve Wordu
    /// a Excelu. Uvnitř tabulky přebíjejí globální význam (Ctrl+B jinak sbaluje levý panel):
    /// v tabulce čeká uživatel tučné, a bez toho by zkratka mimo tabulku zmizet nemohla.
    /// </summary>
    private bool HandleCellFormatKey(KeyEventArgs e)
    {
        if (_cellTable is null || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return false;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return false;
        if (CellFormatting is not { } formatting) return false;

        // Nová hodnota se spočítá **jednou** z aktivní buňky (jako ve Wordu), ne pro každou zvlášť -
        // u výběru oblasti by se jinak buňky navzájem prohodily místo srovnání na jednu hodnotu.
        var style = formatting.ActiveStyle();
        var (label, change) = e.Key switch
        {
            Key.B => ("Tučné",     Set(s => s.Bold          = !style.Bold)),
            Key.I => ("Kurzíva",   Set(s => s.Italic        = !style.Italic)),
            Key.U => ("Podtržení", Set(s => s.Underline     = !style.Underline)),
            _     => (null, null)
        };
        if (label is null || change is null) return false;

        static Action<TextStyle> Set(Action<TextStyle> action) => action;

        if (formatting.Apply(label, change) is { } command) _vm!.Execute(command);
        SyncFormatToolbar();
        InvalidateCanvas();
        InvalidatePreview();
        return true;
    }

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

        // S Altem hlásí WPF klávesu jako systémovou - bez tohohle by Alt+Enter dorazil jako Key.System.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        switch (key)
        {
            case Key.Escape:
                ExitTableCells();
                return true;

            case Key.F2:
                BeginCellEdit(replace: false);
                return true;

            // Alt+Enter = vlastnosti (zavedené ve Windows). Samotný Enter musí zůstat posunu
            // o buňku níž, jinak by se rozbilo vyplňování z klávesnice.
            case Key.Enter when Keyboard.Modifiers.HasFlag(ModifierKeys.Alt):
                OpenCellProperties();
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

            // Celý řádek a sloupec se vědomě nevybírají zkratkou (rozhodnutí Jan 17. 8.) - tažení
            // a Shift pokrývají totéž a excelové Shift/Ctrl+mezerník se uživatel nemá kde naučit.
            case Key.A when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                SelectWholeTable();
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

            // Se Shiftem šipka výběr rozšiřuje, bez něj ho smrskne na jednu buňku (Excel).
            case Key.Up:    MoveCell(-1, 0, rows, cols, extend: shift); return true;
            case Key.Down:  MoveCell(1, 0, rows, cols, extend: shift);  return true;
            case Key.Left:  MoveCell(0, -1, rows, cols, extend: shift); return true;
            case Key.Right: MoveCell(0, 1, rows, cols, extend: shift);  return true;
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

        // ⚠️ Ne, když si klávesy nárokuje pole, kam se píše: režim buňky zůstává zapnutý i po
        // potvrzení textu, takže první napsaný znak by spadl do tabulky místo do pole v panelu
        // (nález revize 12. 8.).
        if (!CellKeysActive) return;

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
    private void MoveCell(int dRow, int dCol, int rows, int cols, bool wrap = false, bool extend = false)
    {
        int radek, sloupec;
        if (wrap)
        {
            int index = Math.Clamp(_cellRow * cols + _cellCol + dCol, 0, rows * cols - 1);
            radek   = index / cols;
            sloupec = index % cols;
        }
        else
        {
            radek   = Math.Clamp(_cellRow + dRow, 0, rows - 1);
            sloupec = Math.Clamp(_cellCol + dCol, 0, cols - 1);
        }

        SetCellSelection(extend
            ? CellSelection.ExtendTo(radek, sloupec)
            : CellRange.MoveTo(radek, sloupec));
        SyncFormatToolbar();
        InvalidateCanvas();
    }

    private void SelectWholeTable() => SelectRange(_ => CellRange.Whole(GridRows(), GridCols()));

    private int GridRows() => _cellTable is null || _vm is null
        ? 0 : _cellTable.GridSize(_vm.State.CurrentPreviewData).Rows;

    private int GridCols() => _cellTable is null || _vm is null
        ? 0 : _cellTable.GridSize(_vm.State.CurrentPreviewData).Cols;

    private void SelectRange(Func<CellRange, CellRange> zmena)
    {
        if (GridRows() == 0 || GridCols() == 0) return;
        if (_cellEditing) EndCellEdit(commit: true);
        SetCellSelection(zmena(CellSelection));
        SyncFormatToolbar();
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

        SetCellSelection(CellSelection.Clamp(rows, cols));
    }

    /// <summary>Vymaže obsah celého výběru jedním vratným krokem (styl zůstává).</summary>
    private void ClearCell()
    {
        if (_vm is null || _cellTable is null || CellFormatting is not { } formatting) return;

        var data   = _vm.State.CurrentPreviewData;
        var before = formatting.Capture();
        if (!_cellTable.ClearRangeContent(data, CellSelection)) return;

        _vm.Execute(formatting.Command("Obsah buňky", before));
        InvalidateCanvas();
    }

    // ── Formátování buňky ─────────────────────────────────────────────────────

    /// <summary>
    /// Buňky, na které míří formátování a mazání: celý výběr, **aktivní buňkou napřed** (podle ní
    /// se počítá nová hodnota a od ní se dorovnávají ostatní).
    /// </summary>
    private IReadOnlyList<(int Row, int Col)> TargetCells =>
        _cellTable is null
            ? []
            : [(_cellRow, _cellCol), .. CellSelection.Cells().Where(c => c != (_cellRow, _cellCol))];

    /// <summary>
    /// Přepne formátovací lištu mezi buňkou a prvkem. Volá se i při každém posunu po buňkách,
    /// takže **nesmí** sahat na panel vlastností - ten by při každé šipce znovu načítal formulář
    /// ze serveru.
    /// </summary>
    private void SyncFormatToolbar()
    {
        if (!InTableCells || CellFormatting is not { } formatting) { RefreshInlineProperties(); return; }

        Action<IDesignerCommand> execute = cmd => _vm!.Execute(cmd);
        Action invalidate = () => { skCanvas.InvalidateVisual(); InvalidatePreview(); };

        // Po akci z lišty se klávesy vrací buňce - tlačítko si jinak fokus nechá a psaní přestane
        // fungovat (Excel se chová stejně: po formátování zůstane aktivní mřížka).
        FormatToolbar.SetContext(execute, () => { invalidate(); ReturnKeysToCell(); });
        FormatToolbar.ShowDescriptors(formatting.Descriptors());

        // Panel patří uvnitř tabulky **té buňce**, ne prvku. Dokud ukazoval nastavení celé tabulky,
        // stála v něm a v liště dvě zaškrtávátka „Tučné" s jiným dosahem a nešlo je od sebe poznat
        // (nález Jan 17. 8.). Nastavení celé tabulky je za tlačítkem v prvním řádku panelu.
        if (_cellTable!.EnsureCell(_vm!.State.CurrentPreviewData, _cellRow, _cellCol) is { } cell)
        {
            InlineElementForm.SetContext(CellCommandExecutor(formatting), invalidate);
            InlineElementForm.PlaceholderFields = _vm.PlaceholderFieldItems();
            InlineElementForm.InheritedValues   = formatting.InheritedFormValues();
            InlineElementForm.ShowElement(cell, CellCacheClass);
            ShowInlineFormPanel(true);
        }

        // Panel se staví i při každém posunu po buňkách; rozepsaná buňka si musí fokus udržet,
        // jinak napsané znaky spadnou do prázdna.
        if (_cellEditing && !InlineEditBox.IsKeyboardFocusWithin) InlineEditBox.Focus();
    }

    /// <summary>
    /// Vrátí klávesy tam, odkud přišly: do rozepsané buňky, jinak na plátno. Volá se po akci,
    /// která si vzala fokus (tlačítko lišty, zavřený dialog) - bez toho zůstane buňka zvýrazněná,
    /// ale psaní i Delete padají do prázdna.
    /// </summary>
    private void ReturnKeysToCell()
    {
        if (_cellEditing) InlineEditBox.Focus();
        else skCanvas.Focus();
    }

    /// <summary>Tlačítko „Nastavení celé tabulky…" - odejde z buňky, takže panel ukáže prvek.
    /// Táž cesta jako Esc, jen pojmenovaná (z panelu nebylo poznat, že se tam takhle dostane).</summary>
    private void ExitToTableSettings()
    {
        ExitTableCells();
        skCanvas.Focus();
    }

    /// <summary>Formátování aktivní buňky - kaskáda, vratný zápis i vlastnosti pro lištu žijí
    /// v modelu (<see cref="TableCellFormatting"/>), tady zůstává jen napojení na plátno.</summary>
    private TableCellFormatting? CellFormatting =>
        _cellTable is null || _vm is null
            ? null
            : new(_cellTable, () => _vm!.State.CurrentPreviewData, TargetCells);

    /// <summary>
    /// Vlastnosti řádku - pozadí a **podmínka viditelnosti**. Tisk podmínku respektoval odjakživa,
    /// ale od zrušení pětizáložkového dialogu nebylo odkud ji zadat; výraz se vyhodnocuje toutéž
    /// funkcí jako u prvků, takže tu nevzniká druhá gramatika.
    /// <para>Změna jde do historie jako **snímek struktury** - řádky se při každé úpravě mřížky
    /// klonují, takže příkaz držící instanci řádku by po zpětném kroku psal do odpojeného objektu.</para>
    /// </summary>
    private void OpenRowProperties()
    {
        if (_vm is null || _cellTable is null) return;
        if (_cellEditing) EndCellEdit(commit: true);

        var table = _cellTable;
        var rows  = table.VisibleRows(_vm.State.CurrentPreviewData);
        if (_cellRow < 0 || _cellRow >= rows.Count) return;

        var dlg = new ElementEditDialog(rows[_cellRow], "ALVA.TemplateDesigner.EDInputForm.TableRowEdit",
            "Vlastnosti řádku") { Owner = this, PlaceholderFields = _vm.PlaceholderFieldItems() };

        var before = table.CaptureStructure();
        if (dlg.ShowDialog() == true && dlg.ResultCommand is { } result)
        {
            result.Execute();
            _vm.Execute(new SetPropertyCommand("Vlastnosti řádku", before, table.CaptureStructure(),
                v => table.RestoreStructure((TableElement.StructureSnapshot)v!)));
        }

        ReturnKeysToCell();
        SyncFormatToolbar();
        InvalidateCanvas();
        InvalidatePreview();
    }

    /// <summary>Schéma vybrané buňky - **jedno** pro panel i pro okno vlastností, stejně jako
    /// u prvků. Dvě různá by se nutně rozešla.</summary>
    private const string CellCacheClass = "ALVA.TemplateDesigner.EDInputForm.TableCellEdit";

    /// <summary>
    /// Zápis z formuláře buňky do historie. Formulář zapisuje do **instance** buňky, jenže každá
    /// změna struktury mřížku naklonuje - proto se do historie ukládá snímek podle souřadnic,
    /// jinak by zpětný krok psal do odpojené buňky a tiše nic neudělal.
    /// </summary>
    private Action<IDesignerCommand> CellCommandExecutor(TableCellFormatting formatting) => cmd =>
    {
        var before = formatting.Capture();
        cmd.Execute();
        // Formulář zapisuje do aktivní buňky; když je označená oblast, čeká uživatel změnu na celé
        // oblasti (jako v Excelu), tak se podle ní ostatní dorovnají.
        formatting.ApplyActiveChangesToOthers(before);
        _vm!.Execute(formatting.Command(cmd.Description, before));
    };

    /// <summary>
    /// Okno vlastností buňky (Alt+Enter, místní nabídka) - **týž formulář jako v panelu**, jen
    /// v modálu; u prvků to funguje stejně. Zděděné hodnoty se předvyplní z kaskády, takže
    /// potvrzení beze změny buňce nic nezapíše.
    /// </summary>
    private void OpenCellProperties()
    {
        if (_vm is null || _cellTable is null || CellFormatting is not { } formatting) return;
        if (_cellEditing) EndCellEdit(commit: true);

        var data = _vm.State.CurrentPreviewData;
        if (_cellTable.EnsureCell(data, _cellRow, _cellCol) is not { } cell) return;

        var dlg = new ElementEditDialog(cell, CellCacheClass, "Vlastnosti buňky")
        {
            Owner = this,
            PlaceholderFields = _vm.PlaceholderFieldItems(),
            InheritedValues   = formatting.InheritedFormValues()
        };

        var before = formatting.Capture();
        if (dlg.ShowDialog() == true && dlg.ResultCommand is { } result)
        {
            result.Execute();
            _vm.Execute(formatting.Command("Vlastnosti buňky", before));
        }

        ReturnKeysToCell();
        SyncFormatToolbar();
        InvalidateCanvas();
        InvalidatePreview();
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

    /// <summary>Zkopíruje výběr ve tvaru, který přečte Excel - jedna buňka je prostý text,
    /// oblast řádky a tabulátory.</summary>
    private void CopyCell()
    {
        if (_vm is null || _cellTable is null) return;
        System.Windows.Clipboard.SetText(
            _cellTable.CopyRangeToText(_vm.State.CurrentPreviewData, CellSelection));
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

        // Jedna hodnota do označené oblasti ji **vyplní** (Excel), blok se vloží od levého horního
        // rohu výběru a tabulka se dorovná. Bez toho by se dala oblast srovnat jen buňku po buňce.
        var range = CellSelection;
        if (block is [[var jedina]] && !range.IsSingle)
        {
            EditStructure("Vložení ze schránky",
                t => t.FillRange(_vm.State.CurrentPreviewData, range, jedina));
            return;
        }

        EditStructure("Vložení ze schránky",
            t => t.PasteBlock(_vm.State.CurrentPreviewData, range.Top, range.Left, block));
    }

    // ── Řádky a sloupce ───────────────────────────────────────────────────────

    /// <summary>Položky místní nabídky pro tabulku, ve které jsme uvnitř. Vrací false, když
    /// nejsme v tabulce - volající pak postaví běžné menu prvku.</summary>
    private bool TryBuildCellContextMenu(ContextMenu menu, float cx, float cy, float cw)
    {
        if (_vm is null || _cellTable is null) return false;

        // ⚠️ Pravý klik **dovnitř výběru ho nechává být** - jinak by se výběr shodil na jednu buňku
        // a nabídka by nikdy neukázala „Sloučit buňky" (nález Jan 17. 8.). Klik mimo výběr ho
        // přesune, jako v Excelu.
        SelectForContextMenu(cx, cy, cw);

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
        Add("Vlastnosti řádku…", OpenRowProperties);
        menu.Items.Add(new Separator());

        int modelCol = table.ModelColumnIndex(_vm.State.CurrentPreviewData, _cellCol);
        Add("Vložit sloupec vlevo",  () => EditStructure("Vložení sloupce", t => t.InsertColumn(modelCol), colDelta: 1));
        Add("Vložit sloupec vpravo", () => EditStructure("Vložení sloupce", t => t.InsertColumn(modelCol + 1)));
        Add("Smazat sloupec",        () => EditStructure("Smazání sloupce", t => t.RemoveColumn(modelCol)));
        menu.Items.Add(new Separator());
        // Sloučení se nabízí jen tam, kde dává smysl - u jedné buňky není co slučovat a rozdělení
        // u nesloučených by byla mrtvá položka.
        var data = _vm.State.CurrentPreviewData;
        if (CellSelection.ColumnCount > 1) Add("Sloučit buňky", MergeCells);
        if (table.HasMergedCell(data, CellSelection)) Add("Rozdělit buňky", SplitCells);
        if (CellSelection.ColumnCount > 1 || table.HasMergedCell(data, CellSelection))
            menu.Items.Add(new Separator());

        // Popisek říká, na co akce reálně sáhne - u označené oblasti je to celá oblast.
        string co = CellSelection.IsSingle ? "buňky" : "výběru";
        Add($"Kopírovat obsah {co} (Ctrl+C)", CopyCell);
        Add("Vložit ze schránky (Ctrl+V)", PasteIntoCells);
        Add($"Vymazat obsah {co} (Del)", ClearCell);
        menu.Items.Add(new Separator());
        Add("Vlastnosti buňky (Alt+Enter)", OpenCellProperties);

        // „Vlastnosti tabulky" tu vědomě nejsou - nabídka pokračuje standardními položkami prvku
        // (Vyjmout, Kopírovat, Smazat, Vrstva, Vlastnosti), společnými pro všechny prvky.
        return true;
    }

    /// <summary>
    /// Sloučí buňky výběru (vodorovně, po řádcích) do jedné a spojí jejich neprázdné obsahy
    /// mezerou. Výběr se po sloučení smrskne na vzniklou buňku, ať je vidět, co je teď jeden celek.
    /// </summary>
    private void MergeCells() => EditCells("Sloučení buněk", t => t.MergeRange(_vm!.State.CurrentPreviewData, CellSelection));

    /// <summary>Rozdělí sloučené buňky výběru zpět; obsah zůstává v té první.</summary>
    private void SplitCells() => EditCells("Rozdělení buněk", t => t.SplitRange(_vm!.State.CurrentPreviewData, CellSelection));

    /// <summary>Vratná změna obsahu a sloučení nad výběrem - snímek buněk nese i obsah a span,
    /// takže zpětný krok vrátí i spojený text zpátky do původních buněk.</summary>
    private void EditCells(string label, Func<TableElement, bool> zmena)
    {
        if (_vm is null || _cellTable is null || CellFormatting is not { } formatting) return;
        if (_cellEditing) EndCellEdit(commit: true);

        var range  = CellSelection;
        var before = formatting.Capture();
        if (!zmena(_cellTable)) return;

        _vm.Execute(formatting.Command(label, before));
        SetCellSelection(CellRange.Single(range.Top, range.Left));
        SyncFormatToolbar();
        InvalidateCanvas();
        InvalidatePreview();
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
        SetCellSelection(CellRange.MoveTo(_cellRow + rowDelta, _cellCol + colDelta).Clamp(rows, cols));

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
        var data   = _vm.State.CurrentPreviewData;
        var cellMm = _cellTable.CellRectMm(data, _cellRow, _cellCol);
        if (elRect is null || cellMm is null) return;

        float px = _canvas.MmToPx;
        SKRect ToCanvas(SKRect mm) => new(
            elRect.Value.Left + mm.Left   * px, elRect.Value.Top + mm.Top    * px,
            elRect.Value.Left + mm.Right  * px, elRect.Value.Top + mm.Bottom * px);

        var cellRect = ToCanvas(cellMm.Value);

        using var fill   = new SKPaint { Color = new SKColor(0x222563EB) };
        using var border = new SKPaint
        {
            Color = new SKColor(0xFF2563EB), IsStroke = true, StrokeWidth = 2f, IsAntialias = true
        };

        // Vybraná oblast se podbarví celá, ale **aktivní buňka zůstane odlišená** silným rámečkem -
        // je to ta, do které se píše a podle které se vkládají řádky a sloupce.
        var selection = CellSelection;
        if (!selection.IsSingle
            && _cellTable.CellRectMm(data, selection.Top, selection.Left) is { } prvni
            && _cellTable.CellRectMm(data, selection.Bottom, selection.Right) is { } posledni)
        {
            var areaRect = ToCanvas(new SKRect(prvni.Left, prvni.Top, posledni.Right, posledni.Bottom));
            canvas.DrawRect(areaRect, fill);
            using var areaBorder = new SKPaint
            {
                Color = new SKColor(0xFF2563EB), IsStroke = true, StrokeWidth = 1f, IsAntialias = true
            };
            canvas.DrawRect(areaRect, areaBorder);
        }

        canvas.DrawRect(cellRect, fill);
        canvas.DrawRect(cellRect, border);

        using var tableBorder = new SKPaint
        {
            Color = new SKColor(0x802563EB), IsStroke = true, StrokeWidth = 1f, IsAntialias = true
        };
        canvas.DrawRect(elRect.Value, tableBorder);
    }
}
