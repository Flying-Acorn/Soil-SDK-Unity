using System.Collections.Generic;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement.Models
{
    /// <summary>
    /// The GameObjects the game used to render a native ad. Registering them lets the SDK
    /// attribute clicks to the ad.
    ///
    /// Everything here is optional: render whichever assets suit your layout and register only
    /// those. Whatever you register becomes clickable.
    ///
    /// The quickest route to "the whole ad is clickable" is <c>containerGameObject</c>: pointer
    /// events bubble up to the nearest ancestor handler, so registering the root makes every
    /// child clickable - including custom views the SDK knows nothing about (a badge, a rating
    /// row, a background). Use <c>additionalGameObjects</c> for extra views outside that root.
    /// </summary>
    public class NativeAdReferences
    {
        public readonly GameObject TitleGameObject;
        public readonly GameObject DescriptionGameObject;
        public readonly GameObject CallToActionGameObject;
        public readonly GameObject IconGameObject;
        public readonly GameObject MainImageGameObject;

        /// <summary>Root of your ad layout. Registering it makes every child clickable.</summary>
        public readonly GameObject ContainerGameObject;

        /// <summary>Any further views that should also open the ad when tapped.</summary>
        public readonly IReadOnlyList<GameObject> AdditionalGameObjects;

        public NativeAdReferences(
            GameObject titleGameObject = null,
            GameObject descriptionGameObject = null,
            GameObject callToActionGameObject = null,
            GameObject iconGameObject = null,
            GameObject mainImageGameObject = null,
            GameObject containerGameObject = null,
            params GameObject[] additionalGameObjects)
        {
            TitleGameObject = titleGameObject;
            DescriptionGameObject = descriptionGameObject;
            CallToActionGameObject = callToActionGameObject;
            IconGameObject = iconGameObject;
            MainImageGameObject = mainImageGameObject;
            ContainerGameObject = containerGameObject;
            AdditionalGameObjects = additionalGameObjects ?? new GameObject[0];
        }

        /// <summary>
        /// Convenience for the common "my whole ad card is one clickable unit" case.
        /// </summary>
        public static NativeAdReferences ForContainer(GameObject containerGameObject)
        {
            return new NativeAdReferences(containerGameObject: containerGameObject);
        }

        /// <summary>
        /// Every non-null GameObject, in no particular order. Clicks are registered on all of
        /// them because a native ad is one clickable unit. Duplicates are harmless - the SDK
        /// binds one handler per GameObject.
        /// </summary>
        public IEnumerable<GameObject> All()
        {
            if (ContainerGameObject) yield return ContainerGameObject;
            if (TitleGameObject) yield return TitleGameObject;
            if (DescriptionGameObject) yield return DescriptionGameObject;
            if (CallToActionGameObject) yield return CallToActionGameObject;
            if (IconGameObject) yield return IconGameObject;
            if (MainImageGameObject) yield return MainImageGameObject;

            foreach (var extra in AdditionalGameObjects)
                if (extra) yield return extra;
        }
    }
}
