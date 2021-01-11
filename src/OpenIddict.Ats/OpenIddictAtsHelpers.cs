/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using OpenIddict.Abstractions;
using OpenIddict.Ats.Models;
using Microsoft.WindowsAzure.Storage.Table.Queryable;

namespace Ats.Driver
{
    /// <summary>
    /// Exposes extensions simplifying the integration between OpenIddict and ATS.
    /// </summary>
    internal static class OpenIddictAtsHelpers
    {
        public static async Task<IEnumerable<TElement>> ExecuteAsync<TElement>(this TableQuery<TElement> tableQuery, CancellationToken ct)
        {
            var nextQuery = tableQuery;
            var continuationToken = default(TableContinuationToken);
            var results = new List<TElement>();

            do
            {
                //Execute the next query segment async.
                var queryResult = await nextQuery.ExecuteSegmentedAsync(continuationToken, ct);

                //Set exact results list capacity with result count.
                results.Capacity += queryResult.Results.Count;

                //Add segment results to results list.
                results.AddRange(queryResult.Results);

                continuationToken = queryResult.ContinuationToken;

                //Continuation token is not null, more records to load.
                if (continuationToken != null && tableQuery.TakeCount.HasValue)
                {
                    //Query has a take count, calculate the remaining number of items to load.
                    var itemsToLoad = tableQuery.TakeCount.Value - results.Count;

                    //If more items to load, update query take count, or else set next query to null.
                    nextQuery = itemsToLoad > 0
                        ? tableQuery.Take<TElement>(itemsToLoad).AsTableQuery()
                        : null;
                }

            } while (continuationToken != null && nextQuery != null && !ct.IsCancellationRequested);

            return results;
        }

        public static async Task<TElement> FirstOrDefaultAsync<TElement>(this TableQuery<TElement> tableQuery, CancellationToken ct)
        {
            var queryResult = await tableQuery.ExecuteSegmentedAsync(default, ct);

            return queryResult.Results.FirstOrDefault();
        }

        /// <summary>
        /// Executes the query and returns the results as a streamed async enumeration.
        /// </summary>
        /// <typeparam name="T">The type of the returned entities.</typeparam>
        /// <param name="source">The query source.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The streamed async enumeration containing the results.</returns>
        //internal static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IAsyncCursorSource<T> source, CancellationToken cancellationToken)
        //{
        //    if (source is null)
        //    {
        //        throw new ArgumentNullException(nameof(source));
        //    }

        //    return ExecuteAsync(source, cancellationToken);

        //    static async IAsyncEnumerable<T> ExecuteAsync(IAsyncCursorSource<T> source, [EnumeratorCancellation] CancellationToken cancellationToken)
        //    {
        //        using var cursor = await source.ToCursorAsync(cancellationToken);

        //        while (await cursor.MoveNextAsync(cancellationToken))
        //        {
        //            foreach (var element in cursor.Current)
        //            {
        //                yield return element;
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// Executes the query and returns the results as a streamed async enumeration.
        /// </summary>
        /// <typeparam name="T">The type of the returned entities.</typeparam>
        /// <param name="source">The query source.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
        /// <returns>The streamed async enumeration containing the results.</returns>
        //internal static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IQueryable<T> source, CancellationToken cancellationToken)
        //{
        //    if (source is null)
        //    {
        //        throw new ArgumentNullException(nameof(source));
        //    }

        //    return ((IAsyncCursorSource<T>) source).ToAsyncEnumerable(cancellationToken);
        //}
    }
}
