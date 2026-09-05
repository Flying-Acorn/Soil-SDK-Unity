using NUnit.Framework;
using FlyingAcorn.Soil.Advertisement.Logic;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// Pins the native asset standard. These numbers are a contract with the backend
    /// (advertisement/validators.py NATIVE_ASSET_SPECS) - if one side changes, this fails.
    /// </summary>
    public class NativeAdAssetSpecTests
    {
        [Test]
        public void Icon_StandardIsAnySquareUnderOneMegabyte()
        {
            Assert.AreEqual(1, NativeAdAssetSpec.IconMaxSizeMb);
            Assert.AreEqual(1024 * 1024, NativeAdAssetSpec.IconMaxSizeBytes);
            Assert.AreEqual(2000, NativeAdAssetSpec.IconMaxDimension);
        }

        [Test]
        public void Image_StandardIs1200x628UnderTwoMegabytes()
        {
            Assert.AreEqual(1200, NativeAdAssetSpec.ImageWidth);
            Assert.AreEqual(628, NativeAdAssetSpec.ImageHeight);
            Assert.AreEqual(2, NativeAdAssetSpec.ImageMaxSizeMb);
            Assert.AreEqual(2 * 1024 * 1024, NativeAdAssetSpec.ImageMaxSizeBytes);
        }

        [Test]
        public void Image_StandardIsRoughly1_91To1()
        {
            var ratio = (double)NativeAdAssetSpec.ImageWidth / NativeAdAssetSpec.ImageHeight;
            Assert.AreEqual(1.91d, ratio, 0.02d);
        }

        [Test]
        public void IsValidIcon_AnySquare_IsAccepted()
        {
            foreach (var side in new[] { 64, 128, 256, 512, 1024, 2000 })
                Assert.IsTrue(NativeAdAssetSpec.IsValidIcon(side, side, 400_000), $"{side}x{side}");
        }

        [Test]
        public void IsValidIcon_NonSquare_IsRejected()
        {
            Assert.IsFalse(NativeAdAssetSpec.IsValidIcon(256, 257, 400_000));
            Assert.IsFalse(NativeAdAssetSpec.IsValidIcon(512, 256, 400_000));
        }

        [Test]
        public void IsValidIcon_SquareButAboveTheCeiling_IsRejected()
        {
            // Guards device texture memory; a 4096 square decodes to ~64MB uncompressed.
            Assert.IsFalse(NativeAdAssetSpec.IsValidIcon(4096, 4096, 400_000));
        }

        [Test]
        public void IsValidIcon_AtExactBudget_IsAccepted()
        {
            Assert.IsTrue(NativeAdAssetSpec.IsValidIcon(256, 256, NativeAdAssetSpec.IconMaxSizeBytes));
        }

        [Test]
        public void IsValidIcon_OversizedSquare_IsStillRejectedOnBytes()
        {
            Assert.IsFalse(NativeAdAssetSpec.IsValidIcon(512, 512, NativeAdAssetSpec.IconMaxSizeBytes + 1));
        }

        [Test]
        public void IsValidIcon_OverBudget_IsRejected()
        {
            Assert.IsFalse(NativeAdAssetSpec.IsValidIcon(256, 256, NativeAdAssetSpec.IconMaxSizeBytes + 1));
        }

        [Test]
        public void IsValidIcon_UnknownSize_IsAccepted()
        {
            // A cache entry restored before the file was measured reports 0 bytes; that must not
            // be mistaken for a spec violation.
            Assert.IsTrue(NativeAdAssetSpec.IsValidIcon(256, 256, 0));
        }

        [Test]
        public void IsValidIcon_UnknownDimensions_IsAccepted()
        {
            // The server validated the upload; a cache entry that never recorded dimensions must
            // not be mistaken for an off-spec creative.
            Assert.IsTrue(NativeAdAssetSpec.IsValidIcon(0, 0, 400_000));
        }

        [Test]
        public void IsValidIcon_UnknownDimensionsButOverBudget_IsStillRejected()
        {
            Assert.IsFalse(NativeAdAssetSpec.IsValidIcon(0, 0, NativeAdAssetSpec.IconMaxSizeBytes + 1));
        }

        [Test]
        public void IsValidImage_ExactStandard_IsAccepted()
        {
            Assert.IsTrue(NativeAdAssetSpec.IsValidImage(1200, 628, 1_500_000));
        }

        [Test]
        public void IsValidImage_CorrectRatioButWrongSize_IsRejected()
        {
            // 600x314 is still 1.91:1 - native assets must be the exact standard size.
            Assert.IsFalse(NativeAdAssetSpec.IsValidImage(600, 314, 1_000));
        }

        [Test]
        public void IsValidImage_OverBudget_IsRejected()
        {
            Assert.IsFalse(NativeAdAssetSpec.IsValidImage(1200, 628, NativeAdAssetSpec.ImageMaxSizeBytes + 1));
        }

        [Test]
        public void IconBudget_IsStricterThanImageBudget()
        {
            Assert.Less(NativeAdAssetSpec.IconMaxSizeBytes, NativeAdAssetSpec.ImageMaxSizeBytes);
        }
    }
}
