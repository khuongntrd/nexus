using Nexus.Core.ValueObjects;

namespace Nexus.Web.Components.Connectors;

/// <summary>
/// Model for connection dialog UI.
/// </summary>
public sealed record ConnectionDialogModel
{
    /// <summary>
    /// The connection ID for editing, or null for creating a new connection.
    /// </summary>
    public Guid? ConnectionId { get; init; }

    /// <summary>
    /// Whether the connection has been saved.
    /// </summary>
    public bool IsSaved { get; set; }

    /// <summary>
    /// An error message to display, if any.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The async function to save the connection.
    /// </summary>
    public Func<Task<bool>>? SaveAsync { get; set; }

    /// <summary>
    /// The dialog title based on whether editing or creating.
    /// </summary>
    public string Title => ConnectionId.HasValue ? "Edit connection" : "Add connection";

    /// <summary>
    /// The initial service type for the connection.
    /// </summary>
    public ServiceType? InitialServiceType { get; init; }

    /// <summary>
    /// Saves the connection if the save function is available.
    /// </summary>
    /// <returns>True if saved successfully, false otherwise.</returns>
    public Task<bool> SaveIfReadyAsync()
        => SaveAsync?.Invoke() ?? Task.FromResult(false);
}
