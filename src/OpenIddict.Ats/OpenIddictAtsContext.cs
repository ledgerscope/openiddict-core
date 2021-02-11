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
        public ValueTask<CloudTableClient> GetTableClientAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<CloudTableClient>(Task.FromCanceled<CloudTableClient>(cancellationToken));
            }

            //TODO KAR
            //    CloudStorageAccount account = GetStorageAccount();

            //    var tableClient = account.CreateCloudTableClient();
            //    tableClient.DefaultRequestOptions = TableRequestOptions;

            //    return tableClient;

            var database = _options.CurrentValue.Database;
            if (database is null)
            {
                database = _provider.GetService<CloudTableClient>();
                //line above would call this in main app?
                //services.AddSingleton(new MongoClient("mongodb://localhost:27017").GetDatabase("openiddict"));
            }

            if (database is null)
            {
                return new ValueTask<CloudTableClient>(Task.FromException<CloudTableClient>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0262))));
            }

            return new ValueTask<CloudTableClient>(database);
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
