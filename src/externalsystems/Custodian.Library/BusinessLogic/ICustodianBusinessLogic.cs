using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library.Custodian.Models;

namespace Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;

public interface ICustodianBusinessLogic
{
    /// <summary>
    /// Creates the wallet for the company of the application
    /// </summary>
    /// <param name="applicationId">Id of the application to create the company for.</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>Returns the response message</returns>
    Task<string> CreateWalletAsync(Guid applicationId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets the wallet data for the given application
    /// </summary>
    /// <param name="applicationId">Application to get the wallet data for</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>Returns the wallet data if existing or null</returns>
    Task<WalletData?> GetWalletByBpnAsync(Guid applicationId, CancellationToken cancellationToken);
}