using System;
using System.Collections.Generic;

namespace ax25
{
    public class AFSK1200Modulator
    {
        public bool ConsoleOut = false;

        private float phase_inc_f0, phase_inc_f1;
        private float phase_inc_symbol;
        private int sample_rate;

        public AFSK1200Modulator(int sample_rate)
        {
            this.sample_rate = sample_rate;
            phase_inc_f0 = (float)(2.0 * Math.PI * 1200.0 / sample_rate);
            phase_inc_f1 = (float)(2.0 * Math.PI * 2200.0 / sample_rate);
            phase_inc_symbol = (float)(2.0 * Math.PI * 1200.0 / sample_rate);
        }

        public int txDelayMs
        {
            set
            {
                tx_delay = value / 10;
            }
            get
            {
                return (int)tx_delay * 10;
            }
        }
        public int txTailMs
        {
            set
            {
                tx_tail = value / 10;
            }
            get
            {
                return tx_tail * 10;
            }
        }

        private enum TxState
        {
            IDLE,
            PREAMBLE,
            DATA,
            TRAILER
        }

        private TxState tx_state = TxState.IDLE;
        private sbyte[] tx_bytes;
        private int tx_index;
        private int tx_delay = 50; // default is 50*10ms = 500ms
        private int tx_tail = 0; // obsolete
        private float tx_symbol_phase, tx_dds_phase;

        private float[] tx_samples;
        private int tx_last_symbol;
        private int tx_stuff_count;

        private void prepareToTransmitFlags(int seconds)
        {
            if (tx_state != TxState.IDLE)
            {
                if(ConsoleOut)
                    Console.WriteLine("Warning: trying to trasmit while Afsk1200 modulator is busy, discarding");
                return;
            };
            tx_bytes = null; // no data
            tx_state = TxState.PREAMBLE;
            tx_index = (int)Math.Ceiling((double)seconds / (8.0 / 1200.0)); // number of flags to transmit
            tx_symbol_phase = tx_dds_phase = 0.0f;
        }

        private void prepareToTransmit(Packet p)
        {
            if (tx_state != TxState.IDLE)
            {
                if (ConsoleOut)
                    Console.WriteLine("Warning: trying to trasmit while Afsk1200 modulator is busy, discarding");
                return;
            };
            tx_bytes = p.bytesWithCRC(); // This includes the CRC
            tx_state = TxState.PREAMBLE;
            tx_index = (int)Math.Ceiling(tx_delay * 0.01 / (8.0 / 1200.0)); // number of flags to transmit
            if (tx_index < 1)
                tx_index = 1;
            tx_symbol_phase = tx_dds_phase = 0.0f;
        }

        public void GetSamples(Packet p, out double[] _samples)
        {
            _samples = null;

            prepareToTransmit(p);

            List<double> smpls = new List<double>();

            int n;
            float[] tx_samples = txSamplesBuffer;
            while ((n = samples) > 0)
                for (int i = 0; i < n; i++)
                    smpls.Add(tx_samples[i]);

            _samples = smpls.ToArray();
        }

        public void GetSamples(Packet[] p, out double[] _samples)
        {
            _samples = null;

            List<double> outlist = new List<double>();

            int delay = this.txDelayMs;
            int tail = this.txTailMs;

            this.txTailMs = 0;
            for (int i = 0; i < p.Length; i++)
            {
                if (i == 1) this.txDelayMs = 0;
                if (i == (p.Length - 1)) this.txTailMs = tail;
                double[] _s;
                GetSamples(p[i], out _s);
                outlist.AddRange(_s);
            };
            this.txDelayMs = delay;
            _samples = outlist.ToArray();
        }

        public void GetSamples(Packet p, out float[] _samples)
        {
            _samples = null;

            prepareToTransmit(p);

            List<float> smpls = new List<float>();

            int n;
            float[] tx_samples = txSamplesBuffer;
            while ((n = samples) > 0)
                for (int i = 0; i < n; i++)
                    smpls.Add(tx_samples[i]);

            _samples = smpls.ToArray();
        }

        public void GetSamples(Packet[] p, out float[] _samples)
        {
            _samples = null;

            List<float> outlist = new List<float>();

            int delay = this.txDelayMs;
            int tail = this.txTailMs;

            this.txTailMs = 0;
            for (int i = 0; i < p.Length; i++)
            {
                if (i == 1) this.txDelayMs = 0;
                if (i == (p.Length - 1)) this.txTailMs = tail;
                float[] _s;
                GetSamples(p[i], out _s);
                outlist.AddRange(_s);
            };
            this.txDelayMs = delay;
            _samples = outlist.ToArray();
        }

        private float[] txSamplesBuffer
        {
            get
            {
                if (tx_samples == null)
                {
                    // each byte makes up to 10 symbols,
                    // each symbol takes (1/1200)s to transmit.
                    tx_samples = new float[(int)((Math.Ceiling((10.0 / 1200.0) * sample_rate) + 1))];
                }
                return tx_samples;
            }
        }

        private int generateSymbolSamples(int symbol, float[] s, int position)
        {
            int count = 0;
            while (tx_symbol_phase < (float)(2.0 * Math.PI))
            {
                s[position] = (float)Math.Sin(tx_dds_phase);

                if (symbol == 0)
                    tx_dds_phase += phase_inc_f0;
                else
                    tx_dds_phase += phase_inc_f1;

                tx_symbol_phase += phase_inc_symbol;

                //if (tx_symbol_phase > (float) (2.0*Math.PI)) tx_symbol_phase -= (float) (2.0*Math.PI);
                if (tx_dds_phase > (float)(2.0 * Math.PI))
                    tx_dds_phase -= (float)(2.0 * Math.PI);

                position++;
                count++;
            }

            tx_symbol_phase -= (float)(2.0 * Math.PI);

            return count;
        }

        private int byteToSymbols(int bits, bool stuff)
        {
            int symbol;
            int position = 0;
            int n;
            //System.out.printf("byte=%02x stuff=%b\n",bits,stuff);
            for (int i = 0; i < 8; i++)
            {
                int bit = bits & 1;
                //System.out.println("i="+i+" bit="+bit);
                bits = bits >> 1;
                if (bit == 0) // we switch sybols (frequencies)
                {
                    symbol = (tx_last_symbol == 0) ? 1 : 0;
                    n = generateSymbolSamples(symbol, tx_samples, position);
                    position += n;

                    if (stuff)
                        tx_stuff_count = 0;
                    tx_last_symbol = symbol;
                }
                else
                {
                    symbol = (tx_last_symbol == 0) ? 0 : 1;
                    n = generateSymbolSamples(symbol, tx_samples, position);
                    position += n;

                    if (stuff)
                        tx_stuff_count++;
                    tx_last_symbol = symbol;

                    if (stuff && tx_stuff_count == 5)
                    {
                        // send a zero
                        //System.out.println("stuffing a zero bit!");
                        symbol = (tx_last_symbol == 0) ? 1 : 0;
                        n = generateSymbolSamples(symbol, tx_samples, position);
                        position += n;

                        tx_stuff_count = 0;
                        tx_last_symbol = symbol;
                    };
                };
            }

            return position;
        }

        private int samples
        {
            get
            {
                int count;

                switch (tx_state)
                {
                    case TxState.IDLE:
                        return 0;

                    case TxState.PREAMBLE:
                        count = byteToSymbols(0x7E, false);

                        tx_index--;
                        if (tx_index == 0)
                        {
                            tx_state = TxState.DATA;
                            tx_index = 0;
                            tx_stuff_count = 0;
                        }
                        break;

                    case TxState.DATA:
                        if (tx_bytes == null) // we just wanted to transmit tones to adjust the transmitter
                        {
                            tx_state = TxState.IDLE;
                            return 0;
                        }
                        //System.out.printf("Data byte %02x\n",tx_bytes[tx_index]);
                        count = byteToSymbols(tx_bytes[tx_index], true);

                        tx_index++;
                        if (tx_index == tx_bytes.Length)
                        {
                            tx_state = TxState.TRAILER;
                            if (tx_tail <= 0) // this should be the normal case
                                tx_index = 2;
                            else
                            {
                                tx_index = (int)Math.Ceiling(tx_tail * 0.01 / (8.0 / 1200.0)); // number of flags to transmit
                                if (tx_tail < 2)
                                    tx_tail = 2;
                            };
                        }
                        break;

                    case TxState.TRAILER:
                        count = byteToSymbols(0x7E, false);
                        tx_index--;
                        if (tx_index == 0)
                            tx_state = TxState.IDLE;
                        break;

                    default:
                        count = -1;
                        break;
                };

                return count;
            }
        }
    }
}