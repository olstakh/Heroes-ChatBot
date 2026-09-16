# Heroes Lobby Messenger

A Windows desktop prototype for configuring rotating messages for private
HoMM3 HD+ online-lobby conversations.

## Current capabilities

- Manage any number of recipient profiles through a WinForms UI.
- Configure an interval and ordered message list for each recipient.
- Save settings automatically under the current user's local application data.
- Detect a running Heroes III window.
- Send the next configured message to the currently open private lobby room.
- Arm one confirmed room for repeated sending and stop it at any time.

The initial version intentionally does not switch private rooms automatically.
The HD+ lobby is custom-drawn and does not expose usernames or controls through
Windows accessibility APIs. Sending is therefore gated by an explicit
confirmation that the intended private room is open.

Message entry follows the legacy lobby's actual input flow: activate the game,
click the private-room text field, type using the keyboard layout active in the
game, and click the send-arrow button. The mouse therefore moves briefly for
each message. After the send attempt, the app refocuses the text field and
issues Backspace events. This is harmless when the message was sent and clears
the text when the lobby leaves it behind for an offline recipient.

## Build

```powershell
dotnet build HeroesChatBot.slnx
```

Create a self-contained Windows build:

```powershell
dotnet publish src\HeroesChatBot\HeroesChatBot.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o artifacts\HeroesLobbyMessenger-win-x64
```

## Use

1. Start Heroes III and enter the HD+ online lobby.
2. Add a recipient and enter one message per line.
3. Open that recipient's private lobby room.
4. Confirm the room in the application.
5. Use **Send next message now** for a controlled test, or arm the selected
   recipient for scheduled messages.

Use conservative intervals and only message people who expect the messages.
