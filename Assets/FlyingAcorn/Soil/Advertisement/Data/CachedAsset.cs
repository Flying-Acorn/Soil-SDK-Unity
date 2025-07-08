using System;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    [Serializable]
    public class CachedAsset
    {
        /// <summary>
        /// Original asset ID from the server
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Unique UUID for easy access and identification
        /// </summary>
        public string UUID { get; set; }

        /// <summary>
        /// Type of asset (image, video, logo)
        /// </summary>
        public AssetType AssetType { get; set; }

        /// <summary>
        /// Ad format this asset belongs to
        /// </summary>
        public AdFormat AdFormat { get; set; }

        /// <summary>
        /// Local file path where the asset is stored
        /// </summary>
        public string LocalPath { get; set; }

        /// <summary>
        /// Original URL from the server
        /// </summary>
        public string OriginalUrl { get; set; }

        /// <summary>
        /// Width of the asset (if applicable)
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Height of the asset (if applicable)
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// Alt text for the asset
        /// </summary>
        public string AltText { get; set; }

        /// <summary>
        /// When the asset was cached
        /// </summary>
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// Gets the file size in bytes
        /// </summary>
        public long FileSize 
        { 
            get 
            { 
                try 
                { 
                    return System.IO.File.Exists(LocalPath) ? new System.IO.FileInfo(LocalPath).Length : 0; 
                } 
                catch 
                { 
                    return 0; 
                } 
            } 
        }

        /// <summary>
        /// Checks if the cached file still exists
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(LocalPath) && System.IO.File.Exists(LocalPath);

        /// <summary>
        /// Gets a user-friendly display name for the asset
        /// </summary>
        public string DisplayName => $"{AdFormat}_{AssetType}_{Id}";

        public override string ToString()
        {
            return $"CachedAsset [UUID: {UUID}, Type: {AssetType}, Format: {AdFormat}, Valid: {IsValid}]";
        }
    }
}
