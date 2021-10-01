using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ax25
{
    public class Packet
    {
        private const int AX25_CRC_CORRECT = 0xF0B8;
        private const int CRC_CCITT_INIT_VAL = 0xFFFF;
        private const int MAX_FRAME_SIZE = 7 + 7 + (8 * 7) + 1 + 1 + 256 + 2; // frame checksum -  information -  control and PID -  path -  source and destination -  not including delimiting flags

        public const int AX25_CONTROL_APRS = 0x03;
        public const int AX25_PROTOCOL_COMPRESSED_TCPIP = 0x06;
        public const int AX25_PROTOCOL_UNCOMPRESSED_TCPIP = 0x07;
        public const int AX25_PROTOCOL_NO_LAYER_3 = 0xF0; // used for APRS

        private readonly int[] crc_ccitt_tab = { 0x0000, 0x1189, 0x2312, 0x329b, 0x4624, 0x57ad, 0x6536, 0x74bf, 0x8c48, 0x9dc1, 0xaf5a, 0xbed3, 0xca6c, 0xdbe5, 0xe97e, 0xf8f7, 0x1081, 0x0108, 0x3393, 0x221a, 0x56a5, 0x472c, 0x75b7, 0x643e, 0x9cc9, 0x8d40, 0xbfdb, 0xae52, 0xdaed, 0xcb64, 0xf9ff, 0xe876, 0x2102, 0x308b, 0x0210, 0x1399, 0x6726, 0x76af, 0x4434, 0x55bd, 0xad4a, 0xbcc3, 0x8e58, 0x9fd1, 0xeb6e, 0xfae7, 0xc87c, 0xd9f5, 0x3183, 0x200a, 0x1291, 0x0318, 0x77a7, 0x662e, 0x54b5, 0x453c, 0xbdcb, 0xac42, 0x9ed9, 0x8f50, 0xfbef, 0xea66, 0xd8fd, 0xc974, 0x4204, 0x538d, 0x6116, 0x709f, 0x0420, 0x15a9, 0x2732, 0x36bb, 0xce4c, 0xdfc5, 0xed5e, 0xfcd7, 0x8868, 0x99e1, 0xab7a, 0xbaf3, 0x5285, 0x430c, 0x7197, 0x601e, 0x14a1, 0x0528, 0x37b3, 0x263a, 0xdecd, 0xcf44, 0xfddf, 0xec56, 0x98e9, 0x8960, 0xbbfb, 0xaa72, 0x6306, 0x728f, 0x4014, 0x519d, 0x2522, 0x34ab, 0x0630, 0x17b9, 0xef4e, 0xfec7, 0xcc5c, 0xddd5, 0xa96a, 0xb8e3, 0x8a78, 0x9bf1, 0x7387, 0x620e, 0x5095, 0x411c, 0x35a3, 0x242a, 0x16b1, 0x0738, 0xffcf, 0xee46, 0xdcdd, 0xcd54, 0xb9eb, 0xa862, 0x9af9, 0x8b70, 0x8408, 0x9581, 0xa71a, 0xb693, 0xc22c, 0xd3a5, 0xe13e, 0xf0b7, 0x0840, 0x19c9, 0x2b52, 0x3adb, 0x4e64, 0x5fed, 0x6d76, 0x7cff, 0x9489, 0x8500, 0xb79b, 0xa612, 0xd2ad, 0xc324, 0xf1bf, 0xe036, 0x18c1, 0x0948, 0x3bd3, 0x2a5a, 0x5ee5, 0x4f6c, 0x7df7, 0x6c7e, 0xa50a, 0xb483, 0x8618, 0x9791, 0xe32e, 0xf2a7, 0xc03c, 0xd1b5, 0x2942, 0x38cb, 0x0a50, 0x1bd9, 0x6f66, 0x7eef, 0x4c74, 0x5dfd, 0xb58b, 0xa402, 0x9699, 0x8710, 0xf3af, 0xe226, 0xd0bd, 0xc134, 0x39c3, 0x284a, 0x1ad1, 0x0b58, 0x7fe7, 0x6e6e, 0x5cf5, 0x4d7c, 0xc60c, 0xd785, 0xe51e, 0xf497, 0x8028, 0x91a1, 0xa33a, 0xb2b3, 0x4a44, 0x5bcd, 0x6956, 0x78df, 0x0c60, 0x1de9, 0x2f72, 0x3efb, 0xd68d, 0xc704, 0xf59f, 0xe416, 0x90a9, 0x8120, 0xb3bb, 0xa232, 0x5ac5, 0x4b4c, 0x79d7, 0x685e, 0x1ce1, 0x0d68, 0x3ff3, 0x2e7a, 0xe70e, 0xf687, 0xc41c, 0xd595, 0xa12a, 0xb0a3, 0x8238, 0x93b1, 0x6b46, 0x7acf, 0x4854, 0x59dd, 0x2d62, 0x3ceb, 0x0e70, 0x1ff9, 0xf78f, 0xe606, 0xd49d, 0xc514, 0xb1ab, 0xa022, 0x92b9, 0x8330, 0x7bc7, 0x6a4e, 0x58d5, 0x495c, 0x3de3, 0x2c6a, 0x1ef1, 0x0f78 };

        private int crc = CRC_CCITT_INIT_VAL;
        private sbyte[] packet = new sbyte[MAX_FRAME_SIZE];
        private int size = 0;

        public string source, destination;
        public string[] path;
        public sbyte[] payload;

        // empty packet for reception
        public Packet() { }

        // packet from raw bytes
        public Packet(sbyte[] bytes)
        {
            //System.Diagnostics.Debug.Assert(crc == CRC_CCITT_INIT_VAL);
            //System.Diagnostics.Debug.Assert(bytes.Length + 2 <= packet.Length);

            for (int i = 0; i < bytes.Length; i++)
            {
                packet[size] = bytes[i];
                crc_ccitt_update(packet[size]);
                size++;
            };

            int crcl = (crc & 0xff) ^ 0xff;
            int crch = (crc >> 8) ^ 0xff;

            packet[size] = (sbyte)crcl;
            crc_ccitt_update(packet[size]);
            size++;

            packet[size] = (sbyte)crch;
            crc_ccitt_update(packet[size]);
            size++;

            //System.Diagnostics.Debug.Assert(crc == AX25_CRC_CORRECT);
        }

        // packet from string
        public Packet(string destination, string source, string[] digipath, int control, int protocol, byte[] frame)
        {
            int n = 7 + 7 + 7 * digipath.Length + 2 + frame.Length;
            sbyte[] bytes = new sbyte[n];

            int offset = 0;

            addCallsign(bytes, offset, destination, false);
            offset += 7;

            addCallsign(bytes, offset, source, digipath == null || digipath.Length == 0);
            offset += 7;

            for (int i = 0; i < digipath.Length; i++)
            {
                addCallsign(bytes, offset, digipath[i], i == digipath.Length - 1);
                offset += 7;
            };

            bytes[offset++] = (sbyte)control;
            bytes[offset++] = (sbyte)protocol;

            for (int j = 0; j < frame.Length; j++)
                bytes[offset++] = (sbyte)frame[j];

            for (int i = 0; i < bytes.Length; i++)
            {
                packet[size] = bytes[i];
                crc_ccitt_update(packet[size]);
                size++;
            };

            int crcl = (crc & 0xff) ^ 0xff;
            int crch = (crc >> 8) ^ 0xff;

            packet[size] = (sbyte)crcl;
            crc_ccitt_update(packet[size]);
            size++;

            packet[size] = (sbyte)crch;
            crc_ccitt_update(packet[size]);
            size++;
            //System.Diagnostics.Debug.Assert(crc == AX25_CRC_CORRECT);
        }

        public sbyte[] bytesWithCRC()
        {
            return Arrays.copyOf(packet, 0, size);
        }

        public sbyte[] bytesWithoutCRC()
        {
            return Arrays.copyOf(packet, 0, size - 2);
        }

        private static void addCallsign(sbyte[] bytes, int offset, string callsign, bool last)
        {
            int i;
            bool call_ended = false;
            char c = ' ';
            int ssid = 0;

            for (i = 0; i < 6; i++)
            {
                if (i < callsign.Length)
                    c = callsign[i];
                else
                    call_ended = true;
                if (call_ended || !char.IsLetterOrDigit(c) || c == '-')
                {
                    call_ended = true;
                    c = ' ';
                }
                else
                    c = char.ToUpper(c);
                bytes[offset++] = (sbyte)(c << 1);
            };

            for (i = 0; i < callsign.Length; i++)
            {
                c = callsign[i];
                if (c == '-' && i + 1 < callsign.Length)
                {
                    try
                    {
                        ssid = Convert.ToInt32(callsign.Substring(i + 1));
                    }
                    catch { ssid = 0; };
                    if (ssid > 15 || ssid < 0) ssid = 0;
                    break;
                };
            };

            // The low-order bit of last call SSID should be set to 1
            ssid = (ssid << 1) | (0x60) | (last ? 0x01 : 0);
            bytes[offset++] = (sbyte)ssid;
        }

        private static string parseCallsign(sbyte[] packet, int offset)
        {
            string call = "";
            int c, i;

            for (i = 0; i < 6; i++)
            {
                c = (packet[offset + i] > 0) ? packet[offset + i] >> 1 : (packet[offset + i] + 256) >> 1;
                if ((char)c != ' ')
                    call += (char)c;
            };

            c = (packet[offset + i] > 0) ? packet[offset + i] >> 1 : (packet[offset + i] + 256) >> 1;
            int ssid = c & 0x0f;
            if (ssid != 0)
                call += string.Format("-{0:D}", ssid);

            return call;
        }

        public void Parse()
        {
            int offset = 0;
            destination = parseCallsign(packet, offset);
            offset += 7;
            source = parseCallsign(packet, offset);
            offset += 7;

            int repeaters = 0;
            while (offset + 7 <= size && (packet[offset - 1] & 0x01) == 0)
            {
                repeaters++;
                if (repeaters > 8) // missing LSB=1 to terminate the path
                    break;

                string path_element = parseCallsign(packet, offset);
                offset += 7;
                if (path == null)
                {
                    path = new string[1];
                    path[0] = path_element;
                }
                else
                {
                    List<string> np = new List<string>(path);
                    np.Add(path_element);
                    path = np.ToArray();
                };
            }

            offset += 2; // skip PID, control
            payload = Arrays.copyOf(packet, offset, size - 2); // chop off CRC
        }

        public static string Format(sbyte[] packet)
        {
            string source, destination;
            string[] path = null;
            sbyte[] payload;
            int offset = 0;
            int repeaters = 0;
            int size = packet.Length;

            destination = parseCallsign(packet, offset);
            offset += 7;
            source = parseCallsign(packet, offset);
            offset += 7;

            while (offset + 7 <= size && (packet[offset - 1] & 0x01) == 0)
            {
                repeaters++;
                if (repeaters > 8) // missing LSB=1 to terminate the path
                    break;

                string path_element = parseCallsign(packet, offset);
                offset += 7;
                if (path == null)
                {
                    path = new string[1];
                    path[0] = path_element;
                }
                else
                {
                    string[] tmppath = new string[path.Length + 1];
                    Array.Copy(path, tmppath, path.Length);
                    tmppath[tmppath.Length - 1] = path_element;
                    path = tmppath;
                };
            }

            offset += 2; // skip PID, control
            payload = Arrays.copyOf(packet, offset, size);


            StringBuilder builder = new StringBuilder();

            builder.Append(source);
            builder.Append('>');
            builder.Append(destination);
            if (path != null)
            {
                foreach (string via in path)
                {
                    builder.Append(',');
                    builder.Append(via);
                };
            };
            builder.Append(':');

            for (int i = 0; i < payload.Length; i++)
            {
                char c = (char)payload[i];
                if (c >= 0x20 && c <= 0x7E)
                    builder.Append(c);
                else
                    builder.Append(string.Format("\\x{0:x2}", payload[i]));
            };
            /*
            for (int i=0; i<size; i++) {
                char c = (char) packet[i];
                if (c >= 0x20 && c <= 0x7E) builder.append(c);
                else builder.append('.');
            }
            //builder.append(Arrays.toString(packet));
             */
            return builder.ToString();
        }

        private void crc_ccitt_update(sbyte b)
        {
            crc = (crc >> 8) ^ crc_ccitt_tab[(crc ^ b) & 0xff];
        }

        public bool addByte(sbyte b)
        {
            if (size >= MAX_FRAME_SIZE)
                return false;

            crc_ccitt_update(b);

            packet[size] = b;
            size++;

            return true;
        }

        public bool terminate()
        {
            //System.out.printf("checking termination last byte %02x\n",packet[size-1]);
            if (size < 18) // at least source, destination, control, pid, FCS.
                return false;
            if (crc == AX25_CRC_CORRECT)
                return true;
            else
                return false;
        }

        public override string ToString()
        {
            Parse();

            StringBuilder builder = new StringBuilder();

            builder.Append(source);
            builder.Append('>');
            builder.Append(destination);

            if (path != null)
                foreach (string via in path)
                {
                    builder.Append(',');
                    builder.Append(via);
                };

            builder.Append(':');

            for (int i = 0; i < payload.Length; i++)
            {
                char c = (char)payload[i];
                if (c >= 0x20 && c <= 0x7E)
                    builder.Append(c);
                else
                    builder.Append(string.Format("\\x{0:x2}", payload[i]));
            };

            /*
            for (int i=0; i<size; i++) {
                char c = (char) packet[i];
                if (c >= 0x20 && c <= 0x7E) builder.append(c);
                else builder.append('.');
            }
            //builder.append(Arrays.toString(packet));
             */

            return builder.ToString();
        }
    }

    public class Arrays
    {
        public static sbyte[] copyOf(sbyte[] src, int from, int to)
        {
            sbyte[] dest = new sbyte[to - from];
            Array.Copy(src, from, dest, 0, to - from);
            return dest;
        }
    }

    public interface PacketHandler
    {
        void handlePacket(sbyte[] packet);
    }
}