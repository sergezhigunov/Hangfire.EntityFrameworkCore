namespace Hangfire.EntityFrameworkCore;

internal interface IExpirable
{
    DateTime? ExpireAt { get; set; }
}
