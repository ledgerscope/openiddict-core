/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Core;
using OpenIddict.Ats.Models;
using Xunit;
using SR = OpenIddict.Abstractions.OpenIddictResources;
using Microsoft.Azure.Cosmos.Table;

namespace OpenIddict.Ats.Tests
{
    public class OpenIddictAtsBuilderTests
    {
        [Fact]
        public void Constructor_ThrowsAnExceptionForNullServices()
        {
            // Arrange
            var services = (IServiceCollection) null!;

            // Act and assert
            var exception = Assert.Throws<ArgumentNullException>(() => new OpenIddictAtsBuilder(services));

            Assert.Equal("services", exception.ParamName);
        }

        [Fact]
        public void ReplaceDefaultApplicationEntity_EntityIsCorrectlySet()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act
            builder.ReplaceDefaultApplicationEntity<CustomApplication>();

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

            Assert.Equal(typeof(CustomApplication), options.DefaultApplicationType);
        }

        [Fact]
        public void ReplaceDefaultAuthorizationEntity_EntityIsCorrectlySet()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act
            builder.ReplaceDefaultAuthorizationEntity<CustomAuthorization>();

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

            Assert.Equal(typeof(CustomAuthorization), options.DefaultAuthorizationType);
        }

        [Fact]
        public void ReplaceDefaultScopeEntity_EntityIsCorrectlySet()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act
            builder.ReplaceDefaultScopeEntity<CustomScope>();

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

            Assert.Equal(typeof(CustomScope), options.DefaultScopeType);
        }

        [Fact]
        public void ReplaceDefaultTokenEntity_EntityIsCorrectlySet()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act
            builder.ReplaceDefaultTokenEntity<CustomToken>();

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

            Assert.Equal(typeof(CustomToken), options.DefaultTokenType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SetApplicationsCollectionName_ThrowsAnExceptionForNullOrEmptyCollectionName(string name)
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act and assert
            var exception = Assert.Throws<ArgumentException>(() => builder.SetApplicationsCollectionName(name));

            Assert.Equal("name", exception.ParamName);
            Assert.StartsWith(SR.GetResourceString(SR.ID0261), exception.Message);
        }

        [Fact]
        public void SetApplicationsCollectionName_CollectionNameIsCorrectlySet()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act
            builder.SetApplicationsCollectionName("custom_collection");

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictAtsOptions>>().CurrentValue;

            Assert.Equal("custom_collection", options.ApplicationsCollectionName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SetAuthorizationsCollectionName_ThrowsAnExceptionForNullOrEmptyCollectionName(string name)
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act and assert
            var exception = Assert.Throws<ArgumentException>(() => builder.SetAuthorizationsCollectionName(name));

            Assert.Equal("name", exception.ParamName);
            Assert.StartsWith(SR.GetResourceString(SR.ID0261), exception.Message);
        }

        [Fact]
        public void SetAuthorizationsCollectionName_CollectionNameIsCorrectlySet()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act
            builder.SetAuthorizationsCollectionName("custom_collection");

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictAtsOptions>>().CurrentValue;

            Assert.Equal("custom_collection", options.AuthorizationsCollectionName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SetScopesCollectionName_ThrowsAnExceptionForNullOrEmptyCollectionName(string name)
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act and assert
            var exception = Assert.Throws<ArgumentException>(() => builder.SetScopesCollectionName(name));

            Assert.Equal("name", exception.ParamName);
            Assert.StartsWith(SR.GetResourceString(SR.ID0261), exception.Message);
        }

        [Fact]
        public void SetScopesCollectionName_CollectionNameIsCorrectlySet()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act
            builder.SetScopesCollectionName("custom_collection");

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictAtsOptions>>().CurrentValue;

            Assert.Equal("custom_collection", options.ScopesCollectionName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SetTokensCollectionName_ThrowsAnExceptionForNullOrEmptyCollectionName(string name)
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act and assert
            var exception = Assert.Throws<ArgumentException>(() => builder.SetTokensCollectionName(name));

            Assert.Equal("name", exception.ParamName);
            Assert.StartsWith(SR.GetResourceString(SR.ID0261), exception.Message);
        }

        [Fact]
        public void SetTokensCollectionName_CollectionNameIsCorrectlySet()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act
            builder.SetTokensCollectionName("custom_collection");

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictAtsOptions>>().CurrentValue;

            Assert.Equal("custom_collection", options.TokensCollectionName);
        }

        [Fact]
        public void UseDatabase_ThrowsAnExceptionForNullDatabase()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);

            // Act and assert
            var exception = Assert.Throws<ArgumentNullException>(delegate
            {
                return builder.UseAts(cloudTableClient: null!);
            });

            Assert.Equal("cloudTableClient", exception.ParamName);
        }
        
        [Fact]
        public void UseDatabase_SetsDatabaseInOptions()
        {
            // Arrange
            var services = CreateServices();
            var builder = CreateBuilder(services);
            var database = Mock.Of<ICloudTableClient>();

            // Act
            builder.UseAts(database);

            // Assert
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictAtsOptions>>().CurrentValue;

            Assert.Equal(database, options.Database);
        }

        private static OpenIddictAtsBuilder CreateBuilder(IServiceCollection services)
            => services.AddOpenIddict().AddCore().UseAts();

        private static IServiceCollection CreateServices()
        {
            var services = new ServiceCollection();
            services.AddOptions();

            return services;
        }

        public class CustomApplication : OpenIddictAtsApplication { }
        public class CustomAuthorization : OpenIddictAtsAuthorization { }
        public class CustomScope : OpenIddictAtsScope { }
        public class CustomToken : OpenIddictAtsToken { }
    }
}
