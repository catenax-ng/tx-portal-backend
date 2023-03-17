using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Base;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortalBackend.PortalEntities.Entities
{
    public class Plans : IBaseEntity
    {
        private Plans()
        {
            Type = AppPriceCategory.PER_MONTH;
            Currency = null!;
            Model = null!;
            Frequency = null!;
            Description = null!;
        }

        public Plans(Guid id, AppPriceCategory type, decimal amount, string currency, string model, string frequency, string description, int sequence) : this()
        {
            Id = id;
            Type = type;
            Amount = amount;
            Currency = currency;
            Model = model;
            Frequency = frequency;
            Description = description;
            Sequence = sequence;
        }
        public Guid Id { get; private set; }

        [MaxLength(300)]
        public AppPriceCategory Type { get; set; }
        public decimal Amount { get; set; }       

        [MaxLength(100)]
        public string? Currency { get; set; }

        [MaxLength(100)]
        public string? Model { get; set; }

        [MaxLength(100)]
        public string? Frequency { get; set; }

        [MaxLength(800)]
        public string? Description { get; set; }
        public int Sequence { get; set; }
        // Navigation properties
        public virtual PricingAdditionalDetail? PricingAdditionalDetails { get; set; }

        public virtual ICollection<PlanFeatures> PlanFeatures { get; private set; }

    }
}
