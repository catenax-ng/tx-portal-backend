using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library.Custodian.Models;

namespace Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;

public interface ICustodianBusinessLogic
{
    Task<string> CreateWalletAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<WalletData> GetWalletByBpnAsync(string bpn, CancellationToken cancellationToken);
}