using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using EDControls;
using EDControls.EDViewModel;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Config;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;
using ALWA.SYS;

namespace TemplateDesigner;

/// <summary>Úpravy prvků: schránka a schránka stylu, rozdělení, vytvoření, mazání, skupiny, vrstvy, zarovnání, srovnání na mřížku, měřítko a posun klávesami.</summary>
public partial class TemplateDesignerViewModel
{
    // ── Clipboard ─────────────────────────────────────────────────────────────
    public void CopySelected()
    {
        if (State.SelectedElements.Count == 0) return;
        _clipboard = State.SelectedElements.Select(el => el.Clone()).ToList();
    }

    public void CutSelected()
    {
        if (State.SelectedElements.Count == 0) return;
        CopySelected();
        DeleteSelected();
    }

    public void SelectAll()
    {
        if (State.ActiveSection is null) return;
        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(State.ActiveSection.Elements);
    }

    public void PasteClipboard()
    {
        if (_clipboard.Count == 0) return;
        if (State.ActiveSection is null)
        {
            PopupDisplayer.Info("Nejprve vyberte sekci.");
            return;
        }

        // Schránka přežije otevření jiné šablony - prvek jízdního řádu jiné agendy se nevloží.
        var vlozitelne = JenPrvkyAgendy(_clipboard);
        if (vlozitelne.Count == 0) return;

        // Mapování starý GroupId → nový GroupId (klony nedědí skupinu originálu)
        var groupIdMap = new Dictionary<string, string>();
        var newElements = vlozitelne.Select(el =>
        {
            var clone = el.Clone();
            clone.X = Math.Min(clone.X + 5f,
                State.CurrentTemplate.PageSize.PrintableWidth - clone.Width);
            clone.Y = Math.Min(clone.Y + 5f,
                State.ActiveSection.EffectiveDesignerHeight - clone.EffectiveDesignerHeight);
            RemapGroupId(clone, groupIdMap);
            return clone;
        }).ToList();

        var commands = newElements
            .Select(el => (IDesignerCommand)new AddElementCommand(State.ActiveSection, el))
            .ToList();

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Vložit prvky", commands));

        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(newElements);
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Style clipboard ──────────────────────────────────────────────────────

    public void CopyStyle()
    {
        var el = State.SelectedElements.FirstOrDefault();
        if (el is null) return;
        var textStyle = el is TextElement te ? te.Style.Clone() : null;
        State.StyleClipboard = new StyleClipboard(textStyle, el.BorderColor, el.BorderWidth, el.BackgroundColor);
    }

    /// <summary>
    /// Smaže font nastavený u jednotlivých prvků, aby převzaly výchozí font dokumentu (`B153`).
    /// </summary>
    /// <remarks>
    /// Šablony založené do 30. 6. 2026 - a jejich kopie - mají font zapsaný u každého prvku, takže
    /// v nich volba „Výchozí font" nic nedělá. Tohle je jediná cesta, jak je narovnat; dodávané
    /// šablony jsou uklizené od 14. 9. Vratné přes Ctrl+Z, proto se na nic neptá.
    /// </remarks>
    public void PrevzitVychoziFont()
    {
        var prvky = State.CurrentTemplate.Sections.SelectMany(s => s.Elements).ToList();
        var prikaz = new PrevzitVychoziFontCommand(prvky);
        Execute(prikaz);

        StatusText = prikaz.PocetZmenenych == 0
            ? "Žádný prvek neměl vlastní font - výchozí font dokumentu platí všude."
            : $"Výchozí font dokumentu převzalo {prikaz.PocetZmenenych}× nastavení u prvků.";
        RebuildSections();
        InvalidateAll();
    }

    public void PasteStyle()
    {
        var clip = State.StyleClipboard;
        if (clip is null || State.SelectedElements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in State.SelectedElements)
        {
            if (el.BorderColor != clip.BorderColor)
                commands.Add(new SetPropertyCommand("Barva rámečku", el.BorderColor, clip.BorderColor,
                    v => { el.BorderColor = (string)v!; }));
            if (MathF.Abs(el.BorderWidth - clip.BorderWidth) > 0.001f)
                commands.Add(new SetPropertyCommand("Tloušťka rámečku", el.BorderWidth, clip.BorderWidth,
                    v => { el.BorderWidth = (float)v!; }));
            if (el.BackgroundColor != clip.BackgroundColor)
                commands.Add(new SetPropertyCommand("Barva pozadí", el.BackgroundColor, clip.BackgroundColor,
                    v => { el.BackgroundColor = (string)v!; }));

            if (clip.TextStyle is not null && el is TextElement te)
            {
                var oldStyle = te.Style;
                var newStyle = clip.TextStyle.Clone();
                commands.Add(new SetPropertyCommand("Styl textu", oldStyle, newStyle,
                    v => { te.Style = (TextStyle)v!; }));
            }
        }

        if (commands.Count == 0) return;
        Execute(new CompositeCommand("Vložit styl", commands));
        RequestCanvasRefresh?.Invoke();
    }

    public void DuplicateSelected() => DuplicateSelected(direction: null);

    public void DuplicateSelected(DuplicateDirection? direction)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float sectionH = State.ActiveSection.EffectiveDesignerHeight;
        float gap = State.SnapGapMm;

        var newElements = new List<TemplateElement>();
        var commands = new List<IDesignerCommand>();
        var groupIdMap = new Dictionary<string, string>();

        foreach (var orig in State.SelectedElements)
        {
            var clone = orig.Clone();
            float origH = orig.Height ?? orig.EffectiveDesignerHeight;
            float cloneH = clone.Height ?? clone.EffectiveDesignerHeight;

            if (direction is null)
            {
                clone.X = Math.Clamp(orig.X + 5f, 0f, Math.Max(0f, sectionW - clone.Width));
                clone.Y = Math.Clamp(orig.Y + 5f, 0f, Math.Max(0f, sectionH - cloneH));
            }
            else
            {
                (clone.X, clone.Y) = direction.Value switch
                {
                    DuplicateDirection.Down  => (orig.X, orig.Y + origH + gap),
                    DuplicateDirection.Up    => (orig.X, orig.Y - cloneH - gap),
                    DuplicateDirection.Right => (orig.X + orig.Width + gap, orig.Y),
                    DuplicateDirection.Left  => (orig.X - clone.Width - gap, orig.Y),
                    _ => (orig.X + 5f, orig.Y + 5f)
                };
                clone.X = Math.Clamp(clone.X, 0f, Math.Max(0f, sectionW - clone.Width));
                clone.Y = Math.Clamp(clone.Y, 0f, Math.Max(0f, sectionH - cloneH));
            }

            // Přemapovat GroupId - klony dostanou nový společný GroupId
            RemapGroupId(clone, groupIdMap);

            newElements.Add(clone);
            commands.Add(new AddElementCommand(State.ActiveSection, clone));
        }

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Duplikovat prvky", commands));

        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(newElements);
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Split element ─────────────────────────────────────────────────────────

    /// <summary>
    /// Rozdělí vybrané elementy na poloviny. Horizontal = nad/pod, Vertical = vedle sebe.
    /// Originál se zmenší na polovinu, klon se umístí do druhé poloviny.
    /// </summary>
    public void SplitSelected(bool horizontal)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        var commands = new List<IDesignerCommand>();
        var newSelection = new List<TemplateElement>();

        foreach (var orig in State.SelectedElements.ToList())
        {
            float origH = orig.Height ?? orig.EffectiveDesignerHeight;
            var clone = orig.Clone();

            if (horizontal)
            {
                // Rozdělit výšku na dvě poloviny
                float halfH = MathF.Round(origH / 2f, 2);
                commands.Add(new ResizeElementCommand(orig,
                    orig.X, orig.Y, orig.Width, orig.Height,
                    orig.X, orig.Y, orig.Width, halfH));
                clone.Y = orig.Y + halfH;
                clone.Height = MathF.Round(origH - halfH, 2);
            }
            else
            {
                // Rozdělit šířku na dvě poloviny
                float halfW = MathF.Round(orig.Width / 2f, 2);
                commands.Add(new ResizeElementCommand(orig,
                    orig.X, orig.Y, orig.Width, orig.Height,
                    orig.X, orig.Y, halfW, orig.Height));
                clone.X = orig.X + halfW;
                clone.Width = MathF.Round(orig.Width - halfW, 2);
            }

            commands.Add(new AddElementCommand(State.ActiveSection, clone));
            newSelection.Add(orig);
            newSelection.Add(clone);
        }

        if (commands.Count > 0)
        {
            Execute(new CompositeCommand(
                horizontal ? "Rozdělit horizontálně" : "Rozdělit vertikálně", commands));
            State.SelectedElements.Clear();
            State.SelectedElements.AddRange(newSelection);
            RequestPropertiesPanelUpdate?.Invoke();
            RequestCanvasRefresh?.Invoke();
        }
    }

    // ── Element factory (shared by palette drag + toolbar menu) ────────────────

    /// <summary>Výchozí velikost prvku z <see cref="ElementCatalog"/> (neznámý typ jako text).</summary>
    public static (float Width, float Height) GetDefaultElementSize(string type) =>
        ElementCatalog.Find(type) is { } e ? (e.Width, e.Height) : (80f, 8f);

    /// <summary>Nový prvek podle <see cref="ElementCatalog"/>.</summary>
    public static TemplateElement CreateElementByType(string type, float x, float y) =>
        ElementCatalog.Create(type, x, y);

    /// <summary>
    /// Vytvoří prvek k vložení s výchozími hodnotami agendy šablony; u pevné tabulky se **nejdřív
    /// zeptá na rozměr** (rychlý formulář,
    /// předvyplněný 3 × 3). Vrací null, když uživatel vkládání zrušil - dřív se vždycky vložila
    /// mřížka 3 × 3 a na 12 × 10 se muselo proklikat devatenáct vložení sloupce a řádku.
    /// Bez dialogové služby (testy) se chová jako dřív, takže se nic nezablokuje.
    /// </summary>
    public TemplateElement? CreateElementInteractive(string type, float x, float y)
    {
        if (type != ElementTypes.Table || Dialogs is null) return SVychozimiAgendy(CreateElementByType(type, x, y));

        var size = new TableGridSize();
        if (!Dialogs.ShowQuickForm(size, "ALVA.TemplateDesigner.EDInputForm.TableInsertEdit", "Vložit tabulku"))
            return null;

        var (w, h) = GetDefaultElementSize(type);
        return NewFixedTable(x, y, w, h, size.Columns, size.Rows);
    }

    /// <summary>
    /// Poznámky linkového JŘ začínají na třech sloupcích u každého druhu (rozhodnutí Jan 9. 10.):
    /// krátké poznámky (negativní značky, týdenní platnost) by v jednom sloupci zabraly zbytečně
    /// místa pod tabulkou (KORID #7487). Vývěska zůstává na jednom sloupci.
    /// </summary>
    private TemplateElement SVychozimiAgendy(TemplateElement prvek)
    {
        if (prvek is NotesLegendElement poznamky && JeLinkovyJr) poznamky.Columns = 3;
        return prvek;
    }

    /// <summary>
    /// Nová pevná tabulka - mřížka 3 × 3 s **pevnou výškou**. Bez výšky (Height null) by prvek
    /// platil za dynamický a canvas by mu nabídl jen boční úchyty, takže by za něj nešlo táhnout
    /// do výšky - a to uživatel od ručně kreslené tabulky čeká (nahlášeno 12. 8.).
    /// </summary>
    private static TableElement NewFixedTable(float x, float y, float w, float? h,
        int columns = 3, int rows = 3)
    {
        var table = TableElement.CreateGrid(columns, rows);
        table.X = x;
        table.Y = y;
        table.Width = w;
        // Výška roste s počtem řádků - u desetiřádkové tabulky by výchozích 18 mm znamenalo,
        // že se obsah hned po vložení zmenší, aby se vešel.
        table.Height = h is null ? null : rows * table.RowHeight;
        return table;
    }

    // ── Delete ──────────────────────────────────────────────────────────────────

    public void DeleteSelected()
    {
        if (State.SelectedElements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in State.SelectedElements.ToList())
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            if (section is null || el.IsLocked) continue;
            commands.Add(new DeleteElementCommand(section, el));
        }

        if (commands.Count == 0) return;

        State.SelectedElements.Clear();

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Smazat prvky", commands));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Group / Ungroup ──────────────────────────────────────────────────────

    /// <summary>Přemapuje GroupId klonu na nový sdílený GroupId (pro paste/duplicate).</summary>
    private static void RemapGroupId(TemplateElement clone, Dictionary<string, string> groupIdMap)
    {
        if (clone.GroupId is null) return;
        if (!groupIdMap.TryGetValue(clone.GroupId, out var newGid))
            groupIdMap[clone.GroupId] = newGid = ElementGroupHelper.NewGroupId();
        clone.GroupId = newGid;
    }

    public void GroupSelected()
    {
        if (State.SelectedElements.Count < 2) return;
        var groupId = ElementGroupHelper.NewGroupId();
        Execute(new GroupCommand(State.SelectedElements, groupId));
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    public void UngroupSelected()
    {
        var grouped = State.SelectedElements.Where(e => e.GroupId is not null).ToList();
        if (grouped.Count == 0) return;
        Execute(new UngroupCommand(grouped));
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    // ── ZIndex ────────────────────────────────────────────────────────────────
    public void ChangeZIndex(int sign, bool toExtreme)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        var section  = State.ActiveSection;
        var selected = State.SelectedElements
            .Where(el => section.Elements.Contains(el))
            .ToList();
        if (selected.Count == 0) return;

        var commands = new List<IDesignerCommand>();

        if (toExtreme && sign > 0)
        {
            int baseZ = section.Elements
                .Where(el => !selected.Contains(el))
                .Select(el => el.ZIndex)
                .DefaultIfEmpty(-1).Max() + 1;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex, newZ = baseZ++;
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("Vrstva", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }
        else if (toExtreme && sign < 0)
        {
            int shift = selected.Count;
            foreach (var el in section.Elements.Where(e => !selected.Contains(e)))
            {
                int oldZ = el.ZIndex, newZ = oldZ + shift;
                commands.Add(new SetPropertyCommand("Vrstva", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
            int z = 0;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex, newZ = z++;
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("Vrstva", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }
        else
        {
            foreach (var el in selected)
            {
                int oldZ = el.ZIndex, newZ = Math.Max(0, oldZ + sign);
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("Vrstva", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }

        if (commands.Count == 0) return;

        string desc = (sign > 0, toExtreme) switch
        {
            (true,  true)  => KeyboardShortcuts.BringToFront.Action,
            (true,  false) => KeyboardShortcuts.BringForward.Action,
            (false, true)  => KeyboardShortcuts.SendToBack.Action,
            _              => KeyboardShortcuts.SendBackward.Action
        };

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Alignment ─────────────────────────────────────────────────────────────
    public void AlignElements(AlignMode mode)
    {
        // X-align pracuje napříč sekcemi (sekce sdílejí levý margin → X je přímo porovnatelné).
        // Y-align zůstává per-aktivní-sekce (Y je relativní k hornímu okraji sekce).
        bool isXMode = mode is AlignMode.Left or AlignMode.Right or AlignMode.CenterH or AlignMode.DistributeH;

        List<TemplateElement> els;
        if (isXMode)
        {
            els = State.SelectedElements.ToList();
        }
        else
        {
            if (State.ActiveSection is null) return;
            els = State.SelectedElements
                .Where(el => State.ActiveSection.Elements.Contains(el))
                .ToList();
        }

        int minCount = mode is AlignMode.DistributeH or AlignMode.DistributeV ? 3 : 2;
        if (els.Count < minCount) return;

        var commands = new List<IDesignerCommand>();

        // Zaokrouhlení na celé mm - align cílí na "čistou" hodnotu konzistentní se soft-snapem při dragu.
        static float Rnd(float v) => MathF.Round(v);

        switch (mode)
        {
            case AlignMode.Left:
            {
                float t = Rnd(els.Min(e => e.X));
                foreach (var el in els)
                { if (Math.Abs(el.X - t) > 0.001f) commands.Add(MakeXCmd(el, t)); }
                break;
            }
            case AlignMode.Right:
            {
                float t = Rnd(els.Max(e => e.X + e.Width));
                foreach (var el in els)
                { float nx = t - el.Width; if (Math.Abs(el.X - nx) > 0.001f) commands.Add(MakeXCmd(el, nx)); }
                break;
            }
            case AlignMode.CenterH:
            {
                float cx = Rnd((els.Min(e => e.X) + els.Max(e => e.X + e.Width)) / 2f);
                foreach (var el in els)
                { float nx = cx - el.Width / 2f; if (Math.Abs(el.X - nx) > 0.001f) commands.Add(MakeXCmd(el, nx)); }
                break;
            }
            case AlignMode.Top:
            {
                float t = Rnd(els.Min(e => e.Y));
                foreach (var el in els)
                { if (Math.Abs(el.Y - t) > 0.001f) commands.Add(MakeYCmd(el, t)); }
                break;
            }
            case AlignMode.Bottom:
            {
                float t = Rnd(els.Max(e => e.Y + e.EffectiveDesignerHeight));
                foreach (var el in els)
                { float ny = t - el.EffectiveDesignerHeight; if (Math.Abs(el.Y - ny) > 0.001f) commands.Add(MakeYCmd(el, ny)); }
                break;
            }
            case AlignMode.MiddleV:
            {
                float cy = Rnd((els.Min(e => e.Y) + els.Max(e => e.Y + e.EffectiveDesignerHeight)) / 2f);
                foreach (var el in els)
                { float ny = cy - el.EffectiveDesignerHeight / 2f; if (Math.Abs(el.Y - ny) > 0.001f) commands.Add(MakeYCmd(el, ny)); }
                break;
            }
            case AlignMode.DistributeH:
            {
                var sorted = els.OrderBy(e => e.X).ToList();
                float totalW = sorted.Sum(e => e.Width);
                float span   = sorted.Last().X + sorted.Last().Width - sorted.First().X;
                float gap    = MmMath.Round((span - totalW) / (sorted.Count - 1));
                float cur    = sorted.First().X;
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    var el = sorted[i];
                    float x = MmMath.Round(cur);
                    if (Math.Abs(el.X - x) > 0.01f) commands.Add(MakeXCmd(el, x));
                    cur += el.Width + gap;
                }
                break;
            }
            case AlignMode.DistributeV:
            {
                var sorted = els.OrderBy(e => e.Y).ToList();
                float totalH = sorted.Sum(e => e.EffectiveDesignerHeight);
                float span   = sorted.Last().Y + sorted.Last().EffectiveDesignerHeight - sorted.First().Y;
                float gap    = MmMath.Round((span - totalH) / (sorted.Count - 1));
                float cur    = sorted.First().Y;
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    var el = sorted[i];
                    float y = MmMath.Round(cur);
                    if (Math.Abs(el.Y - y) > 0.01f) commands.Add(MakeYCmd(el, y));
                    cur += el.EffectiveDesignerHeight + gap;
                }
                break;
            }
        }

        if (commands.Count == 0) return;

        string desc = mode switch
        {
            AlignMode.Left        => "Zarovnat vlevo",
            AlignMode.Right       => "Zarovnat vpravo",
            AlignMode.CenterH     => "Zarovnat na střed (vodorovně)",
            AlignMode.Top         => "Zarovnat nahoru",
            AlignMode.Bottom      => "Zarovnat dolů",
            AlignMode.MiddleV     => "Zarovnat na střed (svisle)",
            AlignMode.DistributeH => "Rozložit vodorovně",
            AlignMode.DistributeV => "Rozložit svisle",
            _                     => "Zarovnat"
        };

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    private static IDesignerCommand MakeXCmd(TemplateElement el, float nx)
        => new SetPropertyCommand("X", el.X, nx, v => el.X = (float)v!);
    private static IDesignerCommand MakeYCmd(TemplateElement el, float ny)
        => new SetPropertyCommand("Y", el.Y, ny, v => el.Y = (float)v!);

    // ── Normalize positions ───────────────────────────────────────────────────
    public void NormalizePositions(IReadOnlyList<TemplateElement> elements, float step)
    {
        if (elements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in elements)
        {
            float nx = MmMath.Snap(el.X, step), ny = MmMath.Snap(el.Y, step);
            float nw = MmMath.Snap(el.Width, step);
            float? nh = el.Height is not null ? MmMath.Snap(el.Height.Value, step) : null;

            bool posChanged  = Math.Abs(nx - el.X) > 0.001f || Math.Abs(ny - el.Y) > 0.001f;
            bool sizeChanged = Math.Abs(nw - el.Width) > 0.001f
                            || (nh is not null && Math.Abs(nh.Value - el.Height!.Value) > 0.001f);

            if (posChanged || sizeChanged)
                commands.Add(new ResizeElementCommand(el, el.X, el.Y, el.Width, el.Height, nx, ny, nw, nh));
        }

        if (commands.Count == 0) return;
        Execute(new CompositeCommand("Normalizovat pozice", commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Template scaling ─────────────────────────────────────────────────────
    public void ScaleTemplate(float scale)
    {
        if (Math.Abs(scale - 1f) < 0.001f) return;
        var cmd = new ScaleTemplateCommand(State.CurrentTemplate, scale);
        Execute(cmd);
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Key movement ──────────────────────────────────────────────────────────
    /// <summary>
    /// Arrow key nudging. Kroky:
    ///   fineStep=true (Ctrl)   → 0.5 mm (jemné doladění)
    ///   largeStep=true (Shift) → GridSize (rychlý skok)
    ///   bez modifikátorů       → 1 mm (default)
    /// </summary>
    public void MoveByKey(Key key, bool gridStep)
    {
        // Krok mřížky drží Ctrl (dřív Shift, a na Ctrl byl jemný krok 0,5 mm) - Shift se uvolnil
        // pro změnu velikosti, jak ji uživatel zná ze starého návrháře (rozhodnutí Jan 1. 9.).
        float step = gridStep ? State.GridSize : 1f;
        float dx   = key switch { Key.Left => -step, Key.Right => step, _ => 0f };
        float dy   = key switch { Key.Up   => -step, Key.Down  => step, _ => 0f };

        var initPositions = State.SelectedElements.ToDictionary(el => el, el => (el.X, el.Y));
        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float clampedDx = dx, clampedDy = dy;

        foreach (var kvp in initPositions)
        {
            var el      = kvp.Key;
            float elH   = el.EffectiveDesignerHeight;
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;
            float maxDx = sectionW - el.Width - kvp.Value.X;
            float maxDy = sectionH - elH    - kvp.Value.Y;
            // Element může být širší/vyšší než sekce (např. background image) - přeskočit clamp
            if (maxDx >= -kvp.Value.X)
                clampedDx = Math.Clamp(clampedDx, -kvp.Value.X, maxDx);
            if (maxDy >= -kvp.Value.Y)
                clampedDy = Math.Clamp(clampedDy, -kvp.Value.Y, maxDy);
        }

        if (clampedDx == 0f && clampedDy == 0f) return;

        foreach (var kvp in initPositions)
        { kvp.Key.X = kvp.Value.X + clampedDx; kvp.Key.Y = kvp.Value.Y + clampedDy; }

        var moves = initPositions
            .Where(kvp => kvp.Key.X != kvp.Value.X || kvp.Key.Y != kvp.Value.Y)
            .Select(kvp => (IDesignerCommand)new MoveElementCommand(
                kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Key.X, kvp.Key.Y))
            .ToList();

        if (moves.Count == 1) Execute(moves[0]);
        else if (moves.Count > 1) Execute(new CompositeCommand("Přesun šipkou", moves));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    /// <summary>
    /// Co udělají šipky nad vybraným prvkem. Rozhodnutí je tady, a ne rozeseté v obsluze kláves,
    /// aby se dalo ohlídat testem - mutace 1. 9. ukázala, že samotné volání
    /// <see cref="ResizeByKey"/> testy pokryjí, ale odpojení od Shiftu už ne.
    /// </summary>
    /// <remarks>
    /// Ctrl+Shift+↑/↓ řeší obsluha kláves dřív (přesun sekce), takže sem ta kombinace nedojde.
    /// </remarks>
    public static ArrowKeyAction ResolveArrowAction(bool ctrl, bool shift) => (ctrl, shift) switch
    {
        (_, true)     => ArrowKeyAction.Resize,      // Shift = velikost, jako ve starém návrháři
        (true, false) => ArrowKeyAction.MoveByGrid,  // Ctrl = posun po mřížce
        _             => ArrowKeyAction.Move,        // samotné šipky = posun o milimetr
    };

    /// <summary>Nejmenší rozměr, na který jde prvek stáhnout - shodně s tažením úchytu myší.</summary>
    private const float MinElementWidthMm = 3f;
    private const float MinElementHeightMm = 2f;

    /// <summary>
    /// Změní velikost vybraných prvků o krok. Mění se <b>pravá a dolní hrana</b>, levý horní roh
    /// zůstává - stejně jako když uživatel táhne pravý dolní úchyt.
    /// </summary>
    /// <remarks>
    /// ⚠️ Prvku s dynamickou výškou se svisle nic nestane a řekne se to ve stavovém řádku. Fixovat
    /// mu výšku by znamenalo, že o dynamiku přijde bez varování a dostane se z toho jen zpětným
    /// krokem; myš mu ze stejného důvodu svislé úchyty vůbec nenabízí (rozhodnutí Jan 1. 9.).
    /// </remarks>
    public void ResizeByKey(Key key, bool gridStep)
    {
        float step = gridStep ? State.GridSize : 1f;
        float dw = key switch { Key.Left => -step, Key.Right => step, _ => 0f };
        float dh = key switch { Key.Up   => -step, Key.Down  => step, _ => 0f };

        var cile = State.SelectedElements.Where(el => !el.IsLocked).ToList();
        if (cile.Count == 0) return;

        // Svislá změna se týká jen prvků s pevnou výškou; když mají všechny vybrané výšku z dat,
        // není co měnit a je lepší to říct, než tiše nedělat nic.
        if (dh != 0f && cile.All(el => el.Height is null))
        {
            StatusText = cile.Count == 1
                ? "Výška tohoto prvku se počítá podle obsahu - zadejte ji v jeho vlastnostech, nebo měňte šířku."
                : "Vybrané prvky mají výšku z dat - svisle je zvětšit nejde.";
            return;
        }

        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        var commands = new List<IDesignerCommand>();

        foreach (var el in cile)
        {
            float oldW = el.Width;
            float? oldH = el.Height;

            float newW = MathF.Max(MinElementWidthMm, MathF.Min(oldW + dw, sectionW - el.X));
            float? newH = oldH;
            if (dh != 0f && oldH is { } h)
            {
                var section = State.CurrentTemplate.Sections.FirstOrDefault(sec => sec.Elements.Contains(el));
                float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;
                newH = MathF.Max(MinElementHeightMm, MathF.Min(h + dh, sectionH - el.Y));
            }

            if (newW == oldW && newH == oldH) continue;
            el.Width = newW;
            el.Height = newH;
            commands.Add(new ResizeElementCommand(el, el.X, el.Y, oldW, oldH, el.X, el.Y, newW, newH));
        }

        if (commands.Count == 0) return;
        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand("Změna velikosti klávesnicí", commands));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }
}
