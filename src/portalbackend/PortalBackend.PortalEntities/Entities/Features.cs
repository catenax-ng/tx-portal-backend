using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Base;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using System.ComponentModel.DataAnnotations;

namespace PortalBackend.PortalEntities.Entities
{
    public class Features : IBaseEntity
    {
        private Features()
        {
            Summary = null!;
            VideoLink = null!;
        }

        public Features(Guid id, string summary, string vidolink, Guid offerId) : this()
        {
            Id = id;
            Summary = summary;
            VideoLink = vidolink;
            OfferId = offerId;
        }       

        public Guid Id { get; private set; }

        [MaxLength(500)]
        public string? Summary { get; set; }

        [MaxLength(500)]
        public string? VideoLink { get; set; }
        // Navigation properties
        public Guid OfferId { get;  set; }
        public virtual Offer? Offer { get; set; }

        public virtual ICollection<KeyFeatures> KeyFeatures { get; private set; }
    }

}
