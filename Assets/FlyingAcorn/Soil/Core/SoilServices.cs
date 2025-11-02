using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
        public UniTaskCompletionSource<bool> TaskSource { get; }
        public InitRequestType RequestType { get; }
        public DateTime QueuedTime { get; }
        public string RequestId { get; }

        public QueuedInitRequest(InitRequestType requestType)
        {
            TaskSource = new UniTaskCompletionSource<bool>();
            RequestType = requestType;
            QueuedTime = DateTime.UtcNow;
            RequestId = Guid.NewGuid().ToString("N")[..8];
        }
    }

    public class SoilServices : MonoBehaviour
    {
        private static SoilServices _instance;
        private static bool _readyBroadcasted;
        private static Task _initTask; // Use Task instead of UniTask to avoid double-await issues
        private static readonly object _initLock = new object();
        private static readonly List<QueuedInitRequest> _queuedInitRequests = new List<QueuedInitRequest>();
        private static readonly object _queueLock = new object();
        private const int MaxQueuedRequests = 200;
        private static bool _initSucceeded; // stable success flag
        private static bool _retryScheduled;
        private static SynchronizationContext _unityContext;
        private bool _sessionAuthSuccess;
        private static int _retryAttempts = 0;
        private static DateTime _lastFailureTime = DateTime.MinValue;
        private static Exception _lastInitFailureException;
        private static DateTime _lastAuthValidTime = DateTime.MinValue;
        private static readonly TimeSpan _authGracePeriod = TimeSpan.FromSeconds(15);

        private static readonly TimeSpan[] _retryIntervals = {
            TimeSpan.FromSeconds(0.5),  // Quick retry for transient issues
            TimeSpan.FromSeconds(1),    // Still quick but slightly longer
            TimeSpan.FromSeconds(2),    // Start building up delay
            TimeSpan.FromSeconds(4),    // Reasonable delay
            TimeSpan.FromSeconds(7),    // Moderate delay
            TimeSpan.FromSeconds(12),   // Longer delay for persistent issues
            TimeSpan.FromSeconds(20),   // Maximum delay for severe issues
        };

        /// <summary>
        /// Gets the current user's information.
        /// </summary>
        [UsedImplicitly] public static UserInfo UserInfo => UserPlayerPrefs.UserInfoInstance;

        /// <summary>
        /// Event fired when the SDK services are ready for use.
        /// </summary>
        [UsedImplicitly] public static Action OnServicesReady;

        /// <summary>
        /// Event fired when SDK initialization fails.
        /// </summary>
        [UsedImplicitly] public static Action<SoilException> OnInitializationFailed;

        /// <summary>
        /// Gets whether the SDK is ready for use.
        /// </summary>
        [UsedImplicitly]
        public static bool Ready
        {
            get
            {
                var hasInstance = _instance != null;
                var instanceReady = _instance?._instanceReady ?? false;
                var userInfoValid = UserPlayerPrefs.UserInfoInstance != null && !string.IsNullOrEmpty(UserPlayerPrefs.UserInfoInstance.uuid);
                var sessionAuthValid = _instance?._sessionAuthSuccess ?? false;
                var initTaskCompleted = _initSucceeded;
                var authValid = IsAuthenticationCurrentlyValid();
                var basicReady = hasInstance && instanceReady && userInfoValid;
                var fullyReady = basicReady && initTaskCompleted && authValid && sessionAuthValid;
                var result = fullyReady; // Using Task-based initialization for better reliability and double-await prevention

                return result;
            }
        }

        [UsedImplicitly]
        public static bool BasicReady
        {
            get
            {
                var hasInstance = _instance != null;
                var instanceReady = _instance?._instanceReady ?? false;
                var userInfoValid = UserPlayerPrefs.UserInfoInstance != null && !string.IsNullOrEmpty(UserPlayerPrefs.UserInfoInstance.uuid);
                var sessionAuthValid = _instance?._sessionAuthSuccess ?? false;
                var initTaskCompleted = _initSucceeded;
                return hasInstance && instanceReady && userInfoValid && initTaskCompleted && sessionAuthValid;
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
            UserPlayerPrefs.ResetSetInMemoryCache();
            _instance.transform.SetParent(null);
            _initTask = null;
            _initSucceeded = false;
            _readyBroadcasted = false;
            _sessionAuthSuccess = false;
            _retryAttempts = 0;
            _lastFailureTime = DateTime.MinValue;
            lock (_queueLock) _queuedInitRequests.Clear();

            DontDestroyOnLoad(gameObject);
            SetupDeeplink();
            UserApiHandler.OnUserFilled += OnUserChanged;
            _instanceReady = true;
        }

        private static void OnUserChanged(bool userChanged)
        {
            if (!userChanged) return;
            _readyBroadcasted = false;
            _initSucceeded = false;
            _retryAttempts = 0;
            _lastFailureTime = DateTime.MinValue;
            CompleteAndClearQueuedRequests(new SoilException("Initialization canceled due to user change", SoilExceptionErrorCode.Canceled));

            InitializeAsync();
        }

        private void OnDestroy()
        {
            _instance = null;
            _initTask = null;
            _unityContext = null;
            _readyBroadcasted = false;
            OnServicesReady = null;
            OnInitializationFailed = null;
            UserApiHandler.OnUserFilled -= OnUserChanged;

            CompleteAndClearQueuedRequests(new SoilException("SoilServices was destroyed", SoilExceptionErrorCode.Canceled));
        }

        /// <summary>
        /// Initializes the Soil SDK Core. Call this at the start of your application.
        /// </summary>
        public static void InitializeAsync()
        {
            InitializeOnMainThreadAsync().Forget();
        }

        public static UniTask InitializeAwaitableAsync(CancellationToken cancellationToken = default)
        {
            return InitializeOnMainThreadAsync(cancellationToken);
        }

        private static async UniTask InitializeOnMainThreadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Initialize(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _initSucceeded = false; // starting new cycle
                try
                {
                    HandleInitializationFailure(ex);
                }
                catch (Exception e)
                {
                    OnInitializationFailed?.Invoke(new SoilException($"Initialization failed: {e.Message}", SoilExceptionErrorCode.Unknown));
                }
            }
        }

        private static async UniTask Initialize(CancellationToken cancellationToken = default)
        {
            var initializationTimeout = UserPlayerPrefs.RequestTimeout * 2;
            var deadline = DateTime.UtcNow.AddSeconds(initializationTimeout);
            CleanupExpiredRequests();

            cancellationToken.ThrowIfCancellationRequested();

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

                    ScheduleRetryAfterCooldown(retryInterval - timeSinceLastFailure);

                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        lock (_queueLock) _queuedInitRequests.Remove(queuedRequest);
                        throw new SoilException("Initialization timed out before retry", SoilExceptionErrorCode.Timeout);
                    }

                    var (isResult, _) = await UniTask.WhenAny(queuedRequest.TaskSource.Task, UniTask.Delay(remaining));
                    if (!isResult)
                    {
                        lock (_queueLock) _queuedInitRequests.Remove(queuedRequest);
                        throw new SoilException("Initialization timed out during cooldown wait", SoilExceptionErrorCode.Timeout);
                    }
                    // Don't await queuedRequest.TaskSource.Task again - WhenAny already completed it
                    return;
                }
            }

            Task runningInit;
            lock (_initLock)
            {
                runningInit = _initTask; // Get the Task directly
            }

            if (runningInit != null && !runningInit.IsCompleted)
            {
                try
                {
                    // Await the existing task directly using Task overload
                    await AwaitWithDeadline(runningInit, deadline, "existing initialization");
                    return;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Already continuation registered") || 
                                                            ex.Message.Contains("double-await") || 
                                                            ex.Message.Contains("multiple await"))
                {
                    // Handle UniTask double-await cases by clearing the task and creating a new one
                    MyDebug.LogWarning($"Soil-Core: Detected UniTask await issue: {ex.Message}. Creating new initialization task.");
                    lock (_initLock)
                    {
                        _initTask = null; // Clear the problematic task
                    }
                }
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
                if (_initTask != null)
                {
                    // Clean up completed tasks to prevent accumulation
                    if (!_initTask.IsCompletedSuccessfully && _initTask.IsCompleted)
                        _initTask = null;
                }
            }

            switch (BasicReady)
            {
                case true when UserPlayerPrefs.TokenData?.Access != null && !JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access):
                    MyDebug.Info("Soil-Core: BasicReady was true but access token is invalid - forcing re-authentication");
                    _initTask = null;
                    _initTask = PerformAuthentication(forceRefresh: true).AsTask();
                    break;
                case true:
                    BroadcastReady();
                    return;
            }

            if (UserPlayerPrefs.AppID == Data.Constants.DemoAppID ||
                UserPlayerPrefs.SDKToken == Data.Constants.DemoAppSDKToken)
                MyDebug.LogError(
                    $"Soil-Core: AppID or SDKToken are not set. You must create and fill {nameof(SDKSettings)}. Using demo values.");

            if (!IsNetworkAvailable)
            {
                // If network is unavailable but we have valid cached auth, proceed offline
                var hasValidAuth = IsAuthenticationCurrentlyValid();
                if (!hasValidAuth)
                {
                    MyDebug.Info("Soil-Core: No network connectivity and no valid cached authentication. Will retry when network becomes available.");
                    throw new SoilException("No network connectivity and no cached authentication data",
                        SoilExceptionErrorCode.TransportError);
                }
                else
                {
                    MyDebug.Info("Soil-Core: No network connectivity but valid cached authentication found. Proceeding with offline mode.");
                    // Allow the initialization to continue with cached data
                }
            }

            lock (_initLock)
            {
                if (_initTask == null)
                {
                    if (!_instance._sessionAuthSuccess)
                    {
                        var authValid = IsAuthenticationCurrentlyValid();
                        _initTask = authValid
                            ? PerformAuthentication(forceSyncPlayerInfo: true).AsTask()
                            : PerformAuthentication(forceRefresh: true).AsTask();
                    }
                    else
                    {
                        _initTask = PerformAuthentication().AsTask();
                    }
                }
            }

            Task initTaskSnapshot;
            lock (_initLock)
            {
                if (_initTask == null)
                    return; // safety guard
                initTaskSnapshot = _initTask;
            }

            // Task is already safe for multiple await operations (unlike UniTask)
            var initTaskAsTask = initTaskSnapshot;

            // Attach success callback (only on success, failures handled by outer catch)
            AttachInitializationSuccessCallback(initTaskAsTask).Forget(); // fire-and-forget

            await AwaitWithDeadline(initTaskAsTask, deadline, "authentication");
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
                _ = UniTask.RunOnThreadPool(async () =>
                {
                    try
                    {
                        await UniTask.Delay(delay);
                    }
                    catch { }
                    finally
                    {
                        _retryScheduled = false;
                        if (_unityContext != null)
                        {
                            RunOnUnityContext(() =>
                            {
                                try
                                {
                                    // Check if we need token refresh or full initialization
                                    if (!Ready)
                                    {
                                        MyDebug.Info($"Soil-Core: Attempting retry #{_retryAttempts + 1} after cooldown");
                                        InitializeAsync();
                                    }
                                    else if (BasicReady && UserPlayerPrefs.TokenData?.Access != null && !JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access))
                                    {
                                        MyDebug.Verbose("Soil-Core: Periodic token validation - access token invalid, forcing refresh");
                                        _ = PerformAuthenticationBackground(forceRefresh: true);
                                    }
                                    else if (Ready)
                                    {
                                        // Everything is good, but continue periodic checks
                                        MyDebug.Verbose("Soil-Core: Periodic validation - all good, scheduling next check");
                                    }

                                    // Always reschedule the next periodic check
                                    if (!_retryScheduled)
                                    {
                                        var periodicInterval = _retryIntervals[_retryIntervals.Length - 1];
                                        ScheduleRetryAfterCooldown(periodicInterval);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MyDebug.LogError($"Soil-Core: Error in periodic check: {ex.Message}");
                                }
                            });
                        }
                    }
                });
            }
        }

        private IEnumerator DelayedBroadcastCheck()
        {
            // Wait a bit for all async operations to complete
            yield return new WaitForSecondsRealtime(0.1f);

            // Check a few times with small delays
            for (int i = 0; i < 5 && !_readyBroadcasted; i++)
            {
                if (Ready)
                {
                    BroadcastReady();
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }

            if (!_readyBroadcasted)
            {
                var hasInstance = _instance != null;
                var instanceReady = _instance?._instanceReady ?? false;
                var sessionAuthValid = _instance?._sessionAuthSuccess ?? false;
                var initTaskCompleted = _initTask?.IsCompletedSuccessfully ?? false;
                var authValid = IsAuthenticationCurrentlyValid();
                var userInfoValid = UserPlayerPrefs.UserInfoInstance != null && !string.IsNullOrEmpty(UserPlayerPrefs.UserInfoInstance.uuid);

                MyDebug.LogWarning($"SoilServices: Ready state did not become true after initialization success. " +
                                 $"Instance={hasInstance}, InstanceReady={instanceReady}, InitCompleted={initTaskCompleted}, " +
                                 $"AuthValid={authValid}, SessionAuthValid={sessionAuthValid}, UserInfoValid={userInfoValid}");

                // If we have instance, session auth, and user info, but just the init task state is wrong, force broadcast
                if (hasInstance && instanceReady && sessionAuthValid && authValid && userInfoValid)
                {
                    MyDebug.LogWarning("SoilServices: Force broadcasting ready state despite initTask completion issue");
                    _readyBroadcasted = true;
                    OnServicesReady?.Invoke();
                }
            }
        }

        private IEnumerator RetryAfterCooldownCoroutine(float seconds)
        {
            if (seconds > 0f)
                yield return new WaitForSecondsRealtime(seconds);

            _retryScheduled = false;

            try
            {
                // Check if we need token refresh or full initialization
                if (!Ready)
                {
                    MyDebug.Info($"Soil-Core: Attempting retry #{_retryAttempts + 1} after cooldown");
                    InitializeAsync();
                }
                else if (BasicReady && UserPlayerPrefs.TokenData?.Access != null && !JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access))
                {
                    MyDebug.Verbose("Soil-Core: Periodic token validation - access token invalid, forcing refresh");
                    _ = PerformAuthenticationBackground(forceRefresh: true);
                }
                else if (Ready)
                {
                    // Everything is good, but continue periodic checks
                    MyDebug.Verbose("Soil-Core: Periodic validation - all good, scheduling next check");
                }

                // Always reschedule the next periodic check
                if (!_retryScheduled)
                {
                    var periodicInterval = _retryIntervals[_retryIntervals.Length - 1];
                    ScheduleRetryAfterCooldown(periodicInterval);
                }
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Soil-Core: Error in periodic check: {ex.Message}");
            }
        }

        private static UniTask PerformAuthentication(bool forceRefresh = false, bool forceSyncPlayerInfo = false)
        {
            return Authenticate.AuthenticateUser(forceRefresh: forceRefresh, forceSyncPlayerInfo: forceSyncPlayerInfo);
        }

        private static async UniTaskVoid PerformAuthenticationBackground(bool forceRefresh = false, bool forceSyncPlayerInfo = false)
        {
            try
            {
                await Authenticate.AuthenticateUser(forceRefresh: forceRefresh, forceSyncPlayerInfo: forceSyncPlayerInfo);
                MyDebug.Verbose("Soil-Core: Background authentication completed successfully");
            }
            catch (Exception ex)
            {
                MyDebug.LogWarning($"Soil-Core: Background authentication failed: {ex.Message}");

                // If this is a critical auth failure during periodic checks, we might need to trigger a full re-initialization
                if (ex is SoilException soilEx &&
                    (soilEx.ErrorCode == SoilExceptionErrorCode.InvalidToken ||
                     soilEx.ErrorCode == SoilExceptionErrorCode.TokenExpired ||
                     soilEx.ErrorCode == SoilExceptionErrorCode.Forbidden))
                {
                    MyDebug.Info("Soil-Core: Critical auth failure in background - triggering re-initialization");
                    InitializeAsync();
                }
            }
        }

        private static void HandleInitializationFailure(Exception exception)
        {
            // Check if this is a different error type than the last one - if so, allow faster retry
            var isDifferentErrorType = _lastInitFailureException == null ||
                                     exception.GetType() != _lastInitFailureException.GetType() ||
                                     exception.Message != _lastInitFailureException.Message;

            if (ReferenceEquals(exception, _lastInitFailureException))
            {
                return;
            }
            _lastInitFailureException = exception;

            // If it's a different error type and we haven't failed too many times, reset retry attempts partially
            if (isDifferentErrorType && _retryAttempts > 2)
            {
                _retryAttempts = Math.Max(1, _retryAttempts - 2); // Reduce penalty but don't go to zero
            }
            else
            {
                _retryAttempts++;
            }

            _lastFailureTime = DateTime.UtcNow;

            var retryInterval = GetCurrentRetryInterval();

            // Use less alarming logging for first few attempts - these are often transient network issues
            var logMessage = $"Soil-Core: Initialization failed (attempt #{_retryAttempts}). " +
                           $"Next retry in {retryInterval.TotalSeconds}s. Error: {exception.Message}";

            if (_retryAttempts <= 3)
            {
                MyDebug.Verbose(logMessage);
            }
            else
            {
                MyDebug.Info(logMessage);
            }

            ScheduleRetryAfterCooldown(retryInterval);
            var completed = DrainQueue(r => r.TaskSource.TrySetException(exception));
            if (completed.Count > 0)
                MyDebug.Verbose($"Soil-Core: Completed {completed.Count} queued requests with exception: [{string.Join(", ", completed)}]");

            try
            {
                var soilEx = exception as SoilException ?? new SoilException($"Initialization failed: {exception.Message}", DetermineInitializationFailureCode(exception));

                var canRetryManually = _retryAttempts < _retryIntervals.Length;

                // Capture the event reference to avoid race conditions
                var failedEvent = OnInitializationFailed;
                if (failedEvent != null)
                {
                    RunOnUnityContext(() =>
                    {
                        try
                        {
                            failedEvent.Invoke(soilEx);
                        }
                        catch (Exception invocationEx)
                        {
                            MyDebug.LogError($"Soil-Core: Exception in OnInitializationFailed handler: {invocationEx.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Soil-Core: Failed to process OnInitializationFailed: {ex.Message}");
            }
        }

        private static void OnInitializationSuccess()
        {
            try
            {
                if (_instance != null)
                    _instance._sessionAuthSuccess = true;
                _initSucceeded = true; // mark completion
                _retryAttempts = 0;
                _lastFailureTime = DateTime.MinValue;

                // Drain and complete all queued init requests
                var completed = DrainQueue(r => r.TaskSource.TrySetResult(true));
                if (completed.Count > 0)
                    MyDebug.Verbose($"Soil-Core: Completed {completed.Count} queued requests successfully: [{string.Join(", ", completed)}]");

                // Try broadcasting immediately, but also schedule a delayed check in case Ready isn't true yet
                BroadcastReady();

                // If broadcasting failed (Ready was false), schedule a retry
                if (!_readyBroadcasted && _instance != null)
                {
                    _instance.StartCoroutine(_instance.DelayedBroadcastCheck());
                }

                // Schedule periodic token validation using the retry mechanism
                if (!_retryScheduled)
                {
                    var periodicCheckInterval = _retryIntervals[_retryIntervals.Length - 1];
                    ScheduleRetryAfterCooldown(periodicCheckInterval);
                }
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Soil-Core: Critical error in OnInitializationSuccess: {ex.Message}");
            }
        }
        private static void CompleteAndClearQueuedRequests(Exception exception)
        {
            try
            {
                var completed = DrainQueue(r => r.TaskSource.TrySetException(exception));
                if (completed.Count > 0)
                    MyDebug.Verbose($"Soil-Core: Completed {completed.Count} queued requests due to reset: [{string.Join(", ", completed)}]");
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Soil-Core: Critical error in CompleteAndClearQueuedRequests: {ex.Message}");
            }
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
            try
            {
                var expiredThreshold = DateTime.UtcNow.AddMinutes(-5);
                List<QueuedInitRequest> expiredRequests;
                lock (_queueLock)
                {
                    expiredRequests = _queuedInitRequests.Where(r => r.QueuedTime < expiredThreshold).ToList();

                    // Process expired requests safely - create copy to avoid modification during enumeration
                    foreach (var expired in expiredRequests)
                    {
                        try
                        {
                            expired.TaskSource.TrySetException(new SoilException(
                                $"Initialization request expired after 5 minutes (ID: {expired.RequestId})",
                                SoilExceptionErrorCode.Timeout));
                        }
                        catch (Exception ex)
                        {
                            MyDebug.LogWarning($"Soil-Core: Failed to set exception for expired request: {ex.Message}");
                        }
                    }

                    // Remove expired requests from the original list
                    foreach (var expired in expiredRequests)
                    {
                        _queuedInitRequests.Remove(expired);
                    }
                }
                if (expiredRequests.Count > 0)
                    MyDebug.Info($"Soil-Core: Cleaned up expired initialization requests");
            }
            catch (Exception ex)
            {
                MyDebug.LogError($"Soil-Core: Critical error in CleanupExpiredRequests: {ex.Message}");
            }
        }

        private static void BroadcastReady()
        {
            if (_readyBroadcasted) return;

            // Double-check that Ready property is actually true before broadcasting
            if (!Ready)
            {
                MyDebug.Verbose("BroadcastReady called but Ready property is false, skipping broadcast");
                return;
            }

            MyDebug.Info($"Soil-Core: Services are ready - {UserInfo?.uuid}");
            _readyBroadcasted = true;
            OnServicesReady?.Invoke();
        }

        private static async UniTask AwaitWithDeadline(UniTask task, DateTime deadline, string stage)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                throw new SoilException($"Initialization timed out during {stage}", SoilExceptionErrorCode.Timeout);

            var completed = await UniTask.WhenAny(task, UniTask.Delay(remaining));
            if (completed != 0)
                throw new SoilException($"Initialization timed out during {stage}", SoilExceptionErrorCode.Timeout);
        }

        /// <summary>
        /// Awaits a Task with a deadline, throwing a timeout exception if the deadline is exceeded.
        /// This overload is specifically for Task objects to avoid UniTask conversion overhead.
        /// </summary>
        private static async UniTask AwaitWithDeadline(Task task, DateTime deadline, string stage)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                throw new SoilException($"Initialization timed out during {stage}", SoilExceptionErrorCode.Timeout);

            var completed = await UniTask.WhenAny(task.AsUniTask(), UniTask.Delay(remaining));
            if (completed != 0)
                throw new SoilException($"Initialization timed out during {stage}", SoilExceptionErrorCode.Timeout);
        }

        private static async UniTaskVoid AttachInitializationSuccessCallback(Task task)
        {
            try
            {
                await task;
                RunOnUnityContext(OnInitializationSuccess);
            }
            catch
            {
                // failure handled elsewhere
            }
        }

        private static bool IsAuthenticationCurrentlyValid()
        {
            try
            {
                var tokenData = UserPlayerPrefs.TokenData;
                if (tokenData == null || string.IsNullOrEmpty(tokenData.Access))
                {
                    MyDebug.Verbose("Soil-Core: No token data available");
                    return false;
                }

                var accessValid = JwtUtils.IsTokenValid(tokenData.Access);
                if (accessValid)
                {
                    _lastAuthValidTime = DateTime.UtcNow; // update last known good
                    MyDebug.Verbose("Soil-Core: Authentication validation passed - access token valid");
                    return true;
                }

                // Access token invalid/expired here.
                var refreshValid = !string.IsNullOrEmpty(tokenData.Refresh) && JwtUtils.IsTokenValid(tokenData.Refresh);
                var initInProgress = _initTask != null && !_initTask.IsCompleted;

                // Allow a grace window after last validity OR while an init/auth cycle is in progress and refresh token is still good
                if (refreshValid)
                {
                    // If initialization is in progress with valid refresh token, be more lenient
                    if (initInProgress)
                    {
                        MyDebug.Verbose("Soil-Core: Access token invalid but init in progress with valid refresh token - treating as temporarily valid");
                        return true;
                    }

                    if (_lastAuthValidTime != DateTime.MinValue)
                    {
                        var sinceValid = DateTime.UtcNow - _lastAuthValidTime;
                        // Extended grace period when network is unavailable - be more lenient offline
                        var graceWindow = IsNetworkAvailable ? _authGracePeriod : _authGracePeriod.Add(TimeSpan.FromMinutes(5));
                        if (sinceValid <= graceWindow)
                        {
                            MyDebug.Verbose($"Soil-Core: Access token expired; within grace window ({sinceValid.TotalMilliseconds:F0}ms <= {graceWindow.TotalMilliseconds:F0}ms) – treating as temporarily valid");
                            return true;
                        }
                    }
                }

                MyDebug.Verbose("Soil-Core: Authentication validation failed - access token invalid and no temporary conditions satisfied");
                return false;
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

        private static SoilExceptionErrorCode DetermineInitializationFailureCode(Exception exception)
        {
            try
            {
                if (exception == null) return SoilExceptionErrorCode.Unknown;

                if (exception is SoilException se)
                    return se.ErrorCode;

                if (exception is OperationCanceledException)
                    return SoilExceptionErrorCode.Canceled;

                if (exception is TimeoutException || exception is OperationCanceledException)
                    return SoilExceptionErrorCode.Timeout;

                if (!IsNetworkAvailable)
                    return SoilExceptionErrorCode.TransportError;

                var msg = exception.Message ?? string.Empty;
                var lower = msg.ToLowerInvariant();

                if (lower.Contains("timeout"))
                    return SoilExceptionErrorCode.Timeout;
                if (lower.Contains("network") || lower.Contains("connection") || lower.Contains("unreachable"))
                    return SoilExceptionErrorCode.TransportError;
                if (lower.Contains("service unavailable") || lower.Contains("temporarily unavailable"))
                    return SoilExceptionErrorCode.ServiceUnavailable;
                if (lower.Contains("too many requests") || lower.Contains("rate limit"))
                    return SoilExceptionErrorCode.TooManyRequests;
                if (lower.Contains("forbidden") || lower.Contains("access denied"))
                    return SoilExceptionErrorCode.Forbidden;
                if (lower.Contains("not found"))
                    return SoilExceptionErrorCode.NotFound;
                if (lower.Contains("conflict"))
                    return SoilExceptionErrorCode.Conflict;
                if (lower.Contains("invalid token"))
                    return SoilExceptionErrorCode.InvalidToken;
                if (lower.Contains("expired token") || lower.Contains("token expired"))
                    return SoilExceptionErrorCode.TokenExpired;
                if (lower.Contains("invalid request") || lower.Contains("bad request"))
                    return SoilExceptionErrorCode.InvalidRequest;
                if (lower.Contains("misconfig") || lower.Contains("config") || lower.Contains("configuration"))
                    return SoilExceptionErrorCode.MisConfiguration;

                if (!Ready)
                    return SoilExceptionErrorCode.NotReady;

                return SoilExceptionErrorCode.Unknown;
            }
            catch
            {
                return SoilExceptionErrorCode.Unknown;
            }
        }

        /// <summary>
        /// Drains the queued initialization requests, invoking the handler on each and clearing the queue.
        /// </summary>
        private static List<string> DrainQueue(Func<QueuedInitRequest, bool> handler)
        {
            var completedIds = new List<string>();
            lock (_queueLock)
            {
                if (_queuedInitRequests.Count == 0)
                    return completedIds;

                var requestsCopy = _queuedInitRequests.ToList();
                foreach (var req in requestsCopy)
                {
                    try
                    {
                        if (handler(req))
                            completedIds.Add(req.RequestId);
                    }
                    catch (Exception ex)
                    {
                        MyDebug.LogWarning($"Soil-Core: Failed to process queued request {req.RequestId}: {ex.Message}");
                    }
                }
                _queuedInitRequests.Clear();
            }
            return completedIds;
        }
    }
}