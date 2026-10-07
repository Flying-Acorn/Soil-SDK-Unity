# Feedback

## Introduction

Let players tell you what they think, from inside the game: a support message from settings, a rating with a
reason, an idea, a word they want added. Everything goes to your team's dashboard (**Feedback** in the Soil
dashboard). It is private: no player ever sees another player's feedback.

## Channels

A game sends feedback to a **channel**, by its key. Channels are created on the dashboard, and each sets its own
rules:

| Rule | Example |
|---|---|
| Rating (1 to 5) | not allowed, optional, or required |
| Message | required, or optional when a rating alone is enough |
| Longest message | 40 characters for a word, 2000 for a support message |
| Per player per day | how many one player can send in any 24 hours |
| On / off | off answers `ChannelNotFound`; nothing already sent is lost |

The dashboard offers ready-made starting points: **Support**, **Rating with a reason**, **Ideas** and **Word
suggestion**. Rules can be changed at any time without a new build; the key cannot, because your game sends it.

A channel can also **group by target**: many players sending the same small thing (a word, a level) become one
item with a player count, reviewed once, exported one per line. See
[Grouped channels](Integration.md#7-grouped-channels-many-players-one-item). Such a channel can also take several
targets in one send, each counted as if sent alone: see
[Several targets in one send](Integration.md#8-several-targets-in-one-send).

## What each submission carries

- `Target` (optional): what it is about - a word, a level id, a feature. The dashboard groups and counts by it,
  so "KITE suggested by 37 players" or "level_12 averages 2.1 stars" is one click.
- `Rating` (optional): 1 to 5, where the channel takes it.
- `Message`: what the player wrote.
- `Data` (optional): up to 2 KB of extra context as JSON - level, score, settings.

The server also records the player's app version, platform, country and device at the time, so you do not need
to send them.

## Closing the loop

Your team marks each submission **planned**, **applied** or **declined** on the dashboard (with an internal note
the player never sees). `Feedback.GetMyFeedback()` returns the player's own submissions with that status, and
whether anyone has seen them - so a word game can say "Your word KITE was added!".

## Spam

Built in, nothing to configure in the game:

- per-player rate limits across all channels, plus each channel's daily limit;
- retrying the same `FeedbackSubmission` (same client id) saves it once;
- the same feedback again within a day is saved once;
- staff can mute a player: they can still send and are told it was sent, but it lands as spam, out of the inbox.

See [Integration](Integration.md) for code.
