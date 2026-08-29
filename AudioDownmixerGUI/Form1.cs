using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AudioDownmixerGUI
{
    public partial class Form1 : Form
    {
        // --- NAudio Components ---
        private WasapiLoopbackCapture capture;
        private WasapiOut playback;
        private BufferedWaveProvider outputBuffer;
        private DolbyProLogicDownmixer downmixer;

        // --- Threading Components ---
        private CancellationTokenSource cts;
        private Task vuMeterTask;

        // --- UI Components ---
        private Label lblCaptureDevice;
        private ComboBox cbCaptureDevice;
        private Label lblPlaybackDevice;
        private ComboBox cbPlaybackDevice;

        private Button btnStartStop;

        // Latency Monitoring and Adjustment UI
        private Label lblCurrentLatency;
        private Label lblBufferDuration;
        private ComboBox cbBufferDuration;

        // New: Output Audio Format UI
        private Label lblSampleRate;
        private ComboBox cbSampleRate;
        private Label lblBitDepth;
        private ComboBox cbBitDepth;

        private Label lblVolume;
        private TrackBar tbVolume;

        // Individual Channel Volume Controls (Labels and TrackBars)
        private Label lblFrontLeft;
        private TrackBar tbFrontLeft;
        private Label lblFrontRight;
        private TrackBar tbFrontRight;
        private Label lblCenter;
        private TrackBar tbCenter;
        private Label lblLFE;
        private TrackBar tbLFE;
        private Label lblSurroundLeft;
        private TrackBar tbSurroundLeft;
        private Label lblSurroundRight;
        private TrackBar tbSurroundRight;

        private Label lblVUFrontLeft;
        private ProgressBar pbVUFrontLeft;
        private Label lblVUFrontRight;
        private ProgressBar pbVUFrontRight;
        private Label lblVUCenter;
        private ProgressBar pbVUCenter;
        private Label lblVULFE;
        private ProgressBar pbVULFE;
        private Label lblVUSurroundLeft;
        private ProgressBar pbVUSurroundLeft;
        private Label lblVUSurroundRight;
        private ProgressBar pbVUSurroundRight;

        private volatile float peakFL = 0f;
        private volatile float peakFR = 0f;
        private volatile float peakC = 0f;
        private volatile float peakLFE = 0f;
        private volatile float peakSL = 0f;
        private volatile float peakSR = 0f;

        public Form1()
        {
            InitializeComponent();
            downmixer = new DolbyProLogicDownmixer(); // Instantiate here
            InitializeUIComponents(); // Initialize UI after downmixer is ready
        }

        private void InitializeUIComponents()
        {
            // --- Form Styling (Default Windows Forms style) ---
            this.Text = "Dolby Pro Logic II Downmixer";
            this.Size = new System.Drawing.Size(400, 950); // Increased height to accommodate new controls
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = false;

            int currentY = 20; // Starting Y position for controls
            int padding = 15; // Padding for elements

            // --- Device Selection Group ---
            GroupBox deviceGroupBox = new GroupBox
            {
                Text = "Audio Devices",
                Location = new Point(padding, currentY),
                Size = new Size(this.ClientSize.Width - (padding * 2), 110),
            };
            this.Controls.Add(deviceGroupBox);

            lblCaptureDevice = new Label { Text = "Capture Device:", Location = new Point(10, 25), AutoSize = true };
            deviceGroupBox.Controls.Add(lblCaptureDevice);
            cbCaptureDevice = new ComboBox { Location = new Point(120, 22), Size = new Size(deviceGroupBox.Width - 130, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            deviceGroupBox.Controls.Add(cbCaptureDevice);

            lblPlaybackDevice = new Label { Text = "Playback Device:", Location = new Point(10, 65), AutoSize = true };
            deviceGroupBox.Controls.Add(lblPlaybackDevice);
            cbPlaybackDevice = new ComboBox { Location = new Point(120, 62), Size = new Size(deviceGroupBox.Width - 130, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            deviceGroupBox.Controls.Add(cbPlaybackDevice);

            currentY += deviceGroupBox.Height + padding;

            // --- Start/Stop Button ---
            btnStartStop = new Button
            {
                Name = "btnStartStop",
                Text = "Start Downmix",
                Location = new Point((this.ClientSize.Width - 160) / 2, currentY),
                Size = new Size(160, 38),
            };
            btnStartStop.Click += btnStartStop_Click;
            this.Controls.Add(btnStartStop);

            currentY += btnStartStop.Height + padding + 5;

            // --- Latency and Buffer Settings Group ---
            GroupBox latencyGroupBox = new GroupBox
            {
                Text = "Latency Settings",
                Location = new Point(padding, currentY),
                Size = new Size(this.ClientSize.Width - (padding * 2), 90),
            };
            this.Controls.Add(latencyGroupBox);

            lblCurrentLatency = new Label { Text = "Current Latency: N/A", Location = new Point(10, 25), AutoSize = true };
            latencyGroupBox.Controls.Add(lblCurrentLatency);

            lblBufferDuration = new Label { Text = "Buffer Duration:", Location = new Point(10, 55), AutoSize = true };
            latencyGroupBox.Controls.Add(lblBufferDuration);
            cbBufferDuration = new ComboBox { Location = new Point(120, 52), Size = new Size(latencyGroupBox.Width - 130, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            cbBufferDuration.Items.AddRange(new string[] { "50 ms", "100 ms", "200 ms", "300 ms", "500 ms" });
            cbBufferDuration.SelectedIndex = 2; // Default to 200 ms
            latencyGroupBox.Controls.Add(cbBufferDuration);

            currentY += latencyGroupBox.Height + padding;

            // --- Output Audio Format Group --- NEW
            GroupBox outputFormatGroupBox = new GroupBox
            {
                Text = "Output Audio Format",
                Location = new Point(padding, currentY),
                Size = new Size(this.ClientSize.Width - (padding * 2), 90),
            };
            this.Controls.Add(outputFormatGroupBox);

            lblSampleRate = new Label { Text = "Sample Rate:", Location = new Point(10, 25), AutoSize = true };
            outputFormatGroupBox.Controls.Add(lblSampleRate);
            cbSampleRate = new ComboBox { Location = new Point(120, 22), Size = new Size(outputFormatGroupBox.Width - 130, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            cbSampleRate.Items.AddRange(new string[] { "44100 Hz", "48000 Hz", "96000 Hz" });
            cbSampleRate.SelectedIndex = 1; // Default to 48000 Hz
            outputFormatGroupBox.Controls.Add(cbSampleRate);

            lblBitDepth = new Label { Text = "Bit Depth:", Location = new Point(10, 55), AutoSize = true };
            outputFormatGroupBox.Controls.Add(lblBitDepth);
            cbBitDepth = new ComboBox { Location = new Point(120, 52), Size = new Size(outputFormatGroupBox.Width - 130, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            cbBitDepth.Items.AddRange(new string[] { "16-bit PCM", "24-bit PCM", "32-bit Float" });
            cbBitDepth.SelectedIndex = 2; // Default to 32-bit Float
            outputFormatGroupBox.Controls.Add(cbBitDepth);

            currentY += outputFormatGroupBox.Height + padding;

            // --- Volume Controls Group (Master and Individual Channels) ---
            GroupBox volumeGroupBox = new GroupBox
            {
                Text = "Channel Volumes",
                Location = new Point(padding, currentY),
                Size = new Size(this.ClientSize.Width - (padding * 2), 285),
            };
            this.Controls.Add(volumeGroupBox);

            int volumeSliderX = 120; // X position for trackbars
            int labelX = 10;        // X position for labels
            int sliderHeight = 35;  // Height of trackbars

            // Master Volume
            lblVolume = new Label { Text = "Master Volume:", Location = new Point(labelX, 25), AutoSize = true };
            volumeGroupBox.Controls.Add(lblVolume);
            tbVolume = new TrackBar { Location = new Point(volumeSliderX, 20), Size = new Size(volumeGroupBox.Width - volumeSliderX - 10, sliderHeight), Minimum = 0, Maximum = 100, Value = 50, TickFrequency = 10 };
            tbVolume.Scroll += tbVolume_Scroll;
            volumeGroupBox.Controls.Add(tbVolume);

            // Individual Channel Volumes
            lblFrontLeft = new Label { Text = "Front Left:", Location = new Point(labelX, 60), AutoSize = true };
            volumeGroupBox.Controls.Add(lblFrontLeft);
            tbFrontLeft = new TrackBar { Location = new Point(volumeSliderX, 55), Size = new Size(volumeGroupBox.Width - volumeSliderX - 10, sliderHeight), Minimum = 0, Maximum = 200, Value = 100, TickFrequency = 10 };
            tbFrontLeft.Scroll += (sender, e) => { downmixer.FrontLeftVolume = tbFrontLeft.Value / 100.0f; };
            volumeGroupBox.Controls.Add(tbFrontLeft);

            lblFrontRight = new Label { Text = "Front Right:", Location = new Point(labelX, 95), AutoSize = true };
            volumeGroupBox.Controls.Add(lblFrontRight);
            tbFrontRight = new TrackBar { Location = new Point(volumeSliderX, 90), Size = new System.Drawing.Size(volumeGroupBox.Width - volumeSliderX - 10, sliderHeight), Minimum = 0, Maximum = 200, Value = 100, TickFrequency = 10 };
            tbFrontRight.Scroll += (sender, e) => { downmixer.FrontRightVolume = tbFrontRight.Value / 100.0f; };
            volumeGroupBox.Controls.Add(tbFrontRight);

            lblCenter = new Label { Text = "Center:", Location = new Point(labelX, 130), AutoSize = true };
            volumeGroupBox.Controls.Add(lblCenter);
            tbCenter = new TrackBar { Location = new Point(volumeSliderX, 125), Size = new System.Drawing.Size(volumeGroupBox.Width - volumeSliderX - 10, sliderHeight), Minimum = 0, Maximum = 200, Value = 100, TickFrequency = 10 };
            tbCenter.Scroll += (sender, e) => { downmixer.CenterVolume = tbCenter.Value / 100.0f; };
            volumeGroupBox.Controls.Add(tbCenter);

            lblLFE = new Label { Text = "LFE:", Location = new Point(labelX, 165), AutoSize = true };
            volumeGroupBox.Controls.Add(lblLFE);
            tbLFE = new TrackBar { Location = new Point(volumeSliderX, 160), Size = new System.Drawing.Size(volumeGroupBox.Width - volumeSliderX - 10, sliderHeight), Minimum = 0, Maximum = 200, Value = 100, TickFrequency = 10 };
            tbLFE.Scroll += (sender, e) => { downmixer.LFEVolume = tbLFE.Value / 100.0f; };
            volumeGroupBox.Controls.Add(tbLFE);

            lblSurroundLeft = new Label { Text = "Surround Left:", Location = new Point(labelX, 200), AutoSize = true };
            volumeGroupBox.Controls.Add(lblSurroundLeft);
            tbSurroundLeft = new TrackBar { Location = new Point(volumeSliderX, 195), Size = new System.Drawing.Size(volumeGroupBox.Width - volumeSliderX - 10, sliderHeight), Minimum = 0, Maximum = 200, Value = 100, TickFrequency = 10 };
            tbSurroundLeft.Scroll += (sender, e) => { downmixer.SurroundLeftVolume = tbSurroundLeft.Value / 100.0f; };
            volumeGroupBox.Controls.Add(tbSurroundLeft);

            lblSurroundRight = new Label { Text = "Surround Right:", Location = new Point(labelX, 235), AutoSize = true };
            volumeGroupBox.Controls.Add(lblSurroundRight);
            tbSurroundRight = new TrackBar { Location = new Point(volumeSliderX, 230), Size = new System.Drawing.Size(volumeGroupBox.Width - volumeSliderX - 10, sliderHeight), Minimum = 0, Maximum = 200, Value = 100, TickFrequency = 10 };
            tbSurroundRight.Scroll += (sender, e) => { downmixer.SurroundRightVolume = tbSurroundRight.Value / 100.0f; };
            volumeGroupBox.Controls.Add(tbSurroundRight);

            currentY += volumeGroupBox.Height + padding;

            // --- VU Meters Group ---
            GroupBox vuMeterGroupBox = new GroupBox
            {
                Text = "Input VU Meters",
                Location = new Point(padding, currentY),
                Size = new Size(this.ClientSize.Width - (padding * 2), 170),
            };
            this.Controls.Add(vuMeterGroupBox);

            int meterY = 25;
            int meterLabelX = 10;
            int progressBarX = 50;
            int progressBarWidth = vuMeterGroupBox.Width - progressBarX - 10;
            int progressBarHeight = 12;

            lblVUFrontLeft = new Label { Text = "FL:", Location = new Point(meterLabelX, meterY), AutoSize = true };
            vuMeterGroupBox.Controls.Add(lblVUFrontLeft);
            pbVUFrontLeft = new ProgressBar { Location = new Point(progressBarX, meterY), Size = new Size(progressBarWidth, progressBarHeight), Minimum = 0, Maximum = 100 };
            vuMeterGroupBox.Controls.Add(pbVUFrontLeft);

            meterY += 25;
            lblVUFrontRight = new Label { Text = "FR:", Location = new Point(meterLabelX, meterY), AutoSize = true };
            vuMeterGroupBox.Controls.Add(lblVUFrontRight);
            pbVUFrontRight = new ProgressBar { Location = new Point(progressBarX, meterY), Size = new Size(progressBarWidth, progressBarHeight), Minimum = 0, Maximum = 100 };
            vuMeterGroupBox.Controls.Add(pbVUFrontRight);

            meterY += 25;
            lblVUCenter = new Label { Text = "C:", Location = new Point(meterLabelX, meterY), AutoSize = true };
            vuMeterGroupBox.Controls.Add(lblVUCenter);
            pbVUCenter = new ProgressBar { Location = new Point(progressBarX, meterY), Size = new Size(progressBarWidth, progressBarHeight), Minimum = 0, Maximum = 100 };
            vuMeterGroupBox.Controls.Add(pbVUCenter);

            meterY += 25;
            lblVULFE = new Label { Text = "LFE:", Location = new Point(meterLabelX, meterY), AutoSize = true };
            vuMeterGroupBox.Controls.Add(lblVULFE);
            pbVULFE = new ProgressBar { Location = new Point(progressBarX, meterY), Size = new Size(progressBarWidth, progressBarHeight), Minimum = 0, Maximum = 100 };
            vuMeterGroupBox.Controls.Add(pbVULFE);

            meterY += 25;
            lblVUSurroundLeft = new Label { Text = "SL:", Location = new Point(meterLabelX, meterY), AutoSize = true };
            vuMeterGroupBox.Controls.Add(lblVUSurroundLeft);
            pbVUSurroundLeft = new ProgressBar { Location = new Point(progressBarX, meterY), Size = new Size(progressBarWidth, progressBarHeight), Minimum = 0, Maximum = 100 };
            vuMeterGroupBox.Controls.Add(pbVUSurroundLeft);

            meterY += 25;
            lblVUSurroundRight = new Label { Text = "SR:", Location = new Point(meterLabelX, meterY), AutoSize = true };
            vuMeterGroupBox.Controls.Add(lblVUSurroundRight);
            pbVUSurroundRight = new ProgressBar { Location = new Point(progressBarX, meterY), Size = new Size(progressBarWidth, progressBarHeight), Minimum = 0, Maximum = 100 };
            vuMeterGroupBox.Controls.Add(pbVUSurroundRight);

            this.Load += Form1_Load;
        }

        private void tbVolume_Scroll(object sender, EventArgs e)
        {
            downmixer.MasterVolume = tbVolume.Value / 100.0f;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

            if (!devices.Any())
            {
                MessageBox.Show("No active audio playback devices found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            cbCaptureDevice.DisplayMember = "FriendlyName";
            cbCaptureDevice.ValueMember = "ID";
            cbCaptureDevice.DataSource = new List<MMDevice>(devices);

            cbPlaybackDevice.DisplayMember = "FriendlyName";
            cbPlaybackDevice.ValueMember = "ID";
            cbPlaybackDevice.DataSource = new List<MMDevice>(devices);

            if (cbCaptureDevice.Items.Count > 0)
                cbCaptureDevice.SelectedIndex = 0;
            if (cbPlaybackDevice.Items.Count > 0)
                cbPlaybackDevice.SelectedIndex = 0;

            // Initialize downmixer volumes based on trackbar default values
            downmixer.MasterVolume = tbVolume.Value / 100.0f;
            downmixer.FrontLeftVolume = tbFrontLeft.Value / 100.0f;
            downmixer.FrontRightVolume = tbFrontRight.Value / 100.0f;
            downmixer.CenterVolume = tbCenter.Value / 100.0f;
            downmixer.LFEVolume = tbLFE.Value / 100.0f;
            downmixer.SurroundLeftVolume = tbSurroundLeft.Value / 100.0f;
            downmixer.SurroundRightVolume = tbSurroundRight.Value / 100.0f;

            SetVolumeControlsEnabled(false);
            // Disable output format selection until downmix starts (as it requires re-initialization)
            cbSampleRate.Enabled = false;
            cbBitDepth.Enabled = false;
        }

        private async void btnStartStop_Click(object sender, EventArgs e)
        {
            if (capture == null || capture.CaptureState != NAudio.CoreAudioApi.CaptureState.Capturing)
            {
                try
                {
                    MMDevice captureSelectedDevice = cbCaptureDevice.SelectedItem as MMDevice;
                    MMDevice playbackSelectedDevice = cbPlaybackDevice.SelectedItem as MMDevice;

                    if (captureSelectedDevice == null || playbackSelectedDevice == null)
                    {
                        MessageBox.Show("Please select both capture and playback devices.", "Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Get selected buffer duration from ComboBox
                    int selectedBufferMs = int.Parse(cbBufferDuration.SelectedItem.ToString().Replace(" ms", ""));
                    TimeSpan bufferDuration = TimeSpan.FromMilliseconds(selectedBufferMs);

                    // Get selected output format parameters
                    int selectedSampleRate = int.Parse(cbSampleRate.SelectedItem.ToString().Replace(" Hz", ""));
                    string bitDepthText = cbBitDepth.SelectedItem.ToString();
                    int selectedBitsPerSample = 0;
                    bool selectedIsFloat = false;

                    if (bitDepthText.Contains("16-bit PCM"))
                    {
                        selectedBitsPerSample = 16;
                        selectedIsFloat = false;
                    }
                    else if (bitDepthText.Contains("24-bit PCM"))
                    {
                        selectedBitsPerSample = 24;
                        selectedIsFloat = false;
                    }
                    else if (bitDepthText.Contains("32-bit Float"))
                    {
                        selectedBitsPerSample = 32;
                        selectedIsFloat = true;
                    }

                    // Configure downmixer with selected output format
                    downmixer.ConfigureOutputFormat(selectedSampleRate, selectedBitsPerSample, selectedIsFloat);

                    capture = new WasapiLoopbackCapture(captureSelectedDevice);
                    playback = new WasapiOut(playbackSelectedDevice, AudioClientShareMode.Shared, false, (int)bufferDuration.TotalMilliseconds);

                    // Use the downmixer's current OutputFormat
                    outputBuffer = new BufferedWaveProvider(downmixer.OutputFormat)
                    {
                        BufferDuration = bufferDuration,
                        DiscardOnBufferOverflow = true
                    };

                    playback.Init(outputBuffer);
                    capture.DataAvailable += Capture_DataAvailable;

                    cts = new CancellationTokenSource();
                    vuMeterTask = Task.Run(async () =>
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            if (this.IsHandleCreated && !this.IsDisposed)
                            {
                                this.Invoke(new Action(() => {
                                    if (pbVUFrontLeft != null && !pbVUFrontLeft.IsDisposed)
                                        pbVUFrontLeft.Value = Math.Min(100, (int)(peakFL * 100));
                                    if (pbVUFrontRight != null && !pbVUFrontRight.IsDisposed)
                                        pbVUFrontRight.Value = Math.Min(100, (int)(peakFR * 100));
                                    if (pbVUCenter != null && !pbVUCenter.IsDisposed)
                                        pbVUCenter.Value = Math.Min(100, (int)(peakC * 100));
                                    if (pbVULFE != null && !pbVULFE.IsDisposed)
                                        pbVULFE.Value = Math.Min(100, (int)(peakLFE * 100));
                                    if (pbVUSurroundLeft != null && !pbVUSurroundLeft.IsDisposed)
                                        pbVUSurroundLeft.Value = Math.Min(100, (int)(peakSL * 100));
                                    if (pbVUSurroundRight != null && !pbVUSurroundRight.IsDisposed)
                                        pbVUSurroundRight.Value = Math.Min(100, (int)(peakSR * 100));

                                    if (lblCurrentLatency != null && !lblCurrentLatency.IsDisposed && outputBuffer != null && outputBuffer.WaveFormat != null)
                                    {
                                        double bufferedMs = (double)outputBuffer.BufferedBytes / outputBuffer.WaveFormat.AverageBytesPerSecond * 1000.0;
                                        lblCurrentLatency.Text = $"Current Latency: {bufferedMs:F0} ms";
                                    }
                                }));
                            }
                            await Task.Delay(50, cts.Token);
                        }
                    }, cts.Token);

                    playback.Play();
                    capture.StartRecording();

                    await Task.Yield(); // Resolves CS1998 warning

                    btnStartStop.Text = "Stop Downmix";
                    cbCaptureDevice.Enabled = false;
                    cbPlaybackDevice.Enabled = false;
                    cbBufferDuration.Enabled = false; // Still disable buffer duration selection while running (requires re-init)
                    cbSampleRate.Enabled = false; // Disable output format selection while running
                    cbBitDepth.Enabled = false;
                    SetVolumeControlsEnabled(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error starting downmix: {ex.Message}\n\nPlease ensure your 5.1 device is active and not exclusively used by another application.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    CleanupAudioComponents();
                }
            }
            else
            {
                StopDownmix();
            }
        }

        private void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.Buffer == null || capture.WaveFormat.Channels < 6 || capture.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                return;
            }

            int frames = e.BytesRecorded / (sizeof(float) * capture.WaveFormat.Channels);
            if (frames == 0) return;

            float[] inSamples = new float[frames * capture.WaveFormat.Channels];
            Buffer.BlockCopy(e.Buffer, 0, inSamples, 0, e.BytesRecorded);

            float currentPeakFL = 0f;
            float currentPeakFR = 0f;
            float currentPeakC = 0f;
            float currentPeakLFE = 0f;
            float currentPeakSL = 0f;
            float currentPeakSR = 0f;

            for (int n = 0; n < frames; n++)
            {
                int inputIndex = n * capture.WaveFormat.Channels;
                currentPeakFL = Math.Max(currentPeakFL, Math.Abs(inSamples[inputIndex + 0]));
                currentPeakFR = Math.Max(currentPeakFR, Math.Abs(inSamples[inputIndex + 1]));
                currentPeakC = Math.Max(currentPeakC, Math.Abs(inSamples[inputIndex + 2]));
                currentPeakLFE = Math.Max(currentPeakLFE, Math.Abs(inSamples[inputIndex + 3]));
                currentPeakSL = Math.Max(currentPeakSL, Math.Abs(inSamples[inputIndex + 4]));
                currentPeakSR = Math.Max(currentPeakSR, Math.Abs(inSamples[inputIndex + 5]));
            }

            peakFL = Math.Max(peakFL * 0.95f, currentPeakFL);
            peakFR = Math.Max(peakFR * 0.95f, currentPeakFR);
            peakC = Math.Max(peakC * 0.95f, currentPeakC);
            peakLFE = Math.Max(peakLFE * 0.95f, currentPeakLFE);
            peakSL = Math.Max(peakSL * 0.95f, currentPeakSL);
            peakSR = Math.Max(peakSR * 0.95f, currentPeakSR);

            byte[] outBytes = downmixer.ProcessAudio(inSamples, e.BytesRecorded, capture.WaveFormat);
            outputBuffer.AddSamples(outBytes, 0, outBytes.Length);
        }

        private void StopDownmix()
        {
            CleanupAudioComponents();
            btnStartStop.Text = "Start Downmix";
            cbCaptureDevice.Enabled = true;
            cbPlaybackDevice.Enabled = true;
            cbBufferDuration.Enabled = true;
            cbSampleRate.Enabled = true; // Re-enable output format selection when stopped
            cbBitDepth.Enabled = true;
            lblCurrentLatency.Text = "Current Latency: N/A";
            SetVolumeControlsEnabled(false);

            pbVUFrontLeft.Value = 0;
            pbVUFrontRight.Value = 0;
            pbVUCenter.Value = 0;
            pbVULFE.Value = 0;
            pbVUSurroundLeft.Value = 0;
            pbVUSurroundRight.Value = 0;
        }

        private void SetVolumeControlsEnabled(bool enabled)
        {
            // Enable/disable all individual volume trackbars and the master volume
            tbVolume.Enabled = enabled;
            tbFrontLeft.Enabled = enabled;
            tbFrontRight.Enabled = enabled;
            tbCenter.Enabled = enabled;
            tbLFE.Enabled = enabled;
            tbSurroundLeft.Enabled = enabled;
            tbSurroundRight.Enabled = enabled;
        }

        private void CleanupAudioComponents()
        {
            if (cts != null)
            {
                cts.Cancel();
                try { vuMeterTask?.Wait(100); }
                catch (OperationCanceledException) { /* Expected exception */ }
                catch (AggregateException ae) { ae.Handle(ex => ex is OperationCanceledException); }
                cts.Dispose();
                cts = null;
            }

            if (capture != null)
            {
                capture.StopRecording();
                capture.DataAvailable -= Capture_DataAvailable;
                capture.Dispose();
                capture = null;
            }

            if (playback != null)
            {
                playback.Stop();
                playback.Dispose();
                playback = null;
            }

            if (outputBuffer != null)
            {
                outputBuffer = null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopDownmix();
            base.OnFormClosing(e);
        }
    }
}