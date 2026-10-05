using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Socialization.Data;
using FlyingAcorn.Soil.Socialization.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.Socialization.Demo
{
    /// <summary>
    /// Friend requests: send, accept/decline, cancel, remove and block, one list at a time.
    /// Needs the app's Friend requests feature.
    /// <para>
    /// Referrals (needs the Referrals feature): "Enter invite code" enters the typed code as the player's
    /// inviter, and "Invites" shows who invited them and how many they invited. Both buttons are copies of
    /// Refresh made at runtime, so the scene needs no change.
    /// </para>
    /// </summary>
    public class FriendRequestsDemoHandler : MonoBehaviour
    {
        [SerializeField] private FriendRequestRow rowPrefab;
        [SerializeField] private VerticalLayoutGroup rowsContainer;
        [SerializeField] private TMP_InputField idInput;
        [SerializeField] private Button sendRequestButton;
        [SerializeField] private Button blockButton;
        [SerializeField] private Button friendsTab;
        [SerializeField] private Button incomingTab;
        [SerializeField] private Button outgoingTab;
        [SerializeField] private Button blockedTab;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI statusText;

        private Button _redeemButton;
        private Button _referralInfoButton;

        private readonly List<FriendRequestRow> _rows = new();
        private FriendListKind _list = FriendListKind.Friends;
        private bool _busy;

        private void Start()
        {
            headerText.text = "Initializing...";
            statusText.text = "Initializing...";
            ClearRows();
            _redeemButton = CopyButton(refreshButton, "Enter invite code");
            _referralInfoButton = CopyButton(refreshButton, "Invites");

            SoilServices.OnServicesReady += OnSoilServicesReady;
            SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;

            sendRequestButton.onClick.AddListener(SendRequest);
            blockButton.onClick.AddListener(BlockTyped);
            friendsTab.onClick.AddListener(ShowFriends);
            incomingTab.onClick.AddListener(ShowIncoming);
            outgoingTab.onClick.AddListener(ShowOutgoing);
            blockedTab.onClick.AddListener(ShowBlocked);
            copyCodeButton.onClick.AddListener(CopyMyCode);
            refreshButton.onClick.AddListener(Refresh);
            _redeemButton.onClick.AddListener(RedeemTyped);
            _referralInfoButton.onClick.AddListener(ShowReferralInfo);

            if (SoilServices.Ready)
                OnSoilServicesReady();
            else
                SoilServices.InitializeAsync();
        }

        private void OnDestroy()
        {
            if (SoilServices.OnServicesReady != null)
                SoilServices.OnServicesReady -= OnSoilServicesReady;
            if (SoilServices.OnInitializationFailed != null)
                SoilServices.OnInitializationFailed -= OnSoilServicesInitializationFailed;

            foreach (var button in Buttons())
                if (button != null)
                    button.onClick.RemoveAllListeners();
        }

        private IEnumerable<Button> Buttons() => new[]
        {
            sendRequestButton, blockButton, friendsTab, incomingTab, outgoingTab, blockedTab, copyCodeButton,
            refreshButton, _redeemButton, _referralInfoButton
        };

        /// <summary>A copy of a button beside it, with its own label and no listeners.</summary>
        private static Button CopyButton(Button template, string label)
        {
            var copy = Instantiate(template, template.transform.parent);
            copy.name = label;
            copy.onClick.RemoveAllListeners();
            copy.GetComponentInChildren<TextMeshProUGUI>().text = label;
            return copy;
        }

        private void OnSoilServicesReady()
        {
            // The code other players type to send this player a request, and to name them as their inviter.
            headerText.text = $"My code: {SoilServices.UserInfo.public_id}";
            statusText.text = "Ready";
            Refresh();
        }

        private void OnSoilServicesInitializationFailed(SoilException exception)
        {
            statusText.text = $"SDK initialization failed: {exception.Message}";
        }

        private void ShowFriends() => Show(FriendListKind.Friends);
        private void ShowIncoming() => Show(FriendListKind.Incoming);
        private void ShowOutgoing() => Show(FriendListKind.Outgoing);
        private void ShowBlocked() => Show(FriendListKind.Blocked);

        private void Show(FriendListKind list)
        {
            _list = list;
            Refresh();
        }

        private void Refresh()
        {
            _ = LoadListAsync();
        }

        private void CopyMyCode()
        {
            var code = SoilServices.Ready ? SoilServices.UserInfo.public_id : null;
            if (string.IsNullOrEmpty(code)) return;
            GUIUtility.systemCopyBuffer = code;
            statusText.text = $"Copied {code}";
        }

        /// <summary>A UUID sends by UUID; anything else is treated as a player code.</summary>
        private void SendRequest()
        {
            var typed = idInput.text.Trim();
            if (string.IsNullOrEmpty(typed))
            {
                statusText.text = "Type a player code or UUID";
                return;
            }

            Run(() => Guid.TryParse(typed, out _) ? Socialization.SendFriendRequest(typed) : Socialization.SendFriendRequestByCode(typed));
        }

        private void BlockTyped()
        {
            var typed = idInput.text.Trim();
            if (!Guid.TryParse(typed, out _))
            {
                // Block takes a UUID only: unlike requests it is not rate limited, so codes are not accepted.
                statusText.text = "Block needs a UUID. Use a row's Block button for players in a list.";
                return;
            }

            Run(() => Socialization.BlockPlayer(typed));
        }

        /// <summary>Enters the typed code as this player's referral code.</summary>
        private void RedeemTyped()
        {
            var typed = idInput.text.Trim();
            if (string.IsNullOrEmpty(typed))
            {
                statusText.text = "Type the code of the player who invited you";
                return;
            }

            RunReferral(async () => Describe(await Socialization.RedeemReferralCode(typed)));
        }

        private void ShowReferralInfo()
        {
            RunReferral(async () => Describe(await Socialization.GetReferralInfo()));
        }

        private void RunReferral(Func<UniTask<string>> action)
        {
            _ = RunReferralAsync(action);
        }

        private async UniTask RunReferralAsync(Func<UniTask<string>> action)
        {
            if (!SetBusy(true)) return;
            statusText.text = "Working...";
            string text;
            try
            {
                text = await action();
            }
            catch (Exception e)
            {
                text = Message(e);
            }

            // The scene may have been left while the call was running.
            if (this == null) return;
            statusText.text = text;
            SetBusy(false);
        }

        /// <summary>Starts an action unless another call is still running, so a double tap sends once.</summary>
        private void Run(Func<UniTask<FriendActionResult>> action)
        {
            _ = RunAsync(action);
        }

        private async UniTask RunAsync(Func<UniTask<FriendActionResult>> action)
        {
            if (!SetBusy(true)) return;
            statusText.text = "Working...";
            FriendActionResult result;
            try
            {
                result = await action();
            }
            catch (Exception e)
            {
                if (this == null) return;
                statusText.text = Message(e);
                SetBusy(false);
                return;
            }

            if (this == null) return;
            statusText.text = Describe(result);
            SetBusy(false);
            await LoadListAsync(keepStatus: true);
        }

        private async UniTask LoadListAsync(bool keepStatus = false)
        {
            if (!SetBusy(true)) return;
            if (!keepStatus) statusText.text = "Refreshing...";
            // The rows and their actions follow the list asked for, whatever is selected by the time it arrives.
            var kind = _list;
            FriendList list;
            try
            {
                list = await Socialization.GetFriendList(kind);
            }
            catch (Exception e)
            {
                if (this == null) return;
                statusText.text = Message(e);
                ClearRows();
                SetBusy(false);
                return;
            }

            if (this == null) return;
            SetBusy(false);
            ShowList(list, kind, keepStatus);
        }

        private void ShowList(FriendList list, FriendListKind kind, bool keepStatus)
        {
            ClearRows();
            SetTabLabels(list.counts, kind);
            foreach (var entry in list.users)
            {
                var row = Instantiate(rowPrefab, rowsContainer.transform);
                row.SetData(entry, Since(entry.since));
                SetActions(row, entry, kind);
                _rows.Add(row);
            }

            if (!keepStatus)
                statusText.text = list.users.Count == 0 ? $"No one in {Title(kind)}" : "";
        }

        /// <summary>What a player can do from each list.</summary>
        private void SetActions(FriendRequestRow row, FriendEntry entry, FriendListKind kind)
        {
            var uuid = entry.uuid;
            switch (kind)
            {
                case FriendListKind.Friends:
                    row.SetAction(true, "Remove", () => Run(() => Socialization.RemoveFriend(uuid)));
                    row.SetAction(false, "Block", () => Run(() => Socialization.BlockPlayer(uuid)));
                    break;
                case FriendListKind.Incoming:
                    row.SetAction(true, "Accept", () => Run(() => Socialization.AcceptFriendRequest(uuid)));
                    row.SetAction(false, "Decline", () => Run(() => Socialization.DeclineFriendRequest(uuid)));
                    break;
                case FriendListKind.Outgoing:
                    row.SetAction(true, "Cancel", () => Run(() => Socialization.CancelFriendRequest(uuid)));
                    row.SetAction(false, null, null);
                    break;
                case FriendListKind.Blocked:
                    row.SetAction(true, "Unblock", () => Run(() => Socialization.UnblockPlayer(uuid)));
                    row.SetAction(false, null, null);
                    break;
            }
        }

        private void SetTabLabels(FriendCounts counts, FriendListKind kind)
        {
            SetLabel(friendsTab, $"Friends ({counts.friends})", kind == FriendListKind.Friends);
            SetLabel(incomingTab, $"Received ({counts.incoming})", kind == FriendListKind.Incoming);
            SetLabel(outgoingTab, $"Sent ({counts.outgoing})", kind == FriendListKind.Outgoing);
            SetLabel(blockedTab, $"Blocked ({counts.blocked})", kind == FriendListKind.Blocked);
        }

        private static void SetLabel(Button button, string text, bool selected)
        {
            button.GetComponentInChildren<TextMeshProUGUI>().text = selected ? $"<b>{text}</b>" : text;
        }

        private static string Title(FriendListKind list) => list switch
        {
            FriendListKind.Incoming => "received requests",
            FriendListKind.Outgoing => "sent requests",
            FriendListKind.Blocked => "blocked players",
            _ => "friends",
        };

        /// <summary>An exception's text, with how long to wait when the server said so (a list read too often).</summary>
        private static string Message(Exception e) =>
            e is SocializationException { RetryAfterSeconds: { } wait } ? $"{e.Message}\nTry again in {wait}s" : e.Message;

        /// <summary>A refusal is an answer: say what happened instead of treating it as an error.</summary>
        private static string Describe(FriendActionResult result)
        {
            var text = DescribeRequest(result);
            // Only when the app counts requests by code as invites.
            return result.invite == null ? text : $"{text}\nInvite: {Describe(result.invite, result.user)}";
        }

        private static string DescribeRequest(FriendActionResult result)
        {
            var name = result.user == null ? "the player" : Name(result.user);
            return result.Status switch
            {
                FriendStatus.RequestSent => $"Request sent to {name}",
                FriendStatus.FriendshipCreated => $"You and {name} are now friends",
                FriendStatus.FriendshipExists => $"You and {name} are already friends",
                FriendStatus.FriendshipDeleted => $"Removed {name}",
                FriendStatus.RequestDeclined => $"Declined {name}'s request",
                FriendStatus.RequestCancelled => $"Cancelled the request to {name}",
                FriendStatus.UserBlocked => $"Blocked {name}",
                FriendStatus.UserUnblocked => $"Unblocked {name}",
                FriendStatus.FriendNotFound => "No player with that code in this game",
                FriendStatus.FriendshipIllegalSelf => "That is you",
                FriendStatus.RequestNotFound => "That request is gone",
                FriendStatus.FriendBlocked => $"You blocked {name}. Unblock them first",
                FriendStatus.FriendLimitReached => "A friend list is full",
                FriendStatus.RequestLimitReached => "Too many requests waiting for an answer",
                FriendStatus.BlockLimitReached => "Too many blocked players",
                FriendStatus.Throttled => $"Too many requests. Try again in {result.RetryAfterSeconds ?? 60}s",
                _ => result.detail?.message ?? "Something went wrong",
            };
        }

        private static string Describe(ReferralRedeemResult result)
        {
            var text = Describe(result, result.inviter);
            return result.Status == ReferralStatus.Throttled
                ? $"{text}. Try again in {result.RetryAfterSeconds ?? 60}s"
                : text;
        }

        private static string Describe(ReferralInvite invite, FriendProfile inviter)
        {
            var name = inviter == null ? "the player" : Name(inviter);
            return invite.Status switch
            {
                ReferralStatus.Invited => invite.reward == null
                    ? $"{name} is now your inviter"
                    // Already in the player's Soil balance: show it, do not grant it again.
                    : $"{name} is now your inviter. You got {invite.reward.amount} {invite.reward.currency}",
                ReferralStatus.CodeNotFound => "No player with that code in this game",
                ReferralStatus.OwnCode => "That is your own code",
                ReferralStatus.AlreadyInvited => "You already have an inviter",
                ReferralStatus.WindowClosed => "Too late to enter a code",
                ReferralStatus.MutualInvite => "You invited that player, so they cannot be your inviter",
                ReferralStatus.Throttled => "Too many tries",
                ReferralStatus.ReferralError => "Could not enter the code. Try again with Enter invite code",
                _ => invite.detail?.message ?? "Something went wrong",
            };
        }

        private static string Describe(ReferralInfo info)
        {
            var invitedBy = !info.invited
                ? "No one invited you"
                : info.invited_by == null
                    ? "Invited by a deleted account"
                    : $"Invited by {Name(info.invited_by)} ({info.invited_by.public_id})";
            var redeem = !info.can_redeem
                ? "You cannot enter a code"
                : info.RedeemUntilUtc is { } until
                    ? $"You can enter a code until {until.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}"
                    : "You can enter a code";
            return $"{invitedBy}\n{redeem}\nYou invited {info.invited_count} player(s)";
        }

        private static string Name(FriendProfile player) =>
            !string.IsNullOrEmpty(player.name) ? player.name
            : !string.IsNullOrEmpty(player.username) ? player.username
            : "the player";

        private static string Since(string iso)
        {
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)
                ? when.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : iso;
        }

        /// <summary>One call at a time; false when another is still running.</summary>
        private bool SetBusy(bool busy)
        {
            if (busy && _busy) return false;
            _busy = busy;
            foreach (var button in Buttons())
                if (button != null)
                    button.interactable = !busy;
            foreach (var row in _rows)
            {
                if (row == null) continue;
                row.primaryButton.interactable = !busy;
                row.secondaryButton.interactable = !busy;
            }

            return true;
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
                if (row != null)
                    Destroy(row.gameObject);
            _rows.Clear();
        }
    }
}
