using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;

namespace PortalBackend.PortalEntities.Entities
{
    public class OfferRecommandation : IBaseEntity
    {
        private OfferRecommandation()
        {
            Offer = null!;
            CompanyUser = null!;
        }

        public OfferRecommandation(Guid id, Offer offer, CompanyUser companyUser) : this()
        {
            Id = id;
            Offer = offer;
            CompanyUser = companyUser;
        }
        public Guid Id { get; private set; }

       
        // Navigation properties
        public virtual Offer? Offer { get; set; }

         // Navigation properties
        public virtual CompanyUser? CompanyUser { get; set; }
    }
}
