using System.ComponentModel.DataAnnotations;

namespace Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Models
{
    public class AppKeyFeatureData
    {
        public AppKeyFeatureData(string title, string shortdescription, int sequence)
        {
           //Id = id;
            Title = title;
            ShortDescription = shortdescription;
            Sequence = sequence;
           //FeaturesId = featuresId;
        }
        //public Guid Id { get; set; }
        //public Guid FeaturesId { get; set; }

        [MaxLength(300)]
        public string Title { get; set; }

        [MaxLength(500)]
        public string? ShortDescription { get; set; }

        public int Sequence { get; set; }
    }
}
