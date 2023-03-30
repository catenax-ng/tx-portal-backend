namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;

public class TechnicalUserProfileAssignedUserRole
{
    private TechnicalUserProfileAssignedUserRole()
    {
    }

    public TechnicalUserProfileAssignedUserRole(Guid technicalUserProfileId, Guid userRoleId)
        :this()
    {
        TechnicalUserProfileId = technicalUserProfileId;
        UserRoleId = userRoleId;
    }
    
    public Guid TechnicalUserProfileId { get; }
    public Guid UserRoleId { get; }

    public virtual TechnicalUserProfile? TechnicalUserProfile { get; set; }
    public virtual UserRole? UserRole { get; set; }
}