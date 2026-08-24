namespace EDControls.MiniApps;

/// <summary>
/// Metadatová "vizitka" jedné mini-app, kterou agenda registruje do katalogu. Popisuje, jak
/// mini-app vyrobit, aniž by ji hned vytvářela (lazy <see cref="CreateViewModel"/>).
/// </summary>
/// <param name="Id">
/// Stabilní <b>typový</b> identifikátor mini-app (ne identita panelu). Mini-app jde otevřít
/// vícekrát; <c>MiniAppManager</c> z něj skládá unikátní AvalonDock <c>ContentId</c>
/// (<c>"{Id}#{instance}"</c>). Musí být stabilní napříč verzemi - slouží k obnově layoutu.
/// </param>
/// <param name="Title">
/// Název v launcheru / katalogu (statický, bez nutnosti instancovat VM). Liší se od živého
/// <c>vm.Title</c> na panelu, který může být dynamický (např. <c>"Vozidla [filtr]"</c>).
/// </param>
/// <param name="CreateViewModel">
/// Továrna na VM. Volá se až při otevření / obnově panelu. Typicky <c>() => new FooMiniAppViewModel()</c>
/// (bezparametrický ctor čte z <c>EDServices.Current</c>).
/// </param>
public sealed record MiniAppDescriptor(string Id, string Title, Func<EDMiniAppViewModel> CreateViewModel);
