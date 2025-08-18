using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.JWTTools;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Soil.Core
{
    public enum InitRequestType
    {
        Normal,
        ForceRefresh,
        UserChanged
    }

    public class QueuedInitRequest
    {
        public TaskCompletionSource<bool> TaskSource { get; }
        public InitRequestType RequestType { get; }
        public DateTime QueuedTime { get; }
        public string RequestId { get; }

        public QueuedInitRequest(InitRequestType requestType)
        {
            TaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            RequestType = requestType;
            QueuedTime = DateTime.UtcNow;
            RequestId = Guid.NewGuid().ToString("N")[..8];
        }
    }

    public class SoilServices : MonoBehaviour
    {
        private static SoilServices _instance;
        private static bool _readyBroadcasted;
        private static Task _initTask;
    private static readonly object _initLock = new object();
        private static readonly List<QueuedInitRequest> _queuedInitRequests = new List<QueuedInitRequest>();
        private static readonly object _queueLock = new object();
        private const int MaxQueuedRequests = 200;
        private static bool _retryScheduled;
        private static bool _isShuttingDown;
        private static SynchronizationContext _unityContext;
        private bool _sessionAuthSuccess;
        private static int _retryAttempts = 0;
        private static DateTime _lastFailureTime = DateTime.MinValue;
        private static readonly TimeSpan[] _retryIntervals = {
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(25),
        };

        [UsedImplicitly] public static UserInfo UserInfo => UserPlayerPrefs.UserInfoInstance;
        [UsedImplicitly] public static Action OnServicesReady;

        [UsedImplicitly]
        public static bool Ready
        {
            get
            {
                var hasInstance = _instance != null;
                var instanceReady = _instance?._instanceReady ?? false;
                var sessionAuthValid = _instance?._sessionAuthSuccess ?? false;
                var initTaskCompleted = _initTask?.IsCompletedSuccessfully ?? false;
                var authValid = IsAuthenticationCurrentlyValid();

                var result = hasInstance && instanceReady && initTaskCompleted && authValid && sessionAuthValid;

                MyDebug.Verbose($"Soil-Core Ready check: Instance={hasInstance}, InstanceReady={instanceReady}, InitCompleted={initTaskCompleted}, AuthValid={authValid}, SessionAuthValid={sessionAuthValid} => {result}");

                return result;
            }
        }

        [UsedImplicitly]
        public static bool IsNetworkAvailable => Application.internetReachability != NetworkReachability.NotReachable;

        private bool _instanceReady;

        private void Awake()
        {
            _unityContext ??= SynchronizationContext.Current;
        }

        private void StartInstance()
        {
            _isShuttingDown = false;
            UserPlayerPrefs.ResetSetInMemoryCache();
            _instance.transform.SetParent(null);
            _initTask = null;
            _readyBroadcasted = false;
            _sessionAuthSuccess = false;
            _retryAttempts = 0;
            _lastFailureTime = DateTime.MinValue;
            lock (_queueLock) _queuedInitRequests.Clear();

            DontDestroyOnLoad(gameObject);
            SetupDeeplink();
            UserApiHandler.OnUserFilled += OnUserChanged;
            _instanceReady = true;

            MyDebug.Verbose($"Soil-Core: StartInstance completed. Ready state: {Ready}");
        }

        private static void OnUserChanged(bool userChanged)
        {
            if (!userChanged) return;
            _readyBroadcasted = false;
            _retryAttempts = 0;
            _lastFailureTime = DateTime.MinValue;
            CompleteAndClearQueuedRequests(new SoilException("Initialization canceled due to user change", SoilExceptionErrorCode.Canceled));

            _ = Initialize();
        }

        private void OnDestroy()
        {
            _isShuttingDown = true;
            _instance = null;
            _initTask = null;
            _readyBroadcasted = false;
            OnServicesReady = null;
            UserApiHandler.OnUserFilled -= OnUserChanged;

            CompleteAndClearQueuedRequests(new SoilException("SoilServices was destroyed", SoilExceptionErrorCode.Canceled));
        }

        internal static async Task Initialize()
        {
            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, UserPlayerPrefs.RequestTimeout));
            CleanupExpiredRequests();

            if (_lastFailureTime != DateTime.MinValue)
            {
                var retryInterval = GetCurrentRetryInterval();
                var timeSinceLastFailure = DateTime.UtcNow - _lastFailureTime;

                if (timeSinceLastFailure < retryInterval)
                {
                    var queuedRequest = new QueuedInitRequest(InitRequestType.Normal);
                    lock (_queueLock)
                    {
                        if (_queuedInitRequests.Count >= MaxQueuedRequests)
                        {
                            queuedRequest.TaskSource.TrySetException(new SoilException(
                                $"Initialization queue capacity exceeded ({MaxQueuedRequests}). Please wait for retry.",
                                SoilExceptionErrorCode.Timeout));
                        }
                        else
                        {
                            _queuedInitRequests.Add(queuedRequest);
                        }
                    }

                    MyDebug.Verbose($"Soil-Core: Initialization request queued (ID: {queuedRequest.RequestId}). Retry cooldown: {retryInterval.TotalSeconds - timeSinceLastFailure.TotalSeconds:F1}s remaining. {GetQueueStatus()}");

                    ScheduleRetryAfterCooldown(retryInterval - timeSinceLastFailure);

                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        lock (_queueLock) _queuedInitRequests.Remove(queuedRequest);
                        throw new SoilException("Initialization timed out before retry", SoilExceptionErrorCode.Timeout);
                    }

                    var completed = await Task.WhenAny(queuedRequest.TaskSource.Task, Task.Delay(remaining));
                    if (completed != queuedRequest.TaskSource.Task)
                    {
                        lock (_queueLock) _queuedInitRequests.Remove(queuedRequest);
                        throw new SoilException("Initialization timed out during cooldown wait", SoilExceptionErrorCode.Timeout);
                    }
                    await queuedRequest.TaskSource.Task;
                    return;
                }
            }

            Task runningInit;
            lock (_initLock)
            {
                runningInit = _initTask;
            }

            if (runningInit != null && !runningInit.IsCompleted)
            {
                await AwaitWithDeadline(runningInit, deadline, "existing initialization");
                return;
            }

            if (!_instance)
            {
                _instance = FindObjectOfType<SoilServices>();
                if (!_instance)
                    _instance = new GameObject(nameof(SoilServices)).AddComponent<SoilServices>();
                _instance.StartInstance();
            }

            lock (_initLock)
            {
                if (_initTask is { IsCompletedSuccessfully: false, IsCompleted: true })
                    _initTask = null;
            }

            switch (Ready)
            {
                case true when !JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access):
                    MyDebug.LogWarning("Soil-Core: Ready was true but access token is invalid - forcing re-authentication");
                    _initTask = null;
                    _initTask = PerformAuthentication(forceRefresh: true);
                    break;
                case true:
                    MyDebug.Verbose("Soil-Core: Already ready - broadcasting ready state");
                    BroadcastReady();
                    return;
            }

            if (UserPlayerPrefs.AppID == Data.Constants.DemoAppID ||
                UserPlayerPrefs.SDKToken == Data.Constants.DemoAppSDKToken)
                MyDebug.LogError(
                    $"Soil-Core: AppID or SDKToken are not set. You must create and fill {nameof(SDKSettings)}. Using demo values.");

            if (!IsNetworkAvailable)
            {
                var exception = new SoilException("No network connectivity and no cached authentication data",
                    SoilExceptionErrorCode.Timeout);
                HandleInitializationFailure(exception);
                throw exception;
            }

            lock (_initLock)
            {
                if (_initTask == null)
                {
                    if (!_instance._sessionAuthSuccess)
                    {
                        var authValid = IsAuthenticationCurrentlyValid();
                        _initTask = authValid
                            ? PerformAuthentication(forceFetchPlayerInfo: true)
                            : PerformAuthentication(forceRefresh: true);
                    }
                    else
                    {
                        _initTask = PerformAuthentication();
                    }
                }
            }

            Task initTaskSnapshot;
            lock (_initLock)
            {
                initTaskSnapshot = _initTask;
            }

            _ = initTaskSnapshot.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    RunOnUnityContext(OnInitializationSuccess);
                }
                else if (t.IsFaulted)
                {
                    var ex = t.Exception?.InnerException ?? t.Exception;
                    RunOnUnityContext(() => HandleInitializationFailure(ex));
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            await AwaitWithDeadline(initTaskSnapshot, deadline, "authentication");
        }

        private static TimeSpan GetCurrentRetryInterval()
        {
            var index = Math.Min(_retryAttempts, _retryIntervals.Length - 1);
            return _retryIntervals[index];
        }

        private static void ScheduleRetryAfterCooldown(TimeSpan delay)
        {
            if (_retryScheduled) return;
            _retryScheduled = true;

            if (_instance)
            {
                _instance.StartCoroutine(_instance.RetryAfterCooldownCoroutine((float)Math.Max(0, delay.TotalSeconds)));
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(delay);
                    }
                    catch { }
                    finally
                    {
                        _retryScheduled = false;
                        if (!_isShuttingDown)
                        {
                            if (_unityContext == null)
                            {
                                MyDebug.Verbose("Soil-Core: Skipping background retry due to missing Unity context");
                            }
                            else
                            {
                                RunOnUnityContext(() =>
                                {
                                    if (_lastFailureTime != DateTime.MinValue && !Ready)
                                    {
                                        MyDebug.Info($"Soil-Core: Attempting retry #{_retryAttempts + 1} after cooldown");
                                        _ = Initialize();
                                    }
                                });
                            }
                        }
                    }
                });
            }
        }

        private IEnumerator RetryAfterCooldownCoroutine(float seconds)
        {
            if (seconds > 0f)
                yield return new WaitForSecondsRealtime(seconds);

            _retryScheduled = false;
            if (_isShuttingDown) yield break;

            if (_lastFailureTime != DateTime.MinValue && !Ready)
            {
                MyDebug.Info($"Soil-Core: Attempting retry #{_retryAttempts + 1} after cooldown");
                _ = Initialize();
            }
        }

        private static Task PerformAuthentication(bool forceRefresh = false, bool forceFetchPlayerInfo = false)
        {
            return Authenticate.AuthenticateUser(forceRefresh: forceRefresh, forceFetchPlayerInfo: forceFetchPlayerInfo);
        }

        private static void HandleInitializationFailure(Exception exception)
        {
            _retryAttempts++;
            _lastFailureTime = DateTime.UtcNow;

            var retryInterval = GetCurrentRetryInterval();
            MyDebug.LogError($"Soil-Core: Initialization failed (attempt #{_retryAttempts}). " +
                           $"Next retry in {retryInterval.TotalSeconds}s. Error: {exception.Message}");

            ScheduleRetryAfterCooldown(retryInterval);

            List<string> completedRequests;
            lock (_queueLock)
            {
                completedRequests = new List<string>(_queuedInitRequests.Count);
                foreach (var request in _queuedInitRequests)
                {
                    if (!request.TaskSource.Task.IsCompleted)
                    {
                        request.TaskSource.TrySetException(exception);
                        completedRequests.Add(request.RequestId);
                    }
                }
                _queuedInitRequests.Clear();
            }
            if (completedRequests.Count > 0)
                MyDebug.Verbose($"Soil-Core: Completed {completedRequests.Count} queued requests with exception: [{string.Join(", ", completedRequests)}]");
        }

        private static void OnInitializationSuccess()
        {
            _instance._sessionAuthSuccess = true;
            _retryAttempts = 0;
            _lastFailureTime = DateTime.MinValue;

            List<string> completedRequests;
            lock (_queueLock)
            {
                completedRequests = new List<string>(_queuedInitRequests.Count);
                foreach (var request in _queuedInitRequests)
                {
                    if (!request.TaskSource.Task.IsCompleted)
                    {
                        request.TaskSource.TrySetResult(true);
                        completedRequests.Add(request.RequestId);
                    }
                }
                _queuedInitRequests.Clear();
            }
            if (completedRequests.Count > 0)
                MyDebug.Verbose($"Soil-Core: Completed {completedRequests.Count} queued requests successfully: [{string.Join(", ", completedRequests)}]");

            BroadcastReady();
        }

        private static void CompleteAndClearQueuedRequests(Exception exception)
        {
            List<string> completed;
            lock (_queueLock)
            {
                if (_queuedInitRequests.Count == 0) return;
                completed = new List<string>(_queuedInitRequests.Count);
                foreach (var request in _queuedInitRequests)
                {
                    if (!request.TaskSource.Task.IsCompleted)
                    {
                        request.TaskSource.TrySetException(exception);
                        completed.Add(request.RequestId);
                    }
                }
                _queuedInitRequests.Clear();
            }
            if (completed.Count > 0)
                MyDebug.Verbose($"Soil-Core: Completed {completed.Count} queued requests due to reset: [{string.Join(", ", completed)}]");
        }

        private static void RunOnUnityContext(Action action)
        {
            try
            {
                if (_unityContext != null)
                {
                    _unityContext.Post(_ =>
                    {
                        try { action(); }
                        catch (Exception ex) { MyDebug.LogError($"Soil-Core: Error on main-thread dispatch: {ex.Message}"); }
                    }, null);
                }
                else
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Soil-Core: Failed to dispatch to Unity context: {ex.Message}");
            }
        }

        [UsedImplicitly]
        public static int QueuedRequestCount
        {
            get { lock (_queueLock) return _queuedInitRequests.Count; }
        }

        [UsedImplicitly]
        public static string GetQueueStatus()
        {
            lock (_queueLock)
            {
                if (_queuedInitRequests.Count == 0)
                    return "No queued requests";

                var typeGroups = _queuedInitRequests
                    .GroupBy(r => r.RequestType)
                    .Select(g => $"{g.Key}: {g.Count()}")
                    .ToArray();

                var oldestRequest = _queuedInitRequests.OrderBy(r => r.QueuedTime).FirstOrDefault();
                var waitTime = oldestRequest != null ? (DateTime.UtcNow - oldestRequest.QueuedTime).TotalSeconds : 0;

                return $"Queued: {_queuedInitRequests.Count} [{string.Join(", ", typeGroups)}], Oldest waiting: {waitTime:F1}s";
            }
        }

        private static void CleanupExpiredRequests()
        {
            var expiredThreshold = DateTime.UtcNow.AddMinutes(-5);
            List<QueuedInitRequest> expiredRequests;
            lock (_queueLock)
            {
                expiredRequests = _queuedInitRequests.Where(r => r.QueuedTime < expiredThreshold).ToList();
                foreach (var expired in expiredRequests)
                {
                    if (!expired.TaskSource.Task.IsCompleted)
                    {
                        expired.TaskSource.TrySetException(new SoilException(
                            $"Initialization request expired after 5 minutes (ID: {expired.RequestId})",
                            SoilExceptionErrorCode.Timeout));
                    }
                    _queuedInitRequests.Remove(expired);
                }
            }
            if (expiredRequests.Count > 0)
                MyDebug.LogWarning($"Soil-Core: Cleaned up {expiredRequests.Count} expired initialization requests");
        }

        private static void BroadcastReady()
        {
            if (_readyBroadcasted) return;
            MyDebug.Info($"Soil-Core: Services are ready - {UserInfo?.uuid}");
            _readyBroadcasted = true;
            OnServicesReady?.Invoke();
        }

        private static async Task AwaitWithDeadline(Task task, DateTime deadline, string stage)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                throw new SoilException($"Initialization timed out during {stage}", SoilExceptionErrorCode.Timeout);

            var completed = await Task.WhenAny(task, Task.Delay(remaining));
            if (completed != task)
                throw new SoilException($"Initialization timed out during {stage}", SoilExceptionErrorCode.Timeout);

            await task;
        }

        private static bool IsAuthenticationCurrentlyValid()
        {
            try
            {
                var tokenData = UserPlayerPrefs.TokenData;
                if (tokenData == null || string.IsNullOrEmpty(tokenData.Access))
                {
                    MyDebug.Verbose("Soil-Core: Authentication validation failed - no token data");
                    return false;
                }

                if (!JwtUtils.IsTokenValid(tokenData.Access))
                {
                    MyDebug.Verbose("Soil-Core: Authentication validation failed - access token is invalid/expired");
                    return false;
                }

                MyDebug.Verbose("Soil-Core: Authentication validation passed - tokens are valid");
                return true;
            }
            catch (Exception ex)
            {
                MyDebug.Verbose($"Soil-Core: Authentication validation failed with exception: {ex.Message}");
                return false;
            }
        }

        internal void SetupDeeplink()
        {
            if (!UserPlayerPrefs.DeepLinkActivated) return;
            if (GetComponent<DeepLinkHandler>())
                return;
            gameObject.AddComponent<DeepLinkHandler>();
        }
    }
}