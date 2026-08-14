namespace CommanderPeriscope;

/// <summary>
/// Marker type for <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> resolution against
/// Resources/SharedResource.resx. Kept out of the Resources/ folder deliberately — a marker class living
/// alongside its .resx there would make MSBuild key the embedded resource off the class's namespace instead
/// of the folder path, breaking the ResourcesPath-based lookup in <see cref="Bootstrap"/>.
/// </summary>
public class SharedResource;
