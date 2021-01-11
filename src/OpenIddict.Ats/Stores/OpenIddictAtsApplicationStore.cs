/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using OpenIddict.Abstractions;
using OpenIddict.Ats.Models;
using Microsoft.WindowsAzure.Storage.Table.Queryable;
using SR = OpenIddict.Abstractions.OpenIddictResources;
using Ats.Driver;

namespace OpenIddict.Ats
{
    /// <summary>
    /// Provides methods allowing to manage the applications stored in a database.
    /// </summary>
    /// <typeparam name="TApplication">The type of the Application entity.</typeparam>
    public class OpenIddictAtsApplicationStore<TApplication> : IOpenIddictApplicationStore<TApplication>
        where TApplication : OpenIddictAtsApplication, new()
    {
        public OpenIddictAtsApplicationStore(
            IOpenIddictAtsContext context,
            IOptionsMonitor<OpenIddictAtsOptions> options)
        {
            Context = context;
            Options = options;

            ConnectionString = "_applicationConfig.GetConnectionString(ConnectionStringKeys.Azure)"; //TODO KAR
        }

        /// <summary>
        /// Gets the database context associated with the current store.
        /// </summary>
        protected IOpenIddictAtsContext Context { get; }

        /// <summary>
        /// Gets the options associated with the current store.
        /// </summary>
        protected IOptionsMonitor<OpenIddictAtsOptions> Options { get; }

        public string ConnectionString { get; set; }

        public CloudStorageAccount GetStorageAccount()
        {
            if (this.ConnectionString != null)
            {
                return CloudStorageAccount.Parse(this.ConnectionString);
            }
            else
            {
                string configConnString = "_applicationConfig.GetConnectionString(ConnectionStringKeys.Azure)"; //TODO KAR
                return CloudStorageAccount.Parse(configConnString);
            }
        }

        public TableRequestOptions TableRequestOptions { get; } = new TableRequestOptions()
        {
            RetryPolicy = new ExponentialRetry(),
            MaximumExecutionTime = TimeSpan.FromMinutes(10),
            ServerTimeout = TimeSpan.FromMinutes(1)
        };

        public CloudTableClient GetCloudTableClient()
        {
            CloudStorageAccount account = GetStorageAccount();

            var tableClient = account.CreateCloudTableClient();
            tableClient.DefaultRequestOptions = TableRequestOptions;

            return tableClient;
        }

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync(CancellationToken cancellationToken)
        {
            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ApplicationsCollectionName);

            var query = new TableQuery<DynamicTableEntity>().Select(new[] { TableConstants.PartitionKey });

            return await CountLongAsync(ct, query, cancellationToken);
        }

        private static async ValueTask<long> CountLongAsync(CloudTable ct, TableQuery<DynamicTableEntity> query, CancellationToken cancellationToken)
        {
            long counter = 0;
            var continuationToken = default(TableContinuationToken);

            do
            {
                var results = await ct.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);
                continuationToken = results.ContinuationToken;
                foreach (var record in results)
                {
                    counter++;
                }
            } while (continuationToken != null);

            return counter;
        }

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync<TResult>(
            Func<IQueryable<TApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            throw new NotImplementedException(); //TODO KAR
        }

        /// <inheritdoc/>
        public virtual async ValueTask CreateAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ApplicationsCollectionName);

            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(application);

            await ct.ExecuteAsync(insertOrMergeOperation, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ApplicationsCollectionName);

            var idFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsApplication.Id), QueryComparisons.Equal, application.Id);
            var tokenFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsApplication.ConcurrencyToken), QueryComparisons.Equal, application.ConcurrencyToken);

            var filter = TableQuery.CombineFilters(idFilter,
                TableOperators.And,
                tokenFilter);

            var applicationDeleteQuery = new TableQuery<OpenIddictAtsApplication>().Where(filter)
                .Select(new string[] { TableConstants.PartitionKey, TableConstants.RowKey });

            await deleteAsync(ct, applicationDeleteQuery);

            //if (!result.Result) //TODO KAR if deleteAsync returns 0 deleted records
            //{
            //    throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0239));
            //}

            // Delete the authorizations associated with the application.
            ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

            var authDeleteQuery = new TableQuery<OpenIddictAtsAuthorization>().Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsApplication.Id), QueryComparisons.Equal, application.Id))
                .Select(new string[] { TableConstants.PartitionKey, TableConstants.RowKey });

            await deleteAsync(ct, authDeleteQuery);

            // Delete the tokens associated with the application.
            ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

            var tokenDeleteQuery = new TableQuery<OpenIddictAtsToken>().Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsApplication.Id), QueryComparisons.Equal, application.Id))
                .Select(new string[] { TableConstants.PartitionKey, TableConstants.RowKey });

            await deleteAsync(ct, tokenDeleteQuery);
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ApplicationsCollectionName);

            var query = ct.CreateQuery<TApplication>()
                .Take(1)
                .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsApplication.Id), QueryComparisons.Equal, identifier))
                .AsTableQuery();

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ApplicationsCollectionName);

            var query = ct.CreateQuery<TApplication>()
                .Take(1)
                .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsApplication.ClientId), QueryComparisons.Equal, identifier))
                .AsTableQuery();

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(
            string address, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0143), nameof(address));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TApplication> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                //TODO KAR EF version
                //var applications = (from application in Applications
                //                    where application.PostLogoutRedirectUris!.Contains(address)
                //                    select application).AsAsyncEnumerable(cancellationToken);

                //await foreach (var application in applications)
                //{
                //    var addresses = await GetPostLogoutRedirectUrisAsync(application, cancellationToken);
                //    if (addresses.Contains(address, StringComparer.Ordinal))
                //    {
                //        yield return application;
                //    }
                //}

                var database = await Context.GetDatabaseAsync(cancellationToken);
                var collection = database.GetCollection<TApplication>(Options.CurrentValue.ApplicationsCollectionName);

                await foreach (var application in collection.Find(application =>
                    application.PostLogoutRedirectUris.Contains(address)).ToAsyncEnumerable(cancellationToken))
                {
                    yield return application;
                }
            }
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TApplication> FindByRedirectUriAsync(
            string address, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0143), nameof(address));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TApplication> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                //TODO KAR EF version
                //var applications = (from application in Applications
                //                    where application.RedirectUris!.Contains(address)
                //                    select application).AsAsyncEnumerable(cancellationToken);

                //await foreach (var application in applications)
                //{
                //    var addresses = await GetRedirectUrisAsync(application, cancellationToken);
                //    if (addresses.Contains(address, StringComparer.Ordinal))
                //    {
                //        yield return application;
                //    }
                //}

                var database = await Context.GetDatabaseAsync(cancellationToken);
                var collection = database.GetCollection<TApplication>(Options.CurrentValue.ApplicationsCollectionName);

                await foreach (var application in collection.Find(application =>
                    application.RedirectUris.Contains(address)).ToAsyncEnumerable(cancellationToken))
                {
                    yield return application;
                }
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TResult> GetAsync<TState, TResult>(
            Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            throw new NotImplementedException();

            //TODO KAR
            //var tableClient = GetCloudTableClient();
            //CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ApplicationsCollectionName);

            //var test = ct.CreateQuery<TApplication>()
            //    .AsTableQuery();

            //return await test.FirstOrDefaultAsync(cancellationToken);

            //mongo
            //return await ((TResult) query(collection.AsQueryable(), state)).FirstOrDefaultAsync(cancellationToken);

            //TODO KAR EF does:
            //return await query(Applications, state).FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetClientIdAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.ClientId);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetClientSecretAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.ClientSecret);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetClientTypeAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.Type);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetConsentTypeAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.ConsentType);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetDisplayNameAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.DisplayName);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }
            //TODO KAR another EF caching one

            //TODO KAR this is what mongodb has. the model has:
            // public virtual IReadOnlyDictionary<CultureInfo, string> DisplayNames { get; set; }
            //= ImmutableDictionary.Create<CultureInfo, string>();

            if (application.DisplayNames is null || application.DisplayNames.Count == 0)
            {
                return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary.Create<CultureInfo, string>());
            }

            return new ValueTask<ImmutableDictionary<CultureInfo, string>>(application.DisplayNames.ToImmutableDictionary());
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetIdAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            return new ValueTask<string?>(application.Id?.ToString()); //TODO KAR is this ok? Or add the Key stuff at top of the class in other file and add Convert method below as per EF?

            //EF has:
            //return new ValueTask<string?>(ConvertIdentifierToString(application.Id));

        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetPermissionsAsync(
            TApplication application, CancellationToken cancellationToken)
        {
            //TODO KAR another EF caching one

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.Permissions is null || application.Permissions.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(application.Permissions.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(
            TApplication application, CancellationToken cancellationToken)
        {
            //TODO KAR another EF caching one
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.PostLogoutRedirectUris is null || application.PostLogoutRedirectUris.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(application.PostLogoutRedirectUris.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TApplication application, CancellationToken cancellationToken)
        {
            //EF used caching... what to do? //TODO KAR

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (string.IsNullOrEmpty(application.Properties))
            {
                return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
            }

            using var document = JsonDocument.Parse(application.Properties.ToJson());
            var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                builder[property.Name] = property.Value.Clone();
            }

            return new ValueTask<ImmutableDictionary<string, JsonElement>>(builder.ToImmutable());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(
            TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.RedirectUris is null || application.RedirectUris.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(application.RedirectUris.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetRequirementsAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.Requirements is null || application.Requirements.Count == 0)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            return new ValueTask<ImmutableArray<string>>(application.Requirements.ToImmutableArray());
        }

        /// <inheritdoc/>
        public virtual ValueTask<TApplication> InstantiateAsync(CancellationToken cancellationToken)
        {
            try
            {
                return new ValueTask<TApplication>(Activator.CreateInstance<TApplication>());
            }

            catch (MemberAccessException exception)
            {
                return new ValueTask<TApplication>(Task.FromException<TApplication>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0240), exception)));
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TApplication> ListAsync(
            int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ApplicationsCollectionName);

            long counter = 0;
            var continuationToken = default(TableContinuationToken);

            var query = new TableQuery<TApplication>();

            var endRecord = count.HasValue ? count.Value + offset.GetValueOrDefault() : (int?)null;

            do
            {
                var results = await ct.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);
                continuationToken = results.ContinuationToken;
                foreach (var record in results)
                {
                    if (offset.GetValueOrDefault(-1) < count)
                    {
                        if (count < endRecord.GetValueOrDefault(int.MaxValue))
                        {
                            yield return record;
                        }
                    }
                    counter++;
                }
            } while (continuationToken != null);
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
            Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ApplicationsCollectionName);

            //var query = new TableQuery<DynamicTableEntity>().Select(new[] { TableConstants.PartitionKey });

            //return await CountLongAsync(ct, query, cancellationToken);

            ///////////////
            //mongo does
            //return ExecuteAsync(cancellationToken);

            //async IAsyncEnumerable<TResult> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            //{
            //    var database = await Context.GetDatabaseAsync(cancellationToken);
            //    var collection = database.GetCollection<TApplication>(Options.CurrentValue.ApplicationsCollectionName);

            //    await foreach (var element in query(collection.AsQueryable(), state).ToAsyncEnumerable(cancellationToken))
            //    {
            //        yield return element;
            //    }
            //}


            /////////////////////////
            //EF does this
            //private DbSet<TApplication> Applications => Context.Set<TApplication>();

            return query(Applications, state).AsAsyncEnumerable(cancellationToken);
        }
        //mongo ver below, EF above
        //public virtual IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        //    Func<IQueryable<TApplication>, TState, IQueryable<TResult>> query,
        //    TState state, CancellationToken cancellationToken)
        //{
        //    if (query is null)
        //    {
        //        throw new ArgumentNullException(nameof(query));
        //    }

        //    return ExecuteAsync(cancellationToken);

        //    async IAsyncEnumerable<TResult> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        //    {
        //        var database = await Context.GetDatabaseAsync(cancellationToken);
        //        var collection = database.GetCollection<TApplication>(Options.CurrentValue.ApplicationsCollectionName);

        //        await foreach (var element in query(collection.AsQueryable(), state).ToAsyncEnumerable(cancellationToken))
        //        {
        //            yield return element;
        //        }
        //    }
        //}

        /// <inheritdoc/>
        public virtual ValueTask SetClientIdAsync(TApplication application,
            string? identifier, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.ClientId = identifier;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetClientSecretAsync(TApplication application,
            string? secret, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.ClientSecret = secret;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetClientTypeAsync(TApplication application,
            string? type, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.Type = type;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetConsentTypeAsync(TApplication application,
            string? type, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.ConsentType = type;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDisplayNameAsync(TApplication application,
            string? name, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.DisplayName = name;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDisplayNamesAsync(TApplication application,
            ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (names is null || names.IsEmpty)
            {
                application.DisplayNames = null;

                return default;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false
            });

            writer.WriteStartObject();

            foreach (var name in names)
            {
                writer.WritePropertyName(name.Key.Name);
                writer.WriteStringValue(name.Value);
            }

            writer.WriteEndObject();
            writer.Flush();

            application.DisplayNames = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }
        //mongo ver below, EF above
        //public virtual ValueTask SetDisplayNamesAsync(TApplication application,
        //    ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
        //{
        //    if (application is null)
        //    {
        //        throw new ArgumentNullException(nameof(application));
        //    }

        //    application.DisplayNames = names;

        //    return default;
        //}

        /// <inheritdoc/>
        public virtual ValueTask SetPermissionsAsync(TApplication application, ImmutableArray<string> permissions, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (permissions.IsDefaultOrEmpty)
            {
                application.Permissions = null;

                return default;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false
            });

            writer.WriteStartArray();

            foreach (var permission in permissions)
            {
                writer.WriteStringValue(permission);
            }

            writer.WriteEndArray();
            writer.Flush();

            application.Permissions = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }
        //mongo ver below, EF above
        //public virtual ValueTask SetPermissionsAsync(TApplication application, ImmutableArray<string> permissions, CancellationToken cancellationToken)
        //{
        //    if (application is null)
        //    {
        //        throw new ArgumentNullException(nameof(application));
        //    }

        //    if (permissions.IsDefaultOrEmpty)
        //    {
        //        application.Permissions = ImmutableList.Create<string>();

        //        return default;
        //    }

        //    application.Permissions = permissions.ToImmutableList();

        //    return default;
        //}

        /// <inheritdoc/>
        public virtual ValueTask SetPostLogoutRedirectUrisAsync(TApplication application,
            ImmutableArray<string> addresses, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (addresses.IsDefaultOrEmpty)
            {
                application.PostLogoutRedirectUris = null;

                return default;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false
            });

            writer.WriteStartArray();

            foreach (var address in addresses)
            {
                writer.WriteStringValue(address);
            }

            writer.WriteEndArray();
            writer.Flush();

            application.PostLogoutRedirectUris = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }
        //mongo ver below, EF above
        //public virtual ValueTask SetPostLogoutRedirectUrisAsync(TApplication application,
        //    ImmutableArray<string> addresses, CancellationToken cancellationToken)
        //{
        //    if (application is null)
        //    {
        //        throw new ArgumentNullException(nameof(application));
        //    }

        //    if (addresses.IsDefaultOrEmpty)
        //    {
        //        application.PostLogoutRedirectUris = ImmutableList.Create<string>();

        //        return default;
        //    }

        //    application.PostLogoutRedirectUris = addresses.ToImmutableList();

        //    return default;
        //}

        /// <inheritdoc/>
        public virtual ValueTask SetPropertiesAsync(TApplication application,
            ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (properties is null || properties.IsEmpty)
            {
                application.Properties = null;

                return default;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false
            });

            writer.WriteStartObject();

            foreach (var property in properties)
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.Flush();

            application.Properties = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }
        //mongo ver below, EF above
        //public virtual ValueTask SetPropertiesAsync(TApplication application,
        //    ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
        //{
        //    if (application is null)
        //    {
        //        throw new ArgumentNullException(nameof(application));
        //    }

        //    if (properties is null || properties.IsEmpty)
        //    {
        //        application.Properties = null;

        //        return default;
        //    }

        //    using var stream = new MemoryStream();
        //    using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        //    {
        //        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        //        Indented = false
        //    });

        //    writer.WriteStartObject();

        //    foreach (var property in properties)
        //    {
        //        writer.WritePropertyName(property.Key);
        //        property.Value.WriteTo(writer);
        //    }

        //    writer.WriteEndObject();
        //    writer.Flush();

        //    application.Properties = BsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));

        //    return default;
        //}

        /// <inheritdoc/>
        public virtual ValueTask SetRedirectUrisAsync(TApplication application,
            ImmutableArray<string> addresses, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (addresses.IsDefaultOrEmpty)
            {
                application.RedirectUris = null;

                return default;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false
            });

            writer.WriteStartArray();

            foreach (var address in addresses)
            {
                writer.WriteStringValue(address);
            }

            writer.WriteEndArray();
            writer.Flush();

            application.RedirectUris = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }
        //mongo ver below, EF above
        //public virtual ValueTask SetRedirectUrisAsync(TApplication application,
        //    ImmutableArray<string> addresses, CancellationToken cancellationToken)
        //{
        //    if (application is null)
        //    {
        //        throw new ArgumentNullException(nameof(application));
        //    }

        //    if (addresses.IsDefaultOrEmpty)
        //    {
        //        application.RedirectUris = ImmutableList.Create<string>();

        //        return default;
        //    }

        //    application.RedirectUris = addresses.ToImmutableList();

        //    return default;
        //}

        /// <inheritdoc/>
        public virtual ValueTask SetRequirementsAsync(TApplication application, ImmutableArray<string> requirements, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (requirements.IsDefaultOrEmpty)
            {
                application.Requirements = null;

                return default;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false
            });

            writer.WriteStartArray();

            foreach (var requirement in requirements)
            {
                writer.WriteStringValue(requirement);
            }

            writer.WriteEndArray();
            writer.Flush();

            application.Requirements = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }
        //mongo ver below, EF above
        //public virtual ValueTask SetRequirementsAsync(TApplication application,
        //    ImmutableArray<string> requirements, CancellationToken cancellationToken)
        //{
        //    if (application is null)
        //    {
        //        throw new ArgumentNullException(nameof(application));
        //    }

        //    if (requirements.IsDefaultOrEmpty)
        //    {
        //        application.Requirements = ImmutableList.Create<string>();

        //        return default;
        //    }

        //    application.Requirements = requirements.ToImmutableList();

        //    return default;
        //}

        /// <inheritdoc/>
        public virtual async ValueTask UpdateAsync(TApplication application, CancellationToken cancellationToken)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            ////////////////////
            //mongo
            // Generate a new concurrency token and attach it
            // to the application before persisting the changes.
            //var timestamp = application.ConcurrencyToken;
            //application.ConcurrencyToken = Guid.NewGuid().ToString();

            //var database = await Context.GetDatabaseAsync(cancellationToken);
            //var collection = database.GetCollection<TApplication>(Options.CurrentValue.ApplicationsCollectionName);

            //if ((await collection.ReplaceOneAsync(entity =>
            //    entity.Id == application.Id &&
            //    entity.ConcurrencyToken == timestamp, application, null as ReplaceOptions, cancellationToken)).MatchedCount == 0)
            //{
            //    throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0239));
            //}
            ///////////////////
            ///EF
            Applications.Attach(application);

            // Generate a new concurrency token and attach it
            // to the application before persisting the changes.
            application.ConcurrencyToken = Guid.NewGuid().ToString();

            Context.Entry(application).State = EntityState.Modified;

            try
            {
                await Context.SaveChangesAsync(cancellationToken);
            }

            catch (DbUpdateConcurrencyException exception)
            {
                // Reset the state of the entity to prevents future calls to SaveChangesAsync() from failing.
                Context.Entry(application).State = EntityState.Unchanged;

                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0239), exception);
            }
        }

        private static async Task deleteAsync<T>(CloudTable ct, TableQuery<T> deleteQuery) where T : ITableEntity, new()
        {
            TableContinuationToken? continuationToken = null;

            do
            {
                var tableQueryResult = ct.ExecuteQuerySegmentedAsync(deleteQuery, continuationToken);

                continuationToken = tableQueryResult.Result.ContinuationToken;

                // Split into chunks of 100 for batching
                List<List<T>> rowsChunked = tableQueryResult.Result.Select((x, index) => new { Index = index, Value = x })
                    .Where(x => x.Value != null)
                    .GroupBy(x => x.Index / 100)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();

                // Delete each chunk of 100 in a batch
                foreach (List<T> rows in rowsChunked)
                {
                    TableBatchOperation tableBatchOperation = new TableBatchOperation();
                    rows.ForEach(x => tableBatchOperation.Add(TableOperation.Delete(x)));

                    await ct.ExecuteBatchAsync(tableBatchOperation);
                }
            }
            while (continuationToken != null);
        }
    }
}