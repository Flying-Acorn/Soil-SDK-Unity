using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Advertisement.Logic;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// The asset cache's decisions: a restart reuses the ad already on disk and only downloads what
    /// is missing (e.g. a video an older SDK kept as a streaming URL), and server ids never shape
    /// file paths.
    /// </summary>
    public class AssetCachePlanTests
    {
        private static Func<string, int> Scores(Dictionary<string, int> scores) =>
            id => scores.TryGetValue(id, out var score) ? score : 0;

        [Test]
        public void PickCandidate_PrefersAnAdAlreadyCached()
        {
            var candidates = new[] { "a", "b", "c" };

            var index = AssetCachePlan.PickCandidate(candidates, Scores(new() { ["c"] = 1 }), _ => 0);

            Assert.AreEqual(2, index);
        }

        [Test]
        public void PickCandidate_PrefersTheAdWithMostOnDisk_FirstOnATie()
        {
            var candidates = new[] { "a", "b", "c" };

            Assert.AreEqual(1, AssetCachePlan.PickCandidate(candidates, Scores(new() { ["a"] = 1, ["b"] = 3, ["c"] = 2 }), _ => 0));
            Assert.AreEqual(0, AssetCachePlan.PickCandidate(candidates, Scores(new() { ["a"] = 2, ["c"] = 2 }), _ => 2));
        }

        [Test]
        public void PickCandidate_IsRandom_WhenNothingIsCached()
        {
            var candidates = new[] { "a", "b", "c" };

            Assert.AreEqual(1, AssetCachePlan.PickCandidate(candidates, _ => 0, _ => 1));
            Assert.AreEqual(2, AssetCachePlan.PickCandidate(candidates, Scores(new() { ["gone"] = 5 }), _ => 2));
            Assert.AreEqual(0, AssetCachePlan.PickCandidate(candidates, null, null));
        }

        [Test]
        public void PickCandidate_WithNoCandidates_IsMinusOne()
        {
            Assert.AreEqual(-1, AssetCachePlan.PickCandidate(new string[0], _ => 1, _ => 0));
            Assert.AreEqual(-1, AssetCachePlan.PickCandidate(null, null, _ => 0));
        }

        [Test]
        public void PickCandidate_ClampsABadRandom()
        {
            Assert.AreEqual(0, AssetCachePlan.PickCandidate(new[] { "a", "b" }, null, n => n + 5));
        }

        [Test]
        public void Missing_IsEveryRequiredFileWithoutAUsableCopy()
        {
            // The image and logo are on disk; the video was cached by an older SDK as a URL.
            var valid = new HashSet<string> { "rewarded_image_1", "rewarded_logo_1" };
            var required = new[] { "rewarded_video_1", "rewarded_image_1", "rewarded_logo_1" };

            var missing = AssetCachePlan.Missing(required, valid.Contains);

            CollectionAssert.AreEqual(new[] { "rewarded_video_1" }, missing);
        }

        [Test]
        public void Missing_IsEmpty_WhenEverythingIsCached()
        {
            var valid = new HashSet<string> { "a", "b" };
            CollectionAssert.IsEmpty(AssetCachePlan.Missing(new[] { "a", "b", "a" }, valid.Contains));
        }

        [Test]
        public void Missing_ListsEachKeyOnce()
        {
            CollectionAssert.AreEqual(new[] { "a" }, AssetCachePlan.Missing(new[] { "a", "a" }, _ => false));
        }

        [Test]
        public void Stale_IsWhatThePlanNoLongerUses()
        {
            var stale = AssetCachePlan.Stale(new[] { "keep", "old", "older" }, new HashSet<string> { "keep", "new" });

            CollectionAssert.AreEqual(new[] { "old", "older" }, stale);
        }

        [Test]
        public void Retired_IsWhatTheNewCacheNoLongerUses_KeepingSharedFiles()
        {
            var retired = AssetCachePlan.Retired(
                new[] { "/c/old_video.mp4", "/c/shared_logo.png", "/c/old_image.jpg", "/c/old_image.jpg" },
                new[] { "/c/new_video.mp4", "/c/shared_logo.png" });

            CollectionAssert.AreEqual(new[] { "/c/old_video.mp4", "/c/old_image.jpg" }, retired);
        }

        [Test]
        public void Retired_IgnoresBlanksAndMissingLists()
        {
            CollectionAssert.IsEmpty(AssetCachePlan.Retired(null, new[] { "/c/a" }));
            CollectionAssert.AreEqual(new[] { "/c/a" }, AssetCachePlan.Retired(new[] { "", null, "/c/a" }, null));
        }

        [TestCase("3f2c9a1e-7b4d-4c1a-9e2f-000000000001")]
        [TestCase("abc_DEF-123")]
        public void SafeFileId_KeepsPlainIds(string id)
        {
            Assert.AreEqual(id, AssetCachePlan.SafeFileId(id));
        }

        [TestCase("../../../etc/passwd")]
        [TestCase("a/b")]
        [TestCase("a\\b")]
        [TestCase("a b")]
        [TestCase("id:1")]
        [TestCase("شناسه")]
        [TestCase("a.b")]
        public void SafeFileId_HashesAnythingElse(string id)
        {
            var safe = AssetCachePlan.SafeFileId(id);

            StringAssert.IsMatch("^h[0-9a-f]{32}$", safe);
            Assert.AreEqual(safe, AssetCachePlan.SafeFileId(id), "stable");
            Assert.AreNotEqual(safe, AssetCachePlan.SafeFileId(id + "x"), "distinct");
        }

        [Test]
        public void SafeFileId_HashesOverlongIds_AndNamesMissingOnes()
        {
            StringAssert.IsMatch("^h[0-9a-f]{32}$", AssetCachePlan.SafeFileId(new string('a', 65)));
            Assert.AreEqual(new string('a', 64), AssetCachePlan.SafeFileId(new string('a', 64)));
            Assert.AreEqual("default", AssetCachePlan.SafeFileId(null));
            Assert.AreEqual("default", AssetCachePlan.SafeFileId(""));
        }

        [TestCase(".mp4", ".mp4")]
        [TestCase(".JPG", ".jpg")]
        [TestCase(".webp", ".webp")]
        [TestCase("", "")]
        [TestCase(null, "")]
        [TestCase(".", "")]
        [TestCase("mp4", "")]
        [TestCase(".mp4/../../x", "")]
        [TestCase(".toolongextension", "")]
        [TestCase(".p%6eg", "")]
        public void SafeExtension_KeepsOnlyPlainExtensions(string extension, string expected)
        {
            Assert.AreEqual(expected, AssetCachePlan.SafeExtension(extension));
        }

        [TestCase(".webm", ".mp4", ".webm")]
        [TestCase(".MOV", ".mp4", ".mov")]
        [TestCase("", ".mp4", ".mp4")]
        [TestCase(null, ".mp4", ".mp4")]
        [TestCase(".mp4/../../x", ".mp4", ".mp4")]
        [TestCase("", ".img", ".img")]
        [TestCase("", null, "")]
        [TestCase("", "../x", "")]
        public void SafeExtension_FallsBackWhenTheUrlHasNone(string extension, string fallback, string expected)
        {
            Assert.AreEqual(expected, AssetCachePlan.SafeExtension(extension, fallback));
        }

        [TestCase("/advertisement/assets/1/a.png", "https://soil.example", "https://soil.example/advertisement/assets/1/a.png")]
        [TestCase("advertisement/assets/1/a.png", "https://soil.example/", "https://soil.example/advertisement/assets/1/a.png")]
        [TestCase("https://cdn.example/a.png", "https://soil.example", "https://cdn.example/a.png")]
        [TestCase("http://cdn.example/a.png", "https://soil.example", "http://cdn.example/a.png")]
        [TestCase("HTTPS://cdn.example/a.png", "https://soil.example", "HTTPS://cdn.example/a.png")]
        [TestCase("//cdn.example/a.png", "https://soil.example", "https://cdn.example/a.png")]
        [TestCase("//cdn.example/a.png", "http://localhost:8000", "http://cdn.example/a.png")]
        [TestCase("", "https://soil.example", "")]
        [TestCase(null, "https://soil.example", null)]
        public void ResolveAssetUrl_CompletesRelativeUrlsOnly(string url, string baseDomain, string expected)
        {
            Assert.AreEqual(expected, AssetCachePlan.ResolveAssetUrl(url, baseDomain));
        }
    }
}
