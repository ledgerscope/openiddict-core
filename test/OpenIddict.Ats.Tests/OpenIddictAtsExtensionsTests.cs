/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.Ats.Models;
using Xunit;

namespace OpenIddict.Ats.Tests
{
    public class OpenIddictAtsExtensionsTests
    {
        [Fact]
        public void UseAts_ThrowsAnExceptionForNullBuilder()
        {
            // Arrange
            var builder = (OpenIddictCoreBuilder) null!;

            // Act and assert
            var exception = Assert.Throws<ArgumentNullException>(() => builder.UseAts());

            Assert.Equal("builder", exception.ParamName);
        }

        [Fact]
        public void UseAts_ThrowsAnExceptionForNullConfiguration()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = new OpenIddictCoreBuilder(services);

            // Act and assert
            var exception = Assert.Throws<ArgumentNullException>(() => builder.UseAts(configuration: null!));

            Assert.Equal("configuration", exception.ParamName);
        }

        [Fact]
        public void UseAts_RegistersDefaultEntities()
        {
            // Arrange
            var services = new ServiceCollection().AddOptions();
            var builder = new OpenIddictCoreBuilder(services);

            // Act
            builder.UseAts();

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

            Assert.Equal(typeof(OpenIddictAtsApplication), options.DefaultApplicationType);
            Assert.Equal(typeof(OpenIddictAtsAuthorization), options.DefaultAuthorizationType);
            Assert.Equal(typeof(OpenIddictAtsScope), options.DefaultScopeType);
            Assert.Equal(typeof(OpenIddictAtsToken), options.DefaultTokenType);
        }

        [Theory]
        [InlineData(typeof(IOpenIddictApplicationStoreResolver), typeof(OpenIddictAtsApplicationStoreResolver))]
        [InlineData(typeof(IOpenIddictAuthorizationStoreResolver), typeof(OpenIddictAtsAuthorizationStoreResolver))]
        [InlineData(typeof(IOpenIddictScopeStoreResolver), typeof(OpenIddictAtsScopeStoreResolver))]
        [InlineData(typeof(IOpenIddictTokenStoreResolver), typeof(OpenIddictAtsTokenStoreResolver))]
        public void UseAts_RegistersAtsStoreResolvers(Type serviceType, Type implementationType)
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = new OpenIddictCoreBuilder(services);

            // Act
            builder.UseAts();

            // Assert
            Assert.Contains(services, service => service.ServiceType == serviceType &&
                                                 service.ImplementationType == implementationType);
        }

        [Theory]
        [InlineData(typeof(OpenIddictAtsApplicationStore<>))]
        [InlineData(typeof(OpenIddictAtsAuthorizationStore<>))]
        [InlineData(typeof(OpenIddictAtsScopeStore<>))]
        [InlineData(typeof(OpenIddictAtsTokenStore<>))]
        public void UseAts_RegistersAtsStore(Type type)
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = new OpenIddictCoreBuilder(services);

            // Act
            builder.UseAts();

            // Assert
            Assert.Contains(services, service => service.ServiceType == type && service.ImplementationType == type);
        }

        [Fact]
        public void UseAts_RegistersAtsContext()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = new OpenIddictCoreBuilder(services);

            // Act
            builder.UseAts();

            // Assert
            Assert.Contains(services, service => service.Lifetime == ServiceLifetime.Singleton &&
                                                 service.ServiceType == typeof(IOpenIddictAtsContext) &&
                                                 service.ImplementationType == typeof(OpenIddictAtsContext));
        }
    }
}
