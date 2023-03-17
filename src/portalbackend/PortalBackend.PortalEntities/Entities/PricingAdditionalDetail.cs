using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Base;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortalBackend.PortalEntities.Entities
{
    public class PricingAdditionalDetail : IBaseEntity
    {
        private PricingAdditionalDetail()
        {
            Model = null!;
            Description = null!;
            FreeTrial = null!;
            FreeVersion = null!;
            Weblink = null!;
        }

        public PricingAdditionalDetail(Guid id, decimal amount, string model, string description, string freetrial, string freeversion, string weblink,Guid offerId) : this()
        {
            Id = id;
            Amount = amount;
            Model = model;
            Description = description;
            FreeTrial = freetrial;
            FreeVersion = freeversion;
            Weblink = weblink;
            OfferId = offerId;
        }
        public Guid Id { get; private set; }
        
        public decimal Amount{ get; set; }

        [MaxLength(300)]
        public string? Model { get; set; }

        [MaxLength(800)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? FreeTrial { get; set; }

        [MaxLength(100)]
        public string? FreeVersion { get; set; }
        
        [MaxLength(300)]
        public string? Weblink { get; set; }

        /// <summary>
        /// ID of the apps subscribed by a company.
        /// </summary>
        public Guid OfferId { get; set; }

        // Navigation properties
        public virtual Offer? Offer { get; set; }

        public virtual ICollection<Plans> Plans { get; private set; }
    }
}
