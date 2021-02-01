using System;

namespace Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext
{
    public class SampleTable
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
    }
}
