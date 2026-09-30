using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Advertisement.Logic;
using FlyingAcorn.Soil.Advertisement.Models;
using FlyingAcorn.Soil.Advertisement.Player;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement
{
    /// <summary>
    /// Soil ads. Picks an ad group per format from the Soil backend and caches its files; banner,
    /// interstitial and rewarded ads are then drawn by a native player (Android: MediaPlayer on a
    /// TextureView, iOS: AVPlayer), while native ads are handed to the game to draw in its own UI.
    /// In the Editor a simulated player draws plain placeholders.
    /// </summary>
    public class Advertisement
    {
        /// <summary>
        /// Gets whether the Advertisement service is ready for use.
        /// </summary>
        [UsedImplicitly]
        public static bool Ready => _campaignSelectionSucceeded;

        /// <summary>
        /// Where banners appear. Applied on the next <see cref="ShowAd(AdFormat)"/> of a banner;
        /// showing a visible banner again moves it.
        /// </summary>
        [UsedImplicitly]
        public static AdPosition BannerPosition { get; set; } = AdPosition.BottomCenter;

        /// <summary>
        /// True while an interstitial or rewarded ad covers the game.
        /// </summary>
        [UsedImplicitly]
        public static bool IsFullscreenAdShowing => _slots?.IsFullscreenShowing ?? false;

        private static string AdvertisementBaseUrl => $"{Core.Data.Constants.ApiUrl}/advertisement/";
        private static string AdGroupsUrl => $"{AdvertisementBaseUrl}adgroups/";
        private static string AdGroupsSelectUrl => $"{AdGroupsUrl}select/";
        private static bool _campaignRequested;
        private static bool _isInitializing;
        private static readonly Dictionary<AdFormat, AdGroup> _selectedAdGroups = new();
        private static bool _campaignSelectionSucceeded;
        private static List<AdFormat> _requestedFormats;
        private static UniTask _cachedAssetsTask;

        // Banner, interstitial and rewarded: one slot per format in front of the native player.
        private static AdSlots _slots;
        private static IAdPlayer _player;
        private static SoilAdsNativeReceiver _receiver;
        private static readonly Queue<NativeAdEvent> _deferredPlayerEvents = new();
        private static readonly Dictionary<AdFormat, Ad> _slotAds = new();

        // Native ads have no player: the game renders them itself, so the loaded ad lives here as
        // plain content plus the click handlers attached to the game's own views while it is on
        // screen.
        private static NativeAdContent _nativeAdContent;
        // One native ad can be rendered in several places at once (the native banner, a
        // leaderboard row). Click handlers are tracked PER registered view set, so showing the ad
        // in a second place does not unbind the first place's taps, and hiding one place does not
        // silence the other.
        private static readonly Dictionary<NativeAdReferences, List<SoilNativeAdClickHandler>>
            _nativeAdClickHandlers = new();

        // Rewarded ad cooldown tracking
        private static DateTime _lastRewardedAdShownTime = DateTime.MinValue;
        private static readonly float RewardedAdCooldownSeconds = 10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _campaignRequested = false;
            _isInitializing = false;
            _selectedAdGroups.Clear();
            _campaignSelectionSucceeded = false;
            _requestedFormats = null;
            _slots = null;
            _player = null;
            _receiver = null;
            _deferredPlayerEvents.Clear();
            _slotAds.Clear();
            _nativeAdContent = null;
            _nativeAdClickHandlers.Clear();
            _lastRewardedAdShownTime = DateTime.MinValue;
        }

        /// <summary>
        /// Initializes the Advertisement service with the desired ad formats. Consider conditional initialization based on user preferences or purchases.
        /// </summary>
        /// <param name="adFormats">List of ad formats to initialize (banner, interstitial, rewarded, native).</param>
        public static void InitializeAsync(List<AdFormat> adFormats)
        {
            if (adFormats == null || adFormats.Count == 0)
            {
                Events.InvokeOnInitializeFailed("No ad formats specified for initialization");
                return;
            }

            if (_selectedAdGroups.Count > 0)
                return;

            if (_isInitializing)
                return;

            _isInitializing = true;
            _requestedFormats = adFormats.Distinct().ToList();

            EnsurePlayerRuntime();
            foreach (var format in _requestedFormats)
                SlotFor(format)?.BeginCaching();

            // Start loading cached assets in background; we'll await inside the success handler
            _cachedAssetsTask = AssetCache.LoadCachedAssetsAsync();

            // If SoilServices already ready, proceed immediately
            if (SoilServices.Ready)
            {
                HandleServicesReadyAsync(_cachedAssetsTask).Forget();
                return;
            }

            // Otherwise subscribe to services events
            UnlistenCore();
            SoilServices.OnInitializationFailed += SoilInitFailed;
            SoilServices.OnServicesReady += SoilInitSuccess;

            // Trigger services initialization if not already started
            SoilServices.InitializeAsync();
        }

        private static void UnlistenCore()
        {
            SoilServices.OnInitializationFailed -= SoilInitFailed;
            SoilServices.OnServicesReady -= SoilInitSuccess;
        }

        private static void SoilInitFailed(Exception exception)
        {
            UnlistenCore();
            _isInitializing = false;
            _campaignRequested = false;
            FailCaching(NativeAdErrors.Network);
            Events.InvokeOnInitializeFailed(exception?.Message ?? "Initialization failed");
        }

        private static async void SoilInitSuccess()
        {
            // Now we can pass the stored cached assets task
            await HandleServicesReadyAsync(_cachedAssetsTask);
        }

        private static async UniTask HandleServicesReadyAsync(UniTask cachedAssetsTask)
        {
            UnlistenCore();

            // Await cached assets task (ignore failures) without unnecessary null check
            try { await cachedAssetsTask; } catch { /* ignore cached asset load failures */ }

            if (_campaignRequested)
            {
                _isInitializing = false;
                return;
            }

            _campaignRequested = true;
            _campaignSelectionSucceeded = false; // reset before attempt
            _selectedAdGroups.Clear();

            // Select formats sequentially (not in parallel): each selection is added to this round's
            // exclusion set immediately, so the next format's request prefers a different ad group
            // (and thus a different advertised app). Frequency capping is best-effort - see
            // SelectAdGroupWithFrequencyCapAsync - so limited inventory falls back to showing an ad
            // rather than starving. A single format's failure is logged and skipped rather than
            // aborting the whole round, since the other formats' requests are independent - but if
            // every single format failed with an exception (e.g. total network outage), that's
            // reported as an init failure rather than silently "no ads available".
            var recentAdGroups = AdvertisementPlayerPrefs.RecentAdGroupIds;
            var recentCampaigns = AdvertisementPlayerPrefs.RecentCampaignIds;
            var roundAdGroups = new List<string>();
            var roundCampaigns = new List<string>();
            Exception lastException = null;
            var failureCount = 0;
            foreach (var format in _requestedFormats)
            {
                try
                {
                    var adGroup = await SelectAdGroupWithFrequencyCapAsync(
                        format, recentAdGroups, recentCampaigns, roundAdGroups, roundCampaigns);
                    if (adGroup == null) continue;

                    _selectedAdGroups[format] = adGroup;

                    // Exclude this app from the remaining formats in THIS round to avoid duplicates.
                    if (!string.IsNullOrEmpty(adGroup.id)) roundAdGroups.Add(adGroup.id);
                    if (!string.IsNullOrEmpty(adGroup.campaign_id)) roundCampaigns.Add(adGroup.campaign_id);

                    // Persist for cross-session variety preference.
                    AdvertisementPlayerPrefs.RecordShownAdGroup(adGroup);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    lastException = ex;
                    MyDebug.LogWarning($"Failed to select ad group for format {format}: {ex.Message}");
                }
            }

            if (failureCount == _requestedFormats.Count && lastException != null)
            {
                _campaignRequested = false;
                _isInitializing = false;
                _campaignSelectionSucceeded = false;
                FailCaching(NativeAdErrors.Network);
                Events.InvokeOnInitializeFailed($"Failed to select ad groups: {lastException.Message}");
                return;
            }

            _campaignSelectionSucceeded = true; // success path, regardless of per-format availability

            // Formats the server had nothing for answer their pending LoadAd calls with NoFill.
            foreach (var format in _requestedFormats.Where(f => !_selectedAdGroups.ContainsKey(f)))
                SetSlotCreative(format, null, null);

            if (_selectedAdGroups.Count == 0)
            {
                await ClearAssetCacheAsync();
                AdvertisementPlayerPrefs.CachedAdGroups = new Dictionary<AdFormat, AdGroup>();
                _campaignRequested = false;
                _isInitializing = false;
                Events.InvokeOnInitialized();
                return;
            }

            Events.InvokeOnInitialized();
            // Start asset caching in background - don't block initialization on this
            CacheAds().Forget();
            _campaignRequested = false;
            _isInitializing = false;
        }

        #region Player runtime

        private static void EnsurePlayerRuntime()
        {
            SoilAdManager.GetOrCreate();
            if (_slots != null && _receiver) return;

            _receiver = SoilAdsNativeReceiver.GetOrCreate();
            _receiver.MessageReceived += json => OnPlayerEvent(NativeAdEvent.Parse(json));
            _receiver.Ticked += TickPlayerRuntime;

            // A player call that fails synchronously is answered on the next frame, like a real
            // player event, so state changes never re-enter the slot that made the call.
            void Defer(NativeAdEvent e) => _deferredPlayerEvents.Enqueue(e);

#if UNITY_ANDROID && !UNITY_EDITOR
            _player = new AndroidAdPlayer(SoilAdsNativeReceiver.ObjectName, SoilAdsNativeReceiver.MethodName, Defer);
#elif UNITY_IOS && !UNITY_EDITOR
            _player = new IosAdPlayer(SoilAdsNativeReceiver.ObjectName, SoilAdsNativeReceiver.MethodName, Defer);
#else
            _player = new EditorAdPlayer(_receiver, OnPlayerEvent);
#endif

            _slots = new AdSlots(_player, IsRewardedAdInCooldown);
            foreach (var slot in _slots.All)
            {
                var format = ToAdFormat(slot.Format);
                slot.Notice += (notice, error) => OnSlotNotice(format, notice, error);
            }
        }

        private static void TickPlayerRuntime()
        {
            var count = _deferredPlayerEvents.Count;
            for (var i = 0; i < count; i++)
                OnPlayerEvent(_deferredPlayerEvents.Dequeue());
            _slots?.Tick();
        }

        private static void OnPlayerEvent(NativeAdEvent e)
        {
            if (e == null || _slots == null) return;
            MyDebug.Verbose($"[Advertisement] Player: {e.Format} {e.Type} {e.Media} {e.Error} {e.Message}");
            _slots.HandleEvent(e);
        }

        private static AdSlot SlotFor(AdFormat format) => _slots?[format.ToString()];

        private static void FailCaching(string error)
        {
            if (_requestedFormats == null) return;
            foreach (var format in _requestedFormats)
                SlotFor(format)?.CachingFailed(error);
        }

        private static void SetSlotCreative(AdFormat format, AdCreative creative, Ad ad)
        {
            var slot = SlotFor(format);
            if (slot == null) return;
            if (ad != null) _slotAds[format] = ad;
            slot.SetCreative(creative);
        }

        private static void OnSlotNotice(AdFormat format, AdSlotNotice notice, string error)
        {
            _slotAds.TryGetValue(format, out var ad);
            var data = new AdEventData(format, error == null ? AdError.None : ToAdError(error)) { ad = ad };

            switch (notice)
            {
                case AdSlotNotice.Loaded:
                    InvokeFormatEvent(format, Events.InvokeOnBannerAdLoaded, Events.InvokeOnInterstitialAdLoaded,
                        Events.InvokeOnRewardedAdLoaded, data);
                    break;
                case AdSlotNotice.LoadFailed:
                case AdSlotNotice.ShowFailed:
                    MyDebug.Verbose($"[Advertisement] {format} {notice}: {error}");
                    InvokeAdErrorEvent(format, data);
                    break;
                case AdSlotNotice.Shown:
                    InvokeFormatEvent(format, Events.InvokeOnBannerAdShown, Events.InvokeOnInterstitialAdShown,
                        Events.InvokeOnRewardedAdShown, data);
                    break;
                case AdSlotNotice.Clicked:
                    InvokeFormatEvent(format, Events.InvokeOnBannerAdClicked, Events.InvokeOnInterstitialAdClicked,
                        Events.InvokeOnRewardedAdClicked, data);
                    break;
                case AdSlotNotice.Rewarded:
                    Events.InvokeOnRewardedAdRewarded(data);
                    break;
                case AdSlotNotice.Closed:
                    if (format == AdFormat.rewarded)
                        SetRewardedAdCooldown();
                    InvokeFormatEvent(format, Events.InvokeOnBannerAdClosed, Events.InvokeOnInterstitialAdClosed,
                        Events.InvokeOnRewardedAdClosed, data);
                    break;
            }
        }

        private static void InvokeFormatEvent(AdFormat format, Action<AdEventData> banner,
            Action<AdEventData> interstitial, Action<AdEventData> rewarded, AdEventData data)
        {
            switch (format)
            {
                case AdFormat.banner: banner(data); break;
                case AdFormat.interstitial: interstitial(data); break;
                case AdFormat.rewarded: rewarded(data); break;
            }
        }

        private static AdError ToAdError(string error)
        {
            return error switch
            {
                NativeAdErrors.NoFill => AdError.NoFill,
                NativeAdErrors.InvalidCreative => AdError.NoFill,
                NativeAdErrors.NotLoaded => AdError.AdNotReady,
                NativeAdErrors.AlreadyShowing => AdError.InvalidRequest,
                NativeAdErrors.InvalidFormat => AdError.InvalidRequest,
                NativeAdErrors.Network => AdError.NetworkError,
                NativeAdErrors.MediaUnreadable => AdError.InternalError,
                NativeAdErrors.NoHost => AdError.InternalError,
                NativeAdErrors.Internal => AdError.InternalError,
                _ => AdError.Unknown
            };
        }

        private static AdFormat ToAdFormat(string format) =>
            Enum.TryParse<AdFormat>(format, out var adFormat) ? adFormat : AdFormat.banner;

        private static string ToBannerPosition(AdPosition position) => position switch
        {
            AdPosition.TopCenter => BannerPositions.Top,
            AdPosition.MiddleCenter => BannerPositions.Center,
            _ => BannerPositions.Bottom
        };

        #endregion

        #region Testing hooks (device end-to-end tests in NativeAds/unity-e2e)

        /// <summary>Replaces the fullscreen lock defaults, e.g. to keep device tests short.</summary>
        internal static FullscreenShowOptions FullscreenOptionsOverride { get; set; }

        internal static IAdPlayer PlayerForTesting => _player;

        /// <summary>Hands local files straight to the player, without the ad server or the cache.</summary>
        internal static void PrepareForTesting(AdFormat format, AdCreative creative)
        {
            EnsurePlayerRuntime();
            _slotAds.Remove(format);
            SetSlotCreative(format, creative, creative == null ? null : new Ad { id = creative.AdId, format = format.ToString() });
        }

        #endregion

        // Downloads and caches ads for each format's selected ad group.
        // This method caches each format separately and prepares each format as soon as it is ready.
        private static async UniTask CacheAds()
        {
            var cachedAdGroups = AdvertisementPlayerPrefs.CachedAdGroups;
            var updatedCachedAdGroups = new Dictionary<AdFormat, AdGroup>(cachedAdGroups);
            var cachingTasks = new List<UniTask>();

            foreach (var (adFormat, adGroup) in _selectedAdGroups)
            {
                // Only re-cache if the ad group selected for this format actually changed, AND we still
                // have cached assets for it (they may have been evicted by ClearOldAssetsAsync/RemoveCachedAsset
                // even though the AdGroup pointer itself didn't change) - otherwise the player would
                // be handed an empty creative.
                bool isSameAdGroupStillCached = cachedAdGroups.TryGetValue(adFormat, out var previousAdGroup)
                    && previousAdGroup?.id == adGroup.id
                    && AssetCache.GetCachedAssets(adFormat).Any(a => a.IsValid);

                if (!isSameAdGroupStillCached)
                {
                    cachingTasks.Add(CacheFormatAssetsAsync(adGroup, adFormat));
                }
                else
                {
                    // Assets already cached from a previous session; just prepare the format.
                    OnFormatAssetsReady(adFormat);
                }

                updatedCachedAdGroups[adFormat] = adGroup;
            }

            AdvertisementPlayerPrefs.CachedAdGroups = updatedCachedAdGroups;

            MyDebug.Verbose($"Caching assets for formats: {string.Join(", ", _selectedAdGroups.Keys)}");

            // Wait for all formats to complete (though each will fire events individually)
            try
            {
                await UniTask.WhenAll(cachingTasks);
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Error during asset caching: {ex.Message}");
            }
        }

        /// <summary>
        /// Caches assets for a specific ad format and marks them ready when done
        /// </summary>
        private static async UniTask CacheFormatAssetsAsync(AdGroup adGroup, AdFormat adFormat)
        {
            try
            {
                await AssetCache.ClearFormatCacheAsync(adFormat);
                await AssetCache.CacheAssetsForAdGroupAsync(adGroup, adFormat, OnFormatAssetsReady);
            }
            catch (Exception ex)
            {
                MyDebug.LogWarning($"[Advertisement] Caching {adFormat} failed: {ex.Message}");
                SlotFor(adFormat)?.CachingFailed(NativeAdErrors.Network);
            }
        }

        /// <summary>
        /// Called when assets for a specific format are cached: builds the ad and hands it to the
        /// player (or, for native ads, builds the content the game renders).
        /// </summary>
        private static void OnFormatAssetsReady(AdFormat adFormat)
        {
            if (adFormat == AdFormat.native)
                LoadNativeAd();
            else
                PrepareFormat(adFormat);

            // Raised after preparing, so a LoadAd from a listener finds the ad already on its way.
            Events.InvokeOnAdFormatAssetsLoaded(adFormat);
        }

        private static void PrepareFormat(AdFormat adFormat)
        {
            var infos = new List<CreativeAssetInfo>();
            foreach (var entry in AssetCache.GetCachedAssets(adFormat))
            {
                if (entry == null || !entry.IsValid) continue;
                CreativeAssetKind kind;
                switch (entry.AssetType)
                {
                    case AssetType.image: kind = CreativeAssetKind.Image; break;
                    case AssetType.video: kind = CreativeAssetKind.Video; break;
                    case AssetType.logo: kind = CreativeAssetKind.Logo; break;
                    default: continue;
                }

                infos.Add(new CreativeAssetInfo
                {
                    Id = entry.Id,
                    Kind = kind,
                    LocalPath = entry.LocalPath,
                    ClickUrl = entry.ClickUrl,
                    AdId = entry.AdId,
                    TitleText = entry.MainHeaderText,
                    DescriptionText = entry.DescriptionText,
                    CallToActionText = entry.ActionButtonText
                });
            }

            var creative = AdCreativeBuilder.Build(adFormat.ToString(), infos, out var error);
            if (creative == null)
                MyDebug.Verbose($"[Advertisement] No {adFormat} ad to prepare: {error}");

            SetSlotCreative(adFormat, creative, creative == null ? null : ToAd(adFormat, creative));
        }

        /// <summary>Describes the prepared ad in the shape events have always carried.</summary>
        private static Ad ToAd(AdFormat format, AdCreative creative)
        {
            Asset Text(string text) => string.IsNullOrEmpty(text)
                ? null
                : new Asset { asset_type = "text", text_content = text, alt_text = text, url = "" };

            Asset File(string id, string type)
            {
                if (string.IsNullOrEmpty(id)) return null;
                var entry = AssetCache.GetCachedAssets(format).FirstOrDefault(a => a.Id == id);
                return new Asset { id = id, asset_type = type, url = entry?.OriginalUrl };
            }

            return new Ad
            {
                id = creative.AdId,
                format = format.ToString(),
                main_header = Text(creative.Title),
                description = Text(creative.Description),
                action_button = new Asset
                {
                    asset_type = "text",
                    text_content = creative.CallToAction,
                    alt_text = creative.CallToAction,
                    url = creative.ClickUrl
                },
                main_image = File(creative.ImageAssetId, AssetType.image.ToString()),
                main_video = File(creative.VideoAssetId, AssetType.video.ToString()),
                logo = File(creative.LogoAssetId, AssetType.logo.ToString())
            };
        }

        private static async UniTask<AdGroupSelectResponse> SelectAdGroupAsync(
            AdFormat adFormat, List<string> previousAdGroups, List<string> previousCampaigns)
        {
            if (!SoilServices.Ready)
                throw new SoilException("Soil services are not ready. Please initialize Soil first.", SoilExceptionErrorCode.NotReady);

            var body = new
            {
                format = adFormat.ToString(),
                previous_ad_groups = previousAdGroups ?? new List<string>(),
                previous_campaigns = previousCampaigns ?? new List<string>()
            };
            var jsonBody = JsonConvert.SerializeObject(body);

            using var request = new UnityWebRequest(AdGroupsSelectUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            var authHeader = Authenticate.GetAuthorizationHeaderString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout * 2);
            }
            catch (SoilException)
            {
                // Preserve specific SoilException types (timeout, transport, ...).
                throw;
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while selecting ad group: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            // A transport-level failure (no network, DNS failure, connection refused, aborted)
            // completes with responseCode 0 and an empty body. Surface it as a TransportError
            // instead of letting it fall through to the status-code branch, which would report
            // the meaningless "Failed to select ad group: 0 - ".
            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                throw new SoilException($"Failed to select ad group: {request.error}", SoilExceptionErrorCode.TransportError);
            }

            // Map non-success status codes
            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                var responseText = request.downloadHandler?.text ?? "No response content";
                var errorCode = (System.Net.HttpStatusCode)request.responseCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => SoilExceptionErrorCode.InvalidToken,
                    System.Net.HttpStatusCode.Forbidden => SoilExceptionErrorCode.Forbidden,
                    System.Net.HttpStatusCode.NotFound => SoilExceptionErrorCode.NotFound,
                    System.Net.HttpStatusCode.BadRequest => SoilExceptionErrorCode.InvalidRequest,
                    System.Net.HttpStatusCode.ServiceUnavailable => SoilExceptionErrorCode.ServiceUnavailable,
                    _ => SoilExceptionErrorCode.TransportError
                };
                throw new SoilException($"Failed to select ad group: {request.responseCode} - {responseText}", errorCode);
            }

            var content = request.downloadHandler?.text ?? string.Empty;
            try
            {
                if (string.IsNullOrEmpty(content))
                    return new AdGroupSelectResponse { ad_group = null, selection_reason = SelectionReason.only_eligible };

                var result = JsonConvert.DeserializeObject<AdGroupSelectResponse>(content);
                return result;
            }
            catch (Exception)
            {
                MyDebug.LogError($"[Advertisement] Failed to parse ad group response. Treating as no available ad group. Raw: {content}");
                return new AdGroupSelectResponse { ad_group = null, selection_reason = SelectionReason.only_eligible };
            }
        }

        /// <summary>
        /// Selects an ad group for a format, applying frequency capping as a best-effort PREFERENCE
        /// rather than a hard filter. The backend hard-excludes previous_ad_groups/previous_campaigns,
        /// so with limited inventory an accumulated exclusion list would starve selection and return
        /// nothing. We therefore try progressively weaker exclusion sets and stop at the first that
        /// yields an ad group:
        ///   1. cross-session recent history + this round's already-selected apps (best variety)
        ///   2. only this round's already-selected apps (still avoids the same app across formats now)
        ///   3. no exclusions (guarantees an ad whenever any eligible inventory exists)
        /// Duplicate exclusion sets are skipped so we never issue the same request twice.
        /// </summary>
        private static async UniTask<AdGroup> SelectAdGroupWithFrequencyCapAsync(
            AdFormat adFormat,
            List<string> recentAdGroups, List<string> recentCampaigns,
            List<string> roundAdGroups, List<string> roundCampaigns)
        {
            var attempts = new List<(List<string> adGroups, List<string> campaigns)>
            {
                (recentAdGroups.Concat(roundAdGroups).Distinct().ToList(),
                 recentCampaigns.Concat(roundCampaigns).Distinct().ToList()),
                (roundAdGroups, roundCampaigns),
                (new List<string>(), new List<string>()),
            };

            var triedSignatures = new HashSet<string>();
            foreach (var (adGroups, campaigns) in attempts)
            {
                // Skip an attempt whose exclusion set is identical to one we already tried.
                var signature = string.Join(",", adGroups.OrderBy(x => x))
                    + "|" + string.Join(",", campaigns.OrderBy(x => x));
                if (!triedSignatures.Add(signature))
                    continue;

                var response = await SelectAdGroupAsync(adFormat, adGroups, campaigns);
                if (response?.ad_group != null)
                    return response.ad_group;
            }

            return null;
        }

        /// <summary>
        /// Checks if rewarded ads are currently in cooldown period
        /// </summary>
        /// <returns>True if in cooldown, false if available</returns>
        public static bool IsRewardedAdInCooldown()
        {
            if (_lastRewardedAdShownTime == DateTime.MinValue)
                return false;

            var timeSinceLastShown = (DateTime.Now - _lastRewardedAdShownTime).TotalSeconds;
            return timeSinceLastShown < RewardedAdCooldownSeconds;
        }

        /// <summary>
        /// Gets the remaining cooldown time for rewarded ads in seconds
        /// </summary>
        /// <returns>Remaining cooldown time in seconds, 0 if no cooldown</returns>
        public static float GetRewardedAdCooldownRemainingSeconds()
        {
            if (_lastRewardedAdShownTime == DateTime.MinValue)
                return 0f;

            var timeSinceLastShown = (DateTime.Now - _lastRewardedAdShownTime).TotalSeconds;
            var remainingTime = RewardedAdCooldownSeconds - timeSinceLastShown;
            return remainingTime > 0 ? (float)remainingTime : 0f;
        }

        /// <summary>
        /// Sets the rewarded ad cooldown timer (called when a rewarded ad is closed)
        /// </summary>
        public static void SetRewardedAdCooldown()
        {
            _lastRewardedAdShownTime = DateTime.Now;
        }

        /// <summary>
        /// Resets the rewarded ad cooldown timer (useful for testing or administrative purposes)
        /// </summary>
        public static void ResetRewardedAdCooldown()
        {
            _lastRewardedAdShownTime = DateTime.MinValue;
        }

        /// <summary>
        /// Helper method to invoke the appropriate error event based on ad format
        /// </summary>
        private static void InvokeAdErrorEvent(AdFormat adFormat, AdEventData errorData)
        {
            switch (adFormat)
            {
                case AdFormat.banner:
                    Events.InvokeOnBannerAdError(errorData);
                    break;
                case AdFormat.interstitial:
                    Events.InvokeOnInterstitialAdError(errorData);
                    break;
                case AdFormat.rewarded:
                    Events.InvokeOnRewardedAdError(errorData);
                    break;
                case AdFormat.native:
                    Events.InvokeOnNativeAdError(errorData);
                    break;
                default:
                    MyDebug.LogError($"Unknown ad format for error event: {adFormat}");
                    break;
            }
        }

        /// <summary>
        /// Shows an ad of the specified format if it is ready. Banners appear at
        /// <see cref="BannerPosition"/>; interstitial and rewarded ads cover the game until closed.
        /// </summary>
        /// <param name="adFormat">The ad format to show (banner, interstitial, rewarded).</param>
        public static void ShowAd(AdFormat adFormat)
        {
            // A native ad needs the game's own views to render into, so it cannot be shown
            // through this format-only entry point.
            if (adFormat == AdFormat.native)
            {
                MyDebug.LogError("[Advertisement] Native ads must be shown via ShowNativeAd(NativeAdReferences).");
                InvokeAdErrorEvent(adFormat, new AdEventData(adFormat, AdError.InvalidRequest));
                return;
            }

            if (_slots == null)
            {
                InvokeAdErrorEvent(adFormat, new AdEventData(adFormat, AdError.AdNotReady));
                return;
            }

            if (adFormat == AdFormat.rewarded && IsRewardedAdInCooldown())
            {
                InvokeAdErrorEvent(adFormat, new AdEventData(adFormat, AdError.AdNotReady));
                return;
            }

            var options = adFormat == AdFormat.banner
                ? BannerPositions.ToJson(ToBannerPosition(BannerPosition))
                : (FullscreenOptionsOverride ?? FullscreenShowOptions.DefaultsFor(adFormat.ToString())).ToJson();
            _slots.Show(adFormat.ToString(), options);
        }

        /// <summary>
        /// Shows a banner at the given position (and remembers it as <see cref="BannerPosition"/>).
        /// </summary>
        [UsedImplicitly]
        public static void ShowBanner(AdPosition position)
        {
            BannerPosition = position;
            ShowAd(AdFormat.banner);
        }

        /// <summary>
        /// Hides an ad of the specified format. Useful for banner ads during gameplay. For an
        /// interstitial or rewarded ad on screen this closes it (no reward unless already earned).
        /// </summary>
        /// <param name="adFormat">The ad format to hide.</param>
        public static void HideAd(AdFormat adFormat)
        {
            if (adFormat == AdFormat.native)
            {
                HideNativeAd();
                return;
            }

            SlotFor(adFormat)?.Hide();
        }

        /// <summary>
        /// Loads an ad for the specified format. Always answered with the format's Loaded or Error
        /// event - later if the ad's files are still downloading. For rewarded ads in cooldown the
        /// Loaded event waits until the cooldown expires.
        /// </summary>
        /// <param name="adFormat">The ad format to load (banner, interstitial, rewarded, native).</param>
        public static void LoadAd(AdFormat adFormat)
        {
            if (adFormat == AdFormat.native)
            {
                LoadNativeAd();
                return;
            }

            var slot = SlotFor(adFormat);
            if (slot == null)
            {
                InvokeAdErrorEvent(adFormat, new AdEventData(adFormat, AdError.AdNotReady));
                return;
            }

            slot.RequestLoad();
        }

        #region Native ads

        /// <summary>
        /// Builds the native ad from the cached native assets and fires OnNativeAdLoaded, or
        /// OnNativeAdError when the cached assets cannot make a complete ad. Called automatically
        /// once native assets finish caching, and by LoadAd(AdFormat.native).
        /// </summary>
        private static void LoadNativeAd()
        {
            var assets = AssetCache.GetCachedAssets(AdFormat.native);
            var model = NativeAdContentBuilder.Build(ToNativeAssetInfo(assets), out var buildError);

            if (model == null)
            {
                _nativeAdContent = null;
                MyDebug.Verbose($"[Advertisement] Native ad not available: {buildError}");
                Events.InvokeOnNativeAdError(new AdEventData(AdFormat.native, ToAdError(buildError)));
                return;
            }

            _nativeAdContent = new NativeAdContent(
                model.AdId,
                model.Title,
                model.Description,
                model.CallToAction,
                model.ClickUrl,
                AssetCache.LoadTexture(model.IconAssetId),
                model.HasMainImage ? AssetCache.LoadTexture(model.MainImageAssetId) : null);

            // The icon is the one image a native layout cannot do without, so a texture that
            // fails to decode makes the ad unusable rather than merely degraded.
            if (_nativeAdContent.Icon == null)
            {
                _nativeAdContent = null;
                MyDebug.LogWarning("[Advertisement] Native ad icon texture could not be loaded.");
                Events.InvokeOnNativeAdError(new AdEventData(AdFormat.native, AdError.InternalError));
                return;
            }

            MyDebug.Verbose($"[Advertisement] Native ad loaded (ad {model.AdId}, image: {_nativeAdContent.HasMainImage})");
            Events.InvokeOnNativeAdLoaded(new AdEventData(AdFormat.native));
        }

        /// <summary>
        /// Projects cached entries onto the Unity-free shape the content builder works with.
        /// Entries that are not native icon/image assets are ignored.
        /// </summary>
        private static List<NativeAdAssetInfo> ToNativeAssetInfo(List<AssetCacheEntry> assets)
        {
            var infos = new List<NativeAdAssetInfo>();
            if (assets == null) return infos;

            foreach (var entry in assets)
            {
                if (entry == null) continue;
                if (entry.AssetType != AssetType.native_icon && entry.AssetType != AssetType.native_image)
                    continue;

                infos.Add(new NativeAdAssetInfo
                {
                    Id = entry.Id,
                    Kind = entry.AssetType == AssetType.native_icon
                        ? NativeAdAssetKind.Icon
                        : NativeAdAssetKind.MainImage,
                    Width = entry.Width ?? 0,
                    Height = entry.Height ?? 0,
                    SizeBytes = entry.FileSize,
                    ClickUrl = entry.ClickUrl,
                    AdId = entry.AdId,
                    TitleText = entry.MainHeaderText,
                    DescriptionText = entry.DescriptionText,
                    CallToActionText = entry.ActionButtonText
                });
            }

            return infos;
        }

        private static AdError ToAdError(NativeAdContentError error)
        {
            return error switch
            {
                NativeAdContentError.NoAssets => AdError.NoFill,
                NativeAdContentError.MissingIcon => AdError.NoFill,
                // An icon that exists but breaks the size standard is a bad creative, not an
                // empty inventory - surfaced separately so it shows up in reporting.
                NativeAdContentError.OffSpecIcon => AdError.InvalidRequest,
                NativeAdContentError.MissingTitle => AdError.InvalidRequest,
                NativeAdContentError.MissingCallToAction => AdError.InvalidRequest,
                _ => AdError.Unknown
            };
        }

        /// <summary>
        /// Gets the loaded native ad content, or null when no native ad is ready. Useful when the
        /// game prefers polling over the OnNativeAdContentReady event.
        /// </summary>
        [UsedImplicitly]
        public static NativeAdContent GetNativeAdContent()
        {
            return _nativeAdContent;
        }

        /// <summary>
        /// Shows the loaded native ad. The SDK does not draw anything: it delivers the content
        /// through OnNativeAdContentReady (and returns it here) and registers the supplied
        /// GameObjects so taps anywhere on the ad are attributed and open the click URL.
        /// </summary>
        /// <param name="references">The GameObjects the game renders the ad into. May be null if the game handles its own clicks.</param>
        /// <returns>The content to render, or null when no native ad is ready.</returns>
        [UsedImplicitly]
        public static NativeAdContent ShowNativeAd(NativeAdReferences references)
        {
            if (_nativeAdContent == null)
            {
                MyDebug.Verbose("[Advertisement] ShowNativeAd called with no native ad ready.");
                Events.InvokeOnNativeAdError(new AdEventData(AdFormat.native, AdError.AdNotReady));
                return null;
            }

            RegisterNativeAdClickTargets(references);

            var content = _nativeAdContent;
            Events.InvokeOnNativeAdContentReady(content);
            Events.InvokeOnNativeAdShown(new AdEventData(AdFormat.native));
            return content;
        }

        private static void RegisterNativeAdClickTargets(NativeAdReferences references)
        {
            if (references == null) return;

            // Re-showing into the SAME views replaces only their handlers.
            ClearNativeAdClickTargets(references);

            var handlers = new List<SoilNativeAdClickHandler>();
            foreach (var target in references.All())
            {
                if (!target) continue;
                // The same view can legitimately fill two slots (e.g. the container is also the
                // main image); bind it once.
                if (handlers.Any(h => h && h.gameObject == target)) continue;

                // Clicks arrive through uGUI raycasting, so a view with no raycast-target Graphic
                // on itself or a child can never be hit and would silently swallow every tap.
                // Children count because a pointer event bubbles up to the nearest handler.
                if (!target.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget))
                    MyDebug.LogWarning($"[Advertisement] Native ad view '{target.name}' has no raycast-target Graphic; clicks on it will not register.");

                if (!target.TryGetComponent(out SoilNativeAdClickHandler handler))
                    handler = target.AddComponent<SoilNativeAdClickHandler>();

                handler.Bind(OnNativeAdClicked);
                handlers.Add(handler);
            }

            _nativeAdClickHandlers[references] = handlers;
        }

        /// <summary>
        /// Unbinds every click handler currently attached to the game's views. The components are
        /// deliberately NOT destroyed: Object.Destroy is deferred to the end of the frame, so a
        /// hide-then-show (or two shows) within one frame would re-bind a component Unity is
        /// about to delete, and clicks would silently stop working. An unbound handler is inert,
        /// and the next show re-binds it.
        /// </summary>
        private static void ClearNativeAdClickTargets(NativeAdReferences references)
        {
            if (references == null || !_nativeAdClickHandlers.TryGetValue(references, out var handlers))
                return;

            foreach (var handler in handlers)
            {
                if (handler)
                    handler.Bind(null);
            }

            _nativeAdClickHandlers.Remove(references);
        }

        /// <summary>Unbinds every registered view set. Used when the ad itself goes away.</summary>
        private static void ClearAllNativeAdClickTargets()
        {
            foreach (var references in _nativeAdClickHandlers.Keys.ToList())
                ClearNativeAdClickTargets(references);
        }

        private static void OnNativeAdClicked()
        {
            var clickUrl = _nativeAdContent?.ClickUrl;
            Events.InvokeOnNativeAdClicked(new AdEventData(AdFormat.native));

            if (string.IsNullOrEmpty(clickUrl))
            {
                MyDebug.Info("[Advertisement] Native ad clicked but no click URL is available.");
                return;
            }

            MyDebug.Verbose("[Advertisement] Opening the native ad's link");
            AdLinks.Open(clickUrl);
        }

        /// <summary>
        /// Stops attributing clicks for ONE place the ad was shown in and fires OnNativeAdClosed.
        /// Pass the same references given to ShowNativeAd; other places showing this ad keep
        /// working. Call this from a view's OnDisable - DestroyNativeAd would take the ad away
        /// from every other place too.
        /// </summary>
        [UsedImplicitly]
        public static void HideNativeAd(NativeAdReferences references)
        {
            ClearNativeAdClickTargets(references);
            if (_nativeAdContent != null)
                Events.InvokeOnNativeAdClosed(new AdEventData(AdFormat.native));
        }

        /// <summary>
        /// Stops attributing clicks for the native ad everywhere it is on screen and fires
        /// OnNativeAdClosed. The loaded content is kept, so the ad can be shown again without a
        /// reload; use DestroyNativeAd to release it.
        /// </summary>
        [UsedImplicitly]
        public static void HideNativeAd()
        {
            ClearAllNativeAdClickTargets();
            if (_nativeAdContent != null)
                Events.InvokeOnNativeAdClosed(new AdEventData(AdFormat.native));
        }

        /// <summary>
        /// Releases the loaded native ad. After this, IsFormatReady(AdFormat.native) is false
        /// until LoadAd(AdFormat.native) succeeds again.
        /// </summary>
        [UsedImplicitly]
        public static void DestroyNativeAd()
        {
            ClearAllNativeAdClickTargets();
            _nativeAdContent = null;
        }

        #endregion

        /// <summary>
        /// Gets a cached asset by ad format and asset type
        /// </summary>
        /// <param name="adFormat">The ad format (banner, interstitial, rewarded)</param>
        /// <param name="assetType">The asset type (image, video, logo)</param>
        /// <returns>The cached asset or null if not found</returns>
        public static AssetCacheEntry GetCachedAsset(AdFormat adFormat, AssetType assetType)
        {
            return AssetCache.GetCachedAsset(adFormat, assetType);
        }

        /// <summary>
        /// Gets a cached asset by its UUID
        /// </summary>
        /// <param name="uuid">The UUID of the cached asset</param>
        /// <returns>The cached asset or null if not found</returns>
        public static AssetCacheEntry GetCachedAssetByUUID(string uuid)
        {
            return AssetCache.GetCachedAssetByUUID(uuid);
        }

        /// <summary>
        /// Gets all cached assets for a specific ad format
        /// </summary>
        /// <param name="adFormat">The ad format</param>
        /// <returns>List of cached assets for the format</returns>
        public static List<AssetCacheEntry> GetCachedAssets(AdFormat adFormat)
        {
            return AssetCache.GetCachedAssets(adFormat);
        }

        /// <summary>
        /// Gets all cached assets
        /// </summary>
        /// <returns>List of all cached assets</returns>
        public static List<AssetCacheEntry> GetAllCachedAssets()
        {
            return AssetCache.GetAllCachedAssets();
        }

        /// <summary>
        /// Removes a cached asset by UUID
        /// </summary>
        /// <param name="uuid">The UUID of the asset to remove</param>
        /// <returns>True if the asset was removed, false if not found</returns>
        public static bool RemoveCachedAsset(string uuid)
        {
            return AssetCache.RemoveCachedAsset(uuid);
        }

        /// <summary>
        /// Clears all cached assets asynchronously
        /// </summary>
        public static async UniTask ClearAssetCacheAsync()
        {
            await AssetCache.ClearCacheAsync();
            AdvertisementPlayerPrefs.CachedAssets = new List<AssetCacheEntry>();
        }

        /// <summary>
        /// Clears old cached assets based on age (older than specified days)
        /// </summary>
        public static async UniTask ClearOldAssetsAsync(int olderThanDays = 7)
        {
            await AssetCache.ClearOldAssetsAsync(olderThanDays);

            // Update persisted cache
            var remainingAssets = AssetCache.GetAllCachedAssets();
            AdvertisementPlayerPrefs.CachedAssets = remainingAssets;
        }

        /// <summary>
        /// Loads a texture from a cached asset
        /// </summary>
        /// <param name="uuid">The UUID of the cached asset</param>
        /// <returns>Texture2D or null if not found or failed to load</returns>
        public static Texture2D LoadTexture(string uuid)
        {
            return AssetCache.LoadTexture(uuid);
        }

        /// <summary>
        /// Gets the file path for a cached asset
        /// </summary>
        /// <param name="uuid">The UUID of the cached asset</param>
        /// <returns>File path or null if not found</returns>
        public static string GetAssetPath(string uuid)
        {
            return AssetCache.GetAssetPath(uuid);
        }

        /// <summary>
        /// Initializes the asset cache from persisted data
        /// </summary>
        public static void InitializeAssetCache()
        {
            try
            {
                var persistedAssets = AdvertisementPlayerPrefs.CachedAssets;
                if (persistedAssets != null && persistedAssets.Count > 0)
                {
                    // Validate that cached files still exist
                    var validAssets = persistedAssets.Where(a => a.IsValid).ToList();
                    if (validAssets.Count != persistedAssets.Count)
                    {
                        AdvertisementPlayerPrefs.CachedAssets = validAssets;
                    }
                }
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Failed to initialize asset cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if an ad format is ready to be shown: its ad is decoded by the player (or, for
        /// native, built) and, for rewarded ads, the cooldown has passed. Always check this before
        /// calling ShowAd.
        /// </summary>
        /// <param name="adFormat">The ad format to check</param>
        /// <returns>True if the ad can be shown immediately</returns>
        public static bool IsFormatReady(AdFormat adFormat)
        {
            if (adFormat == AdFormat.native)
                return _nativeAdContent != null;

            return SlotFor(adFormat)?.IsReady ?? false;
        }

        /// <summary>
        /// Gets the readiness status of all requested ad formats
        /// </summary>
        /// <returns>Dictionary mapping ad formats to their ready status</returns>
        public static Dictionary<AdFormat, bool> GetFormatReadiness()
        {
            var readiness = new Dictionary<AdFormat, bool>();

            if (_requestedFormats != null)
            {
                foreach (var format in _requestedFormats)
                {
                    readiness[format] = IsFormatReady(format);
                }
            }

            return readiness;
        }
    }
}
