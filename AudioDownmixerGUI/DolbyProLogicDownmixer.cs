using NAudio.Wave;
using System;
using System.Runtime.InteropServices;

public class DolbyProLogicDownmixer : IAudioDownmixer
{
    private WaveFormat _outputFormat;

    private float _masterVolume = 0.5f;
    private float _frontLeftVolume = 1.0f;
    private float _frontRightVolume = 1.0f;
    private float _centerVolume = 1.0f;
    private float _lfeVolume = 1.0f;
    private float _surroundLeftVolume = 1.0f;
    private float _surroundRightVolume = 1.0f;

    // Internal state for selected output format
    private int _currentSampleRate;
    private int _currentBitsPerSample;
    private bool _currentIsFloat;

    public DolbyProLogicDownmixer()
    {
        // Default output format
        _currentSampleRate = 48000;
        _currentBitsPerSample = 32;
        _currentIsFloat = true;
        UpdateOutputFormat();
    }

    // Public properties for each channel's volume and master volume
    public float MasterVolume { get => _masterVolume; set => _masterVolume = value; }
    public float FrontLeftVolume { get => _frontLeftVolume; set => _frontLeftVolume = value; }
    public float FrontRightVolume { get => _frontRightVolume; set => _frontRightVolume = value; }
    public float CenterVolume { get => _centerVolume; set => _centerVolume = value; }
    public float LFEVolume { get => _lfeVolume; set => _lfeVolume = value; }
    public float SurroundLeftVolume { get => _surroundLeftVolume; set => _surroundLeftVolume = value; }
    public float SurroundRightVolume { get => _surroundRightVolume; set => _surroundRightVolume = value; }

    public WaveFormat OutputFormat => _outputFormat;

    // New: Method to set the output format from UI selection
    public void ConfigureOutputFormat(int sampleRate, int bitsPerSample, bool isFloat)
    {
        _currentSampleRate = sampleRate;
        _currentBitsPerSample = bitsPerSample;
        _currentIsFloat = isFloat;
        UpdateOutputFormat();
    }

    // Helper to update the internal _outputFormat based on current settings
    private void UpdateOutputFormat()
    {
        if (_currentIsFloat)
        {
            _outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(_currentSampleRate, 2); // Always 2 channels (stereo)
        }
        else
        {
            _outputFormat = new WaveFormat(_currentSampleRate, _currentBitsPerSample, 2); // Always 2 channels (stereo)
        }
    }

    public byte[] ProcessAudio(float[] inputSamples, int bytesRecorded, WaveFormat inputFormat)
    {
        if (inputFormat.Channels < 6 || inputFormat.Encoding != WaveFormatEncoding.IeeeFloat)
        {
            return new byte[0];
        }

        int frames = inputSamples.Length / inputFormat.Channels;
        if (frames == 0)
        {
            return new byte[0];
        }

        // Always process in float for internal precision
        float[] stereoFloatSamples = new float[frames * 2]; // 2 output channels (stereo)

        for (int n = 0; n < frames; n++)
        {
            int inputIndex = n * inputFormat.Channels;

            float FL = inputSamples[inputIndex + 0] * _frontLeftVolume;
            float FR = inputSamples[inputIndex + 1] * _frontRightVolume;
            float C = inputSamples[inputIndex + 2] * _centerVolume;
            float LFE = inputSamples[inputIndex + 3] * _lfeVolume;
            float SL = inputSamples[inputIndex + 4] * _surroundLeftVolume;
            float SR = inputSamples[inputIndex + 5] * _surroundRightVolume;

            float Lt = FL + (0.7071f * C) - (0.866f * SL) - (0.5f * SR);
            float Rt = FR + (0.7071f * C) + (0.5f * SL) + (0.866f * SR);

            Lt += LFE * 0.5f;
            Rt += LFE * 0.5f;

            float finalLeft = Math.Max(-1.0f, Math.Min(1.0f, Lt * this.MasterVolume));
            float finalRight = Math.Max(-1.0f, Math.Min(1.0f, Rt * this.MasterVolume));

            stereoFloatSamples[n * 2 + 0] = finalLeft;
            stereoFloatSamples[n * 2 + 1] = finalRight;
        }

        // Convert the final float samples to the desired output format (PCM or Float)
        byte[] outBytes;
        if (_currentIsFloat)
        {
            outBytes = new byte[stereoFloatSamples.Length * sizeof(float)];
            Buffer.BlockCopy(stereoFloatSamples, 0, outBytes, 0, outBytes.Length);
        }
        else // Convert to PCM (16-bit or 24-bit)
        {
            int bytesPerSample = _currentBitsPerSample / 8;
            outBytes = new byte[stereoFloatSamples.Length * bytesPerSample];

            for (int i = 0; i < stereoFloatSamples.Length; i++)
            {
                float sample = stereoFloatSamples[i];
                if (_currentBitsPerSample == 16)
                {
                    short shortSample = (short)(sample * 32767f);
                    byte[] shortBytes = BitConverter.GetBytes(shortSample);
                    Buffer.BlockCopy(shortBytes, 0, outBytes, i * bytesPerSample, bytesPerSample);
                }
                else if (_currentBitsPerSample == 24)
                {
                    // Convert float to 24-bit PCM (bytes)
                    // Scale float sample to 24-bit range (-8388608 to 8388607)
                    int intSample = (int)(sample * 8388607f);
                    outBytes[i * bytesPerSample] = (byte)(intSample & 0xFF); // LSB
                    outBytes[i * bytesPerSample + 1] = (byte)((intSample >> 8) & 0xFF);
                    outBytes[i * bytesPerSample + 2] = (byte)((intSample >> 16) & 0xFF); // MSB
                }
                // Add other bit depths if needed
            }
        }

        return outBytes;
    }
}