using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    /// <summary>
    /// The renderable payload of a native ad. The SDK does not draw native ads - it hands this
    /// to the game, which lays the pieces out in its own UI and registers the GameObjects it used
    /// via <see cref="Advertisement.ShowNativeAd"/> so clicks are attributed.
    ///
    /// <see cref="MainImage"/> may be null (icon-only native ads are valid); the title, call to
    /// action and icon are always present.
    /// </summary>
    public class NativeAdContent
    {
        /// <summary>Id of the ad this content came from. Every field below belongs to it.</summary>
        public readonly string AdId;
        public readonly string Title;
        public readonly string Description;
        public readonly string CallToAction;
        public readonly string ClickUrl;
        public readonly Texture2D Icon;
        public readonly Texture2D MainImage;

        public NativeAdContent(string adId, string title, string description, string callToAction,
            string clickUrl, Texture2D icon, Texture2D mainImage)
        {
            AdId = adId;
            Title = title;
            Description = description;
            CallToAction = callToAction;
            ClickUrl = clickUrl;
            Icon = icon;
            MainImage = mainImage;
        }

        public bool HasMainImage => MainImage != null;
    }
}
