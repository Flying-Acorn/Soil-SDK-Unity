using System;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Models.AdPlacements
{
    public interface IAdPlacement
    {
        string Id { get; }
        string Name { get; }
        AdFormat AdFormat { get; }
        void Show();
        void Hide();
        bool IsReady();
        void Load();

        Action OnError { get; set; }
        Action OnLoaded { get; set; }
        Action OnShown { get; set; }
        Action OnHidden { get; set; }
        Action OnClicked { get; set; }
        Action OnImpression { get; set; }
        Action OnAdClosed { get; set; }
    }
}