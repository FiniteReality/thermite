# Thermite container format #

Thermite uses a custom container format for transferring audio between
decoders, transcoders and ultimately the audio player. This is done to
encourage modularity and a separation of concerns, as well as to encourage code
re-use.

## Overview ##

Thermite's container format is a streaming container format, split into
multiple frames, each of which contain a single unit of data to be passed to a
transcoder or the audio player. This is done to make parsing and transmission
easier, requiring less logic. For example, for Opus, a frame would contain a
single Opus packet.

## Header ##

As Thermite's container format is designed to be a stream-based transfer format
for intra-process communication, there is little need for a header. If one is
absolutely necessary, it can be added in the data unit section. However, both
the producer and consumer must externally negotiate these attributes.

## Frames ##

A frame is the primitive unit of encapsulation in Thermite. It consists of a
little endian signed short, followed by a single data unit. The contents of the
data unit should be opaque to all observers, including the producer - only the
consumer needs to understand how to decode the contents.

To illustrate, a frame looks something like this:

     0                   1           
     0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 
    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    |          Data Length          |
    +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
    |           Data unit           |
    |              ...              |
    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

Reading such a frame can be done using the following pseudocode:

    const MinFrameSize = 2 // bytes
    void read_frame(stream_container stream)
    {
        // read at least MinFrameSize bytes
        frame = stream.read(MinFrameSize)

        // split the read frame to get the data and its length
        data_size = read_little_endian(frame[0..1])
        data = frame[2..frame_size]

        // handle the data unit in an appropriate way
        handle_frame(data)

        // advance the stream by the amount of data read
        stream.advance(MinFrameSize + data_size)
    }

For a more complete sample, [VoiceDataClient.cs][TryReadPacket] can be used as
a reference.

[TryReadPacket]: ../../src/Core/Discord/VoiceDataClient.cs#L139