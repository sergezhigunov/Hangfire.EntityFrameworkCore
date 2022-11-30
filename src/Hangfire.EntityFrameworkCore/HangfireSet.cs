using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a scored key value pairs.
/// </summary>
public class HangfireSet : IExpirable
{
    /// <summary>
    /// Gets or sets the pair name.
    /// </summary>
    [MaxLength(100)]
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the pair value.
    /// </summary>
    [MaxLength(256)]
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the pair score.
    /// </summary>
    public double Score { get; set; }

    /// <inheritdoc/>
    public DateTime? ExpireAt { get; set; }
}
