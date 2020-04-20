# Contributing #

Thermite is still in intensive development, but contributions are welcome. If
you'd like to test Thermite in a music bot of your own, check the usage notes
at the end of this document.

You can find in-dev packages at
`https://www.myget.org/F/thermite/api/v3/index.json`.

## Dependencies ##

- .NET Core 3.0
- libopus
- libsodium

## Building ##

```
$ git clone https://github.com/FiniteReality/thermite.git
$ cd thermite
$ dotnet build
```

## Usage ##

- Create a PlayerManager, passing the user ID and socket pool size.
- Hook your library's Voice State Update/Voice Server Update events
  - Call UpdateVoiceState with the appropriate values when these occur.
    (ALL VALUES MUST BE PASSED!)
- Send a Voice State Update to Discord to connect to voice.
- Call TryGetPlayer or GetPlayer to get an IPlayer instance.
- Call EnqueueAsync to enqueue one or more songs.
