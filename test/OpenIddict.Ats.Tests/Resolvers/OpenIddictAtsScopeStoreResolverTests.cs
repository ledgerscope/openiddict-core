/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Ats.Models;
using Xunit;
using SR = OpenIddict.Abstractions.OpenIddictResources;

namespace OpenIddict.Ats.Tests
{
    public class OpenIddictAtsScopeStoreResolverTests
    {
        [Fact]
        public void Get_ReturnsCustomStoreCorrespondingToTheSpecifiedTypeWhenAvailable()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Mock.Of<IOpenIddictScopeStore<CustomScope>>());

            var provider = services.BuildServiceProvider();
            var resolver = new OpenIddictAtsScopeStoreResolver(provider);
            
            // Act and assert
            Assert.NotNull(resolver.Get<CustomScope>());
        }

        [Fact]
        public void Get_ThrowsAnExceptionForInvalidEntityType()
        {
            // Arrange
            var services = new ServiceCollection();

            var provider = services.BuildServiceProvider();
            var resolver = new OpenIddictAtsScopeStoreResolver(provider);

            // Act and assert
            var exception = Assert.Throws<InvalidOperationException>(() => resolver.Get<CustomScope>());

            Assert.Equal(SR.GetResourceString(SR.ID0259), exception.Message);
        }

        [Fact]
        public void Get_ReturnsDefaultStoreCorrespondingToTheSpecifiedTypeWhenAvailable()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Mock.Of<IOpenIddictScopeStore<CustomScope>>());
            services.AddSingleton(CreateStore());

            var provider = services.BuildServiceProvider();
            var resolver = new OpenIddictAtsScopeStoreResolver(provider);

            // Act and assert
            Assert.NotNull(resolver.Get<MyScope>());
        }

        private static OpenIddictAtsScopeStore<MyScope> CreateStore()
            => new Mock<OpenIddictAtsScopeStore<MyScope>>(
                Mock.Of<IOpenIddictAtsContext>(),
                Mock.Of<IOptionsMonitor<OpenIddictAtsOptions>>()).Object;

        public class CustomScope { }

        public class MyScope : OpenIddictAtsScope { }
    }
}
