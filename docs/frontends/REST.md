# Thermite REST frontend #

A simple REST API for interacting with Thermite.

## Configuration ##

TODO.

## Metrics ##

TODO.

## Ratelimiting ##

Where we're going, we don't *need* ratelimits.

## Error handling ##

Did you expect this to be enterprise quality?

## Endpoints ##

### GET `/players/:guildId` ###

Returns information about the player for the given guild id. If no player has
been connected, a 404 is returned. The JSON returned is of the form:

```json
{
    // contains information about the current track being played back
    "current_track": {
        // name of the track, as reported by location
        "name": "track name",
        // the URL which may be fed back to a user to locate the track
        // (e.g. a /watch?v=dQw4w9WgXcQ URL for YouTube)
        "location": "friendly location"
    },

    // contains information about the playback of current_track
    "playback": {
        // whether the playback is paused or not
        // currently always false (not implemented)
        "paused": false,

        // position, in seconds, from the beginning of the track
        // currently always 0 (not implemented)
        "position": 0,

        // length, in seconds, of the track
        // currently always 0 (not implemented)
        "length": 0
    },

    // contains the current player queue
    // currently always an empty array (not implemented)
    "queue": [
        {
            "name": "track name",
            "location": "friendly location"
        }
    ]
}
```

### PUT `/players/:guildId` ###

Updates the voice state for the given guild id, and returns a 204 on success.
Takes a JSON object containing the following data:

```json
{
    "guild_id": "guild id",
    "session_id": "voice gateway session id",
    "endpoint": "voice gateway endpoint",
    "token": "voice gateway token"
}
```

This information is as-received from Discord's
[Voice State Update] and
[Voice Server Update] events.

It is expected that in your bot, you update your voice state and then call this
endpoint when both events have been received. You should probably set
`self_deaf` to `true`, as Thermite does not support voice receive, but it is
inconsequential.

As an example, for Discord.Net, your code for informing Thermite about
connecting to voice should look something like:

```cs
SocketVoiceState lastState = null;

Task StateUpdated(SocketUser user, SocketVoiceState before,
    SocketVoiceState after)
{
    if (user.Id == _bot.CurrentUser.Id)
        lastState = after;

    return Task.CompletedTask;
}

async Task ServerUpdated(SocketVoiceServer server)
{
    if (lastState?.VoiceChannel != null)
    {
        // Create and send a HTTP request to the Thermite REST frontend.
        // Use HttpResponseMessage.EnsureSuccessStatusCode() to check for
        // errors.
        await UpdateThermiteVoiceState(lastState, server);
    }
}

async Task RunAsync()
{
    _bot.UserVoiceStateUpdate += StateUpdated;
    _bot.VoiceServerUpdate += ServerUpdated;

    await _bot.StartAsync();
    await Task.Delay(-1);
}
```

After this, moving the client is pretty easy. Just update your voice state to
point towards the correct channel. I have no idea if this supports the
disconnect case yet.

### DELETE `/players/:guildId` ###

Disconnects from the given guild. Returns a 204 on success.

NOTE: Doesn't actually work yet. Since, of course, it's not implemented yet.


### POST `/players/:guildId/queue` ###

Adds a track to the player for a given guild. Returns a 204 on success.

The request body should be the URI for the track you wish to enqueue. Yes, this
includes URNs, so if you wanted, you could use `urn:isbn:xxxx` for audio books.
Or `urn:tts:dectalk?=text=aeiou&name=Paul` if you really wanted to emulate Moon
Base Alpha.

[Voice State Update]: https://discordapp.com/developers/docs/topics/gateway#voice-state-update
[Voice Server Update]: https://discordapp.com/developers/docs/topics/gateway#voice-server-update
