namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents an entity that can be expire.
/// </summary>
public interface IExpirable
{
    /// <summary>
    /// Gets or set an entity expiration date and time.
    /// </summary>
    DateTime? ExpireAt { get; set; }
}
