/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Azure.Cosmos.Table;

namespace OpenIddict.Ats.Models
{
    /// <summary>
    /// Represents an OpenIddict scope.
    /// </summary>
    //public class OpenIddictAtsScope : OpenIddictAtsScope<string>
    //{
    //    public OpenIddictAtsScope()
    //    {
    //        // Generate a new string identifier.
    //        Id = Guid.NewGuid().ToString();
    //    }
    //}

    ///// <summary>
    ///// Represents an OpenIddict scope.
    ///// </summary>
    //[DebuggerDisplay("Id = {Id.ToString(),nq} ; Name = {Name,nq}")]
    //public class OpenIddictAtsScope<TKey> where TKey : IEquatable<TKey>
    public class OpenIddictAtsScope : TableEntity
    {
        /// <summary>
        /// Gets or sets the concurrency token.
        /// </summary>
        public virtual string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the public description associated with the current scope.
        /// </summary>
        public virtual string? Description { get; set; }

        /// <summary>
        /// Gets or sets the localized public descriptions associated with the current scope.
        /// </summary>
        public virtual string? Descriptions { get; set; }

        /// <summary>
        /// Gets or sets the display name associated with the current scope.
        /// </summary>
        public virtual string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the localized display names associated with the current scope.
        /// </summary>
        public virtual string? DisplayNames { get; set; }

        /// <summary>
        /// Gets or sets the unique name associated with the current scope.
        /// </summary>
        public virtual string? Name { get; set; }

        /// <summary>
        /// Gets or sets the additional properties associated with the current scope.
        /// </summary>
        public virtual string? Properties { get; set; }

        /// <summary>
        /// Gets or sets the resources associated with the current scope.
        /// </summary>
        public virtual string? Resources { get; set; }
    }
}
