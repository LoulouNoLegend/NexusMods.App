using JetBrains.Annotations;

namespace NexusMods.Abstractions.Settings;

[PublicAPI]
public interface IPropertyUIBuilder<TSettings, TProperty>
    where TSettings : class, ISettings, new()
{
    /// <summary>
    /// Sets the display name of the property.
    /// </summary>
    /// <remarks>
    /// This property does not allow for Markdown.
    /// </remarks>
    IWithDescriptionStep WithDisplayName(string displayName);

    [PublicAPI]
    public interface IWithDescriptionStep
    {
        /// <summary>
        /// Sets the description of the property.
        /// </summary>
        /// <remarks>
        /// This property allows for Markdown.
        /// </remarks>
        IOptionalStep WithDescription(string description);
    }

    [PublicAPI]
    public interface IOptionalStep : IFinishedStep
    {
        /// <summary>
        /// Adds validation to the property.
        /// </summary>
        IOptionalStep WithValidation(Func<TProperty, ValidationResult> validator);

        /// <summary>
        /// Sets the property to require a restart when changed.
        /// </summary>
        /// <remarks>
        /// Use <see cref="RequiresRestart()"/> if you want to use
        /// a generic message instead of a custom one.
        /// </remarks>
        IOptionalStep RequiresRestart(string message);

        /// <summary>
        /// Sets the property to require a restart when changed.
        /// </summary>
        /// <remarks>
        /// This displays a generic message to the user. Use
        /// <see cref="RequiresRestart(string)"/> if you want to
        /// customize the message.
        /// </remarks>
        IOptionalStep RequiresRestart();
    }

    [PublicAPI]
    public interface IFinishedStep;
}