using System;
using System.Collections.Generic;

using NAudio.Wave;

namespace ReadWave
{
    public class DirectAudioAFSKDemodulator
    {
        private ax25.AFSK1200Demodulator dem;
        private WaveInEvent recored;

        public static string[] WaveInDevices()
        {
            List<string> str = new List<string>();
            int waveInDevices = WaveIn.DeviceCount;
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                str.Add(String.Format("Device {0}: {1}", waveInDevice, deviceInfo.ProductName));
            };
            return str.ToArray();
        }

        public static string[] WaveOutDevices()
        {
            List<string> str = new List<string>();
            int waveOutDevices = WaveOut.DeviceCount;
            for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++)
            {
                WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(waveOutDevice);
                str.Add(String.Format("Device {0}: {1}", waveOutDevice, deviceInfo.ProductName));
            };
            return str.ToArray();
        }

        public DirectAudioAFSKDemodulator(int deviceNo, ax25.PacketHandler h)
        {
            dem = new ax25.AFSK1200Demodulator(44100, 1, 0, h);
            recored = new WaveInEvent();
            recored.DeviceNumber = deviceNo;
            recored.WaveFormat = new WaveFormat(44100, 16, 1);
            recored.DataAvailable += new EventHandler<WaveInEventArgs>(recored_DataAvailable);
        }

        public void Start()
        {
            recored.StartRecording();
        }

        public void Stop()
        {
            recored.StopRecording();
        }

        ~DirectAudioAFSKDemodulator()
        {
            Dispose();
        }

        public void Dispose()
        {
            Stop();
        }

        public int DecodeCount { get { return dem.DecodeCount; } }
        public sbyte[] LastPacket { get { return dem.LastPacket; } }

        private void recored_DataAvailable(object sender, WaveInEventArgs e)
        {
            int pos = 0;
            float[] samples = new float[e.BytesRecorded / 2];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)WaveStream.bytesToDouble(e.Buffer[pos++], e.Buffer[pos++]);
            dem.AddSamples(samples, samples.Length);
        }
    }
}
