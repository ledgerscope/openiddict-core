/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Ats.Models;
using Xunit;
using SR = OpenIddict.Abstractions.OpenIddictResources;

namespace OpenIddict.Ats.Tests
{
    public class OpenIddictAtsContextTests
    {
        [Fact]
        public async Task GetDatabaseAsync_ThrowsAnExceptionForCanceledToken()
        {
            // Arrange
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var options = Mock.Of<IOptionsMonitor<OpenIddictAtsOptions>>();
            var token = new CancellationToken(canceled: true);

            var context = new OpenIddictAtsContext(options, provider);

            // Act and assert
            var exception = await Assert.ThrowsAsync<TaskCanceledException>(async delegate
            {
                await context.GetTableClientAsync(token);
            });

            Assert.Equal(token, exception.CancellationToken);
        }

        [Fact]
        public async Task GetDatabaseAsync_PrefersDatabaseRegisteredInOptionsToDatabaseRegisteredInDependencyInjectionContainer()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddSingleton(Mock.Of<ICloudTableClient>());

            var provider = services.BuildServiceProvider();

            var database = Mock.Of<ICloudTableClient>();
            var options = Mock.Of<IOptionsMonitor<OpenIddictAtsOptions>>(
                mock => mock.CurrentValue == new OpenIddictAtsOptions
                {
                    Database = database
                });

            var context = new OpenIddictAtsContext(options, provider);

            // Act and assert
            Assert.Same(database, await context.GetTableClientAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetDatabaseAsync_ThrowsAnExceptionWhenDatabaseCannotBeFound()
        {
            // Arrange
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            var options = Mock.Of<IOptionsMonitor<OpenIddictAtsOptions>>(
                mock => mock.CurrentValue == new OpenIddictAtsOptions
                {
                    Database = null
                });

            var context = new OpenIddictAtsContext(options, provider);

            // Act and assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async delegate
            {
                await context.GetTableClientAsync(CancellationToken.None);
            });

            Assert.Equal(SR.GetResourceString(SR.ID0262), exception.Message);
        }

        [Fact]
        public async Task GetDatabaseAsync_UsesDatabaseRegisteredInDependencyInjectionContainer()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Mock.Of<ICloudTableClient>());

            var database = Mock.Of<ICloudTableClient>();
            services.AddSingleton(database);

            var provider = services.BuildServiceProvider();

            var options = Mock.Of<IOptionsMonitor<OpenIddictAtsOptions>>(
                mock => mock.CurrentValue == new OpenIddictAtsOptions
                {
                    Database = null
                });

            var context = new OpenIddictAtsContext(options, provider);

            // Act and assert
            Assert.Same(database, await context.GetTableClientAsync(CancellationToken.None));
        }
    }
}
