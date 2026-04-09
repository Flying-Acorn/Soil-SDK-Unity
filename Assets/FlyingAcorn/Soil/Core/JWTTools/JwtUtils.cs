using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core.User;
using JWT;
using JWT.Algorithms;
using JWT.Exceptions;
using JWT.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        /// <summary>
        /// Computes and persists the offset (in seconds) between server time and device time.
        /// offset = serverIAT - deviceNowInSeconds.
        /// A positive value means device clock is behind the server.
        /// A negative value means device clock is ahead of the server.
        /// </summary>
        public static void FillTimesOffset(string token)
        {
            try
            {
                var serverIAT = GetIATFromToken(token);
                if (serverIAT <= 0)
                {
                    UserPlayerPrefs.DeviceTimeOffset = 0;
                    return;
                }

                var deviceNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                UserPlayerPrefs.DeviceTimeOffset = serverIAT - deviceNow;
            }
            catch (Exception)
            {
                UserPlayerPrefs.DeviceTimeOffset = 0;
            }
        }

        public static long GetIATFromToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return 0;

            try
            {
                var validationParameters = ValidationParameters.None;
                IJwtAlgorithm algorithm = new HMACSHA256Algorithm();
                IDateTimeProvider provider = new UtcDateTimeProvider();
                IJsonSerializer serializer = new JsonNetSerializer();
                IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
                IJwtValidator validator = new JwtValidator(serializer, provider, validationParameters);

                var decoder = new JwtDecoder(jsonSerializer: serializer, jwtValidator: validator,
                    urlEncoder: urlEncoder, algorithm: algorithm);
                var payload = decoder.Decode(token, false);
                var jsonObj = JsonConvert.DeserializeObject<JObject>(payload);
                if (jsonObj == null || !jsonObj.TryGetValue("iat", out var iatVal))
                    return 0;
                return long.TryParse(iatVal.ToString(), out var result) ? result : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static bool IsTokenValid(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            var validationParameters = new ValidationParameters
            {
                ValidateSignature = false,
                ValidateExpirationTime = true,
                ValidateIssuedTime = true,
                // Add a small margin to absorb network latency (download time),
                // thread starvation, or fractional second truncation.
                TimeMargin = 30
            };
            IJwtAlgorithm algorithm = new HMACSHA256Algorithm();
            IDateTimeProvider provider = new OffsetDateTimeProvider(UserPlayerPrefs.DeviceTimeOffset);
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
                return false;
            }
            catch (TokenExpiredException)
            {
                return false;
            }
            catch (SignatureVerificationException)
            {
                return false;
            }
        }
    }
}