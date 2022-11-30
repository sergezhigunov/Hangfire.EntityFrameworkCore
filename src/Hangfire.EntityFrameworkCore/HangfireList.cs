using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a named numbered list item.
/// </summary>

public class HangfireList : IExpirable
{
    /// <summary>
    /// Gets or sets the list name.
    /// </summary>
    [MaxLength(256)]
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the list item index.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the list item value.
    /// </summary>
    public string Value { get; set; }

    /// <inheritdoc/>
    public DateTime? ExpireAt { get; set; }
}
