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
    public class KeyFeatures : IBaseEntity
    {
        private KeyFeatures()
        {
            Title = null!;
            ShortDescription = null!;
        }

        public KeyFeatures(Guid id, string title, string shortdescription, int sequence) : this()
        {
            Id = id;
            Title = title;
            ShortDescription = shortdescription;
            Sequence = sequence;
        }
        public Guid Id { get; private set; }

        [MaxLength(300)]
        public string? Title { get; set; }

        [MaxLength(500)]
        public string? ShortDescription { get; set; }

        public int Sequence { get; set; }
        // Navigation properties
        public virtual Features? Features { get; set; }
    }
}
