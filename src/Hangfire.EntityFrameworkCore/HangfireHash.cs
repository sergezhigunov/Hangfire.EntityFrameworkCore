using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

internal class HangfireHash : IExpirable
{
    [MaxLength(256)]
    public string Key { get; set; }

    [MaxLength(256)]
    public string Field { get; set; }

    public string Value { get; set; }

    public DateTime? ExpireAt { get; set; }
}
