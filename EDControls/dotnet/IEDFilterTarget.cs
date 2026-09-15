using System.Collections.Generic;
using System.Threading.Tasks;

namespace EDControls;

/// <summary>
/// Cíl filtru <c>EDFilter</c> - komponenta, která umí zobrazit data zúžená filtrem
/// (EDList, mapa, graf). Záměrně v root namespace: filtr nesmí znát konkrétní control.
///
/// Klíče slovníku jsou filter klíče polí (<c>EDFilterFieldBase.Key</c>), prázdný slovník
/// znamená bez filtru. Komponenty nad Caché je posílají jako <c>^||ML("IN","filter",Key)</c>.
/// </summary>
public interface IEDFilterTarget
{
    Task ApplyFilterAsync(IReadOnlyDictionary<string, string> filter);
}
