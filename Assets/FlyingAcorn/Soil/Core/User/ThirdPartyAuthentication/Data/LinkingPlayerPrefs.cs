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

        internal static void RemoveLink(UnlinkResponse unlinkResponse)
        {
            RemoveLink(unlinkResponse.detail.app_party.party);
        }

        internal static void RemoveLink(Constants.ThirdParty party)
        {
            MyDebug.Info($"Removing link for {party}");
            var links = Links;
            links?.RemoveAll(l => l.detail.app_party.party == party);
            Links = links;
            DequeueSilentUnlink(party);
            MyDebug.Verbose($"Link removed for {party}");
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

        public static List<Constants.ThirdParty> SilentUnlinkQueue
        {
            get
            {
                var silentUnlinksString =
                    PlayerPrefs.GetString($"{UserPlayerPrefs.GetKeysPrefix()}silent_unlink", string.Empty);
                try
                {
                    return string.IsNullOrEmpty(silentUnlinksString)
                        ? new List<Constants.ThirdParty>()
                        : JsonConvert.DeserializeObject<List<Constants.ThirdParty>>(silentUnlinksString);
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning(
                        $"Failed to parse silent unlinks from PlayerPrefs: {e.Message} - {silentUnlinksString}");
                    return new List<Constants.ThirdParty>();
                }
            }

            private set
            {
                try
                {
                    PlayerPrefs.SetString($"{UserPlayerPrefs.GetKeysPrefix()}silent_unlink",
                        JsonConvert.SerializeObject(value));
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning($"Failed to save silent unlinks to PlayerPrefs: {e.Message}");
                }
            }
        }

        private static string ThirdPartyPrefix => $"{UserPlayerPrefs.GetKeysPrefix()}thirdparty_";

        internal static string GetUserId(Constants.ThirdParty thirdParty)
        {
            return PlayerPrefs.GetString(ThirdPartyPrefix + thirdParty, "");
        }

        internal static void SetUserId(Constants.ThirdParty thirdParty, string userId)
        {
            PlayerPrefs.SetString(ThirdPartyPrefix + thirdParty, userId);
        }

        public static void EnqueueSilentUnlink(Constants.ThirdParty party)
        {
            var silentUnlinks = SilentUnlinkQueue;
            if (silentUnlinks.Contains(party)) return;
            MyDebug.Info($"Silent unlink tracked for {party}");
            silentUnlinks.Add(party);
            SilentUnlinkQueue = silentUnlinks;
        }

        private static void DequeueSilentUnlink(Constants.ThirdParty party)
        {
            var silentUnlinks = SilentUnlinkQueue;
            if (!silentUnlinks.Contains(party)) return;
            MyDebug.Info($"Silent unlink removed for {party}");
            silentUnlinks.Remove(party);
            SilentUnlinkQueue = silentUnlinks;
        }
    }
}