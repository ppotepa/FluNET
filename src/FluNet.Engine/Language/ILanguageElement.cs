namespace FluNET.Language;

/// <summary>
/// Common identity contract for elements that become part of the compiled FluNET language.
/// Stable identifiers are intended for diagnostics, manifests, tooling and caches.
/// </summary>
public interface ILanguageElement
{
    string StableId { get; }
    string Name { get; }
}
