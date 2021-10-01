using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace ReadWave
{
    class Program
    {        
        static void Main(string[] args)
        {
            AFSK1200ModemTest.Test5(args == null || args.Length == 0 ? -1 : int.Parse(args[0]));
            return;

            bool test = true;
            bool aprsis = true;
            bool afwe = false;
            bool sound = true;

            if (test)
            {
                AFSK1200ModemTest.Test1();
                AFSK1200ModemTest.Test2();
                AFSK1200ModemTest.Test3();
                AFSK1200ModemTest.Test4();
            };
            
            
            TcpClient tcpc = null;
            if (aprsis)
            {
                tcpc = new TcpClient();
                tcpc.Connect("127.0.0.1", 12015);
                string auth = "user AFSKX25 pass -1 vers ASFKModem 0.1\r\n";
                tcpc.Client.Send(System.Text.Encoding.ASCII.GetBytes(auth));
            };

            AgwpePort.AgwpePort agwe = null;
            if (afwe)
            {
                agwe = new AgwpePort.AgwpePort();
                agwe.Open((byte)0, "127.0.0.1", 8000);
                agwe.SendUnproto((byte)0, "TESTER", "APRS", System.Text.Encoding.GetEncoding(1251).GetBytes("=5533.00N\03733.00Ek Testing"));                
                agwe.StartMonitoring();
                agwe.FrameReceived += new AgwpePort.AgwpePort.AgwpeFrameReceivedEventHandler(agwe_client_FrameReceived);
            };

            ax25.AFSK1200Modulator mod = null;
            if (sound)
            {
                mod = new ax25.AFSK1200Modulator(44100);
                mod.txDelayMs = 750;
            };

            while ((tcpc != null) && tcpc.Client.Connected)
            {
                string rxText = "";
                byte[] rxBuffer = new byte[4096];
                int rxCount = 0;
                int rxAvailable = tcpc.Client.Available;
                while (rxAvailable > 0)
                {
                    try { rxAvailable -= (rxCount = tcpc.Client.Receive(rxBuffer, 0, rxBuffer.Length > rxAvailable ? rxAvailable : rxBuffer.Length, SocketFlags.None)); }
                    catch { break; };
                    if (rxCount > 0) rxText += Encoding.ASCII.GetString(rxBuffer, 0, rxCount);
                };
                if (rxText != "")
                {
                    string[] lines = rxText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    List<ax25.Packet> packets = new List<ax25.Packet>();
                    foreach (string line in lines)
                    {
                        Console.WriteLine("{0}/{1}>> {2}","T",DateTime.UtcNow.ToString("HHmmss"),line);
                        if (line.IndexOf("#") != 0)
                        {
                            string from = line.Substring(0, line.IndexOf(">"));
                            string pckt = line.Substring(line.IndexOf(":") + 1);
                            if(agwe != null)
                                agwe.SendUnproto((byte)0, from, "APRS", System.Text.Encoding.ASCII.GetBytes(pckt));

                            if (sound)
                            {
                                packets.Add(new ax25.Packet(
                                    "APRS", from, new String[] { "WIDE1-1", "WIDE2-2" },
                                    ax25.Packet.AX25_CONTROL_APRS, ax25.Packet.AX25_PROTOCOL_NO_LAYER_3,
                                    System.Text.Encoding.ASCII.GetBytes(pckt)
                                    ));
                            };
                        };
                    };                    
                    if (packets.Count > 0)
                    {
                        float[] samples;
                        mod.GetSamples(packets.ToArray(), out samples);
                        WaveStream.PlaySamples(44100, samples, false);
                    };
                    packets.Clear();
                };                    
                System.Threading.Thread.Sleep(10);
            };
            Console.ReadLine();

            tcpc.Close();
            if(agwe != null) agwe.Close();
        }

        static void agwe_client_FrameReceived(object sender, AgwpePort.AgwpeEventArgs e)
        {
            // AGWPE Config and Information Frames
            // Read AX25 Unproto (UI) frames and data sent from a remote packet radio station
            if (e.FrameHeader.DataKind == ((byte)'U'))
            {
                AgwpePort.AgwpeMoniUnproto md = (AgwpePort.AgwpeMoniUnproto)e.FrameData;
                string cmd = md.AX25CallFrom + "WE>>" + md.AX25CallTo.Replace(" Via ", ",").Replace(" ", "") + ":" + System.Text.Encoding.GetEncoding(1251).GetString(md.AX25Data);
                Console.WriteLine("{0}{1} {2}", "A", DateTime.UtcNow.ToString("HHmmss"), cmd);
            };
        }        
    }  
}
