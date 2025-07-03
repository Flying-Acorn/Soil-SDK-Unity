using System;
using System.Collections.Generic;

namespace FlyingAcorn.Soil.Advertisement.Models
{
    public class Campaign
    {
        public string id;
        public string name;
        public string target_country;
        public DateTime start_date;
        public DateTime end_date;
        public DateTime created;
        public List<AdGroup> ad_groups;
    }
}