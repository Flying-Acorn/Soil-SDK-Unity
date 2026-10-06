# Push

## Introduction

Soil tells players about what happens to them while they are away, as phone notifications sent through Firebase
Cloud Messaging:

| Push | Who gets it |
|---|---|
| Friend request | the player who got a request |
| Friend request accepted | the player whose request was accepted |
| Leaderboard prize | each player a leaderboard reset paid, on leaderboards with **notify winners** on |
| Invite reward | the player who invited someone, when they are paid for it |

The server sends every push at the moment the event happens. The game sends nothing; it only hands over its
device token, and with the Firebase Messaging package in the project, the SDK's Firebase bridge does that on its
own.

## What the game gets

- **Nothing to call** for the pushes above. Add the SDK, keep Firebase Messaging in the project, and register on
  the dashboard.
- **Taps**: `Push.OnOpenedFromNotification` says what the push was about (`PushKind`) and who or what (`Ref`), so a
  tap can open the friends screen or the leaderboard. A tap that launched the game is kept until you subscribe.
- **Foreground pushes**: the phone shows nothing while the game is open; `Push.OnMessageReceived` lets you show an
  in-game note instead.
- **Tray**: `Push.ClearDelivered()` removes the game's notifications from the tray, say when the friends screen
  opens.

## The same news never shows twice

A request sent, cancelled and sent again is one notification. The server waits 30 seconds before a friend request
or invite reward goes out (a cancel in that time sends nothing), sends at most one per sender a day, and each push
carries a key that makes the phone replace an earlier notification about the same thing instead of stacking it.

## Language

Each push is written in the player's game language. `Push.SetLanguage("fa")` sets it; without it Soil uses the
`language` player property your game already sends, then the game's default language. Texts for Persian and
English are edited on the dashboard (**Push → Messages**).

## When push cannot work

Devices without Google Play services, and devices Google refuses a token to, never get a token: they simply get
no pushes, and nothing in the game waits on it. Staff can also switch push off for every game at once, and Soil
stops on its own while Google cannot be reached. Registering keeps working in every case; the game never needs to
know.
