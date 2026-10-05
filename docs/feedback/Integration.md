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

## Things to know

- A refusal (unknown channel, a rule, a limit) is a `FeedbackSendResult` with a `Status`, not an exception. Only a
  transport failure, an expired sign-in, or the Feedback feature being off throws a `FeedbackException`
  (`Forbidden` for the feature).
- Rate limits: 10 sends a minute and 100 a day per player across all channels, plus each channel's own daily limit.
  Refused sends count too. `RetryAfterSeconds` says how long to wait.
- Each player can read back their latest 50 submissions. Staff notes are never sent to the game, and feedback set
  aside as spam reads as `New`.
- Hard limits whatever the channel says: message 4000 characters, target 100, data 2 KB.

## API Reference

| Call | Returns |
|---|---|
| `Feedback.GetChannels()` | `UniTask<FeedbackChannelList>` |
| `Feedback.Send(FeedbackSubmission submission)` | `UniTask<FeedbackSendResult>` |
| `Feedback.SendMessage(string channel, string message, string target = null, IDictionary<string, object> data = null)` | `UniTask<FeedbackSendResult>` |
| `Feedback.SendRating(string channel, int rating, string reason = null, string target = null, IDictionary<string, object> data = null)` | `UniTask<FeedbackSendResult>` |
| `Feedback.GetMyFeedback(string channel = null)` | `UniTask<FeedbackList>` |
| `FeedbackProtocol.Check(FeedbackChannelInfo channel, FeedbackSubmission submission)` | `FeedbackStatus?` |

## Other Documentations

- [Introduction](Introduction.md)
- [Installation](../Installation.md)
