using System;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

internal class HangfireList : IExpirable
{
    [MaxLength(256)]
    public string Key { get; set; }

    public int Position { get; set; }

    public string Value { get; set; }

    public DateTime? ExpireAt { get; set; }
}
