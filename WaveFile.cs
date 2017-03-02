using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace ReadWave
{
    public class WaveStream
    {
        public static double bytesToDouble(byte firstByte, byte secondByte)
        {
            short s = (short)((secondByte << 8) | firstByte);
            return s / 32768.0;
        }

        public static byte[] DoubleToBytes(double d)
        {
            d = d * 32768.0;
            short s = (short)d;
            return new byte[] { (byte)(s), (byte)(s >> 8) };
        }

        public static byte[] StrToByteArray(String pStr)
        {
            System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
            return encoding.GetBytes(pStr);
        }

        // if mono left only
        public static void ReadWavFile(string filename, out int channels, out int sampleRate, out int bitDepth, out double[] left, out double[] right)
        {
            left = right = null;
            byte[] wav = File.ReadAllBytes(filename);

            Int16[] I2 = new Int16[1];
            Single[] F4 = new Single[1];
            Int32[] I4 = new Int32[1];

            System.Buffer.BlockCopy(wav, 22, I2, 0, 2);
            channels = I2[0];

            System.Buffer.BlockCopy(wav, 24, I4, 0, 4);
            sampleRate = I4[0];

            System.Buffer.BlockCopy(wav, 34, I2, 0, 2);
            bitDepth = I2[0];

            // Get past all the other sub chunks to get to the data subchunk:
            int pos = 12;   // First Subchunk ID from 12 to 16
            // Keep iterating until we find the `data` chunk (i.e. 64 61 74 61 ...... (i.e. 100 97 116 97 in decimal))
            while (!(wav[pos] == 100 && wav[pos + 1] == 97 && wav[pos + 2] == 116 && wav[pos + 3] == 97))
            {
                pos += 4;
                int chunkSize = wav[pos] + wav[pos + 1] * 256 + wav[pos + 2] * 65536 + wav[pos + 3] * 16777216;
                pos += 4 + chunkSize;
            };
            pos += 4; // `data`
            System.Buffer.BlockCopy(wav, pos, I4, 0, 4);
            int ttlbytes = I4[0];
            int bytesForSamp = bitDepth / 8;
            int samples = ttlbytes / bytesForSamp;
            int sampchl = channels == 2 ? samples / 2 : samples;
            pos += 4; // samples bytes length

            // Pos is now positioned to start of actual sound data.

            left = new double[sampchl];
            if (channels == 2) right = new double[sampchl];

            // Read samples
            for (int i = 0; i < sampchl; i++)
            {
                bool leftCH = true;
                double[] F8 = left;
                while (true)
                {
                    switch (bitDepth)
                    {
                        case 64:
                            System.Buffer.BlockCopy(wav, pos, F8, i, 8);
                            pos += 8;
                            break;
                        case 32:
                            System.Buffer.BlockCopy(wav, pos, F4, 0, 4);
                            pos += 4;
                            F8[i] = F4[0];
                            break;
                        case 16:
                            System.Buffer.BlockCopy(wav, pos, I2, 0, 2);
                            F8[i] = I2[0] / 32768.0;
                            pos += 2;
                            break;
                    };
                    if (leftCH && (channels == 2))
                    {
                        leftCH = false;
                        F8 = right;
                    }
                    else
                        break;
                };
            };
        }

        // if mono left only
        public static void ReadWavFile(string filename, out int channels, out int sampleRate, out int bitDepth, out float[] left, out float[] right)
        {
            left = right = null;
            byte[] wav = File.ReadAllBytes(filename);

            Int16[] I2 = new Int16[1];
            Single[] F4 = new Single[1];
            Int32[] I4 = new Int32[1];

            System.Buffer.BlockCopy(wav, 22, I2, 0, 2);
            channels = I2[0];

            System.Buffer.BlockCopy(wav, 24, I4, 0, 4);
            sampleRate = I4[0];

            System.Buffer.BlockCopy(wav, 34, I2, 0, 2);
            bitDepth = I2[0];

            // Get past all the other sub chunks to get to the data subchunk:
            int pos = 12;   // First Subchunk ID from 12 to 16
            // Keep iterating until we find the `data` chunk (i.e. 64 61 74 61 ...... (i.e. 100 97 116 97 in decimal))
            while (!(wav[pos] == 100 && wav[pos + 1] == 97 && wav[pos + 2] == 116 && wav[pos + 3] == 97))
            {
                pos += 4;
                int chunkSize = wav[pos] + wav[pos + 1] * 256 + wav[pos + 2] * 65536 + wav[pos + 3] * 16777216;
                pos += 4 + chunkSize;
            };
            pos += 4; // `data`
            System.Buffer.BlockCopy(wav, pos, I4, 0, 4);
            int ttlbytes = I4[0];
            int bytesForSamp = bitDepth / 8;
            int samples = ttlbytes / bytesForSamp;
            int sampchl = channels == 2 ? samples / 2 : samples;
            pos += 4; // samples bytes length

            // Pos is now positioned to start of actual sound data.

            left = new float[sampchl];
            if (channels == 2) right = new float[sampchl];

            // Read samples
            for (int i = 0; i < sampchl; i++)
            {
                bool leftCH = true;
                F4 = left;
                while (true)
                {
                    switch (bitDepth)
                    {
                        case 64:
                            double[] F8 = new double[1];
                            System.Buffer.BlockCopy(wav, pos, F8, 0, 8);
                            F4[i] = (float)F8[0];
                            pos += 8;
                            break;
                        case 32:
                            System.Buffer.BlockCopy(wav, pos, F4, i, 4);
                            pos += 4;
                            break;
                        case 16:
                            System.Buffer.BlockCopy(wav, pos, I2, 0, 2);
                            F4[i] = (float)(I2[0] / 32768.0);
                            pos += 2;
                            break;
                    };
                    if (leftCH && (channels == 2))
                    {
                        leftCH = false;
                        F4 = right;
                    }
                    else
                        break;
                };
            };
        }

        public static void WriteWavHeader(Stream stream, bool isFloatingPoint, ushort channelCount, ushort bitDepth, int sampleRate, int totalSampleCount)
        {
            stream.Position = 0;
            // RIFF header.
            // Chunk ID.
            stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);

            // Chunk size.
            stream.Write(BitConverter.GetBytes(((bitDepth / 8) * totalSampleCount) + 36), 0, 4);

            // Format.
            stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);



            // Sub-chunk 1.
            // Sub-chunk 1 ID.
            stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);

            // Sub-chunk 1 size.
            stream.Write(BitConverter.GetBytes(16), 0, 4);

            // Audio format (floating point (3) or PCM (1)). Any other format indicates compression.
            stream.Write(BitConverter.GetBytes((ushort)(isFloatingPoint ? 3 : 1)), 0, 2);

            // Channels.
            stream.Write(BitConverter.GetBytes(channelCount), 0, 2);

            // Sample rate.
            stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);

            // Bytes rate.
            stream.Write(BitConverter.GetBytes(sampleRate * channelCount * (bitDepth / 8)), 0, 4);

            // Block align.
            stream.Write(BitConverter.GetBytes((ushort)channelCount * (bitDepth / 8)), 0, 2);

            // Bits per sample.
            stream.Write(BitConverter.GetBytes(bitDepth), 0, 2);



            // Sub-chunk 2.
            // Sub-chunk 2 ID.
            stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4);

            // Sub-chunk 2 size.
            stream.Write(BitConverter.GetBytes((bitDepth / 8) * totalSampleCount), 0, 4);
        }

        public static void WriteWav16BitMono(Stream stream, int sampleRate, float[] samples)
        {
            WriteWavHeader(stream, false, 1, 16, sampleRate, samples.Length);
            List<byte> ba = new List<byte>();
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    byte[] fs = WaveStream.DoubleToBytes(samples[i]);
                    ba.AddRange(fs);
                };
            };
            stream.Write(ba.ToArray(), 0, ba.Count);
        }

        public static void SaveWav16BitMono(string filename, int sampleRate, float[] samples)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            WriteWav16BitMono(fs, sampleRate, samples);
            fs.Close();
        }

        public static void WriteWav16BitMono(Stream stream, int sampleRate, double[] samples)
        {
            WriteWavHeader(stream, false, 1, 16, sampleRate, samples.Length);
            List<byte> ba = new List<byte>();
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    byte[] fs = WaveStream.DoubleToBytes(samples[i]);
                    ba.AddRange(fs);
                };
            };
            stream.Write(ba.ToArray(), 0, ba.Count);
        }

        public static void SaveWav16BitMono(string filename, int sampleRate, double[] samples)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            WriteWav16BitMono(fs, sampleRate, samples);
            fs.Close();
        }

        public static void WriteWav16BitStereo(Stream stream, int sampleRate, float[] L, float[] R)
        {
            WriteWavHeader(stream, false, 2, 16, sampleRate, L.Length * 2);
            List<byte> ba = new List<byte>();
            {
                for (int i = 0; i < L.Length; i++)
                {
                    byte[] fs = WaveStream.DoubleToBytes(L[i]);
                    ba.AddRange(fs);
                    fs = WaveStream.DoubleToBytes(R[i]);
                    ba.AddRange(fs);
                };
            };
            stream.Write(ba.ToArray(), 0, ba.Count);
        }

        public static void SaveWav16BitStereo(string filename, int sampleRate, float[] L, float[] R)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            WriteWav16BitStereo(fs, sampleRate, L, R);
            fs.Close();
        }

        public static void WriteWav16BitStereo(Stream stream, int sampleRate, double[] L, double[] R)
        {
            WriteWavHeader(stream, false, 2, 16, sampleRate, L.Length * 2);
            List<byte> ba = new List<byte>();
            {
                for (int i = 0; i < L.Length; i++)
                {
                    byte[] fs = WaveStream.DoubleToBytes(L[i]);
                    ba.AddRange(fs);
                    fs = WaveStream.DoubleToBytes(R[i]);
                    ba.AddRange(fs);
                };
            };
            stream.Write(ba.ToArray(), 0, ba.Count);
        }

        public static void SaveWav16BitStereo(string filename, int sampleRate, double[] L, double[] R)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            WriteWav16BitStereo(fs, sampleRate, L, R);
            fs.Close();
        }

        public static void WriteWav16BitStereo(Stream stream, int sampleRate, float[] mono)
        {
            WriteWav16BitStereo(stream, sampleRate, mono, mono);
        }

        public static void SaveWav16BitStereo(string filename, int sampleRate, float[] mono)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            WriteWav16BitStereo(fs, sampleRate, mono);
            fs.Close();
        }

        public static void WriteWav16BitStereo(Stream stream, int sampleRate, double[] mono)
        {
            WriteWav16BitStereo(stream, sampleRate, mono, mono);
        }

        public static void SaveWav16BitStereo(string filename, int sampleRate, double[] mono)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            WriteWav16BitStereo(fs, sampleRate, mono);
            fs.Close();
        }

        public static void PlaySamples(int sampleRate, float[] mono, bool wait)
        {
            MemoryStream ms = new MemoryStream();
            WriteWav16BitMono(ms, sampleRate, mono);
            PlayStream(ms, wait);
        }

        public static void PlaySamples(int sampleRate, double[] mono, bool wait)
        {
            MemoryStream ms = new MemoryStream();
            WriteWav16BitMono(ms, sampleRate, mono);
            PlayStream(ms, wait);
        }

        public static void PlaySamples(int sampleRate, float[] left, float[] right, bool wait)
        {
            MemoryStream ms = new MemoryStream();
            WriteWav16BitStereo(ms, sampleRate, left, right);
            PlayStream(ms, wait);
        }

        public static void PlaySamples(int sampleRate, double[] left, double[] right, bool wait)
        {
            MemoryStream ms = new MemoryStream();
            WriteWav16BitStereo(ms, sampleRate, left, right);
            PlayStream(ms, wait);
        }

        public static void PlayStream(Stream stream, bool wait)
        {            
            stream.Position = 0;

            //Microsoft.DirectX.DirectSound.DevicesCollection dc = new Microsoft.DirectX.DirectSound.DevicesCollection();
            //foreach (Microsoft.DirectX.DirectSound.DeviceInformation di in dc)
            //    Console.WriteLine(di.Description);
            //Microsoft.DirectX.DirectSound.Device d = new Microsoft.DirectX.DirectSound.Device(dc[1].DriverGuid);
            //Microsoft.DirectX.DirectSound.Buffer b = new Microsoft.DirectX.DirectSound.Buffer(stream, d);
            //b.Play(int.MaxValue, Microsoft.DirectX.DirectSound.BufferPlayFlags.Default);
            //return;

            if (wait)
            {
                System.Media.SoundPlayer sp = new System.Media.SoundPlayer(stream);
                sp.PlaySync();
                stream.Close();                
            }
            else
            {
                System.Threading.Thread thr = new System.Threading.Thread(Play);
                thr.Start(stream);
            };
        }

        public static void PlayFile(string filename, bool wait)
        {            
            FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            stream.Position = 0;
            if (wait)
            {
                System.Media.SoundPlayer sp = new System.Media.SoundPlayer(stream);
                sp.PlaySync();
                stream.Close();
            }
            else
            {
                System.Threading.Thread thr = new System.Threading.Thread(Play);
                thr.Start(stream);
            };
        }

        private static void Play(object strm)
        {
            Stream stream = (Stream)strm;
            System.Media.SoundPlayer sp = new System.Media.SoundPlayer(stream);
            sp.PlaySync();
            stream.Close();
            stream = null;
        }
    }    
}
