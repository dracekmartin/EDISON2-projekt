namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Kaskáda stylů čte procesně-globální <c>DocumentDefaults.Style</c> (kořen kaskády).
/// Všechny testy, které kaskádu resolví nebo DocumentDefaults mutují, patří do této
/// kolekce - xUnit je pak spouští sekvenčně a nemohou se navzájem ovlivnit.
/// </summary>
[CollectionDefinition("StyleCascade")]
public class StyleCascadeCollection;
