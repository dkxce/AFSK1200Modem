using System;
using System.Net;
using System.Text;

namespace ReadWave
{
    public class AFSK1200ModemTest
    {
        // modulate -> demodulate
        public static void Test1()
        {
            Console.WriteLine("Starting test {0}", 1);

            ax25.AFSK1200Modulator mod = new ax25.AFSK1200Modulator(44100);
            mod.txDelayMs = 500;
            ax25.AFSK1200Demodulator dem = new ax25.AFSK1200Demodulator(44100, 1, 0, new TestConsole("T1>> {0}"));
            
            ax25.Packet packet1 = new ax25.Packet(
                "APRS", "TESTER1", new String[] { "WIDE1-1", "WIDE2-2" },
                ax25.Packet.AX25_CONTROL_APRS, ax25.Packet.AX25_PROTOCOL_NO_LAYER_3,
                System.Text.Encoding.ASCII.GetBytes(@"=5533.00N\03733.00Ek000/000 /A=00010 AFSK Test 1")
                );

            ax25.Packet packet2 = new ax25.Packet(
                "APRS", "TESTER2", new String[] { "WIDE1-1", "WIDE2-2" },
                ax25.Packet.AX25_CONTROL_APRS, ax25.Packet.AX25_PROTOCOL_NO_LAYER_3,
                System.Text.Encoding.ASCII.GetBytes(@">Currently testing...")
                );


            ax25.Packet packet3 = new ax25.Packet(
                "APRS", "TESTER3", new String[] { "WIDE1-1", "WIDE2-2" },
                ax25.Packet.AX25_CONTROL_APRS, ax25.Packet.AX25_PROTOCOL_NO_LAYER_3,
                System.Text.Encoding.ASCII.GetBytes(@"=4802.37N/03934.12E0")
                );
            
            float[] samples;

            mod.GetSamples(packet1, out samples);
            dem.AddSamples(samples, samples.Length);

            mod.GetSamples(new ax25.Packet[] { packet2, packet3 }, out samples);
            dem.AddSamples(samples, samples.Length);

            Console.WriteLine("Test {0} done", 1);
        }
        
        // demodulate from direct input
        public static void Test2()
        {
            Console.WriteLine("Starting test {0}", 2);

            DirectAudioAFSKDemodulator m = new DirectAudioAFSKDemodulator(0, new TestConsole("T2>> {0}"));
            m.Start();
            WaveStream.PlayFile("test2.wav", true);
            //while (true) System.Threading.Thread.Sleep(100);
            m.Stop();

            Console.WriteLine("Test {0} done", 2);
        }

        // modulate and play, save & demodulate
        public static void Test3()
        {
            Console.WriteLine("Starting test {0}", 3);

            ax25.Packet packet;
            ax25.AFSK1200Modulator mod;
            float[] samples;
            ax25.AFSK1200Demodulator dem;

            mod = new ax25.AFSK1200Modulator(44100);
            mod.txDelayMs = 500;
            dem = new ax25.AFSK1200Demodulator(44100, 1, 0, new TestConsole("T3>> {0}"));
            //ax25.AFSK1200Demodulator dem = new ax25.Afsk1200Demodulator(44100, 1, 6, new Packet2Console("MO>> {0}"));

            packet = new ax25.Packet(
                "APRS", "TESTER", new String[] { "WIDE1-1", "WIDE2-2" },
                ax25.Packet.AX25_CONTROL_APRS, ax25.Packet.AX25_PROTOCOL_NO_LAYER_3,
                System.Text.Encoding.ASCII.GetBytes(@"=5533.00N\03733.00Ek000/000 /A=00010 AFSK Test")
                );

            mod.GetSamples(packet, out samples);
            dem.AddSamples(samples, samples.Length);

            // PLAY
            WaveStream.PlaySamples(44100, samples, false);

            // SAVE
            WaveStream.SaveWav16BitMono(@"test3.wav", 44100, samples);

            Console.WriteLine("Test {0} done", 3);
        }

        // demodulate from wav file
        public static void Test4()
        {
            Console.WriteLine("Starting test {0}", 4);

            float[] L, R;
            int ch, sr, bd;
            WaveStream.ReadWavFile(@"test4.wav", out ch, out sr, out bd, out L, out R);

            ax25.AFSK1200Demodulator dem = new ax25.AFSK1200Demodulator(sr, 36, 0, new TestConsole("T4>> {0}"));
            dem.AddSamples(L, L.Length);
            string rp = ax25.Packet.Format(dem.LastPacket);

            Console.WriteLine("Test {0} done", 4);
        }

        // demodulate from direct input
        public static void Test5(int deviceNo)
        {
            Console.WriteLine("AFSK1200 AX.25 Modem");
            string[] lwd = DirectAudioAFSKDemodulator.WaveInDevices();
            if (lwd != null)
            {
                Console.WriteLine("You Can Use Device:");
                foreach (string l in lwd)
                    Console.WriteLine(" {0}", l);
            };
            if (deviceNo < 0)
            {
                Console.WriteLine("Syntax:");
                Console.WriteLine(" AFSKModem.exe <deviceNo>");
                Console.WriteLine("Example:");
                if (lwd != null)
                    for(int i=0;i<lwd.Length;i++)
                        Console.WriteLine(" AFSKModem.exe {0}", i);
                Console.WriteLine();
                deviceNo = 0;
            };
            Console.WriteLine("Starting Demodulation of Audio Input AFSK1200 AX.25 APRS");
            Console.WriteLine("Using Device: {0}", DirectAudioAFSKDemodulator.WaveInDevices()[deviceNo]);

            DirectAudioAFSKDemodulator m = new DirectAudioAFSKDemodulator(deviceNo, new TestConsole("{0}"));
            m.Start();
            while (true) Console.ReadLine();
            m.Stop();
        }

        // IPv4 ROUTES
        public static void Test6(bool play = true)
        {
            ax25.AFSK1200Modulator mod = new ax25.AFSK1200Modulator(44100);
            
            // make packet
            string text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";
            byte[] data = System.Text.Encoding.ASCII.GetBytes(text);
            ax25.Packet packet = new ax25.Packet("10.0.0.2", "10.0.0.1", new string[] { "10.0.0.254" }, data);
            Console.WriteLine($"Outgoing packet: {text}");

            // save wave
            mod.GetSamples(packet, out float[] samples);
            if(play) WaveStream.PlaySamples(44100, samples, play);
            WaveStream.SaveWav16BitMono(@"test6.wav", 44100, samples);
            
            // load wave
            WaveStream.ReadWavFile(@"test6.wav", out _, out int sr, out _, out float[] L, out _);

            // read wave
            ax25.AFSK1200Demodulator dem = new ax25.AFSK1200Demodulator(sr, 36, 0, new IPv4ConsolePacketHandler());
            dem.AddSamples(L, L.Length);
            Console.WriteLine($"Incoming packet: {ax25.Packet.Format(dem.LastPacket)}");            
        }


        public class TestConsole : ax25.PacketHandler
        {
            private string frm = "{0}";

            public TestConsole() { }
            public TestConsole(string frm) { this.frm = frm; }

            public void handlePacket(sbyte[] bytes)
            {
                string packet = ax25.Packet.Format(bytes);
                Console.WriteLine(frm, packet);
            }
        }

        public class IPv4ConsolePacketHandler : ax25.PacketHandler
        {
            public IPv4ConsolePacketHandler() { }

            public void handlePacket(sbyte[] bytes)
            {
                byte[] dp = ax25.Packet.FormatIPv4(bytes, out IPAddress f, out IPAddress t, out IPAddress[] v);
                if (dp == null) return;
                try
                {
                    string dsp = Encoding.ASCII.GetString(dp);
                    string sv = ""; foreach (IPAddress ip in v) sv = (sv.Length > 0 ? "," : "") + ip.ToString();
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("Incoming packet from ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(f);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(" to ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(t);
                    if (!string.IsNullOrEmpty(sv))
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write(" via ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(sv);
                    };
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(": ");
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(dsp);
                    Console.ForegroundColor = ConsoleColor.White;
                }
                catch (Exception ex) { Console.Write($"{ex}"); };
            }
        }
    }
}
