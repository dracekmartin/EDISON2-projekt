using System.Collections;
using System.Reflection;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Commands;

/// <summary>
/// Vyprázdní font u všech prvků šablony, aby zdědily výchozí font dokumentu (`B153`).
/// </summary>
/// <remarks>
/// Prvek s vlastním fontem výchozí font dokumentu přebíjí, takže v šabloně, kde má font každý
/// prvek zapsaný u sebe, volba „Výchozí font" nic nedělá. Tak vznikaly šablony do 30. 6. 2026 -
/// každý nový prvek tehdy font dostal natvrdo - a mají ho i jejich kopie.
/// <para>⚠️ Mění se **jen rodina písma**, nic jiného. Velikost, tučnost ani barvy zůstávají: ty
/// výchozí styl dokumentu sice taky umí, ale v UI se nenabízejí, takže by je uživatel nečekaně
/// ztratil.</para>
/// <para>Místa, kde font sedí, se **hledají reflexí**, ne výčtem: styl prvku, styly zón, styly
/// jednotlivých sloupců i buněk. Ručně psaný seznam by při přidání dalšího stylu tiše zastaral
/// a příkaz by část fontů nechal být.</para>
/// </remarks>
public class PrevzitVychoziFontCommand : IDesignerCommand
{
    private readonly List<TextStyle> _zmenene = [];
    private readonly List<string> _puvodni = [];
    private readonly IReadOnlyList<TemplateElement> _prvky;

    public PrevzitVychoziFontCommand(IEnumerable<TemplateElement> prvky) => _prvky = [.. prvky];

    public string Description => "Použít výchozí font u všech prvků";

    /// <summary>Kolik stylů příkaz změnil - pro hlášku ve stavovém řádku. Platné až po Execute.</summary>
    public int PocetZmenenych => _zmenene.Count;

    public void Execute()
    {
        // Opakované Execute (redo) musí zabrat znovu, ale seznam už je nasbíraný z prvního běhu.
        if (_zmenene.Count > 0)
        {
            foreach (var styl in _zmenene) styl.FontFamily = null;
            return;
        }

        foreach (var prvek in _prvky)
            foreach (var styl in NajdiStyly(prvek))
            {
                if (string.IsNullOrEmpty(styl.FontFamily)) continue;
                _zmenene.Add(styl);
                _puvodni.Add(styl.FontFamily);
                styl.FontFamily = null;
            }
    }

    public void Undo()
    {
        for (int i = 0; i < _zmenene.Count; i++) _zmenene[i].FontFamily = _puvodni[i];
    }

    /// <summary>Všechny styly dosažitelné z prvku - včetně těch ve sloupcích, řádcích a buňkách.</summary>
    private static IEnumerable<TextStyle> NajdiStyly(object koren)
    {
        var videne = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var fronta = new Queue<object>();
        fronta.Enqueue(koren);

        while (fronta.Count > 0)
        {
            object uzel = fronta.Dequeue();
            if (!videne.Add(uzel)) continue;

            if (uzel is TextStyle styl) { yield return styl; continue; }

            if (uzel is IEnumerable seznam and not string)
            {
                foreach (var polozka in seznam)
                    if (polozka is not null && StojiZaProjiti(polozka.GetType())) fronta.Enqueue(polozka);
                continue;
            }

            foreach (var p in uzel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (!StojiZaProjiti(p.PropertyType)) continue;
                object? hodnota;
                try { hodnota = p.GetValue(uzel); }
                catch { continue; }          // vlastnost počítaná z dat může bez dat vyhodit
                if (hodnota is not null) fronta.Enqueue(hodnota);
            }
        }
    }

    /// <summary>
    /// Prochází se jen to, co může styl obsahovat - vlastní modelové typy a jejich seznamy.
    /// </summary>
    /// <remarks>
    /// ⚠️ <see cref="DataContext"/> se vynechává schválně: prvky si na něj drží odkaz (zdrojový
    /// řádek zastávky) a byl by to vstup do celého datového stromu sestavy.
    /// </remarks>
    private static bool StojiZaProjiti(Type typ)
    {
        if (typ == typeof(string) || typ.IsPrimitive || typ.IsEnum) return false;
        if (typeof(DataContext).IsAssignableFrom(typ)) return false;
        if (typeof(IEnumerable).IsAssignableFrom(typ)) return true;
        return typ.Namespace?.StartsWith("TemplateDesigner.Models", StringComparison.Ordinal) == true;
    }
}
