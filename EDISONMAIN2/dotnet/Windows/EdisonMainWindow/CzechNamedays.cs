using System;

namespace EDISONMAIN2.Windows.EdisonMainWindow;

/// <summary>
/// Český kalendář jmen pro INFO label v menu shellu. Statický seznam - neuložený v Caché,
/// neměnný napříč zákazníky. Pro státní svátky bez jmenného nositele (Den vítězství, …) je
/// hodnota prázdná - caller pak format string vynechá " svátek má X" suffix.
/// </summary>
internal static class CzechNamedays
{
    /// <summary>
    /// Vrátí jméno pro daný den (1-12 měsíc, 1-31 den), nebo null pokud je pole prázdné
    /// (státní svátek bez jména) nebo datum je out of range (29.2 v nepřestupném roce).
    /// </summary>
    public static string? GetByDate(DateOnly date)
    {
        if (date.Month < 1 || date.Month > 12) return null;
        var month = ByMonth[date.Month - 1];
        if (date.Day < 1 || date.Day > month.Length) return null;
        var name = month[date.Day - 1];
        return string.IsNullOrEmpty(name) ? null : name;
    }

    // [month-1][day-1] → nameday. Empty string = státní svátek bez nositele jména.
    // 29.2 je v poli února na pozici [28]; v nepřestupný rok nikdy nedosáhneme přes Day kontrolu.
    private static readonly string[][] ByMonth =
    [
        // Leden
        [
            "", "Karina", "Radmila", "Diana", "Dalimil", "", "Vilma", "Čestmír",
            "Vladan", "Břetislav", "Bohdana", "Pravoslav", "Edita", "Radovan", "Alice", "Ctirad",
            "Drahoslav", "Vladislav", "Doubravka", "Ilona", "Běla", "Slavomír", "Zdeněk", "Milena",
            "Miloš", "Zora", "Ingrid", "Otýlie", "Zdislava", "Robin", "Marika"
        ],
        // Únor
        [
            "Hynek", "Nela", "Blažej", "Jarmila", "Dobromila", "Vanda", "Veronika", "Milada",
            "Apolena", "Mojmír", "Božena", "Slavěna", "Věnceslav", "Valentýn", "Jiřina", "Ljuba",
            "Miloslava", "Gizela", "Patrik", "Oldřich", "Lenka", "Petr", "Svatopluk", "Matěj",
            "Liliana", "Dorota", "Alexandr", "Lumír", "Horymír"
        ],
        // Březen
        [
            "Bedřich", "Anežka", "Kamil", "Stela", "Kazimír", "Miroslav", "Tomáš", "Gabriela",
            "Františka", "Viktorie", "Anděla", "Řehoř", "Růžena", "Rút", "Ida", "Elena",
            "Vlastimil", "Eduard", "Josef", "Světlana", "Radek", "Leona", "Ivona", "Gabriel",
            "Marián", "Emanuel", "Dita", "Soňa", "Taťána", "Arnošt", "Kvido"
        ],
        // Duben
        [
            "Hugo", "Erika", "Richard", "Ivana", "Miroslava", "Vendula", "Heřman", "Ema",
            "Dušan", "Darja", "Izabela", "Julius", "Aleš", "Vincenc", "Anastázie", "Irena",
            "Rudolf", "Valérie", "Rostislav", "Marcela", "Alexandra", "Evženie", "Vojtěch", "Jiří",
            "Marek", "Oto", "Jaroslav", "Vlastislav", "Robert", "Blahoslav"
        ],
        // Květen
        [
            "", "Zikmund", "Alexej", "Květoslav", "Klaudie", "Radoslav", "Stanislav", "",
            "Ctibor", "Blažena", "Svatava", "Pankrác", "Servác", "Bonifác", "Žofie", "Přemysl",
            "Aneta", "Nataša", "Ivo", "Zbyšek", "Monika", "Emil", "Vladimír", "Jana",
            "Viola", "Filip", "Valdemar", "Vilém", "Maxmilián", "Ferdinand", "Kamila"
        ],
        // Červen
        [
            "Laura", "Jarmil", "Tamara", "Dalibor", "Dobroslav", "Norbert", "Iveta", "Medard",
            "Stanislava", "Gita", "Bruno", "Antonie", "Antonín", "Roland", "Vít", "Zbyněk",
            "Adolf", "Milan", "Leoš", "Květa", "Alois", "Pavla", "Zdeňka", "Jan",
            "Ivan", "Adriana", "Ladislav", "Lubomír", "Petr a Pavel", "Šárka"
        ],
        // Červenec
        [
            "Jaroslava", "Patricie", "Radomír", "Prokop", "Cyril a Metoděj", "", "Bohuslava", "Nora",
            "Drahoslava", "Libuše", "Olga", "Bořek", "Markéta", "Karolína", "Jindřich", "Luboš",
            "Martina", "Drahomíra", "Čeněk", "Ilja", "Vítězslav", "Magdaléna", "Libor", "Kristýna",
            "Jakub", "Anna", "Věroslav", "Viktor", "Marta", "Bořivoj", "Ignác"
        ],
        // Srpen
        [
            "Oskar", "Gustav", "Miluše", "Dominik", "Kristián", "Oldřiška", "Lada", "Soběslav",
            "Roman", "Vavřinec", "Zuzana", "Klára", "Alena", "Alan", "Hana", "Jáchym",
            "Petra", "Helena", "Ludvík", "Bernard", "Johana", "Bohuslav", "Sandra", "Bartoloměj",
            "Radim", "Luděk", "Otakar", "Augustýn", "Evelína", "Vladěna", "Pavlína"
        ],
        // Září
        [
            "Linda", "Adéla", "Bronislav", "Jindřiška", "Boris", "Boleslav", "Regína", "Mariana",
            "Daniela", "Irma", "Denisa", "Marie", "Lubor", "Radka", "Jolana", "Ludmila",
            "Naděžda", "Kryštof", "Zita", "Oleg", "Matouš", "Darina", "Berta", "Jaromír",
            "Zlata", "Andrea", "Jonáš", "Václav", "Michal", "Jeroným"
        ],
        // Říjen
        [
            "Igor", "Olívie", "Bohumil", "František", "Eliška", "Hanuš", "Justýna", "Věra",
            "Štefan", "Marina", "Andrej", "Marcel", "Renáta", "Agáta", "Tereza", "Havel",
            "Hedvika", "Lukáš", "Michaela", "Vendelín", "Brigita", "Sabina", "Teodor", "Nina",
            "Beáta", "Erik", "Šarlota", "", "Silvie", "Tadeáš", "Štěpánka"
        ],
        // Listopad
        [
            "Felix", "", "Hubert", "Karel", "Miriam", "Liběna", "Saskie", "Bohumír",
            "Bohdan", "Evžen", "Martin", "Benedikt", "Tibor", "Sáva", "Leopold", "Otmar",
            "Mahulena", "Romana", "Alžběta", "Nikola", "Albert", "Cecílie", "Klement", "Emílie",
            "Kateřina", "Artur", "Xenie", "René", "Zina", "Ondřej"
        ],
        // Prosinec
        [
            "Iva", "Blanka", "Svatoslav", "Barbora", "Jitka", "Mikuláš", "Ambrož", "Květoslava",
            "Vratislav", "Julie", "Dana", "Simona", "Lucie", "Lýdie", "Radana", "Albína",
            "Daniel", "Miloslav", "Ester", "Dagmar", "Natálie", "Šimon", "Vlasta", "Adam a Eva",
            "", "Štěpán", "Žaneta", "Bohumila", "Judita", "David", "Silvestr"
        ],
    ];
}
