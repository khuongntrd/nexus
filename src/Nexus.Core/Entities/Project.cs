using Nexus.Core.ValueObjects;

namespace Nexus.Core.Entities;

/// <summary>
/// Represents a project that can contain multiple integrations and other related entities.
/// </summary>
public sealed class Project
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Project"/> class with the specified parameters.
    /// </summary>
    /// <param name="name">Project name.</param>
    /// <param name="externalRef">Optional external reference.</param>
    public Project(string name, ExternalRef? externalRef = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        ExternalRef = externalRef;
    }

    /// <summary>Unique identifier for the project.</summary>
    public Guid Id { get; private set; }

    /// <summary>Name of the project.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>External reference to the source project.</summary>
    public ExternalRef? ExternalRef { get; private set; }

    /// <summary>
    /// Updates the project with new values.
    /// </summary>
    /// <param name="name">Optional new name.</param>
    /// <param name="externalRef">Optional new external reference.</param>
    public void Update(string? name = null, ExternalRef? externalRef = null)
    {
        if (name is not null)
        {
            Name = name;
        }

        if (externalRef is not null)
        {
            ExternalRef = externalRef;
        }
    }

#pragma warning disable SA1201 // EF Core materialization ctor must follow the public API (SA1202).
    private Project()
    {
    }
#pragma warning restore SA1201
}
