using System.Collections.Generic;

namespace FlyingAcorn.Soil.Advertisement.Models
{
    public class AdGroup
    {
        public string id;
        public string name;
        public string impression_url;
        public string click_url;
        public List<Ad> ads;
    }
}