using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Messaging.Events;
using Readarr.Http.Extensions;

namespace NzbDrone.Http.Authentication
{
    public class UiAuthorizationHandler : AuthorizationHandler<BypassableDenyAnonymousAuthorizationRequirement>, IAuthorizationRequirement, IHandle<ConfigSavedEvent>
    {
        private readonly IConfigFileProvider _configService;
        private static AuthenticationRequiredType _authenticationRequired;
        private static AuthenticationType _authenticationMethod;

        public UiAuthorizationHandler(IConfigFileProvider configService)
        {
            _configService = configService;
            _authenticationRequired = configService.AuthenticationRequired;
            _authenticationMethod = configService.AuthenticationMethod;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, BypassableDenyAnonymousAuthorizationRequirement requirement)
        {
            if (_authenticationMethod == AuthenticationType.None)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            if (_authenticationRequired == AuthenticationRequiredType.DisabledForLocalAddresses)
            {
                if (context.Resource is HttpContext httpContext &&
                    IPAddress.TryParse(httpContext.GetRemoteIP(), out var ipAddress))
                {
                    if (ipAddress.IsLocalAddress() ||
                        (_configService.TrustCgnatIpAddresses && ipAddress.IsCgnatIpAddress()))
                    {
                        context.Succeed(requirement);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public void Handle(ConfigSavedEvent message)
        {
            _authenticationRequired = _configService.AuthenticationRequired;
            _authenticationMethod = _configService.AuthenticationMethod;
        }
    }
}
