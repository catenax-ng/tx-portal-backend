namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;

public class TechnicalUserProfile
{
    private TechnicalUserProfile()
    {
        Name = null!;
        UserRoles = new HashSet<UserRole>();
    }

    public TechnicalUserProfile(Guid id, string name, Guid offerId)
        : this()
    {
        Id = id;
        Name = name;
        OfferId = offerId;
    }

    public Guid Id { get; private set; }

    public string Name { get; set; }

    public Guid OfferId { get; private set; }

    public virtual Offer? Offer { get; set; }

    public ICollection<UserRole> UserRoles { get; set; }
}