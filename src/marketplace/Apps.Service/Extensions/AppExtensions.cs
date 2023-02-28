using Org.Eclipse.TractusX.Portal.Backend.Apps.Service.ViewModels;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;

namespace Apps.Service.Extensions;

/// <summary>
/// Extension methods for the apps
/// </summary>
public static class AppExtensions
{
    /// <summary>
    /// Validates the app user role data
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="appUserRolesDescription"></param>
    /// <exception cref="ControllerArgumentException"></exception>
    public static void ValidateAppUserRole(Guid appId, IEnumerable<AppUserRole> appUserRolesDescription)
    {
        if (appId == Guid.Empty)
        {
            throw new ControllerArgumentException("AppId must not be empty");
        }
        var descriptions = appUserRolesDescription.SelectMany(x => x.descriptions).Where(item => !string.IsNullOrWhiteSpace(item.languageCode)).Distinct();
        if (!descriptions.Any())
        {
            throw new ControllerArgumentException("Language Code must not be empty");
        }
    }

}