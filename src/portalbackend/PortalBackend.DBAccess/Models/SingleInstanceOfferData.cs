namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;

public record SingleInstanceOfferData(
    Guid CompanyId,
    string? OfferName,
    string? Bpn,
    bool IsSingleInstance,
    IEnumerable<string> InternalClientIds,
    IEnumerable<Guid> InstanceIds
);
