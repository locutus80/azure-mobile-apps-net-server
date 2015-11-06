﻿// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Azure.Mobile.Server.Authentication.AppService;
using Microsoft.Azure.Mobile.Server.Properties;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;

namespace Microsoft.Azure.Mobile.Server.Authentication
{
    /// <summary>
    /// The <see cref="MobileAppAuthenticationHandler"/> authenticates a caller who has already authenticated using the Login controller,
    /// or has provided HTTP basic authentication credentials matching either the application key or the master key (for admin access).
    /// </summary>
    public class MobileAppAuthenticationHandler : AuthenticationHandler<MobileAppAuthenticationOptions>
    {
        public const string AuthenticationHeaderName = "x-zumo-auth";

        private readonly ILogger logger;
        private readonly IMobileAppTokenHandler tokenUtility;

        /// <summary>
        /// Initializes a new instance of the <see cref="MobileAppAuthenticationHandler"/> class with the given <paramref name="logger"/>.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to use for logging.</param>
        /// <param name="tokenHandler">The <see cref="IMobileAppTokenHandler"/> to use.</param>
        public MobileAppAuthenticationHandler(ILogger logger, IMobileAppTokenHandler tokenHandler)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            if (tokenHandler == null)
            {
                throw new ArgumentNullException("tokenHandler");
            }

            this.logger = logger;
            this.tokenUtility = tokenHandler;
        }

        protected override Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            return Task.FromResult(this.Authenticate(this.Request, this.Options));
        }

        protected virtual AuthenticationTicket Authenticate(IOwinRequest request, MobileAppAuthenticationOptions options)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            ClaimsIdentity authenticatedIdentity = null;
            try
            {
                bool signingKeyExists = !string.IsNullOrEmpty(options.SigningKey);
                if (signingKeyExists)
                {
                    // Validate the token.
                    authenticatedIdentity = this.ValidateIdentity(request, options);
                }
                else
                {
                    // We can't validate without the signing key.
                    throw new InvalidOperationException(RResources.Authentication_MissingSigningKey);
                }
            }
            catch (Exception ex)
            {
                // An exception occurred. Ensure, we do not return an authenticated identity.
                this.logger.WriteError(RResources.Authentication_Error.FormatForUser(ex.Message), ex);
            }

            return this.CreateAuthenticationTicket(authenticatedIdentity);
        }

        protected virtual AuthenticationTicket CreateAuthenticationTicket(ClaimsIdentity identity)
        {
            if (identity == null)
            {
                // If we don't return a new ClaimsIdentity, it will cause request.User to be null.
                identity = new ClaimsIdentity();
            }

            return new AuthenticationTicket(identity, null);
        }

        /// <summary>
        /// Authenticates the login token from the <see cref="IOwinRequest"/> header, if it exists, and 
        /// returns a <see cref="ClaimsIdentity"/> if authentication succeeded, or false if 
        /// authentication failed. If token parsing failed, returns null.
        /// </summary>
        /// <param name="request">The <see cref="IOwinRequest"/> to authenticate.</param>
        /// <param name="options">Authentication options.</param>
        /// <returns>Returns the <see cref="ClaimsIdentity"/> if token validation succeeded.
        /// Returns null if token parsing failed for any reason.</returns>
        protected virtual ClaimsIdentity ValidateIdentity(IOwinRequest request, MobileAppAuthenticationOptions options)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            string hostname = request.Uri.GetLeftPart(UriPartial.Authority) + "/";

            bool tokenHeaderExists = request.Headers.ContainsKey(AuthenticationHeaderName);
            if (!tokenHeaderExists)
            {
                return null;
            }

            string tokenFromHeader = request.Headers.Get(AuthenticationHeaderName);

            // Attempt to parse and validate the token from header
            ClaimsPrincipal claimsPrincipalFromToken;
            bool claimsAreValid = this.tokenUtility.TryValidateLoginToken(tokenFromHeader, options.SigningKey, hostname, hostname, out claimsPrincipalFromToken);
            if (claimsAreValid)
            {
                return claimsPrincipalFromToken.Identity as ClaimsIdentity;
            }
            else
            {
                this.logger.WriteInformation(RResources.Authentication_InvalidToken);
                return null;
            }
        }
    }
}
