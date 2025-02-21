namespace AudioCodec
{
    public class AudioFormat
    {
        public int samplesPerSec;             /** Sampling rate [Hz] */
        public int channels;                  /** Number of channels [#] */
        public int bitsPerSample;             /** Number of bits per sample assume float for 32 bit [bit]*/
        public AudioPayloadType payloadType;   /** Audio payload type */
        public int bitrate;                   /** Audio bitrate [bits/s] */
        public int pTime;
    }

    public enum AudioPayloadType
    {
        G711ALaw = 8,
        G711MuLaw = 0,
        G722 = 9,
        G729 = 18,
        L16 = 98,
        OPUS = 102,
        SILKNarrow = 103,
        SILKWide = 104,
        SILKSuperWide = 105,
        SATIN = 108,
        SATINFB = 109,
        MUCH = 110,
        SIREN = 111,
        G7221 = 112,
        MUCHv2 = 113,
        MSRTAudio16KHz = 114,
        MSRTAudio8KHz = 115,
        G722_2 = 117,
        AMR_WB = 121,
        /* This ends the codec payload types */
        CN = 13,       /* Comfort Noise format */
        RED = 97,      /* RTP redundancy */
        DTMF = 101,    /* DTMF */
        DTMF_WB = 106, /* DTMF Wide Band format*/
        CNWB = 118,    /* Comfort Noise Wide Band format*/
        CNSWB = 119,
        CNFB = 120,
        Max = 255,    /* 0xff */
        Invalid = 255 /* 0xff */
    }
}
