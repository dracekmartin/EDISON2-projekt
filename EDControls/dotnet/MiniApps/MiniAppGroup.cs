namespace EDControls.MiniApps;

/// <summary>Mini-apps jednoho provideru pro submenu shellu (menu „Miniaplikace").</summary>
public sealed record MiniAppGroup(string Title, IReadOnlyList<MiniAppDescriptor> Apps);
