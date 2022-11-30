using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

internal class HangfireCounter : IExpirable
{
    public long Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string Key { get; set; }

    public long Value { get; set; }

    public DateTime? ExpireAt { get; set; }
}
