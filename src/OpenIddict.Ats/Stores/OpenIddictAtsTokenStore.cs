/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using static OpenIddict.Abstractions.OpenIddictConstants;
using SR = OpenIddict.Abstractions.OpenIddictResources;

namespace OpenIddict.Ats
{
    /// <summary>
    /// Provides methods allowing to manage the tokens stored in a database.
    /// </summary>
    /// <typeparam name="TToken">The type of the Token entity.</typeparam>
    public class OpenIddictAtsTokenStore<TToken> : IOpenIddictTokenStore<TToken>
        where TToken : OpenIddictAtsToken, new()
    {
        public OpenIddictAtsTokenStore(
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
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

            var query = new TableQuery<DynamicTableEntity>().Select(new[] { TableConstants.PartitionKey });

            return await OpenIddictAtsHelpers.CountLongAsync(ct, query, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync<TResult>(
            Func<IQueryable<TToken>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            throw new NotImplementedException(); //TODO KAR

            //var database = await Context.GetDatabaseAsync(cancellationToken);
            //var collection = database.GetCollection<TToken>(Options.CurrentValue.TokensCollectionName);

            //return await ((TToken) query(collection.AsQueryable())).LongCountAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask CreateAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(token);

            await ct.ExecuteAsync(insertOrMergeOperation, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

            var idFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.Id), QueryComparisons.Equal, token.Id);
            var tokenFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.ConcurrencyToken), QueryComparisons.Equal, token.ConcurrencyToken);

            var filter = TableQuery.CombineFilters(idFilter,
                TableOperators.And,
                tokenFilter);

            var tokenDeleteQuery = new TableQuery<OpenIddictAtsToken>().Where(filter)
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
        public virtual IAsyncEnumerable<TToken> FindAsync(string subject,
            string client, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TToken> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = GetCloudTableClient();
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

                var clientFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.ApplicationId), QueryComparisons.Equal, client);
                var subjectFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.Subject), QueryComparisons.Equal, subject);

                var filter = TableQuery.CombineFilters(clientFilter,
                    TableOperators.And,
                    subjectFilter);

                var query = ct.CreateQuery<TToken>()
                    .Where(filter)
                    .AsTableQuery();

                var continuationToken = default(TableContinuationToken);

                do
                {
                    var results = await ct.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);

                    continuationToken = results.ContinuationToken;

                    foreach (var token in results)
                    {
                        yield return token;
                    }
                } while (continuationToken != null);
            }
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TToken> FindAsync(
            string subject, string client,
            string status, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0199), nameof(status));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TToken> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = GetCloudTableClient();
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

                var clientFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.ApplicationId), QueryComparisons.Equal, client);
                var subjectFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.Subject), QueryComparisons.Equal, subject);
                var statusFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.Status), QueryComparisons.Equal, status);

                var filter = TableQuery.CombineFilters(clientFilter,
                    TableOperators.And,
                    subjectFilter);
                //TODO KAR how to add status filter to where clause?

                var query = ct.CreateQuery<TToken>()
                    .Where(filter)
                    .AsTableQuery();

                var continuationToken = default(TableContinuationToken);

                do
                {
                    var results = await ct.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);

                    continuationToken = results.ContinuationToken;

                    foreach (var token in results)
                    {
                        yield return token;
                    }
                } while (continuationToken != null);
            }
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TToken> FindAsync(
            string subject, string client,
            string status, string type, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0124), nameof(client));
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0199), nameof(status));
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0200), nameof(type));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TToken> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = GetCloudTableClient();
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

                var clientFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.ApplicationId), QueryComparisons.Equal, client);
                var subjectFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.Subject), QueryComparisons.Equal, subject);
                var statusFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.Status), QueryComparisons.Equal, status);
                var typeFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.Type), QueryComparisons.Equal, type);

                var filter = TableQuery.CombineFilters(clientFilter,
                    TableOperators.And,
                    subjectFilter);
                //TODO KAR how to add status and type filter to where clause?

                var query = ct.CreateQuery<TToken>()
                    .Where(filter)
                    .AsTableQuery();

                var continuationToken = default(TableContinuationToken);

                do
                {
                    var results = await ct.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);

                    continuationToken = results.ContinuationToken;

                    foreach (var token in results)
                    {
                        yield return token;
                    }
                } while (continuationToken != null);
            }
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TToken> FindByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TToken> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = GetCloudTableClient();
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

                var query = ct.CreateQuery<TToken>()
                    .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.ApplicationId), QueryComparisons.Equal, identifier))
                    .AsTableQuery();

                var continuationToken = default(TableContinuationToken);

                do
                {
                    var results = await ct.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);
                    
                    continuationToken = results.ContinuationToken;

                    foreach (var token in results)
                    {
                        yield return token;
                    }
                } while (continuationToken != null);
            }
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TToken> FindByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TToken> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = GetCloudTableClient();
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

                var query = ct.CreateQuery<TToken>()
                    .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.AuthorizationId), QueryComparisons.Equal, identifier))
                    .AsTableQuery();

                var continuationToken = default(TableContinuationToken);

                do
                {
                    var results = await ct.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);

                    continuationToken = results.ContinuationToken;

                    foreach (var token in results)
                    {
                        yield return token;
                    }
                } while (continuationToken != null);
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

            var query = ct.CreateQuery<TToken>()
                .Take(1)
                .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.Id), QueryComparisons.Equal, identifier))
                .AsTableQuery();

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TToken?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

            var query = ct.CreateQuery<TToken>()
                .Take(1)
                .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.ReferenceId), QueryComparisons.Equal, identifier))
                .AsTableQuery();

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TToken> FindBySubjectAsync(string subject, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TToken> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = GetCloudTableClient();
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

                var query = ct.CreateQuery<TToken>()
                    .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.Subject), QueryComparisons.Equal, subject))
                    .AsTableQuery();

                var continuationToken = default(TableContinuationToken);

                do
                {
                    var results = await ct.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);

                    continuationToken = results.ContinuationToken;

                    foreach (var token in results)
                    {
                        yield return token;
                    }
                } while (continuationToken != null);
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetApplicationIdAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            //TODO KAR is this ok?
            //EF had
            //if (token.Application is null)
            //{
            //    var reference = Context.Entry(token).Reference(entry => entry.Application);
            //    if (reference.EntityEntry.State == EntityState.Detached)
            //    {
            //        return null;
            //    }

            //    await reference.LoadAsync(cancellationToken);
            //}

            //if (token.Application is null)
            //{
            //    return null;
            //}

            //return ConvertIdentifierToString(token.Application.Id);

            if (token.ApplicationId == null)
            {
                return new ValueTask<string?>(result: null);
            }

            return new ValueTask<string?>(token.ApplicationId.ToString());
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TResult> GetAsync<TState, TResult>(
            Func<IQueryable<TToken>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            //TODO KAR
            //var database = await Context.GetDatabaseAsync(cancellationToken);
            //var collection = database.GetCollection<TToken>(Options.CurrentValue.TokensCollectionName);

            //return await ((TResult) query(collection.AsQueryable(), state)).FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetAuthorizationIdAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }
            //TODO KAR is this ok? see method above for similar
            if (token.AuthorizationId == null)
            {
                return new ValueTask<string?>(result: null);
            }

            return new ValueTask<string?>(token.AuthorizationId.ToString());
        }

        /// <inheritdoc/>
        public virtual ValueTask<DateTimeOffset?> GetCreationDateAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (token.CreationDate is null)
            {
                return new ValueTask<DateTimeOffset?>(result: null);
            }

            return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(token.CreationDate.Value, DateTimeKind.Utc));
        }

        /// <inheritdoc/>
        public virtual ValueTask<DateTimeOffset?> GetExpirationDateAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (token.ExpirationDate is null)
            {
                return new ValueTask<DateTimeOffset?>(result: null);
            }

            return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(token.ExpirationDate.Value, DateTimeKind.Utc));
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetIdAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string?>(token.Id?.ToString());
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetPayloadAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string?>(token.Payload);
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (token.Properties is null)
            {
                return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
            }

            using var document = JsonDocument.Parse(token.Properties);
            var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                builder[property.Name] = property.Value.Clone();
            }

            return new ValueTask<ImmutableDictionary<string, JsonElement>>(builder.ToImmutable());
        }

        /// <inheritdoc/>
        public virtual ValueTask<DateTimeOffset?> GetRedemptionDateAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (token.RedemptionDate is null)
            {
                return new ValueTask<DateTimeOffset?>(result: null);
            }

            return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(token.RedemptionDate.Value, DateTimeKind.Utc));
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetReferenceIdAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string?>(token.ReferenceId);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetStatusAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string?>(token.Status);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetSubjectAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string?>(token.Subject);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetTypeAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new ValueTask<string?>(token.Type);
        }

        /// <inheritdoc/>
        public virtual ValueTask<TToken> InstantiateAsync(CancellationToken cancellationToken)
        {
            try
            {
                return new ValueTask<TToken>(Activator.CreateInstance<TToken>());
            }

            catch (MemberAccessException exception)
            {
                return new ValueTask<TToken>(Task.FromException<TToken>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0248), exception)));
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TToken> ListAsync(
            int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

            long counter = 0;
            var continuationToken = default(TableContinuationToken);

            var query = new TableQuery<TToken>();

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
            Func<IQueryable<TToken>, TState, IQueryable<TResult>> query,
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
                //var database = await Context.GetDatabaseAsync(cancellationToken);
                //var collection = database.GetCollection<TToken>(Options.CurrentValue.TokensCollectionName);

                //await foreach (var element in query(collection.AsQueryable(), state).ToAsyncEnumerable(cancellationToken))
                //{
                //    yield return element;
                //}
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
        {
            //TODO KAR
            var database = await Context.GetDatabaseAsync(cancellationToken);
            var collection = database.GetCollection<TToken>(Options.CurrentValue.TokensCollectionName);

            // Note: directly deleting the resulting set of an aggregate query is not supported by MongoDB. //TODO KAR need this code?
            // To work around this limitation, the token identifiers are stored in an intermediate list
            // and delete requests are sent to remove the documents corresponding to these identifiers.

            var identifiers =
                await (from token in collection.AsQueryable()
                       join authorization in database.GetCollection<OpenIddictAtsAuthorization>(Options.CurrentValue.AuthorizationsCollectionName).AsQueryable()
                                          on token.AuthorizationId equals authorization.Id into authorizations
                       where token.CreationDate < threshold.UtcDateTime
                       where (token.Status != Statuses.Inactive && token.Status != Statuses.Valid) ||
                              token.ExpirationDate < DateTime.UtcNow ||
                              authorizations.Any(authorization => authorization.Status != Statuses.Valid)
                       select token.Id).ToListAsync(cancellationToken);

            // Note: to avoid generating delete requests with very large filters, a buffer is used here and the
            // maximum number of elements that can be removed by a single call to PruneAsync() is limited to 50000.
            foreach (var buffer in Buffer(identifiers.Take(50_000), 1_000))
            {
                await collection.DeleteManyAsync(token => buffer.Contains(token.Id), cancellationToken);
            }

            static IEnumerable<List<TSource>> Buffer<TSource>(IEnumerable<TSource> source, int count)
            {
                List<TSource>? buffer = null;

                foreach (var element in source)
                {
                    buffer ??= new List<TSource>(capacity: 1);
                    buffer.Add(element);

                    if (buffer.Count == count)
                    {
                        yield return buffer;

                        buffer = null;
                    }
                }

                if (buffer is not null)
                {
                    yield return buffer;
                }
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask SetApplicationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
        {
            //TODO KAR do in AuthorizationStore then copy here
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (!string.IsNullOrEmpty(identifier))
            {
                token.ApplicationId = ObjectId.Parse(identifier);
            }

            else
            {
                token.ApplicationId = ObjectId.Empty;
            }

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetAuthorizationIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
        {
            //TODO KAR do in AuthorizationStore then copy here
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (!string.IsNullOrEmpty(identifier))
            {
                token.AuthorizationId = ObjectId.Parse(identifier);
            }

            else
            {
                token.AuthorizationId = ObjectId.Empty;
            }

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetCreationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.CreationDate = date?.UtcDateTime;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetExpirationDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.ExpirationDate = date?.UtcDateTime;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetPayloadAsync(TToken token, string? payload, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.Payload = payload;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetPropertiesAsync(TToken token,
            ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (properties is null || properties.IsEmpty)
            {
                token.Properties = null;

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

            token.Properties = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetRedemptionDateAsync(TToken token, DateTimeOffset? date, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.RedemptionDate = date?.UtcDateTime;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetReferenceIdAsync(TToken token, string? identifier, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.ReferenceId = identifier;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetStatusAsync(TToken token, string? status, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.Status = status;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetSubjectAsync(TToken token, string? subject, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.Subject = subject;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetTypeAsync(TToken token, string? type, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            token.Type = type;

            return default;
        }

        /// <inheritdoc/>
        public virtual async ValueTask UpdateAsync(TToken token, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            var tableClient = GetCloudTableClient();
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(token);

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