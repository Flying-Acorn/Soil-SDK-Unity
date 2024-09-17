using System;
using System.Collections.Generic;
using JWT;
using JWT.Algorithms;
using JWT.Serializers;

namespace FlyingAcorn.Soil.Core.JWTTools
{
    public class Utils
    {
        public static string GenerateJwt(Dictionary<string, object> header, Dictionary<string, object> payload, string secret)
        {
            payload.Add("iat", DateTimeOffset.Now.ToUnixTimeSeconds());
            payload.Add("iss", AuthenticatePlayerPrefs.AppID);

            IJwtAlgorithm algorithm = new HMACSHA256Algorithm();
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtEncoder encoder = new JwtEncoder(algorithm, serializer, urlEncoder);

            var token = encoder.Encode(payload, secret);
            Console.WriteLine(token);
            return token;
        }
    }
}