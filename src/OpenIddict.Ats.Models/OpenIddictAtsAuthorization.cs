/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage.Table;

namespace OpenIddict.Ats.Models
{
    /// <summary>
    /// Represents an OpenIddict authorization.
    /// </summary>
    //public class OpenIddictAtsAuthorization : OpenIddictAtsAuthorization<string, OpenIddictAtsApplication, OpenIddictAtsToken>
    //{
    //    public OpenIddictAtsAuthorization()
    //    {
    //        // Generate a new string identifier.
    //        Id = Guid.NewGuid().ToString();
    //    }
    //}

    ///// <summary>
    ///// Represents an OpenIddict authorization.
    ///// </summary>
    //public class OpenIddictAtsAuthorization<TKey> : OpenIddictAtsAuthorization<TKey, OpenIddictAtsApplication<TKey>, OpenIddictAtsToken<TKey>>
    //    where TKey : IEquatable<TKey>
    //{
    //}

    ///// <summary>
    ///// Represents an OpenIddict authorization.
    ///// </summary>
    //[DebuggerDisplay("Id = {Id.ToString(),nq} ; Subject = {Subject,nq} ; Type = {Type,nq} ; Status = {Status,nq}")]
    //public class OpenIddictAtsAuthorization<TKey, TApplication, TToken>
    //    where TKey : IEquatable<TKey>
    //    where TApplication : class
    //    where TToken : class
    public class OpenIddictAtsAuthorization : TableEntity
    {
        /// <summary>
        /// Gets or sets the identifier of the application associated with the current authorization.
        /// </summary>
        public virtual string? ApplicationId { get; set; }

        /// <summary>
        /// Gets or sets the concurrency token.
        /// </summary>
        public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the UTC creation date of the current authorization.
        /// </summary>
        public virtual DateTime? CreationDate { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier associated with the current authorization.
        /// </summary>
        public virtual string? Id { get; set; }

        /// <summary>
        /// Gets or sets the additional properties associated with the current authorization.
        /// </summary>
        public virtual string? Properties { get; set; }

        /// <summary>
        /// Gets or sets the scopes associated with the current authorization.
        /// </summary>
        public virtual string? Scopes { get; set; }

        /// <summary>
        /// Gets or sets the status of the current authorization.
        /// </summary>
        public virtual string? Status { get; set; }

        /// <summary>
        /// Gets or sets the subject associated with the current authorization.
        /// </summary>
        public virtual string? Subject { get; set; }

        /// <summary>
        /// Gets or sets the type of the current authorization.
        /// </summary>
        public virtual string? Type { get; set; }
    }
}
