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
using Ats.Driver;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using Microsoft.WindowsAzure.Storage.Table.Queryable;
using OpenIddict.Abstractions;
using OpenIddict.Ats.Models;
using SR = OpenIddict.Abstractions.OpenIddictResources;

namespace OpenIddict.Ats
{
    /// <summary>
    /// Provides methods allowing to manage the scopes stored in a database.
    /// </summary>
    /// <typeparam name="TScope">The type of the Scope entity.</typeparam>
    public class OpenIddictAtsScopeStore<TScope> : IOpenIddictScopeStore<TScope>
        where TScope : OpenIddictAtsScope, new()
    {
        public OpenIddictAtsScopeStore(
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
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ScopesCollectionName);

            var query = new TableQuery<DynamicTableEntity>().Select(new[] { TableConstants.PartitionKey });

            return await OpenIddictAtsHelpers.CountLongAsync(ct, query, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync<TResult>(
            Func<IQueryable<TScope>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            throw new NotImplementedException(); //TODO KAR<>
        }

        /// <inheritdoc/>
        public virtual async ValueTask CreateAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ScopesCollectionName);

            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(scope);

            await ct.ExecuteAsync(insertOrMergeOperation, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ScopesCollectionName);

            var idFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsScope.Id), QueryComparisons.Equal, scope.Id);
            var tokenFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsScope.ConcurrencyToken), QueryComparisons.Equal, scope.ConcurrencyToken);

            var filter = TableQuery.CombineFilters(idFilter,
                TableOperators.And,
                tokenFilter);

            var tokenDeleteQuery = new TableQuery<OpenIddictAtsScope>().Where(filter)
                .Select(new string[] { TableConstants.PartitionKey, TableConstants.RowKey });

            try
            {
                await OpenIddictAtsHelpers.DeleteAsync(ct, tokenDeleteQuery);
            }
            catch (StorageException exception)
            {
                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0247), exception);
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TScope?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ScopesCollectionName);

            var query = ct.CreateQuery<TScope>()
                .Take(1)
                .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsScope.Id), QueryComparisons.Equal, identifier))
                .AsTableQuery();

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TScope?> FindByNameAsync(string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0202), nameof(name));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ScopesCollectionName);

            var query = ct.CreateQuery<TScope>()
                .Take(1)
                .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsScope.Name), QueryComparisons.Equal, name))
                .AsTableQuery();

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TScope> FindByNamesAsync(ImmutableArray<string> names, CancellationToken cancellationToken)
        {
            if (names.Any(name => string.IsNullOrEmpty(name)))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0203), nameof(names));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TScope> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var database = await Context.GetDatabaseAsync(cancellationToken);
                var collection = database.GetCollection<TScope>(Options.CurrentValue.ScopesCollectionName);
                //TODO KAR
                // Note: Enumerable.Contains() is deliberately used without the extension method syntax to ensure 
                // ImmutableArray.Contains() (which is not fully supported by MongoDB) is not used instead.
                await foreach (var scope in collection.Find(scope => Enumerable.Contains(names, scope.Name)).ToAsyncEnumerable(cancellationToken))
                {
                    yield return scope;
                }
            }
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TScope> FindByResourceAsync(string resource, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0062), nameof(resource));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TScope> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var database = await Context.GetDatabaseAsync(cancellationToken);
                var collection = database.GetCollection<TScope>(Options.CurrentValue.ScopesCollectionName);

                await foreach (var scope in collection.Find(scope => scope.Resources.Contains(resource)).ToAsyncEnumerable(cancellationToken))
                {
                    yield return scope;
                }
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TResult> GetAsync<TState, TResult>(
            Func<IQueryable<TScope>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var database = await Context.GetDatabaseAsync(cancellationToken);
            var collection = database.GetCollection<TScope>(Options.CurrentValue.ScopesCollectionName);

            return await ((TResult) query(collection.AsQueryable(), state)).FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetDescriptionAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return new ValueTask<string?>(scope.Description);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (string.IsNullOrEmpty(scope.Descriptions))
            {
                return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary.Create<CultureInfo, string>());
            }

            using var document = JsonDocument.Parse(scope.Properties);
            var builder = ImmutableDictionary.CreateBuilder<CultureInfo, string>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.GetString();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                builder[CultureInfo.GetCultureInfo(property.Name)] = value;
            }

            return new ValueTask<ImmutableDictionary<CultureInfo, string>>(builder.ToImmutable());
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetDisplayNameAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return new ValueTask<string?>(scope.DisplayName);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (string.IsNullOrEmpty(scope.DisplayNames))
            {
                return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary.Create<CultureInfo, string>());
            }

            using var document = JsonDocument.Parse(scope.DisplayNames);
            var builder = ImmutableDictionary.CreateBuilder<CultureInfo, string>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.GetString();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                builder[CultureInfo.GetCultureInfo(property.Name)] = value;
            }

            return new ValueTask<ImmutableDictionary<CultureInfo, string>>(builder.ToImmutable());
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetIdAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return new ValueTask<string?>(scope.Id?.ToString());
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetNameAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return new ValueTask<string?>(scope.Name);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (scope.Properties is null)
            {
                return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
            }

            using var document = JsonDocument.Parse(scope.Properties);
            var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                builder[property.Name] = property.Value.Clone();
            }

            return new ValueTask<ImmutableDictionary<string, JsonElement>>(builder.ToImmutable());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetResourcesAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (scope.Resources is null)
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            using var document = JsonDocument.Parse(scope.Resources);
            var builder = ImmutableArray.CreateBuilder<string>(document.RootElement.GetArrayLength());


            foreach (var element in document.RootElement.EnumerateArray())
            {
                var value = element.GetString();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                builder.Add(value);
            }

            return new ValueTask<ImmutableArray<string>>(builder.ToImmutable());
        }

        /// <inheritdoc/>
        public virtual ValueTask<TScope> InstantiateAsync(CancellationToken cancellationToken)
        {
            try
            {
                return new ValueTask<TScope>(Activator.CreateInstance<TScope>());
            }

            catch (MemberAccessException exception)
            {
                return new ValueTask<TScope>(Task.FromException<TScope>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0246), exception)));
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TScope> ListAsync(
            int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ScopesCollectionName);

            long counter = 0;
            var continuationToken = default(TableContinuationToken);

            var query = new TableQuery<TScope>();

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
            Func<IQueryable<TScope>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TResult> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                //TODO KAR
                //ef
                //return query(Scopes, state).AsAsyncEnumerable(cancellationToken);

                //mongo
                //var database = await Context.GetDatabaseAsync(cancellationToken);
                //var collection = database.GetCollection<TScope>(Options.CurrentValue.ScopesCollectionName);

                //await foreach (var element in query(collection.AsQueryable(), state).ToAsyncEnumerable(cancellationToken))
                //{
                //    yield return element;
                //}
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDescriptionAsync(TScope scope, string? description, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            scope.Description = description;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDescriptionsAsync(TScope scope,
            ImmutableDictionary<CultureInfo, string> descriptions, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (descriptions is null || descriptions.IsEmpty)
            {
                scope.Descriptions = null;

                return default;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false
            });

            writer.WriteStartObject();

            foreach (var description in descriptions)
            {
                writer.WritePropertyName(description.Key.Name);
                writer.WriteStringValue(description.Value);
            }

            writer.WriteEndObject();
            writer.Flush();

            scope.Descriptions = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDisplayNamesAsync(TScope scope,
            ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (names is null || names.IsEmpty)
            {
                scope.DisplayNames = null;

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

            scope.DisplayNames = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetDisplayNameAsync(TScope scope, string? name, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            scope.DisplayName = name;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetNameAsync(TScope scope, string? name, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            scope.Name = name;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetPropertiesAsync(TScope scope,
            ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (properties is null || properties.IsEmpty)
            {
                scope.Properties = null;

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

            scope.Properties = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetResourcesAsync(TScope scope, ImmutableArray<string> resources, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (resources.IsDefaultOrEmpty)
            {
                scope.Resources = null;

                return default;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false
            });

            writer.WriteStartArray();

            foreach (var resource in resources)
            {
                writer.WriteStringValue(resource);
            }

            writer.WriteEndArray();
            writer.Flush();

            scope.Resources = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }

        /// <inheritdoc/>
        public virtual async ValueTask UpdateAsync(TScope scope, CancellationToken cancellationToken)
        {
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ScopesCollectionName);

            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(scope);

            try
            {
                await ct.ExecuteAsync(insertOrMergeOperation);
            }
            catch (StorageException exception)
            {
                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241), exception);
            }
        }
    }
}