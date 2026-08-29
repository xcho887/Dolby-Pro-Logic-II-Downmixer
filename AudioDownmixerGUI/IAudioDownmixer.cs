using NAudio.Wave;

public interface IAudioDownmixer
{
    byte[] ProcessAudio(float[] inputSamples, int bytesRecorded, WaveFormat inputFormat);
    WaveFormat OutputFormat { get; }

    float MasterVolume { get; set; }
    float FrontLeftVolume { get; set; }
    float FrontRightVolume { get; set; }
    float CenterVolume { get; set; }
    float LFEVolume { get; set; }
    float SurroundLeftVolume { get; set; }
    float SurroundRightVolume { get; set; }

    // New: Method to configure the output format
    void ConfigureOutputFormat(int sampleRate, int bitsPerSample, bool isFloat);
}