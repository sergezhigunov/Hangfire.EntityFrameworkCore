using System;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EFCoreStorageExtensionsFacts
    {
        [Fact]
        public void UseEFCoreStorage_Throws_WhenConfigurationParameterIsNull()
        {
            IGlobalConfiguration configuration = null;
            Action<DbContextOptionsBuilder> optionsAction = builder => { };
            var options = new EFCoreStorageOptions();

            Assert.Throws<ArgumentNullException>(nameof(configuration),
                () => configuration.UseEFCoreStorage(optionsAction));

            Assert.Throws<ArgumentNullException>(nameof(configuration),
                () => configuration.UseEFCoreStorage(optionsAction, options));
        }

        [Fact]
        public void UseEFCoreStorage_Throws_WhenOptionsActionParameterIsNull()
        {
            var configuration = new Mock<IGlobalConfiguration>().Object;
            Action<DbContextOptionsBuilder> optionsAction = null;
            var options = new EFCoreStorageOptions();

            Assert.Throws<ArgumentNullException>(nameof(optionsAction),
                () => configuration.UseEFCoreStorage(optionsAction));

            Assert.Throws<ArgumentNullException>(nameof(optionsAction),
                () => configuration.UseEFCoreStorage(optionsAction, options));
        }

        [Fact]
        public void UseEFCoreStorage_Throws_WhenOptionsParameterIsNull()
        {
            var configuration = new Mock<IGlobalConfiguration>().Object;
            Action<DbContextOptionsBuilder> optionsAction = builder => { };
            EFCoreStorageOptions options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => configuration.UseEFCoreStorage(optionsAction, options));
        }

        [Fact]
        public void UseEFCoreStorage_CompletesSuccesfully()
        {
            var configurationMock = new Mock<IGlobalConfiguration>();
            var configuration = configurationMock.Object;
            bool optionsActionExposed = false;
            Action<DbContextOptionsBuilder> optionsAction =
                builder => { optionsActionExposed = true; };
            var options = new EFCoreStorageOptions();

            var result = configuration.UseEFCoreStorage(optionsAction, options);

            Assert.NotNull(result);
            var genericConfiguration =
                Assert.IsAssignableFrom<IGlobalConfiguration<EFCoreStorage>>(result);
            Assert.NotNull(genericConfiguration.Entry);
            Assert.True(optionsActionExposed);
        }

        [Fact]
        public void UseEFCoreStorage_CompletesSuccesfully_WithDefaultOptions()
        {
            var configurationMock = new Mock<IGlobalConfiguration>();
            var configuration = configurationMock.Object;
            bool optionsActionExposed = false;
            Action<DbContextOptionsBuilder> optionsAction =
                builder => { optionsActionExposed = true; };

            var result = configuration.UseEFCoreStorage(optionsAction);

            Assert.NotNull(result);
            var genericConfiguration =
                Assert.IsAssignableFrom<IGlobalConfiguration<EFCoreStorage>>(result);
            Assert.NotNull(genericConfiguration.Entry);
            Assert.True(optionsActionExposed);
        }
    }
}
