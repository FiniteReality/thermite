# Contributing #

Thermite is still in intensive development, but contributions are welcome. If
you'd like to test Thermite in a music bot of your own, check the usage notes
at the end of this document.

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

The steps from here are still in development, but for testing, you can:

- Use the PipeWriter to write encoded Opus packets to transmit.
  - There is a particular format you should follow. This is because Opus
    packets are not delimited, so it is necessary to be able to delimit them.
  - Write a **little endian** signed short indicating the packet length.
  - Then, write the rest of the Opus packet. No parsing needs to be done, it is
    all done internally.