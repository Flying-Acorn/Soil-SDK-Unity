// ReSharper disable InconsistentNaming

using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Core.Models
{
    /* json schema:
     {
           "username": "username",
           "uuid": "uuid",
           "name": "DentalGuy53",
           "app": "NumberChain",
           "app_id": "uuid",
           "bundle": "com.test1.sa",
           "current_build": "4.0.3",
           "created_at": "2024-09-17 09:41:28.697506+00:00",
           "country": "FI",
           "properties": {
               "flyingacorn_platform": "Android",
               "flyingacorn_version": "4.0.3",
               "flyingacorn_build": "164",
               "flyingacorn_store_name": "AppStore",
               "flyingacorn_package": "com.test1.sa"
           }
       }
     */
    [UsedImplicitly]
    public abstract class PlayerInfo
    {
        public string username;
        public string uuid;
        public string name;
        
        public string app;
        public string app_id;
        public string bundle;
        
        public string country;
        public string created_at;
        public string current_build;
        public Properties properties;

        public abstract class Properties
        {
            public string flyingacorn_build;
            public string flyingacorn_package;
            public string flyingacorn_platform;
            public string flyingacorn_store_name;
            public string flyingacorn_version;
        }
    }
}