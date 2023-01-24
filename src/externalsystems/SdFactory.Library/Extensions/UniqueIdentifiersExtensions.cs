using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.SdFactory.Library.Extensions;

public static class UniqueIdentifiersExtensions
{
    public static string GetSdUniqueIdentifierValue(this UniqueIdentifierId uniqueIdentifierId) =>
        uniqueIdentifierId switch
        {
            UniqueIdentifierId.COMMERCIAL_REG_NUMBER => "local",
            UniqueIdentifierId.VAT_ID => "vatID",
            UniqueIdentifierId.LEI_CODE => "leiCode",
            UniqueIdentifierId.VIES => "EUID",
            UniqueIdentifierId.EORI => "EORI",
            _ => throw new ArgumentOutOfRangeException(nameof(uniqueIdentifierId), uniqueIdentifierId, null)
        };
}