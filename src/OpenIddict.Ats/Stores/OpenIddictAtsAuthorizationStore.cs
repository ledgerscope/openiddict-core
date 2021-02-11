/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
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
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Cosmos.Table.Protocol;
using OpenIddict.Abstractions;
using OpenIddict.Ats.Models;
using static OpenIddict.Abstractions.OpenIddictConstants;
using SR = OpenIddict.Abstractions.OpenIddictResources;

namespace OpenIddict.Ats
{
    /// <summary>
    /// Provides methods allowing to manage the authorizations stored in a database.
    /// </summary>
    /// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
    public class OpenIddictAtsAuthorizationStore<TAuthorization> : IOpenIddictAuthorizationStore<TAuthorization>
        where TAuthorization : OpenIddictAtsAuthorization, new()
    {
        public OpenIddictAtsAuthorizationStore(
            IOpenIddictAtsContext context,
            IOptionsMonitor<OpenIddictAtsOptions> options)
        {
            Context = context;
            Options = options;
        }

        /// <summary>
        /// Gets the database context associated with the current store.
        /// </summary>
        protected IOpenIddictAtsContext Context { get; }

        /// <summary>
        /// Gets the options associated with the current store.
        /// </summary>
        protected IOptionsMonitor<OpenIddictAtsOptions> Options { get; }

        public TableRequestOptions TableRequestOptions { get; } = new TableRequestOptions()
        {
            RetryPolicy = new ExponentialRetry(),
            MaximumExecutionTime = TimeSpan.FromMinutes(10),
            ServerTimeout = TimeSpan.FromMinutes(1)
        };

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync(CancellationToken cancellationToken)
        {
            var tableClient = await Context.GetTableClientAsync(cancellationToken);
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

            var query = new TableQuery<DynamicTableEntity>().Select(new[] { TableConstants.PartitionKey });

            return await OpenIddictAtsHelpers.CountLongAsync(ct, query, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask<long> CountAsync<TResult>(
            Func<IQueryable<TAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var tableClient = await Context.GetTableClientAsync(cancellationToken);
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.ApplicationsCollectionName);

            var tableQuery = ct.CreateQuery<TAuthorization>();

            long counter = 0;
            var continuationToken = default(TableContinuationToken);

            do
            {
                var results = await ct.ExecuteQuerySegmentedAsync(tableQuery, continuationToken, cancellationToken);
                continuationToken = results.ContinuationToken;
                foreach (var record in query(results.AsQueryable()))
                {
                    counter++;
                }
            } while (continuationToken != null);

            return counter;
        }

        /// <inheritdoc/>
        public virtual async ValueTask CreateAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var tableClient = await Context.GetTableClientAsync(cancellationToken);
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(authorization);

            await ct.ExecuteAsync(insertOrMergeOperation, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var tableClient = await Context.GetTableClientAsync(cancellationToken);
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

            var idFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Id), QueryComparisons.Equal, authorization.Id);
            var tokenFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.ConcurrencyToken), QueryComparisons.Equal, authorization.ConcurrencyToken);

            var filter = TableQuery.CombineFilters(idFilter,
                TableOperators.And,
                tokenFilter);

            var authorisationDeleteQuery = new TableQuery<OpenIddictAtsAuthorization>().Where(filter)
                .Select(new string[] { TableConstants.PartitionKey, TableConstants.RowKey });

            try
            {
                await OpenIddictAtsHelpers.DeleteAsync(ct, authorisationDeleteQuery);

                // Delete the tokens associated with the authorization.
                ct = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

                var tokenDeleteQuery = new TableQuery<OpenIddictAtsToken>().Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsToken.AuthorizationId), QueryComparisons.Equal, authorization.Id))
                    .Select(new string[] { TableConstants.PartitionKey, TableConstants.RowKey });

                await OpenIddictAtsHelpers.DeleteAsync(ct, tokenDeleteQuery);
            }
            catch (StorageException exception)
            {
                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241), exception);
            }
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TAuthorization> FindAsync(
            string subject, string client, CancellationToken cancellationToken)
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

            async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = await Context.GetTableClientAsync(cancellationToken);
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

                var subjectFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Subject), QueryComparisons.Equal, subject);
                var clientFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.ApplicationId), QueryComparisons.Equal, client);

                var filter = TableQuery.CombineFilters(subjectFilter,
                    TableOperators.And,
                    clientFilter);

                var query = ct.CreateQuery<TAuthorization>()
                    .Where(filter);

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
        public virtual IAsyncEnumerable<TAuthorization> FindAsync(
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

            async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = await Context.GetTableClientAsync(cancellationToken);
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

                var subjectFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Subject), QueryComparisons.Equal, subject);
                var clientFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.ApplicationId), QueryComparisons.Equal, client);
                var statusFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Status), QueryComparisons.Equal, status);

                var filters = OpenIddictAtsHelpers.CombineFilters(TableOperators.And, new string[] { subjectFilter, clientFilter, statusFilter });

                var query = ct.CreateQuery<TAuthorization>()
                    .Where(filters);

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
        public virtual IAsyncEnumerable<TAuthorization> FindAsync(
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

            async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = await Context.GetTableClientAsync(cancellationToken);
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

                var subjectFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Subject), QueryComparisons.Equal, subject);
                var clientFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.ApplicationId), QueryComparisons.Equal, client);
                var statusFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Status), QueryComparisons.Equal, status);
                var typeFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Type), QueryComparisons.Equal, type);

                var filters = OpenIddictAtsHelpers.CombineFilters(TableOperators.And, new string[] { subjectFilter, clientFilter, statusFilter, typeFilter });

                var query = ct.CreateQuery<TAuthorization>()
                    .Where(filters);

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
        public virtual IAsyncEnumerable<TAuthorization> FindAsync(
            string subject, string client,
            string status, string type,
            ImmutableArray<string> scopes, CancellationToken cancellationToken)
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

            async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = await Context.GetTableClientAsync(cancellationToken);
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

                var subjectFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Subject), QueryComparisons.Equal, subject);
                var clientFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.ApplicationId), QueryComparisons.Equal, client);
                var statusFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Status), QueryComparisons.Equal, status);
                var typeFilter = TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Type), QueryComparisons.Equal, type);

                var filters = OpenIddictAtsHelpers.CombineFilters(TableOperators.And, new string[] { subjectFilter, clientFilter, statusFilter, typeFilter });

                var query = ct.CreateQuery<TAuthorization>()
                    .Where(filters);

                var continuationToken = default(TableContinuationToken);

                do
                {
                    var results = await ct.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);

                    continuationToken = results.ContinuationToken;

                    //Enumerable.All(scopes, scope => authorization.Scopes.Contains(scope))).ToAsyncEnumerable(cancellationToken))
                    //TODO KAR improve into 1 line?
                    foreach (var authorization in results)
                    {
                        if (authorization.Scopes != null)
                        { 
                            foreach (var scope in scopes)
                            {
                                if (!authorization.Scopes.Contains(scope))
                                {
                                    continue;
                                }
                            }

                            yield return authorization;
                        }
                    }
                } while (continuationToken != null);
            }
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TAuthorization> FindByApplicationIdAsync(
            string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = await Context.GetTableClientAsync(cancellationToken);
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

                var query = ct.CreateQuery<TAuthorization>()
                    .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.ApplicationId), QueryComparisons.Equal, identifier));

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
        public virtual async ValueTask<TAuthorization?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0195), nameof(identifier));
            }

            var tableClient = await Context.GetTableClientAsync(cancellationToken);
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

            var query = ct.CreateQuery<TAuthorization>()
                .Take(1)
                .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Id), QueryComparisons.Equal, identifier));

            var queryResult = await query.ExecuteSegmentedAsync(default, cancellationToken);

            return queryResult.Results.FirstOrDefault();
        }

        /// <inheritdoc/>
        public virtual IAsyncEnumerable<TAuthorization> FindBySubjectAsync(
            string subject, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0198), nameof(subject));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TAuthorization> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = await Context.GetTableClientAsync(cancellationToken);
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

                var query = ct.CreateQuery<TAuthorization>()
                    .Where(TableQuery.GenerateFilterCondition(nameof(OpenIddictAtsAuthorization.Subject), QueryComparisons.Equal, subject));

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
        public virtual ValueTask<string?> GetApplicationIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }
            
            if (authorization.ApplicationId == null)
            {
                return new ValueTask<string?>(result: null);
            }

            return new ValueTask<string?>(authorization.ApplicationId.ToString());
        }

        /// <inheritdoc/>
        public virtual async ValueTask<TResult> GetAsync<TState, TResult>(
            Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var tableClient = await Context.GetTableClientAsync(cancellationToken);
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

            var cloudQuery = ct.CreateQuery<TAuthorization>().AsQueryable();
            var result = query(cloudQuery, state);

            //TODO KAR make async
            //.AsTableQuery().FirstOrDefaultAsync so how can I get it working here?
            return result.FirstOrDefault();//.FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual ValueTask<DateTimeOffset?> GetCreationDateAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (authorization.CreationDate is null)
            {
                return new ValueTask<DateTimeOffset?>(result: null);
            }

            return new ValueTask<DateTimeOffset?>(DateTime.SpecifyKind(authorization.CreationDate.Value, DateTimeKind.Utc));
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetIdAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return new ValueTask<string?>(ConvertIdentifierToString(authorization.Id));

        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (string.IsNullOrEmpty(authorization.Properties))
            {
                return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary.Create<string, JsonElement>());
            }

            using var document = JsonDocument.Parse(authorization.Properties);
            var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                builder[property.Name] = property.Value.Clone();
            }

            return new ValueTask<ImmutableDictionary<string, JsonElement>>(builder.ToImmutable());
        }

        /// <inheritdoc/>
        public virtual ValueTask<ImmutableArray<string>> GetScopesAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (string.IsNullOrEmpty(authorization.Scopes))
            {
                return new ValueTask<ImmutableArray<string>>(ImmutableArray.Create<string>());
            }

            using var document = JsonDocument.Parse(authorization.Scopes);
            var builder = ImmutableArray.CreateBuilder<string>();

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
        public virtual ValueTask<string?> GetStatusAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return new ValueTask<string?>(authorization.Status);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetSubjectAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return new ValueTask<string?>(authorization.Subject);
        }

        /// <inheritdoc/>
        public virtual ValueTask<string?> GetTypeAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            return new ValueTask<string?>(authorization.Type);
        }

        /// <inheritdoc/>
        public virtual ValueTask<TAuthorization> InstantiateAsync(CancellationToken cancellationToken)
        {
            try
            {
                return new ValueTask<TAuthorization>(Activator.CreateInstance<TAuthorization>());
            }

            catch (MemberAccessException exception)
            {
                return new ValueTask<TAuthorization>(Task.FromException<TAuthorization>(
                    new InvalidOperationException(SR.GetResourceString(SR.ID0242), exception)));
            }
        }

        /// <inheritdoc/>
        public virtual async IAsyncEnumerable<TAuthorization> ListAsync(
            int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var tableClient = await Context.GetTableClientAsync(cancellationToken);
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

            long counter = 0;
            var continuationToken = default(TableContinuationToken);

            var query = new TableQuery<TAuthorization>();

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
            Func<IQueryable<TAuthorization>, TState, IQueryable<TResult>> query,
            TState state, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return ExecuteAsync(cancellationToken);

            async IAsyncEnumerable<TResult> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var tableClient = await Context.GetTableClientAsync(cancellationToken);
                CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

                var tableQuery = ct.CreateQuery<TAuthorization>();

                var continuationToken = default(TableContinuationToken);

                do
                {
                    var results = await ct.ExecuteQuerySegmentedAsync(tableQuery, continuationToken, cancellationToken);
                    continuationToken = results.ContinuationToken;

                    foreach (var authorization in query(results.AsQueryable(), state))
                    {
                        yield return authorization;
                    }
                } while (continuationToken != null);
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
        {
            var tableClient = await Context.GetTableClientAsync(cancellationToken);
            CloudTable ctAuth = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);
            CloudTable ctToken = tableClient.GetTableReference(Options.CurrentValue.TokensCollectionName);

            var authQuery = ctAuth.CreateQuery<TAuthorization>();

            var tokenQuery = ctToken.CreateQuery<OpenIddictAtsToken>();

            var identifiers =
                (from authorization in authQuery
                       join token in tokenQuery
                                  on authorization.Id equals token.AuthorizationId into tokens
                       where authorization.CreationDate < threshold.UtcDateTime
                       where authorization.Status != Statuses.Valid ||
                            (authorization.Type == AuthorizationTypes.AdHoc && !tokens.Any())
                       select authorization).ToList();

            var offset = 0;
            while (offset < identifiers.Count)
            {
                var batch = new TableBatchOperation();
                var rows = identifiers.Skip(offset).Take(100).ToList();
                foreach (var row in rows)
                {
                    batch.Delete(row);
                }

                ctAuth.ExecuteBatch(batch);
                offset += rows.Count;
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask SetApplicationIdAsync(TAuthorization authorization,
            string? identifier, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (!string.IsNullOrEmpty(identifier))
            {
                authorization.ApplicationId = identifier;
            }
            else
            {
                authorization.ApplicationId = null;
            }

            return default;
        }

        /// <summary>
        /// Converts the provided identifier to a strongly typed key object.
        /// </summary>
        /// <param name="identifier">The identifier to convert.</param>
        /// <returns>An instance of representing the provided identifier.</returns>
        public virtual string? ConvertIdentifierFromString(string? identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return default;
            }

            return (string)TypeDescriptor.GetConverter(typeof(string)).ConvertFromInvariantString(identifier);
        }

        /// <inheritdoc/>
        public virtual ValueTask SetCreationDateAsync(TAuthorization authorization,
            DateTimeOffset? date, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            authorization.CreationDate = date?.UtcDateTime;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetPropertiesAsync(TAuthorization authorization,
            ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (properties is null || properties.IsEmpty)
            {
                authorization.Properties = null;

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

            authorization.Properties = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetScopesAsync(TAuthorization authorization,
            ImmutableArray<string> scopes, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            if (scopes.IsDefaultOrEmpty)
            {
                authorization.Scopes = null;

                return default;
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false
            });

            writer.WriteStartArray();

            foreach (var scope in scopes)
            {
                writer.WriteStringValue(scope);
            }

            writer.WriteEndArray();
            writer.Flush();

            authorization.Scopes = Encoding.UTF8.GetString(stream.ToArray());

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetStatusAsync(TAuthorization authorization, string? status, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            authorization.Status = status;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetSubjectAsync(TAuthorization authorization, string? subject, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            authorization.Subject = subject;

            return default;
        }

        /// <inheritdoc/>
        public virtual ValueTask SetTypeAsync(TAuthorization authorization, string? type, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            authorization.Type = type;

            return default;
        }

        /// <inheritdoc/>
        public virtual async ValueTask UpdateAsync(TAuthorization authorization, CancellationToken cancellationToken)
        {
            if (authorization is null)
            {
                throw new ArgumentNullException(nameof(authorization));
            }

            var tableClient = await Context.GetTableClientAsync(cancellationToken);
            CloudTable ct = tableClient.GetTableReference(Options.CurrentValue.AuthorizationsCollectionName);

            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(authorization);

            try
            {
                await ct.ExecuteAsync(insertOrMergeOperation);
            }
            catch (StorageException exception)
            {
                throw new OpenIddictExceptions.ConcurrencyException(SR.GetResourceString(SR.ID0241), exception);
            }
        }

        /// <summary>
        /// Converts the provided identifier to its string representation.
        /// </summary>
        /// <param name="identifier">The identifier to convert.</param>
        /// <returns>A <see cref="string"/> representation of the provided identifier.</returns>
        public virtual string? ConvertIdentifierToString(string? identifier)
        {
            if (Equals(identifier, default(string)))
            {
                return null;
            }

            return TypeDescriptor.GetConverter(typeof(string)).ConvertToInvariantString(identifier);
        }
    }
}