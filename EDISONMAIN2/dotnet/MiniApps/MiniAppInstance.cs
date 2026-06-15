using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Jedna otevřená mini-app v docku. <see cref="ContentId"/> je unikátní per panel (multi-instance,
/// <c>"{DescriptorId}#{n}"</c>); <see cref="DescriptorId"/> je typový (kterou mini-app to je).
/// </summary>
public sealed record MiniAppInstance(
    string DescriptorId,
    string ContentId,
    EDMiniAppViewModel ViewModel,
    IMiniAppPane Pane);
