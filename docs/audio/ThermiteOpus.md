# Thermite-Compatible Opus #

Thermite-Compatible Opus is a custom container format, designed to be simple
and efficient. It's applicable to any non-delimited codecs, but in Thermite,
it is used specifically for Opus.

## Support ##

Thermite-Compatible Opus supports any Opus stream, as long as the sampling rate
does not vary. As per a traditional Opus application, the producer and consumer
should negotiate a shared sampling rate to allow proper encoding and decoding.
This sampling rate is not to be included in Thermite-Compatible Opus frames.

## Overview ##

A Thermite-Compatible Opus stream is delimited into multiple frames, each of
which contain a single Opus packet. This is to make parsing and transmission
extremely easy, requiring very little logic.

## Header ##

Thermite-Compatible Opus is designed to be a purely stream-based transfer
format for internal use, where a header is unnecessary and can be substituted
for other indicators, such as a MIME type string indicating the options
traditionally included in a file header. In addition, due to Opus packets
containing almost all of the necessary state in order for a decoder to
successfully decode packets, a traditional file header is not necessary.

## Frames ##

A frame is the primitive unit of encapsulation in a Thermite-Compatible Opus
stream. It consists of a little-endian signed short, followed by a single Opus
packet, as returned by [`opus_encode`][OpusEncode] or
[`opus_encode_float`][OpusEncodeFloat].

To illustrate, a frame looks something like this:

     0                   1           
     0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 
    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    |         Packet Length         |
    +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
    |          Opus Packet          |
    |              ...              |
    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

Reading such a frame can be done using the following pseudocode:

    const MinPacketSize = 2 // bytes
    void read_frame(stream_container stream)
    {
        // read at least MinPacketSize bytes
        data = stream.read(MinPacketSize)

        // split the read data to get the packet and its length
        packet_size = read_little_endian(data[0..1])
        packet = data[2..packet_size]

        // handle the Opus data in an appropriate way
        handle_opus_packet(packet)

        // advance the stream by the amount of data read
        stream.advance(MinPacketSize + packet_size)
    }

For a more complete sample, [VoiceDataClient.cs][TryReadPacket] can be used as
a reference.

[OpusEncode]: https://www.opus-codec.org/docs/html_api/group__opusencoder.html#ga88621a963b809ebfc27887f13518c966
[OpusEncodeFloat]: https://www.opus-codec.org/docs/html_api/group__opusencoder.html#gace941e4ef26ed844879fde342ffbe546
[TryReadPacket]: ../../src/Core/Discord/VoiceDataClient.cs#L139