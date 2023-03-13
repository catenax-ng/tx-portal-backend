namespace PortalBackend.DBAccess.Models;

public record OfferDocumentContentData(
    bool IsValidDocumentType,
    bool IsDocumentLinkedToOffer,
    bool IsValidOfferType,
    bool IsInactive,
    byte[]? Content,
    string FileName,
    string MimeType
);