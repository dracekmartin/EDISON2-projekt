using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Jedna otevřená mini-app v docku. <see cref="ContentId"/> je unikátní per panel (multi-instance,
/// <c>"{DescriptorId}#{n}"</c>); <see cref="DescriptorId"/> je typový (kterou mini-app to je).
/// </summary>
/// <param name="Pinned">Z deskriptoru - uživatel ji zavřít nemůže, <c>CloseClosable</c> ji přeskočí.</param>
public sealed record MiniAppInstance(
    string DescriptorId,
    string ContentId,
    EDMiniAppViewModel ViewModel,
    IMiniAppPane Pane,
    bool Pinned);
