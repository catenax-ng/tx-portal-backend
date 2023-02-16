using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Administration.Service.BusinessLogic;

public static class DocumentTypeIdExtensions
{
    public static string GetContentType(this DocumentTypeId documentTypeId) =>
        documentTypeId switch
        {
            DocumentTypeId.SELF_DESCRIPTION => "application/json",
            _ => "application/pdf"
        };
}
