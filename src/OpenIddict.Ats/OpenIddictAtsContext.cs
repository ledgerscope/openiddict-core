/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;
using SR = OpenIddict.Abstractions.OpenIddictResources;

namespace OpenIddict.Ats
{
    /// <inheritdoc/>
    public class OpenIddictAtsContext : IOpenIddictAtsContext
    {
        private readonly IOptionsMonitor<OpenIddictAtsOptions> _options;
        private readonly IServiceProvider _provider;

        public OpenIddictAtsContext(
            IOptionsMonitor<OpenIddictAtsOptions> options,
            IServiceProvider provider)
        {
            _options = options;
            _provider = provider;
        }

        /// <inheritdoc/>
        public ValueTask<CloudTable> GetDatabaseAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<CloudTable>(Task.FromCanceled<CloudTable>(cancellationToken));
            }

            var database = _options.CurrentValue.Database;
            if (database is null)
            {
                database = _provider.GetService<CloudTable>();
            }

            if (database is null)
            {
                return new ValueTask<CloudTable>(Task.FromException<CloudTable>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0262))));
            }

            return new ValueTask<CloudTable>(database);
        }
    }
}
