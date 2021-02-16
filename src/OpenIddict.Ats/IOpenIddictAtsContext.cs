/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using OpenIddict.Ats.Models;

namespace OpenIddict.Ats
{
    /// <summary>
    /// Exposes the ATS table client used by the OpenIddict stores.
    /// </summary>
    public interface IOpenIddictAtsContext
    {
        /// <summary>
        /// Gets the <see cref="ICloudTableClient"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that can be used to monitor the
        /// asynchronous operation, whose result returns the ATS table client.
        /// </returns>
        ValueTask<ICloudTableClient> GetTableClientAsync(CancellationToken cancellationToken);
    }
}