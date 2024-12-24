using System;
using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Analytics;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    public static class LinkingPlayerPrefs
    {
        private static readonly string LinksKey = $"{UserPlayerPrefs.GetKeysPrefix()}links";

        internal static void AddLink(LinkPostResponse link)
        {
            var links = Links;
            if (links.Any(l => l.detail.app_party == link.detail.app_party))
            {
                MyDebug.LogWarning($"Link already exists for {link.detail.app_party}");
            }
            else
            {
                MyDebug.Info($"Link tracked for {link.detail.app_party.id}");
                links.Add(link);
                Links = links;
            }
        }

        internal static void RemoveLink(Constants.ThirdParty settingsThirdParty)
        {
            var links = Links;
            links.RemoveAll(l => l.detail.app_party.party == settingsThirdParty.ToString());
        }

        public static List<LinkPostResponse> Links
        {
            get
            {
                var linksString = PlayerPrefs.GetString(LinksKey, string.Empty);
                try
                {
                    return string.IsNullOrEmpty(linksString)
                        ? new List<LinkPostResponse>()
                        : JsonConvert.DeserializeObject<List<LinkPostResponse>>(linksString);
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning($"Failed to parse links from PlayerPrefs: {e.Message} - {linksString}");
                    return new List<LinkPostResponse>();
                }
            }

            internal set
            {
                try
                {
                    PlayerPrefs.SetString(LinksKey, JsonConvert.SerializeObject(value));
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning($"Failed to save links to PlayerPrefs: {e.Message}");
                }
            }
        }
    }
}