using FlyingAcorn.Soil.Core.JWTTools;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Core.Data
{
    [UsedImplicitly]
    public class TokenData
    {
        [UsedImplicitly] public string Access;
        [UsedImplicitly] public string Refresh;

        public TokenData ChangeTokenData(TokenData tokens)
        {
            tokens.Validate();
            Access = tokens.Access;
            Refresh = tokens.Refresh;
            return this;
        }

        public void Validate()
        {
            if (!JwtUtils.IsTokenValid(Access) || !JwtUtils.IsTokenValid(Refresh))
            {
                throw new System.Exception("Invalid token");
            }
        }
    }
}