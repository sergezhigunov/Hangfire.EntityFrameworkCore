using System;
using Hangfire.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireModelCacheKey : ModelCacheKey
    {
        internal string Schema { get; }

        public HangfireModelCacheKey(DbContext context)
            : base(context)
        {
            Schema = (context as HangfireContext)?.Schema;
        }

        protected override bool Equals([NotNull] ModelCacheKey other)
        {
            return base.Equals(other) &&
                (other as HangfireModelCacheKey)?.Schema == Schema;
        }

        public override int GetHashCode()
        {
            var hashCode = base.GetHashCode();
            if (Schema != null)
                hashCode =
#if NETSTANDARD2_0
                    CombineHash(hashCode, Schema.GetHashCode());
#else
                    HashCode.Combine(hashCode, Schema.GetHashCode(StringComparison.Ordinal));
#endif
            return hashCode;
        }

#if NETSTANDARD2_0
        private static int CombineHash(int hash1, int hash2)
        {
            return ((hash1 << 7) | (hash1 >> 25)) ^ hash2;
        }
#endif
    }
}
