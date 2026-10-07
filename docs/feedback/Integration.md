# Feedback Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

**Service Enablement**: Ensure the Feedback service is enabled for your app. Reach out to your Soil contact to
enable it, then create your channels in the dashboard under **Feedback → Channels**.

## 1. Setup

```csharp
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Feedback;
using FlyingAcorn.Soil.Feedback.Logic;

if (!SoilServices.Ready)
{
    SoilServices.OnServicesReady += OnSDKReady;
    SoilServices.InitializeAsync();
}
```

## 2. A support or ideas form

```csharp
private async void OnSendPressed(string text)
{
    var result = await Feedback.SendMessage("support", text,
        data: new Dictionary<string, object> { ["screen"] = "settings", ["level"] = currentLevel });

    switch (result.Status)
    {
        case FeedbackStatus.FeedbackSent:
        case FeedbackStatus.AlreadyReceived:
            ShowThanks();
            break;
        case FeedbackStatus.DailyLimitReached:
        case FeedbackStatus.Throttled:
            ShowTryLater(result.RetryAfterSeconds);
            break;
        case FeedbackStatus.MessageTooLong:
            ShowError("That's a bit long.");
            break;
        default:
            ShowError("Could not send feedback.");
            break;
    }
}
```

## 3. An in-game rating panel that asks why

Create a channel with **Rating: Required** and **Message required: off** (the *Rating with a reason* preset).

```csharp
private async void OnStarsPicked(int stars)
{
    if (stars >= 4)
    {
        await Feedback.SendRating("rate_app", stars);
        OpenStoreReview();           // Happy players go to the store.
        return;
    }
    reasonPanel.Show(stars);         // Below four: ask what went wrong.
}

private async void OnReasonSubmitted(int stars, string reason)
{
    var result = await Feedback.SendRating("rate_app", stars, reason, target: "level_" + currentLevel);
    if (result.Succeeded) ShowThanks();
}
```

## 4. Word suggestions, with the result shown back

```csharp
var result = await Feedback.SendMessage("word_suggestion", typed, target: typed.Trim().ToUpperInvariant());

// Later, for example when the player opens the suggestions screen:
var mine = await Feedback.GetMyFeedback("word_suggestion");
foreach (var entry in mine.feedback)
{
    if (entry.ReviewStatus == FeedbackReviewStatus.Applied) ShowAdded(entry.target);
}
```

## 5. Retrying safely

A `FeedbackSubmission` carries a client id. Keep the same instance when retrying after a timeout, and the server
saves it once (`AlreadyReceived` on the retry).

```csharp
var submission = new FeedbackSubmission("support") { Message = text };
for (var attempt = 0; attempt < 3; attempt++)
{
    try { var result = await Feedback.Send(submission); break; }
    catch (FeedbackException e) when (e.ErrorCode == SoilExceptionErrorCode.Timeout) { await UniTask.Delay(2000); }
}
```

## 6. Checking a form before sending

`GetChannels()` returns each channel's rules and what the player has left today. Use it to hide a button whose
channel is off, set a text field's character limit, or check a form without a round trip:

```csharp
var channels = await Feedback.GetChannels();
var support = channels.Find("support");
supportButton.SetActive(support != null && support.remaining_today > 0);
inputField.characterLimit = support?.max_message_length ?? 0;

var problem = FeedbackProtocol.Check(support, new FeedbackSubmission("support") { Message = inputField.text });
if (problem != null) ShowError(problem.ToString());
```

## 7. Grouped channels: many players, one item

A channel can **group by target** (set on the dashboard): every distinct target is one item with a status and a
count of distinct players, reviewed once under **Feedback → Targets**. With **once per target**, a player counts
once per target, ever. Use it when many players send the same small thing: a word they want accepted, a level they
find too hard.

- The target is required (`TargetRequired` otherwise); a rating and a message may both be left out.
- The server only matches targets ignoring case, spacing and Unicode normalization (NFC);
  `FeedbackProtocol.TargetKey(target)` gives the same key in the game. **Settle everything else in the game before
  sending** (letter variants, diacritics, your own format): the first spelling the server receives of each target,
  with control characters dropped and spaces collapsed, is what the dashboard shows and exports, one per line. For
  example, a word game can send `"BAR, English"`.
- With **once per target**, sending the same target again answers `AlreadyReceived` and saves nothing, so a double
  tap or a retry is harmless. Without it, a repeat is only answered `AlreadyReceived` when it is a retry of the same
  `FeedbackSubmission`, or has the same rating and message within a day; otherwise it is saved again and adds to the
  item's submissions (the player still counts once).
- When staff decide the item, every player who sent it sees that status in `GetMyFeedback`.

```csharp
var result = await Feedback.Send(new FeedbackSubmission("word_suggestion") { Target = $"{word}, {language}" });
if (result.Succeeded) ShowThanks();   // FeedbackSent or AlreadyReceived
```

## 8. Several targets in one send

A grouped channel whose `max_targets_per_send` is above 1 (set on the dashboard, up to 10) takes a list, such as
words a player added one by one with a **+**. It is only a lighter way to send: each distinct target is saved,
counted and reviewed exactly as if sent alone, so keep building each target as in section 7. Don't pack the list
into a message or `data` yourself, or the server cannot count or merge the words.

- Cap the list at the channel's `max_targets_per_send`; more is refused whole with `TooManyTargets`. So is any
  empty or too-long item (`TargetRequired`, `TargetTooLong`). `FeedbackProtocol.Check` tells you first.
- Targets with the same key (`FeedbackProtocol.TargetKey`) in one list are sent once, under the first spelling.
- Each target counts toward the daily limit, so a list can be partly saved. The first ones that fit go through.
- `result.targets` has each distinct target's own result. `Accepted` is true when the server has it from this
  player (sent now or before), and `retry_after` is set when the limit left it out. `result.Status` is for the send
  as a whole: `FeedbackSent` if any was saved, else `DailyLimitReached` if any hit the limit, else `AlreadyReceived`.
- Each result's `target` is the target as the server kept it (trimmed, spaces collapsed), not always the string you
  sent. Match results to your list by `FeedbackProtocol.TargetKey`, as below, not by comparing strings.
- Retrying the same `FeedbackSubmission` after a timeout saves nothing twice, and still tries the targets the limit
  left out.

```csharp
var channel = (await Feedback.GetChannels()).Find("word_suggestion");
var submission = new FeedbackSubmission("word_suggestion") { Targets = words.Select(w => $"{w}, {language}").ToList() };
if (FeedbackProtocol.Check(channel, submission) is { } problem) { ShowError(problem); return; }

var result = await Feedback.Send(submission);        // or Feedback.SendTargets("word_suggestion", targets)
var accepted = new HashSet<string>((result.targets ?? new List<FeedbackTargetResult>())
    .Where(each => each.Accepted).Select(each => FeedbackProtocol.TargetKey(each.target)));
foreach (var target in submission.Targets)
    if (accepted.Contains(FeedbackProtocol.TargetKey(target))) RememberSuggested(target);
ShowThanks();
```

## Things to know

- A refusal (unknown channel, a rule, a limit) is a `FeedbackSendResult` with a `Status`, not an exception. Only a
  transport failure, an expired sign-in, or the Feedback feature being off throws a `FeedbackException`
  (`Forbidden` for the feature).
- Rate limits: 10 sends a minute and 100 a day per player across all channels, plus each channel's own daily limit.
  Refused sends count too. `RetryAfterSeconds` says how long to wait.
- Each player can read back their latest 50 submissions. Staff notes are never sent to the game, and feedback set
  aside as spam reads as `New`.
- Hard limits whatever the channel says: message 4000 characters, target 100, data 2 KB.
- `FeedbackProtocol.Check` cannot know a target was already sent: under once-per-target a repeat past the daily
  limit is answered `AlreadyReceived` by the server, while `Check` says `DailyLimitReached`.

## API Reference

| Call | Returns |
|---|---|
| `Feedback.GetChannels()` | `UniTask<FeedbackChannelList>` |
| `Feedback.Send(FeedbackSubmission submission)` | `UniTask<FeedbackSendResult>` |
| `Feedback.SendMessage(string channel, string message, string target = null, IDictionary<string, object> data = null)` | `UniTask<FeedbackSendResult>` |
| `Feedback.SendRating(string channel, int rating, string reason = null, string target = null, IDictionary<string, object> data = null)` | `UniTask<FeedbackSendResult>` |
| `Feedback.SendTargets(string channel, IList<string> targets, IDictionary<string, object> data = null)` | `UniTask<FeedbackSendResult>` with `targets` |
| `Feedback.GetMyFeedback(string channel = null)` | `UniTask<FeedbackList>` |
| `FeedbackProtocol.Check(FeedbackChannelInfo channel, FeedbackSubmission submission)` | `FeedbackStatus?` |
| `FeedbackProtocol.TargetKey(string target)` | `string`: the key a grouped channel matches targets by |

## Other Documentations

- [Introduction](Introduction.md)
- [Installation](../Installation.md)
