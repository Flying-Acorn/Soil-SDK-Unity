using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    /// <summary>
    /// The pure decisions of the ad asset cache: which ad of a group to cache, which files are
    /// still missing, which cache entries are no longer needed, and how server ids become file
    /// names.
    /// </summary>
    public static class AssetCachePlan
    {
        private const int MaxIdLength = 64;
        private const int MaxExtensionLength = 8;

        /// <summary>
        /// Picks the ad to cache out of <paramref name="candidateAdIds"/>: the one with the best
        /// positive <paramref name="cachedScore"/> (how much of it is already on disk; the first
        /// one on a tie), so a restart reuses the files it has instead of downloading another ad of
        /// the same group; a random one when nothing is cached.
        /// </summary>
        /// <param name="cachedScore">How much of an ad is cached; 0 or less means nothing.</param>
        /// <param name="random">Returns a value in [0, n) for n.</param>
        /// <returns>The index of the chosen candidate, or -1 when there are none.</returns>
        public static int PickCandidate(IReadOnlyList<string> candidateAdIds, Func<string, int> cachedScore,
            Func<int, int> random)
        {
            if (candidateAdIds == null || candidateAdIds.Count == 0) return -1;
            if (cachedScore != null)
            {
                var best = -1;
                var bestScore = 0;
                for (var i = 0; i < candidateAdIds.Count; i++)
                {
                    var id = candidateAdIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    var score = cachedScore(id);
                    if (score <= bestScore) continue;
                    best = i;
                    bestScore = score;
                }

                if (best >= 0) return best;
            }

            var index = random?.Invoke(candidateAdIds.Count) ?? 0;
            return index < 0 || index >= candidateAdIds.Count ? 0 : index;
        }

        /// <summary>The required cache keys that have no usable file yet, in order.</summary>
        public static List<string> Missing(IEnumerable<string> requiredKeys, Func<string, bool> isCachedAndValid)
        {
            var missing = new List<string>();
            if (requiredKeys == null) return missing;
            foreach (var key in requiredKeys)
                if (key != null && (isCachedAndValid == null || !isCachedAndValid(key)) && !missing.Contains(key))
                    missing.Add(key);
            return missing;
        }

        /// <summary>
        /// Cached keys of a format that the current plan does not use (an ad that left the group,
        /// or one no longer picked); their files can be deleted.
        /// </summary>
        public static List<string> Stale(IEnumerable<string> cachedKeys, ICollection<string> requiredKeys)
        {
            var stale = new List<string>();
            if (cachedKeys == null) return stale;
            foreach (var key in cachedKeys)
                if (key != null && (requiredKeys == null || !requiredKeys.Contains(key)))
                    stale.Add(key);
            return stale;
        }

        /// <summary>
        /// Files a format used before its cache was replaced that the new cache does not use (a
        /// file both share is kept); they can be deleted once nothing can still read them.
        /// </summary>
        public static List<string> Retired(IEnumerable<string> oldPaths, IEnumerable<string> newPaths)
        {
            var kept = new HashSet<string>(StringComparer.Ordinal);
            if (newPaths != null)
                foreach (var path in newPaths)
                    if (!string.IsNullOrEmpty(path))
                        kept.Add(path);

            var retired = new List<string>();
            if (oldPaths == null) return retired;
            foreach (var path in oldPaths)
                if (!string.IsNullOrEmpty(path) && !kept.Contains(path) && !retired.Contains(path))
                    retired.Add(path);
            return retired;
        }

        /// <summary>
        /// A server id as it may appear in a file name: kept when it is only letters, digits, '-'
        /// and '_' (and not too long), otherwise replaced by a hash of it, so an id can never
        /// point a file outside the cache directory.
        /// </summary>
        public static string SafeFileId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "default";
            if (id.Length <= MaxIdLength && IsSafe(id)) return id;

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(id));
            var builder = new StringBuilder("h", 33);
            for (var i = 0; i < 16; i++)
                builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        /// <summary>A file extension (with its dot) taken from a URL, or "" when it is not a plain one.</summary>
        public static string SafeExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension) || extension[0] != '.') return string.Empty;
            var body = extension.Substring(1);
            if (body.Length == 0 || body.Length > MaxExtensionLength) return string.Empty;
            foreach (var c in body)
                if (!IsAsciiLetterOrDigit(c)) return string.Empty;
            return "." + body.ToLowerInvariant();
        }

        /// <summary>
        /// <see cref="SafeExtension(string)"/>, or <paramref name="fallback"/> when the URL has no
        /// plain extension. Players that pick a decoder by extension (iOS AVURLAsset for videos)
        /// need one on every file.
        /// </summary>
        public static string SafeExtension(string extension, string fallback)
        {
            var safe = SafeExtension(extension);
            return safe.Length > 0 ? safe : SafeExtension(fallback);
        }

        private static bool IsSafe(string value)
        {
            foreach (var c in value)
                if (!IsAsciiLetterOrDigit(c) && c != '-' && c != '_') return false;
            return true;
        }

        private static bool IsAsciiLetterOrDigit(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }
}
