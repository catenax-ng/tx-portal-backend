namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;

public class AppInstanceAssignedCompanyServiceAccount
{
    private AppInstanceAssignedCompanyServiceAccount()
    {
    }

    public AppInstanceAssignedCompanyServiceAccount(Guid appInstanceId, Guid companyServiceAccountId)
    {
        AppInstanceId = appInstanceId;
        CompanyServiceAccountId = companyServiceAccountId;
    }
    
    public Guid AppInstanceId { get; private set; }

    public Guid CompanyServiceAccountId { get; private set; }

    public virtual AppInstance? AppInstance { get; set; }

    public virtual CompanyServiceAccount? CompanyServiceAccount { get; set; }
}