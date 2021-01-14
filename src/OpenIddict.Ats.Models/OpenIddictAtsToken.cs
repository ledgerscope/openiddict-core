/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage.Table;

namespace OpenIddict.Ats.Models
{
    //public class OpenIddictAtsToken : OpenIddictAtsToken<string, OpenIddictAtsApplication, OpenIddictAtsAuthorization>
    //{
    //    /// <summary>
    //    /// Represents an OpenIddict token.
    //    /// </summary>
    //    public OpenIddictAtsToken()
    //    {
    //        // Generate a new string identifier.
    //        Id = Guid.NewGuid().ToString();
    //    }
    //}

    ///// <summary>
    ///// Represents an OpenIddict token.
    ///// </summary>
    //public class OpenIddictAtsToken<TKey> : OpenIddictAtsToken<TKey, OpenIddictAtsApplication<TKey>, OpenIddictAtsAuthorization<TKey>>
    //    where TKey : IEquatable<TKey>
    //{
    //}

    ///// <summary>
    ///// Represents an OpenIddict token.
    ///// </summary>
    //[DebuggerDisplay("Id = {Id.ToString(),nq} ; Subject = {Subject,nq} ; Type = {Type,nq} ; Status = {Status,nq}")]
    //public class OpenIddictAtsToken<TKey, TApplication, TAuthorization>
    //    where TKey : IEquatable<TKey>
    //    where TApplication : class
    //    where TAuthorization : class
    public class OpenIddictAtsToken : TableEntity
    {
        /// <summary>
        /// Gets or sets the identifier of the application associated with the current token.
        /// </summary>
        public virtual string? ApplicationId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the authorization associated with the current token.
        /// </summary>
        public virtual string? AuthorizationId { get; set; }

        /// <summary>
        /// Gets or sets the concurrency token.
        /// </summary>
        public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the UTC creation date of the current token.
        /// </summary>
        public virtual DateTime? CreationDate { get; set; }

        /// <summary>
        /// Gets or sets the UTC expiration date of the current token.
        /// </summary>
        public virtual DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier associated with the current token.
        /// </summary>
        public virtual string? Id { get; set; }

        /// <summary>
        /// Gets or sets the payload of the current token, if applicable.
        /// Note: this property is only used for reference tokens
        /// and may be encrypted for security reasons.
        /// </summary>
        public virtual string? Payload { get; set; }

        /// <summary>
        /// Gets or sets the additional properties associated with the current token.
        /// </summary>
        public virtual string? Properties { get; set; }

        /// <summary>
        /// Gets or sets the UTC redemption date of the current token.
        /// </summary>
        public virtual DateTime? RedemptionDate { get; set; }

        /// <summary>
        /// Gets or sets the reference identifier associated
        /// with the current token, if applicable.
        /// Note: this property is only used for reference tokens
        /// and may be hashed or encrypted for security reasons.
        /// </summary>
        public virtual string? ReferenceId { get; set; }

        /// <summary>
        /// Gets or sets the status of the current token.
        /// </summary>
        public virtual string? Status { get; set; }

        /// <summary>
        /// Gets or sets the subject associated with the current token.
        /// </summary>
        public virtual string? Subject { get; set; }

        /// <summary>
        /// Gets or sets the type of the current token.
        /// </summary>
        public virtual string? Type { get; set; }
    }
}
