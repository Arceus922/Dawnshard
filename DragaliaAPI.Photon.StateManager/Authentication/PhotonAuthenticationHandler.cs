﻿using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DragaliaAPI.Photon.StateManager.Authentication;

public class PhotonAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public PhotonAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock
    )
        : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (
            !AuthenticationHeaderValue.TryParse(
                this.Request.Headers.Authorization,
                out AuthenticationHeaderValue? authenticationHeader
            )
        )
        {
            Logger.LogDebug("Failed to parse Authorization header.");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (authenticationHeader.Parameter is null)
        {
            this.Logger.LogDebug("AuthenticationHeader.Parameter was null");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string configuredToken =
            Environment.GetEnvironmentVariable("PHOTON_TOKEN")
            ?? throw new ArgumentNullException("PHOTON_TOKEN environment variable not set!");

        if (authenticationHeader.Parameter != configuredToken)
        {
            this.Logger.LogInformation(
                "AuthenticationHeader.Parameter value {param} did not match configured token.",
                authenticationHeader.Parameter
            );
            return Task.FromResult(AuthenticateResult.Fail("Invalid token."));
        }

        ClaimsIdentity identity = new(this.Scheme.Name);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, this.Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
