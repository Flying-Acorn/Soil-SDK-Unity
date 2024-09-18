using System;
using System.Collections.Generic;
using JWT;
using JWT.Algorithms;
using JWT.Builder;
using JWT.Serializers;

namespace FlyingAcorn.Soil.Core.JWTTools
{
    public static class Utils
    {
        public static string GenerateJwt(Dictionary<string, string> payload, string secret, string algorithm = "HS256")
        {
            if (algorithm != "HS256")
            {
                throw new NotImplementedException("Algorithm not implemented");
            }
            if (!payload.ContainsKey("iat"))
                payload.Add("iat", DateTimeOffset.Now.ToUnixTimeSeconds().ToString());

            IJwtAlgorithm algorithmInstance = new HMACSHA256Algorithm();
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtEncoder encoder = new JwtEncoder(algorithmInstance, serializer, urlEncoder);

            var token = encoder.Encode(payload, secret);
            return token;
        }

        public static bool IsTokenAlmostExpired(string token)
        {
            var jwt = new JwtBuilder()
                .WithAlgorithm(new HMACSHA256Algorithm())
                .MustVerifySignature()
                .Decode<IDictionary<string, object>>(token);
            var exp = Convert.ToInt64(jwt["exp"]);
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            return exp - now < 60;
        }
    }
}