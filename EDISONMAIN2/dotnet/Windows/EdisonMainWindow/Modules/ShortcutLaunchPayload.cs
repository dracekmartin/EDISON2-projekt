using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Wire tvar zástupce pro 14. argument launch kontraktu (viz <c>docs/DESKTOP_SHORTCUTS.md</c>).
    /// Pole odpovídají legacy <c>ALVA.LIBRARY.Remote.SerializableShortcut</c> (XML nad veřejnými
    /// poli) - host je deserializuje toutéž třídou, takže **jména ani typy polí se nesmí měnit**.
    /// </summary>
    [XmlRoot("SerializableShortcut")]
    public class ShortcutLaunchPayload
    {
        public string ID = "";
        public string Name = "";
        /// <summary>Hostitelská forma, numerické <c>EDISFormEnum</c> id.</summary>
        public int Form = -1;
        public string InputProFrm = "";
        /// <summary>Legacy <c>DesktopShortcutCallType</c>: 0 = Formular, 1 = Procedura.</summary>
        public int CallType = -1;
        /// <summary>Legacy <c>DesktopShortcutItemType</c>, pořadí viz <see cref="ShortcutKind"/> mapování.</summary>
        public int ItemType = -1;
        public bool IsHidden;
        public string Agenda = "";
        public string Oblast = "";
        public string TypSestavy = "";
        public string Sestava = "";
        public string Predvolba = "";
        public bool JeVerejny;
        public bool JdeOReportShortcut;
    }

    /// <summary>
    /// Překlad zástupce plochy na 14. argument hosta a zpět. Base64 je tu proto, že
    /// <c>BuildLauncherArguments</c> hodnoty jen obaluje uvozovkami a neescapuje je - syrové XML
    /// (uvozovky, mezery, konce řádků) by rozbilo celý příkazový řádek.
    /// </summary>
    public static class ShortcutLaunchPayloadCodec
    {
        private static readonly XmlSerializer Serializer = new(typeof(ShortcutLaunchPayload));

        /// <summary>Legacy <c>DesktopShortcutItemType</c> - pořadí v enumu je součástí wire kontraktu.</summary>
        private static int ItemTypeOf(ShortcutKind kind) => kind switch
        {
            ShortcutKind.MenuModule => 0,    // FormularHlavniNabidky
            ShortcutKind.PackageForm => 1,   // FormularDanehoBaliku
            ShortcutKind.DwhView => 2,       // PohledDatovehoSkladu
            ShortcutKind.SelectReport => 3,  // SestavaVyberova
            ShortcutKind.PrintReport => 4,   // SestavaTiskova
            _ => -1,
        };

        /// <summary>Tiskovou sestavu spouští procedura balíku (1), ostatní druhy forma (0).</summary>
        private static int CallTypeOf(ShortcutKind kind) => kind == ShortcutKind.PrintReport ? 1 : 0;

        /// <summary>
        /// <c>true</c> = zástupce se spouští přes 14. argument (procedura / forma s payloadem).
        /// Modul hlavní nabídky se spouští běžnou cestou, payload nepotřebuje.
        /// </summary>
        public static bool NeedsPayload(DesktopShortcut shortcut) => shortcut.Kind != ShortcutKind.MenuModule;

        public static string Encode(DesktopShortcut shortcut)
        {
            var payload = new ShortcutLaunchPayload
            {
                // Pozor: ID je u sestav ČÍSLO (legacy GC<enum>.FromTypeToString dělá ((int)v).ToString()
                // a zpětný převod nečíselný text tiše degraduje na 0 = tichý no-op tisku).
                ID = shortcut.Id,
                Name = shortcut.DisplayedName,
                Form = int.TryParse(shortcut.ElementId, out var form) ? form : -1,
                InputProFrm = shortcut.InputParam,
                CallType = CallTypeOf(shortcut.Kind),
                ItemType = ItemTypeOf(shortcut.Kind),
                JdeOReportShortcut = shortcut.Report is not null,
            };
            if (shortcut.Report is { } r)
            {
                payload.Agenda = r.Agenda;
                payload.Oblast = r.Oblast;
                payload.TypSestavy = r.TypSestavy;
                payload.Sestava = r.Sestava;
                payload.Predvolba = r.Predvolba;
                payload.JeVerejny = r.JeVerejny;
            }

            using var buffer = new StringWriter();
            Serializer.Serialize(buffer, payload);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(buffer.ToString()));
        }

        /// <summary>Zpětný převod - jen pro testy kontraktu; za běhu dekóduje host.</summary>
        public static ShortcutLaunchPayload Decode(string encoded)
        {
            var xml = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            using var reader = new StringReader(xml);
            return (ShortcutLaunchPayload)Serializer.Deserialize(reader)!;
        }
    }
}
