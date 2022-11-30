using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a named dictionary item value record.
/// </summary>
public class HangfireHash : IExpirable
{
    /// <summary>
    /// Gets or sets named dictionary name.
    /// </summary>
    [MaxLength(256)]
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets named dictionary item name.
    /// </summary>
    [MaxLength(256)]
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the dictionary item value.
    /// </summary>
    public string Value { get; set; }

    /// <inheritdoc/>
    public DateTime? ExpireAt { get; set; }
}
