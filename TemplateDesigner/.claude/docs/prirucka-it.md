# Návrhář sestav - technické požadavky (pro IT)

Aby se Návrhář sestav na počítači spustil, musí být nainstalovaný **.NET Desktop Runtime 10.0
(x64)** od Microsoftu - jednorázová instalace, víc není potřeba (Windows 10/11, 64bit).

## Instalace

1. Otevřete **[dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0)**.
2. V sekci **.NET Desktop Runtime 10.0** zvolte variantu **Windows x64**.
3. Stáhněte a spusťte instalátor (další → další → dokončit). Restart počítače není nutný.

**Musí to být „Desktop" Runtime** (ne jen „.NET Runtime") ve variantě **x64** - Návrhář je
desktopová (WPF) aplikace. Více verzí runtime vedle sebe koexistuje bez problémů. Nejčastější
příčina, proč se Návrhář nespustí, je právě chybějící Desktop Runtime.

Návod k použití nástroje → [Návrhář sestav - uživatelská příručka](https://podpora.m-line.cz/support/wiki/90511fc5-10e5-476d-b7c1-3cd70672f26d).
