using System;
using System.Collections.Generic;
using JWT;
using JWT.Algorithms;
using JWT.Exceptions;
using JWT.Serializers;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.JWTTools
{
    public static class JwtUtils
    {
        public static string GenerateJwt(Dictionary<string, string> payload, string secret)
        {
            if (!payload.ContainsKey("iat"))
                payload.Add("iat", DateTimeOffset.Now.ToUnixTimeSeconds().ToString());

            IJwtAlgorithm algorithmInstance = new HMACSHA256Algorithm();
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtEncoder encoder = new JwtEncoder(algorithmInstance, serializer, urlEncoder);

            var token = encoder.Encode(payload, secret);
            return token;
        }

        public static bool IsTokenValid(string token)
        {
            var validationParameters = new ValidationParameters
            {
                ValidateSignature = false,
                ValidateExpirationTime = true,
                ValidateIssuedTime = true,
            };
            IJwtAlgorithm algorithm = new HMACSHA256Algorithm();
            IDateTimeProvider provider = new UtcDateTimeProvider();
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtValidator validator = new JwtValidator(serializer, provider, validationParameters);
            IJwtDecoder decoder = new JwtDecoder(serializer, validator, urlEncoder, algorithm);
            const string fakeKey = "fakeKey";
            try
            {
                decoder.Decode(token, fakeKey);
                return true;
            }
            catch (TokenNotYetValidException)
            {
                Debug.Log("Token is not valid yet");
                return false;
            }
            catch (TokenExpiredException)
            {
                Debug.Log("Token has expired");
                return false;
            }
            catch (SignatureVerificationException)
            {
                Debug.Log("Token has invalid signature");
                return false;
            }
        }
    }
}