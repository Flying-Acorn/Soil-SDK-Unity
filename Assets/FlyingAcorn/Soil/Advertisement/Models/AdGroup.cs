using System.Collections.Generic;

namespace FlyingAcorn.Soil.Advertisement.Models
{
    public class AdGroup
    {
        public string id;
        public string name;
        public string impression_url;
        public string click_url;
        public List<Ad> image_ads;
        public List<Ad> video_ads;
        
        /// <summary>
        /// Gets all ads (both image and video) for backward compatibility
        /// </summary>
        public List<Ad> ads
        {
            get
            {
                var allAds = new List<Ad>();
                if (image_ads != null) allAds.AddRange(image_ads);
                if (video_ads != null) allAds.AddRange(video_ads);
                return allAds;
            }
        }
    }
}