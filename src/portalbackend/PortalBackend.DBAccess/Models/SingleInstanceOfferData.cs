namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;

public record SingleInstanceOfferData(
    Guid CompanyId,
    string? OfferName,
    string? Bpn,
    string? InternalClientId,
    Guid InstanceSetupId,
    Guid ClientId);