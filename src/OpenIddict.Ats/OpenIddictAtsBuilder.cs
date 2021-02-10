/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using System.ComponentModel;
using OpenIddict.Core;
using OpenIddict.Ats;
using OpenIddict.Ats.Models;
using SR = OpenIddict.Abstractions.OpenIddictResources;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Exposes the necessary methods required to configure the OpenIddict ATS services.
    /// </summary>
    public class OpenIddictAtsBuilder
    {
        /// <summary>
        /// Initializes a new instance of <see cref="OpenIddictAtsBuilder"/>.
        /// </summary>
        /// <param name="services">The services collection.</param>
        public OpenIddictAtsBuilder(IServiceCollection services)
            => Services = services ?? throw new ArgumentNullException(nameof(services));

        /// <summary>
        /// Gets the services collection.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public IServiceCollection Services { get; }

        /// <summary>
        /// Amends the default OpenIddict ATS configuration.
        /// </summary>
        /// <param name="configuration">The delegate used to configure the OpenIddict options.</param>
        /// <remarks>This extension can be safely called multiple times.</remarks>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public OpenIddictAtsBuilder Configure(Action<OpenIddictAtsOptions> configuration)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            Services.Configure(configuration);

            return this;
        }

        /// <summary>
        /// Configures OpenIddict to use the specified entity as the default application entity.
        /// </summary>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public OpenIddictAtsBuilder ReplaceDefaultApplicationEntity<TApplication>()
            where TApplication : OpenIddictAtsApplication
        {
            Services.Configure<OpenIddictCoreOptions>(options => options.DefaultApplicationType = typeof(TApplication));

            return this;
        }

        /// <summary>
        /// Configures OpenIddict to use the specified entity as the default authorization entity.
        /// </summary>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public OpenIddictAtsBuilder ReplaceDefaultAuthorizationEntity<TAuthorization>()
            where TAuthorization : OpenIddictAtsAuthorization
        {
            Services.Configure<OpenIddictCoreOptions>(options => options.DefaultAuthorizationType = typeof(TAuthorization));

            return this;
        }

        /// <summary>
        /// Configures OpenIddict to use the specified entity as the default scope entity.
        /// </summary>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public OpenIddictAtsBuilder ReplaceDefaultScopeEntity<TScope>()
            where TScope : OpenIddictAtsScope
        {
            Services.Configure<OpenIddictCoreOptions>(options => options.DefaultScopeType = typeof(TScope));

            return this;
        }

        /// <summary>
        /// Configures OpenIddict to use the specified entity as the default token entity.
        /// </summary>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public OpenIddictAtsBuilder ReplaceDefaultTokenEntity<TToken>()
            where TToken : OpenIddictAtsToken
        {
            Services.Configure<OpenIddictCoreOptions>(options => options.DefaultTokenType = typeof(TToken));

            return this;
        }

        /// <summary>
        /// Replaces the default applications collection name (by default, openiddict.applications).
        /// </summary>
        /// <param name="name">The collection name</param>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public OpenIddictAtsBuilder SetApplicationsCollectionName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0261), nameof(name));
            }

            return Configure(options => options.ApplicationsCollectionName = name);
        }

        /// <summary>
        /// Replaces the default authorizations collection name (by default, openiddict.authorizations).
        /// </summary>
        /// <param name="name">The collection name</param>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public OpenIddictAtsBuilder SetAuthorizationsCollectionName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0261), nameof(name));
            }

            return Configure(options => options.AuthorizationsCollectionName = name);
        }

        /// <summary>
        /// Replaces the default scopes collection name (by default, openiddict.scopes).
        /// </summary>
        /// <param name="name">The collection name</param>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public OpenIddictAtsBuilder SetScopesCollectionName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0261), nameof(name));
            }

            return Configure(options => options.ScopesCollectionName = name);
        }

        /// <summary>
        /// Replaces the default tokens collection name (by default, openiddict.tokens).
        /// </summary>
        /// <param name="name">The collection name</param>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public OpenIddictAtsBuilder SetTokensCollectionName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(SR.GetResourceString(SR.ID0261), nameof(name));
            }

            return Configure(options => options.TokensCollectionName = name);
        }

        /// <summary>
        /// Configures ATS to use the specified database
        /// instead of retrieving it from the dependency injection container.
        /// </summary>
        /// <param name="database">The <see cref="CloudTable"/>.</param>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        /// //TODO KAR
        public OpenIddictAtsBuilder UseDatabase(CloudTable database)
        {
            if (database is null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            return Configure(options => options.Database = database.ServiceClient);
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object? obj) => base.Equals(obj);

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() => base.GetHashCode();

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string? ToString() => base.ToString();
    }
}
