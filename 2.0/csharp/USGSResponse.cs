using System.Collections.Generic;

namespace ExternalScalerSample
{
    public class USGSResponse
    {
        public IEnumerable<USGSFeature> features { get; set; }
    }

    public class USGSFeature
    {
        public USGSProperties properties { get; set; }
    }

    public class USGSProperties
    {
        public double mag { get; set; }
    }
}