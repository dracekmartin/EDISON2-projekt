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

/// <summary>Výběr a vkládání: pole pro okno vlastností, text výběru ve stavovém řádku, strom šablony, vložení pole a prvku, klik na plátno.</summary>
public partial class TemplateDesignerViewModel
{
    // ── Available fields (for ElementPropertiesWindow) ────────────────────────
    public IReadOnlyList<DataField>? GetAvailableFields()
    {
        var schemaFields = SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "")?.Fields;
        if (schemaFields is null && _agendaParameters.Count == 0) return null;
        var paramFields  = _agendaParameters.Select(p => new DataField
            { FieldName = p.Name, Label = p.Label + " (parametr)", DataType = FieldDataType.Text });
        return [.. (schemaFields ?? []), .. paramFields];
    }

    public IReadOnlyList<DataFieldCollection>? GetAvailableGroups()
        => SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "")?.FieldCollections;

    // ── Status text for selection ──────────────────────────────────────────────

    /// <summary>Aktualizuje SelectionStatusText podle aktuálního výběru (klidová zóna status baru).</summary>
    // Jen kontextová informace o výběru - pevné nápovědy („2× klik = vlastnosti") sem nepatří,
    // ovládání je v nápovědě F1 (rozhodnutí Jan 24. 9.). Názvy prvků jako v paletě.
    public void UpdateStatusForSelection()
    {
        if (State.SelectedElements.Count == 1)
        {
            var el = State.SelectedElements[0];
            string vyska = el.Height is { } h ? $"{h:0.#} mm" : "výška podle obsahu";
            SelectionStatusText = $"{ElementNames.SPopisem(el)}  ·  X {el.X:0.#}  Y {el.Y:0.#} mm  ·  {el.Width:0.#} mm × {vyska}"
                + (el.IsLocked ? "  ·  Uzamčeno" : "");
        }
        else if (State.SelectedElements.Count > 1)
        {
            int n = State.SelectedElements.Count;
            SelectionStatusText = $"Vybráno {n} {TemplateTreeNames.Prvku(n)}";
        }
        else if (State.ActiveSection is { } sekce)
        {
            string typ = TemplateTreeNames.NazevTypuSekce(sekce.Typ).ToLower();
            string vyska = sekce.DesignerHeight is { } h ? $"výška {h:0.#} mm" : "výška podle obsahu";
            int n = sekce.Elements.Count;
            SelectionStatusText = $"Sekce „{sekce.Name}“  ·  {typ}  ·  {vyska}  ·  {n} {TemplateTreeNames.Prvku(n)}";
        }
        else
        {
            SelectionStatusText = "";
        }
    }

    // ── Tree selection ──────────────────────────────────────────────────────────

    /// <summary>Zpracuje výběr z template tree - element, skupina nebo sekce (dříve v code-behind).</summary>
    public void SelectFromTree(object? tag)
    {
        if (tag is TemplateElement element)
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(element));
            if (section is not null)
            {
                ActivateSection(section);
                State.SelectedElements.Clear();
                State.SelectedElements.Add(element);
            }
        }
        else if (tag is string groupId)
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Any(el => el.GroupId == groupId));
            if (section is not null)
            {
                ActivateSection(section);
                State.SelectedElements.Clear();
                State.SelectedElements.AddRange(section.Elements.Where(el => el.GroupId == groupId));
            }
        }
        else if (tag is Section section)
        {
            ActivateSection(section);
            State.SelectedElements.Clear();
        }
        else return;

        UpdateStatusForSelection();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Schema field insert ─────────────────────────────────────────────────────

    /// <summary>Vloží placeholder do vybraného TextElementu nebo zkopíruje do schránky (dříve v code-behind).</summary>
    public void InsertOrCopyPlaceholder(string placeholder)
    {
        if (State.SelectedElements.Count == 1 && State.SelectedElements[0] is TextElement textEl)
        {
            string oldText = textEl.Text;
            string newText = oldText + placeholder;
            Execute(new SetPropertyCommand(
                $"Vložit {placeholder}",
                oldText, newText,
                v => textEl.Text = (string)v!));
            RequestCanvasRefresh?.Invoke();
            RequestPropertiesPanelUpdate?.Invoke();
        }
        else
        {
            Dialogs?.SetClipboardText(placeholder);
            StatusText = $"Zkopírováno do schránky: {placeholder}";
        }
    }

    // ── Element drop (adding) ───────────────────────────────────────────────────

    /// <summary>Připojí placeholder k existujícímu TextElementu (field drag na element).</summary>
    public void AppendPlaceholderToElement(TextElement textEl, string fieldName)
    {
        string placeholder = $"{{{fieldName}}}";
        string oldText = textEl.Text;
        string newText = oldText + placeholder;
        Execute(new SetPropertyCommand(
            $"Vložit {placeholder}",
            oldText, newText,
            v => textEl.Text = (string)v!));

        var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(textEl));
        if (section is not null) ActivateSection(section);
        State.SelectedElements.Clear();
        State.SelectedElements.Add(textEl);
        UpdateStatusForSelection();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    /// <summary>Vytvoří a umístí nový element na danou pozici v sekci (dříve v EndAddingElement code-behind).</summary>
    public void AddElementAtPosition(Section targetSection, float mmX, float mmY, string? elementType, string? fieldName)
    {
        var (defW, defH) = GetDefaultElementSize(elementType ?? ElementTypes.Text);

        // Centrovat element na pozici kurzoru
        mmX -= defW / 2f;
        mmY -= defH / 2f;

        // Clamp do hranic sekce
        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float sectionH = targetSection.EffectiveDesignerHeight;
        mmX = Math.Clamp(mmX, 0, Math.Max(0, sectionW - defW));
        mmY = Math.Clamp(mmY, 0, Math.Max(0, sectionH - defH));

        // Snap na mřížku
        if (State.SnapToGrid)
        {
            float g = State.GridSize;
            mmX = MathF.Round(mmX / g) * g;
            mmY = MathF.Round(mmY / g) * g;
        }

        // Vytvoření elementu
        TemplateElement element;
        if (fieldName is not null)
        {
            // Přes CreateElementByType, ať DB-pole dostane stejné defaulty jako element z palety
            // (výchozí rámeček, rozměry) - dřív se tvořil holý TextElement bez rámečku.
            var textEl = (TextElement)CreateElementByType(ElementTypes.Text, mmX, mmY);
            textEl.Text = $"{{{fieldName}}}";
            element = textEl;
        }
        else
        {
            // Pevná tabulka se cestou z palety zeptá na rozměr; zrušení = nevkládat nic.
            if (CreateElementInteractive(elementType!, mmX, mmY) is not { } created) return;
            element = created;
            // Výchozí vzor sloupců (rozhodnutí Jan 10. 8.: nové jedou z návrháře, linka je backup)
            // dostane jen PRVNÍ prvek odjezdů v šabloně a jen u agendy, jejíž server definici čte -
            // druhý prvek by definicí tiše přepnul data celé šablony a MHD by v dialogu tvrdila
            // vlastní sloupce, které její server ignoruje. PDF import vzor nedostává (zrcadlí
            // rozvržení importované vývěsky).
            if (element is DepartureGridElement grid && ShouldPrefillDepartureColumns())
                grid.CustomColumns = DefaultDepartureColumns();
        }

        // Snap i rozměrů - výchozí velikosti (80×8, 190×25…) nejsou násobky kroku mřížky,
        // takže bez tohoto spodní/pravá hrana nikdy neseděla na mřížce a element působil
        // „mimo grid" hned po přidání. Zaokrouhlení na 0 (element menší než krok) → původní rozměr.
        if (State.SnapToGrid)
        {
            float g = State.GridSize;
            static float SnapSize(float v, float step)
            {
                float snapped = MathF.Round(v / step) * step;
                return snapped > 0 ? snapped : v;
            }
            element.Width = SnapSize(element.Width, g);
            if (element.Height is { } h) element.Height = SnapSize(h, g);
        }

        Execute(new AddElementCommand(targetSection, element));
        ActivateSection(targetSection);
        State.SelectedElements.Clear();
        State.SelectedElements.Add(element);
        UpdateStatusForSelection();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Hit handling (element selection logic from mouse down) ────────────────

    /// <summary>Zpracuje klik na element - výběr/toggle s Ctrl/Alt/Group logikou (dříve v OnCanvasMouseDown).</summary>
    public void HandleElementHit(Section section, TemplateElement element, bool ctrl, bool alt)
    {
        if (ctrl)
        {
            // Ctrl+klik: toggle element (nebo celou skupinu)
            if (element.GroupId is not null && !alt)
            {
                var groupMembers = section.Elements.Where(e => e.GroupId == element.GroupId).ToList();
                if (State.SelectedElements.Contains(element))
                    foreach (var m in groupMembers) State.SelectedElements.Remove(m);
                else
                    foreach (var m in groupMembers.Where(m2 => !State.SelectedElements.Contains(m2)))
                        State.SelectedElements.Add(m);
            }
            else
            {
                if (State.SelectedElements.Contains(element))
                    State.SelectedElements.Remove(element);
                else
                    State.SelectedElements.Add(element);
            }
        }
        else if (!State.SelectedElements.Contains(element))
        {
            State.SelectedElements.Clear();
            // Klik na člen skupiny → vyber celou skupinu (Alt = jen jeden element)
            if (element.GroupId is not null && !alt)
            {
                var groupMembers = section.Elements.Where(e => e.GroupId == element.GroupId).ToList();
                State.SelectedElements.AddRange(groupMembers);
            }
            else
            {
                State.SelectedElements.Add(element);
            }
        }

        ActivateSection(section);
    }

    /// <summary>Zpracuje klik do prázdné oblasti - zruší výběr (pokud ne Ctrl), aktivuje sekci.</summary>
    public void HandleEmptyAreaHit(Section? section, bool ctrl)
    {
        if (!ctrl)
            State.SelectedElements.Clear();
        ActivateSection(section);
        UpdateStatusForSelection();
    }
}
