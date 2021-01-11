/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/openiddict/openiddict-core for more information concerning
 * the license and the contributors participating to this project.
 */

using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Ats;
using OpenIddict.Ats.Models;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Exposes extensions allowing to register the OpenIddict ATS services.
    /// </summary>
    public static class OpenIddictAtsExtensions
    {
        /// <summary>
        /// Registers the ATS stores services in the DI container and
        /// configures OpenIddict to use the ATS entities by default.
        /// </summary>
        /// <param name="builder">The services builder used by OpenIddict to register new services.</param>
        /// <remarks>This extension can be safely called multiple times.</remarks>
        /// <returns>The <see cref="OpenIddictAtsBuilder"/>.</returns>
        public static OpenIddictAtsBuilder UseAts(this OpenIddictCoreBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // Note: Mongo uses simple binary comparison checks by default so the additional
            // query filtering applied by the default OpenIddict managers can be safely disabled.
            builder.DisableAdditionalFiltering();

            builder.SetDefaultApplicationEntity<OpenIddictAtsApplication>()
                   .SetDefaultAuthorizationEntity<OpenIddictAtsAuthorization>()
                   .SetDefaultScopeEntity<OpenIddictAtsScope>()
                   .SetDefaultTokenEntity<OpenIddictAtsToken>();

            // Note: the Mongo stores/resolvers don't depend on scoped/transient services and thus
            // can be safely registered as singleton services and shared/reused across requests.
            builder.ReplaceApplicationStoreResolver<OpenIddictAtsApplicationStoreResolver>(ServiceLifetime.Singleton)
                   .ReplaceAuthorizationStoreResolver<OpenIddictAtsAuthorizationStoreResolver>(ServiceLifetime.Singleton)
                   .ReplaceScopeStoreResolver<OpenIddictAtsScopeStoreResolver>(ServiceLifetime.Singleton)
                   .ReplaceTokenStoreResolver<OpenIddictAtsTokenStoreResolver>(ServiceLifetime.Singleton);

            builder.Services.TryAddSingleton(typeof(OpenIddictAtsApplicationStore<>));
            builder.Services.TryAddSingleton(typeof(OpenIddictAtsAuthorizationStore<>));
            builder.Services.TryAddSingleton(typeof(OpenIddictAtsScopeStore<>));
            builder.Services.TryAddSingleton(typeof(OpenIddictAtsTokenStore<>));

            builder.Services.TryAddSingleton<IOpenIddictAtsContext, OpenIddictAtsContext>();

            return new OpenIddictAtsBuilder(builder.Services);
        }

        /// <summary>
        /// Registers the ATS stores services in the DI container and
        /// configures OpenIddict to use the ATS entities by default.
        /// </summary>
        /// <param name="builder">The services builder used by OpenIddict to register new services.</param>
        /// <param name="configuration">The configuration delegate used to configure the ATS services.</param>
        /// <remarks>This extension can be safely called multiple times.</remarks>
        /// <returns>The <see cref="OpenIddictCoreBuilder"/>.</returns>
        public static OpenIddictCoreBuilder UseAts(
            this OpenIddictCoreBuilder builder, Action<OpenIddictAtsBuilder> configuration)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            configuration(builder.UseAts());

            return builder;
        }
    }
}
