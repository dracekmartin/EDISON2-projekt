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

/// <summary>Sekce šablony a předlohy stránek.</summary>
public partial class TemplateDesignerViewModel
{
    // ── Sections ──────────────────────────────────────────────────────────────
    public void RebuildSections()
    {
        // Sync ambientního dokumentového defaultu (font…) na aktuální šablonu. RebuildSections se volá
        // po každém (re)loadu šablony → ambient vždy odpovídá aktuální šabloně (i reset na null u nové).
        DocumentDefaults.Style = State.CurrentTemplate.DefaultTextStyle;

        Sections.Clear();
        var template = State.CurrentTemplate;
        var stranky = DesignerPages.ObsahStranek(template);
        if (stranky.Count < 2)
        {
            foreach (var s in template.Sections)
                Sections.Add(new SectionVM(s, this));
        }
        else
        {
            // Šablona o víc stránkách (`B184`): nahoře společné sekce (hlavička, patička - patří
            // dokladu, jsou na každé předloze, kde se tisknou), pod nimi jen sekce otevřené
            // předlohy. Pořadí v šabloně se nemění, jen zobrazení.
            int otevrena = OtevrenaPredloha;
            bool prvni = true;
            foreach (var s in template.Sections.Where(DesignerPages.JeOpakovana))
            {
                Sections.Add(new SectionVM(s, this) { ZahlaviStranky = prvni ? "Společné (všechny stránky)" : null });
                prvni = false;
            }
            for (int j = 0; j < stranky[otevrena].Count; j++)
                Sections.Add(new SectionVM(stranky[otevrena][j], this)
                {
                    ZahlaviStranky = j == 0 ? stranky[otevrena][0].Name : null,
                    ZahlaviAktivni = j == 0,
                });
        }
        OnPredlohyZmeneny();
        SectionsRebuilt?.Invoke();
    }

    public void ActivateSection(Section? section)
    {
        State.ActiveSection = section;
        // Sekce z jiné předlohy (výběr ze stromu, z hlášky, ze seznamu) předlohu otevře - jinak by
        // byla vybraná sekce, která na plátně není. Společná sekce je na každé, nic se nepřepíná.
        if (section is not null && !DesignerPages.JeOpakovana(section))
        {
            int index = DesignerPages.IndexStranky(State.CurrentTemplate, section);
            if (index != OtevrenaPredloha) NastavPredlohu(index, zNahledu: false);
        }
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Předlohy stránek (`B184`) ─────────────────────────────────────────────

    /// <summary>Otevřená předloha stránky, srovnaná do rozsahu (šablona se mohla zkrátit).</summary>
    public int OtevrenaPredloha =>
        Math.Clamp(State.Predloha, 0, DesignerPages.ObsahStranek(State.CurrentTemplate).Count - 1);

    public int  PocetPredloh => DesignerPages.ObsahStranek(State.CurrentTemplate).Count;

    /// <summary>Přepínač předloh je vidět jen u šablony o víc stránkách - jednostránková (jízdní
    /// řády) vypadá jako dřív (rozhodnutí Jan 25. 9.).</summary>
    public bool MaPredlohy => PocetPredloh > 1;

    /// <summary>Lišta nad náhledem je vidět, když je co přepínat: stránky dat nebo stránky šablony.</summary>
    public bool MaListuNadNahledem => HasPageNavigation || MaPredlohy;

    /// <summary>„Stránka 2 z 3". Jméno sekce ani „podle dat" se nepíše - dublovalo seznam sekcí
    /// a náhled a přepínač se nedal přečíst (nález Jan 28. 9.).</summary>
    public string PredlohaText => $"Stránka {OtevrenaPredloha + 1} z {PocetPredloh}";

    /// <summary>Náhled ukazuje všechny stránky šablony (jinak jen otevřenou).</summary>
    public bool NahledVsechStranek
    {
        get => State.NahledVsechStranek;
        set
        {
            if (State.NahledVsechStranek == value) return;
            State.NahledVsechStranek = value;
            OnPropertyChanged();
            PredlohaZmenena?.Invoke(OtevrenaPredloha, false);
        }
    }

    /// <summary>Předloha se přepnula; true = přepnul ji posun náhledu (náhled už se neposouvá).</summary>
    public Action<int, bool>? PredlohaZmenena { get; set; }

    private RelayCommand? _predchoziPredloha, _dalsiPredloha;
    public RelayCommand PredchoziPredlohaCommand => _predchoziPredloha ??= Command(
        () => OtevriPredlohu(OtevrenaPredloha - 1), () => OtevrenaPredloha > 0);
    public RelayCommand DalsiPredlohaCommand => _dalsiPredloha ??= Command(
        () => OtevriPredlohu(OtevrenaPredloha + 1), () => OtevrenaPredloha < PocetPredloh - 1);

    /// <summary>
    /// Otevře předlohu na plátně: vybere se její první sekce (tam jde „Přidat sekci") a zruší se
    /// výběr prvků, které na ní nejsou.
    /// </summary>
    public void OtevriPredlohu(int index, bool zNahledu = false)
    {
        index = Math.Clamp(index, 0, PocetPredloh - 1);
        if (index == OtevrenaPredloha && State.Predloha == index) return;
        NastavPredlohu(index, zNahledu);
        var naPredloze = DesignerPages.SekceStranky(State.CurrentTemplate, index);
        if (State.ActiveSection is null || !naPredloze.Contains(State.ActiveSection))
            State.ActiveSection = DesignerPages.ObsahStranek(State.CurrentTemplate)[index].FirstOrDefault();
        State.SelectedElements.RemoveAll(el => !naPredloze.Any(sekce => sekce.Elements.Contains(el)));
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
    }

    private void NastavPredlohu(int index, bool zNahledu)
    {
        State.Predloha = index;
        OnPredlohyZmeneny();
        RequestCanvasSizeUpdate?.Invoke();
        PredlohaZmenena?.Invoke(index, zNahledu);
    }

    private void OnPredlohyZmeneny()
    {
        OnPropertyChanged(nameof(MaPredlohy));
        OnPropertyChanged(nameof(MaListuNadNahledem));
        OnPropertyChanged(nameof(HasPocetStranSazby));
        OnPropertyChanged(nameof(PredlohaText));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Přidá sekci a uvolní pro ni místo: nejdřív z volného zbytku stránky, teprve pak zkrácením
    /// ostatních sekcí. Vratné jedním krokem zpět.
    /// </summary>
    /// <remarks>
    /// ⚠️ Výpočet drží <see cref="SectionLayoutMath.RozdelMistoProNovouSekci"/> - sdílí dno
    /// (obsah sekce) s tažením hrany i s dialogem výšky. Do 10. 9. tahle cesta krátila vždycky
    /// a měla vlastní dno na konstantě 5 mm, takže jako jediná uměla zmáčknout sekci pod její
    /// obsah, i když byla stránka poloprázdná (`B129`).
    /// </remarks>
    public void AddSection(Section section)
    {
        var commands = new List<IDesignerCommand>();
        var template = State.CurrentTemplate;

        // Stránky, na které nová sekce padne (`B184`): obsah na konec aktivní stránky, hlavička
        // a patička na všechny stránky, kde se tisknou - místo musí najít na každé z nich.
        int aktivni = OtevrenaPredloha;
        bool opakovana = DesignerPages.JeOpakovana(section);
        int stranek = DesignerPages.ObsahStranek(template).Count;
        var dotcene = opakovana
            ? Enumerable.Range(0, stranek).Where(i => DesignerPages.TiskneSeNaStrance(section, i, stranek))
                .Select(i => DesignerPages.SekceStranky(template, i)).ToList()
            : [DesignerPages.SekceStranky(template, aktivni)];
        if (dotcene.Count == 0) dotcene.Add(DesignerPages.SekceStranky(template, aktivni));

        float pozadovana = section.DesignerHeight ?? 30f;
        // Výška, která se vejde na všechny dotčené stránky; teprve s ní se počítá, co zkrátit.
        float vyska = dotcene.Min(p => SectionLayoutMath.RozdelMistoProNovouSekci(template, pozadovana, p).VyskaNoveSekce);
        var zkraceni = new Dictionary<Section, float>();
        foreach (var p in dotcene)
            foreach (var (sekce, novaVyska) in SectionLayoutMath.RozdelMistoProNovouSekci(template, vyska, p).Zkraceni)
                zkraceni[sekce] = zkraceni.TryGetValue(sekce, out var dosavadni) ? MathF.Min(dosavadni, novaVyska) : novaVyska;
        var misto = new SectionLayoutMath.MistoProNovouSekci(vyska,
            [.. zkraceni.Select(z => new SectionLayoutMath.ZkraceniSekce(z.Key, z.Value))]);

        foreach (var (sekce, novaVyska) in misto.Zkraceni)
        {
            float stara = sekce.DesignerHeight!.Value;
            commands.Add(new SetPropertyCommand(
                $"Výška sekce {sekce.Name}", stara, novaVyska,
                v => sekce.DesignerHeight = (float)v!));
        }

        // Sekci s dynamickou výškou (`DesignerHeight == null`) se výška nevnucuje - tu počítá
        // engine z obsahu.
        if (section.DesignerHeight is not null)
            section.DesignerHeight = misto.VyskaNoveSekce;

        commands.Add(new AddSectionCommand(template, section,
            opakovana ? null : DesignerPages.PoziceNaKonciStranky(template, aktivni)));

        if (commands.Count > 1)
            Execute(new CompositeCommand("Přidat sekci", commands));
        else
            Execute(commands[0]);

        ActivateSection(section);
        RequestCanvasSizeUpdate?.Invoke();

        if (section.DesignerHeight is not null
            && MathF.Abs(misto.VyskaNoveSekce - pozadovana) > SectionLayoutMath.TolerancePrekryvuMm)
        {
            StatusText = $"Sekce {section.Name} přidána s výškou {misto.VyskaNoveSekce:0.#} mm "
                       + "- na víc už na stránce nebylo místo. Zkraťte jinou sekci, jestli má být vyšší.";
        }
    }

    /// <summary>
    /// Přidá stránku dokladu: sekci se „Začít na nové stránce", která zabere celou volnou plochu
    /// nové stránky (bez hlavičky a patičky, které se na ni tisknou). Ostatní sekce se nemění.
    /// </summary>
    /// <remarks>
    /// Nejde přes <see cref="AddSection"/> - ten hledá místo na dosavadní poslední stránce a kvůli
    /// nové by zkrátil její sekce. Nová stránka je prázdná, takže místo krátit nemusí.
    /// </remarks>
    public void AddPage()
    {
        var template = State.CurrentTemplate;
        int stranek = DesignerPages.ObsahStranek(template).Count;
        bool prvniJePrazdna = stranek == 1 && DesignerPages.ObsahStranek(template)[0].Count == 0;
        int nova = prvniJePrazdna ? 0 : stranek;
        int pocet = nova + 1;
        float opakovane = template.Sections
            .Where(s => DesignerPages.JeOpakovana(s) && DesignerPages.TiskneSeNaStrance(s, nova, pocet))
            .Sum(s => s.EffectiveDesignerHeight);

        var section = new Section
        {
            Name = $"Strana {nova + 1}",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            ZacitNaNoveStrance = !prvniJePrazdna,
            DesignerHeight = MathF.Max(SectionLayoutMath.MinimalniVyskaNoveSekceMm,
                template.PageSize.PrintableHeight - opakovane),
        };
        Execute(new AddSectionCommand(template, section));
        ActivateSection(section);   // otevře novou předlohu
        RequestCanvasSizeUpdate?.Invoke();
    }

    /// <summary>
    /// Roztáhne sekci na celé zbývající místo stránky. Vratné krokem zpět.
    /// </summary>
    /// <remarks>
    /// Sekce nad ní se nehne, sekce pod ní se **posune a podrží svou výšku**; prvky jdou se svou
    /// sekcí samy, protože jejich souřadnice jsou k ní relativní (rozhodnutí Jan 3. 9.).
    ///
    /// <para>⚠️ Je to **akce v místní nabídce**, ne pole ve formuláři sekce - formulář sekce přichází
    /// z Caché (`SectionEdit.cls`), takže tlačítko přidané do C# descriptorů se vůbec nezobrazí.
    /// Do 3. 9. tam bylo právě takhle položené a nešlo na něj kliknout.</para>
    /// </remarks>
    public void VyplnitZbytekStranky(Section section)
    {
        if (section.DesignerHeight is not { } stara) return;   // dynamická výška roste sama

        float nova = SectionLayoutMath.ZbyvajiciMistoMm(State.CurrentTemplate, section);
        if (MathF.Abs(nova - stara) < 0.05f) return;           // ať krok zpět nesbírá prázdné změny

        Execute(new SetPropertyCommand("výška sekce", stara, nova,
            v => section.DesignerHeight = (float?)v));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
        RequestPreviewRefresh?.Invoke();
    }

    /// <summary>
    /// Srazí ručně zadanou výšku sekce do týchže mezí, jaké platí pro tažení a pro „Vyplnit zbytek
    /// stránky". Vrací true, když hodnotu opravdu změnil.
    /// </summary>
    /// <remarks>
    /// ⚠ Formulář sekce přichází z Caché (`SectionEdit.cls`) a **nezná ostatní sekce šablony**,
    /// takže má statický rozsah 5-9999 mm. Bez tohohle sražení šla mez obejít cestou, kterou
    /// uživatel zkusí jako první: tažením sekci pod obsah nezmenšil, ale číslem ano - a prvek se
    /// pak v návrháři oříznul, přestože se na papíře vytiskl celý (nález Jan 4. 9.).
    ///
    /// <para>Korekce se zapisuje **mimo příkaz** - krok zpět vrátí hodnotu z dialogu, takže se stav
    /// nerozejde. Vratná zvlášť by znamenala mačkat zpět dvakrát.</para>
    /// </remarks>
    public bool SrazVyskuSekceDoMezi(Section section)
    {
        if (section.DesignerHeight is not { } zadana) return false;   // dynamická výška roste sama

        float dno     = SectionLayoutMath.MinimalniVyskaProSekci(section);
        float strop   = SectionLayoutMath.ZbyvajiciMistoMm(State.CurrentTemplate, section);
        float srazena = Math.Clamp(zadana, dno, MathF.Max(dno, strop));
        if (srazena == zadana) return false;

        section.DesignerHeight = srazena;
        // Setinové smetí srazit mlčky - hláška o „zmenšení na 21,9 mm" u rozdílu 0,02 mm nic
        // neříká. Srazit se ale musí vždy, jinak smetí zůstane v šabloně a engine ho pak řeší
        // tolerancí (`SectionLayoutMath.TolerancePrekryvuMm`) na každé stránce znovu.
        if (MathF.Abs(srazena - zadana) < SectionLayoutMath.TolerancePrekryvuMm) return true;

        StatusText = srazena > zadana
            ? $"Výška sekce {section.Name} zvětšena na {srazena:0.#} mm - menší by uřízla její obsah"
            : $"Výška sekce {section.Name} zmenšena na {srazena:0.#} mm - větší by nevešla na stránku";
        return true;
    }

    public void MoveSection(Section section, int direction)
    {
        int idx    = State.CurrentTemplate.Sections.IndexOf(section);
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= State.CurrentTemplate.Sections.Count) return;

        Execute(new MoveSectionCommand(State.CurrentTemplate, section, direction));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void ReorderSection(Section section, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || toIndex < 0) return;
        if (toIndex >= State.CurrentTemplate.Sections.Count) return;

        Execute(new ReorderSectionCommand(State.CurrentTemplate, section, fromIndex, toIndex));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void DeleteSection(Section section)
    {
        if (State.CurrentTemplate.Sections.Count <= 1)
        {
            PopupDisplayer.Info("Šablona musí mít alespoň jednu sekci.");
            return;
        }

        int elementCount = section.Elements.Count;
        if (elementCount > 0)
        {
            if (!PopupDisplayer.Confirm($"Opravdu smazat sekci „{section.Name}“ s {elementCount} prvky?"))
                return;
        }

        State.SelectedElements.RemoveAll(el => section.Elements.Contains(el));

        if (State.ActiveSection == section)
            State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault(s => s != section);

        var commands = new List<IDesignerCommand>();
        commands.Add(new DeleteSectionCommand(State.CurrentTemplate, section));

        // Redistribuovat uvolněnou výšku mezi zbývající fixed-height sekce
        float freed = section.DesignerHeight ?? 0f;
        var remaining = State.CurrentTemplate.Sections
            .Where(s => s != section && s.DesignerHeight.HasValue)
            .ToList();

        if (freed > 0 && remaining.Count > 0)
        {
            float totalRemaining = remaining.Sum(s => s.DesignerHeight!.Value);
            foreach (var s in remaining)
            {
                float oldH = s.DesignerHeight!.Value;
                float share = totalRemaining > 0 ? oldH / totalRemaining * freed : freed / remaining.Count;
                float newH = oldH + share;
                if (Math.Abs(newH - oldH) > 0.01f)
                    commands.Add(new SetPropertyCommand(
                        $"Výška sekce {s.Name}", oldH, newH,
                        v => s.DesignerHeight = (float)v!));
            }
        }

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand("Smazat sekci", commands));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void SaveSectionTemplate(Section section, string name)
    {
        try
        {
            _sectionTemplates.Save(section, name);
            PopupDisplayer.Info($"Šablona „{name}“ byla uložena.");
        }
        catch (Exception ex)
        {
            PopupDisplayer.Error(ex);
        }
    }

    public IReadOnlyList<string> GetCustomSectionTemplateNames() =>
        _sectionTemplates.GetAvailableTemplates();

    /// <summary>Uložená šablona sekce - ukládá se mimo šablonu, takže ji jde vložit do jakékoli
    /// agendy; prvky jízdních řádů jiné agendy se z ní vynechají.</summary>
    public Section? LoadCustomSectionTemplate(string name)
    {
        var sekce = _sectionTemplates.Load(name)?.Clone();
        if (sekce is not null) OdeberPrvkyJineAgendy(sekce);
        return sekce;
    }

    /// <summary>Druh JŘ prvků agendy šablony; null = agenda v registru není (registr se nenačetl,
    /// šablona bez agendy) a pak se nic neblokuje.</summary>
    public string? ZnamyDruhAgendy => AgendaEntry(State.CurrentTemplate.AgendaCode)?.ElementKind;

    /// <summary>Je šablona linkového JŘ? Řídí, co formulář poznámek nabízí (`#7487`).</summary>
    public bool JeLinkovyJr => string.Equals(ZnamyDruhAgendy, "LINE", StringComparison.OrdinalIgnoreCase);

    /// <summary>Je šablona rozdílového linkového JŘ? Řídí, jestli formulář tabulky ukáže barvy rozdílů.</summary>
    public bool JeRozdilovyJr => AgendaEntry(State.CurrentTemplate.AgendaCode)?.RozdilyVerzi == true;

    /// <summary>Rozdělí prvky na ty, které do šablony téhle agendy smí, a řekne uživateli, které
    /// vynechal (<see cref="AgendaCapabilities.Allows"/>).</summary>
    private List<TemplateElement> JenPrvkyAgendy(IEnumerable<TemplateElement> prvky)
    {
        var druh = ZnamyDruhAgendy;
        var povolene = new List<TemplateElement>();
        var vynechane = new List<string>();
        foreach (var prvek in prvky)
        {
            if (AgendaCapabilities.Allows(druh, prvek)) povolene.Add(prvek);
            else vynechane.Add(ElementNames.ForUser(prvek));
        }
        if (vynechane.Count > 0)
            PopupDisplayer.Info($"Vynecháno, patří k jiné agendě než tato šablona: {string.Join(", ", vynechane.Distinct())}.");
        return povolene;
    }

    internal void OdeberPrvkyJineAgendy(Section sekce)
    {
        var povolene = JenPrvkyAgendy(sekce.Elements);
        sekce.Elements.Clear();
        sekce.Elements.AddRange(povolene);
    }

    /// <summary>Upozorní na prvky jízdních řádů, které k agendě šablony nepatří - hotová šablona
    /// je může mít z doby, kdy vložení agendu nekontrolovalo. Sám je nemaže; klik na hlášku prvek
    /// vybere.</summary>
    private void ZkontrolujPrvkyAgendy()
    {
        var druh = ZnamyDruhAgendy;
        NastavHlaseni(ZdrojHlaseni.Agenda, [.. State.CurrentTemplate.Sections
            .SelectMany(s => s.Elements)
            .Where(p => !AgendaCapabilities.Allows(druh, p))
            .Select(p => new Hlaseni(ZavaznostHlaseni.Upozorneni, ElementNames.ForUser(p),
                "patří k jiné agendě než šablona, data se v něm nezobrazí správně", "prvek smažte", p.Id))]);
    }
}
