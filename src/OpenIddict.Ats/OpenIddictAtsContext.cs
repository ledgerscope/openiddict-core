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
using Microsoft.Azure.Cosmos.Table;
using SR = OpenIddict.Abstractions.OpenIddictResources;
using OpenIddict.Ats.Models;

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
        public ValueTask<ICloudTableClient> GetTableClientAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<ICloudTableClient>(Task.FromCanceled<ICloudTableClient>(cancellationToken));
            }

            //TODO KAR
            //    CloudStorageAccount account = GetStorageAccount();

            //    var tableClient = account.CreateCloudTableClient();
            //    tableClient.DefaultRequestOptions = TableRequestOptions;

            //    return tableClient;

            var database = _options.CurrentValue.Database;
            if (database is null)
            {
                database = _provider.GetService<ICloudTableClient>();
            }

            if (database is null)
            {
                return new ValueTask<ICloudTableClient>(Task.FromException<ICloudTableClient>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0262))));
            }

            return new ValueTask<ICloudTableClient>(database);
        }

        //public CloudStorageAccount GetStorageAccount()
        //{
        //    if (this.ConnectionString != null)
        //    {
        //        return CloudStorageAccount.Parse(this.ConnectionString);
        //    }
        //    else
        //    {
        //        string configConnString = "_applicationConfig.GetConnectionString(ConnectionStringKeys.Azure)"; //TODO KAR
        //        return CloudStorageAccount.Parse(configConnString);
        //    }
        //}
    }
}
