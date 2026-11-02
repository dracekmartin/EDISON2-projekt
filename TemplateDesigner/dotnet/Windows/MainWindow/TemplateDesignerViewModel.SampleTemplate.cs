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

/// <summary>Ukázková šablona nového dokumentu.</summary>
public partial class TemplateDesignerViewModel
{
    // ── Sample template ───────────────────────────────────────────────────────
    /// <summary>Fallback startovní šablona - agendu typicky přepíše EDISON kontrakt nebo
    /// poslední použitá agenda uživatele (viz ctor).</summary>
    /// <remarks>
    /// Výchozí je <b>linkový JŘ (AD)</b> - pracuje se na něm (vráceno 3. 9., rozhodnutí Jan; od 1. 9.
    /// tu byl kvůli vydání zastávkový).
    ///
    /// ⚠️ **Musí to být zveřejněná agenda.** Když se agenda skryje před vydáním, tahle konstanta
    /// skrytí obchází: nová šablona nastartuje na skryté agendě, ta se stane aktuální agendou
    /// a výběr ji pak ukáže, protože aktuální agendu zobrazuje vždy (aby šly otevřít hotové
    /// šablony). Přesně takhle uživatel linkové JŘ viděl dál, i když byly skryté (nález Jan 1. 9.).
    /// **Skrývá-li se agenda, musí se změnit i tady** - hlídá `SkrytaAgendaTests`.
    ///
    /// ⚠️ Uplatní se jen tam, kde není co obnovit: **poslední použitá agenda uživatele má přednost**
    /// (viz ctor), takže kdo si už jednou vybral jinou, dostane po startu tu svoji.
    /// </remarks>
    public static Template CreateSampleTemplate()
    {
        var t = new Template { Name = "Nová šablona", AgendaCode = "LINKOVY_JR_AD" };
        t.Sections.AddRange(CreateDefaultSections(t.PageSize));
        return t;
    }

    /// <summary>Výška výchozí sekce záhlaví (mm). Sekce roste s obsahem, tohle je jen startovní
    /// plocha, aby do ní šlo něco položit.</summary>
    private const float VychoziVyskaZahlavi = 30f;

    /// <summary>
    /// Výchozí sekce nové šablony: <b>záhlaví a obsah zvlášť</b> - sdílené novou i sample šablonou.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Proč dvě a ne jedna.** Prvky uvnitř sekce se navzájem <b>neposouvají</b>, takže text,
    /// který roste s daty (výčet dopravců), přetiskne tabulku pod sebou. Změřeno 25. 8.: záhlaví
    /// s 26 dopravci leželo přes řádek se zastávkami; přesně na to si stěžuje KORID v #9417.
    /// Sekce se naopak posouvají správně - ta pod rostoucí se odsune o její výšku - takže rozdělení
    /// na dvě sekce ten překryv řeší samo, bez zásahu do stránkování.
    /// </remarks>
    private static List<Section> CreateDefaultSections(PageSize pageSize) =>
    [
        new()
        {
            Name = "Záhlaví",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
            DesignerHeight = VychoziVyskaZahlavi
        },
        new()
        {
            Name = "Obsah",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
            DesignerHeight = MathF.Max(20f, pageSize.PrintableHeight - VychoziVyskaZahlavi)
        }
    ];
}
