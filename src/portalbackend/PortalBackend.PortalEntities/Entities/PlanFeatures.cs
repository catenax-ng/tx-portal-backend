using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortalBackend.PortalEntities.Entities
{
    public class PlanFeatures : IBaseEntity
    {
        private PlanFeatures()
        {
            Name = null!;
        }

        public PlanFeatures(Guid id, string name, bool isenable) : this()
        {
            Id = id;
            Name = name;
            IsEnable = isenable;
        }
        public Guid Id { get; private set; }

        [MaxLength(300)]
        public string? Name { get; set; }
        public bool IsEnable { get; set; }
        // Navigation properties
        public virtual Plans? Plans { get; set; }
    }
}
