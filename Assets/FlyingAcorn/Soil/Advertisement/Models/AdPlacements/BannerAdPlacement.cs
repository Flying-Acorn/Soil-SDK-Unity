using System;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Models.AdPlacements
{
    public class BannerAdPlacement : IAdPlacement
    {
        public string Id => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public AdFormat AdFormat => throw new NotImplementedException();

        public Action OnError { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Action OnLoaded { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Action OnShown { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Action OnHidden { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Action OnClicked { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Action OnImpression { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Action OnAdClosed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Hide()
        {
            throw new NotImplementedException();
        }

        public bool IsReady()
        {
            throw new NotImplementedException();
        }

        public void Load()
        {
            throw new NotImplementedException();
        }

        public void Show()
        {
            throw new NotImplementedException();
        }
    }
}