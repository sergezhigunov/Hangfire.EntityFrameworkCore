using System;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public static class EFCoreLockFacts
    {
        [Fact]
        public static void Ctor_Throws_WhenProviderParameterIsNull()
        {
            ILockProvider provider = null;
            const string resource = "resource";
            TimeSpan timeout = default;

            Assert.Throws<ArgumentNullException>(nameof(provider),
                () => new EFCoreLock(provider, resource, timeout));
        }

        [Fact]
        public static void Ctor_CreatesInstance()
        {
            var providerMock = new Mock<ILockProvider>();
            const string resource = "resource";
            var timeout = new TimeSpan(123);
            providerMock.Setup(x => x.Acquire(resource, timeout));
            var provider = providerMock.Object;

            var instance = new EFCoreLock(provider, resource, timeout);

            Assert.Equal(provider,
                Assert.IsAssignableFrom<ILockProvider>(
                    instance.GetFieldValue("_provider")));
            Assert.Equal(resource,
                Assert.IsType<string>(
                    instance.GetFieldValue("_resource")));
            providerMock.Verify(x => x.Acquire(resource, timeout));
        }

        [Fact]
        public static void Dispose_InvokesProviderReleaseMethod()
        {
            var providerMock = new Mock<ILockProvider>();
            const string resource = "resource";
            var timeout = new TimeSpan(123);
            var provider = providerMock.Object;
            providerMock.Setup(x => x.Release(resource));
            var instance = new EFCoreLock(provider, resource, timeout);

            instance.Dispose();

            providerMock.Verify(x => x.Release(resource));

            instance.Dispose();

            providerMock.Verify(x => x.Release(resource));
        }

    }
}
