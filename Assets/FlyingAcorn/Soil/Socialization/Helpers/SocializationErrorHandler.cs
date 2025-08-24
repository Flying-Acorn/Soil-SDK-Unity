using System;
using System.Net;
using System.Net.Http;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Socialization.Data;

namespace FlyingAcorn.Soil.Socialization.Helpers
{
    internal static class SocializationErrorHandler
    {
        public static SocializationException HandleHttpException(Exception exception, SocializationOperation operation)
        {
            return exception switch
            {
                OperationCanceledException ex when ex.InnerException is TimeoutException => 
                    new SocializationException($"Request timed out while {GetOperationDescription(operation)}", 
                        operation, SoilExceptionErrorCode.Timeout),
                
                HttpRequestException ex => 
                    new SocializationException($"Network error while {GetOperationDescription(operation)}: {ex.Message}", 
                        operation, SoilExceptionErrorCode.TransportError),
                
                SocializationException socEx => socEx,
                
                _ => new SocializationException($"Unexpected error while {GetOperationDescription(operation)}: {exception.Message}", 
                    operation, SoilExceptionErrorCode.Unknown)
            };
        }

        public static SocializationException HandleHttpResponse(HttpResponseMessage response, string responseContent, 
            SocializationOperation operation)
        {
            var errorCode = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => SoilExceptionErrorCode.InvalidToken,
                HttpStatusCode.Forbidden => SoilExceptionErrorCode.Forbidden,
                HttpStatusCode.NotFound => SoilExceptionErrorCode.NotFound,
                HttpStatusCode.Conflict => SoilExceptionErrorCode.Conflict,
                HttpStatusCode.TooManyRequests => SoilExceptionErrorCode.TooManyRequests,
                HttpStatusCode.BadRequest => SoilExceptionErrorCode.InvalidRequest,
                HttpStatusCode.ServiceUnavailable => SoilExceptionErrorCode.ServiceUnavailable,
                _ => SoilExceptionErrorCode.TransportError
            };

            return new SocializationException(
                $"Server returned error {response.StatusCode} while {GetOperationDescription(operation)}: {responseContent}",
                operation, errorCode);
        }

        public static SocializationException HandleSerializationError(string responseContent, SocializationOperation operation)
        {
            return new SocializationException(
                $"Invalid response format while {GetOperationDescription(operation)}. Response: {responseContent}",
                operation, SoilExceptionErrorCode.InvalidResponse);
        }

        public static void ValidateParameter(string parameter, string parameterName, SocializationOperation operation)
        {
            if (string.IsNullOrEmpty(parameter))
            {
                throw new SocializationException($"{parameterName} cannot be null or empty", 
                    operation, SoilExceptionErrorCode.InvalidRequest);
            }
        }

        private static string GetOperationDescription(SocializationOperation operation)
        {
            return operation switch
            {
                SocializationOperation.GetFriends => "getting friends",
                SocializationOperation.AddFriend => "adding friend",
                SocializationOperation.RemoveFriend => "removing friend",
                SocializationOperation.GetFriendsLeaderboard => "fetching friends leaderboard",
                _ => "performing socialization operation"
            };
        }
    }
}
