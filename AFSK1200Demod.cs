using System;

namespace ax25
{
    public class AFSK1200Demodulator
    {
        public bool ConsoleOut = false;

        private int sample_rate;
        private float decay;

        private float[] td_filter;
        private float[] cd_filter;

        private int rate_index;

        private float samples_per_bit;
        private float[] u1, x;
        private float[] c0_real, c0_imag, c1_real, c1_imag;
        private float[] diff;
        private float previous_fdiff;
        private int last_transition;
        private int data, bitcount;

        private float phase_inc_f0, phase_inc_f1;
        private float phase_inc_symbol;

        private Packet packet; // received packet
        private PacketHandler handler = null;
        private sbyte[] lastPacket = null;
        private int packetsDecoded = 0;

        private enum State
        {
            WAITING,
            JUST_SEEN_FLAG,
            DECODING
        }
        private State state = State.WAITING;

        private int filter_index;
        private int emphasis;

        private bool interpolate = false;
        private float interpolate_last;
        private bool interpolate_original;

        private volatile bool data_carrier = false;
        
        public PacketHandler OnPacket
        {
            set
            {
                this.handler = value;
            }
        }

        public int DecodeCount
        {
            get
            {
                return packetsDecoded;
            }
        }

        public sbyte[] LastPacket
        {
            get
            {
                return lastPacket;
            }
        }

        public AFSK1200Demodulator(int sample_rate, int filter_length, PacketHandler h)
        {
            this.Init(sample_rate, filter_length, 0, h);
        }

        public AFSK1200Demodulator(int sample_rate, int filter_length)
        {
            this.Init(sample_rate, filter_length, 0, null);
        }

        public AFSK1200Demodulator(int sample_rate, int filter_length, int emphasis)
        {
            this.Init(sample_rate, filter_length, emphasis, null);
        }

        public AFSK1200Demodulator(int sample_rate, int filter_length, int emphasis, PacketHandler h)
        {
            this.Init(sample_rate, filter_length, emphasis, h);
        }

        private void Init(int sample_rate, int filter_length, int emphasis, PacketHandler h)
        {
            this.sample_rate = sample_rate;
            this.emphasis = emphasis;
            this.handler = h;
            this.decay = (float)(1.0 - Math.Exp(Math.Log(0.5) / (double)sample_rate));
            if (ConsoleOut)
                Console.WriteLine("decay = {0:e}", (double)decay);

            if (this.sample_rate == 8000)
            {
                interpolate = true;
                this.sample_rate = 16000;
            };

            this.samples_per_bit = (float)sample_rate / 1200.0f;
            if (ConsoleOut)
                Console.WriteLine("samples per bit = {0}", this.samples_per_bit);

            for (rate_index = 0; rate_index < AFSK1200Filters.sample_rates.Length; rate_index++)
                if (AFSK1200Filters.sample_rates[rate_index] == sample_rate)
                    break;

            if (rate_index == AFSK1200Filters.sample_rates.Length)
                throw new Exception("Sample rate " + sample_rate + " not supported");

            float[][][] tdf;
            switch (emphasis)
            {
                case 0:
                    tdf = AFSK1200Filters.time_domain_filter_none;
                    break;
                case 6:
                    tdf = AFSK1200Filters.time_domain_filter_full;
                    break;
                default:
                    if (ConsoleOut)
                        Console.WriteLine("Filter for de-emphasis of {0}dB is not availabe, using 6dB", emphasis);
                    tdf = AFSK1200Filters.time_domain_filter_full;
                    break;
            };

            for (filter_index = 0; filter_index < tdf.Length; filter_index++)
            {
                if (ConsoleOut)
                    Console.WriteLine("Available filter length {0}", tdf[filter_index][rate_index].Length);
                if (filter_length == tdf[filter_index][rate_index].Length)
                {
                    if (ConsoleOut)
                        Console.WriteLine("Using filter length {0}", filter_length);
                    break;
                }
            };

            if (filter_index == tdf.Length)
            {
                filter_index = tdf.Length - 1;
                if (ConsoleOut)
                    Console.WriteLine("Filter length {0} not supported, using length {1}", filter_length, tdf[filter_index][rate_index].Length);
            }

            td_filter = tdf[filter_index][rate_index];
            cd_filter = AFSK1200Filters.corr_diff_filter[filter_index][rate_index];

            x = new float[td_filter.Length];
            u1 = new float[td_filter.Length];

            c0_real = new float[(int)Math.Floor(samples_per_bit)];
            c0_imag = new float[(int)Math.Floor(samples_per_bit)];
            c1_real = new float[(int)Math.Floor(samples_per_bit)];
            c1_imag = new float[(int)Math.Floor(samples_per_bit)];

            diff = new float[cd_filter.Length];

            phase_inc_f0 = (float)(2.0 * Math.PI * 1200.0 / sample_rate);
            phase_inc_f1 = (float)(2.0 * Math.PI * 2200.0 / sample_rate);
            phase_inc_symbol = (float)(2.0 * Math.PI * 1200.0 / sample_rate);
        }

        private bool dcd()
        {
            return data_carrier;
        }

        private float correlation(float[] x, float[] y, int j)
        {
            float c = (float)0.0;
            for (int i = 0; i < x.Length; i++)
            {
                c += x[j] * y[j];
                j--;
                if (j == -1)
                    j = x.Length - 1;
            };
            return c;
        }

        private float sum(float[] x, int j)
        {
            float c = (float)0.0;
            for (int i = 0; i < x.Length; i++)
            {
                c += x[j--];
                if (j == -1)
                    j = x.Length - 1;
            }
            return c;
        }

        private int j_td; // time domain index
        private int j_cd; // time domain index
        private int j_corr; // correlation index
        private float phase_f0, phase_f1;
        private int t; // running sample counter		
        private int flag_count = 0;
        private bool flag_separator_seen = false; // to process the single-bit separation period between flags

        public void AddSamples(double[] s, int n)
        {
            float[] _s = new float[n];
            for (int i = 0; i < n; i++)
                _s[i] = (float)s[i];
            AddSamples(_s, n);
        }

        public void AddSamples(float[] s, int n)
        {
            int i = 0;
            while (i < n)
            {
                float sample;
                if (interpolate)
                {
                    if (interpolate_original)
                    {
                        sample = s[i];
                        interpolate_last = sample;
                        interpolate_original = false;
                        i++;
                    }
                    else
                    {
                        sample = 0.5f * (s[i] + interpolate_last);
                        interpolate_original = true;
                    };
                }
                else
                {
                    sample = s[i];
                    i++;
                };

                //if (sample > vox_threshold || sample < -vox_threshold) {
                //	vox_countdown = sample_rate; // 1s lingering
                //	if (vox_state==false)
                //		System.err.println("vox activating");
                //	vox_state = true;
                //}

                //if (vox_countdown == 0) {
                //	if (vox_state==true)
                //		System.err.println("vox deactivating");
                //	vox_state = false;
                //	continue;
                //} else vox_countdown--;

                u1[j_td] = sample;
                //u1[j_td]= s[i];			
                //u2[j] = Filter.filter(u1, j, Filter.BANDPASS_1150_1250_48000_39);
                //x[j]  = Filter.filter(u2, j, Filter.BANDPASS_2150_2250_48000_39);
                //u2[j] = Filter.filter(u1, j, Filter.BANDPASS_1150_1250_48000_39);
                x[j_td] = Filter.filter(u1, j_td, td_filter);

                // compute correlation running value
                //c0_real[j] = x[j_td]*f0_cos[j];
                //c0_imag[j] = x[j_td]*f0_sin[j];
                //
                //c1_real[j] = x[j_td]*f1_cos[j_f1];
                //c1_imag[j] = x[j_td]*f1_sin[j_f1];

                c0_real[j_corr] = x[j_td] * (float)Math.Cos(phase_f0);
                c0_imag[j_corr] = x[j_td] * (float)Math.Sin(phase_f0);

                c1_real[j_corr] = x[j_td] * (float)Math.Cos(phase_f1);
                c1_imag[j_corr] = x[j_td] * (float)Math.Sin(phase_f1);

                phase_f0 += phase_inc_f0;
                if (phase_f0 > (float)(2.0 * Math.PI))
                    phase_f0 -= (float)(2.0 * Math.PI);
                phase_f1 += phase_inc_f1;
                if (phase_f1 > (float)(2.0 * Math.PI))
                    phase_f1 -= (float)(2.0 * Math.PI);

                float cr = sum(c0_real, j_corr);
                float ci = sum(c0_imag, j_corr);
                float c0 = (float)Math.Sqrt(cr * cr + ci * ci);

                cr = sum(c1_real, j_corr);
                ci = sum(c1_imag, j_corr);
                float c1 = (float)Math.Sqrt(cr * cr + ci * ci);

                //diff[j_corr] = c0-c1;
                diff[j_cd] = c0 - c1;
                //fdiff[j_corr] = Filter.filter(diff,j_corr,Filter.LOWPASS_1200_48000_39);
                //float fdiff = Filter.filter(diff,j_corr,cd_filter);
                float fdiff = Filter.filter(diff, j_cd, cd_filter);

                //System.out.printf("%d %f %f : ",j,diff[j],fdiff[j]);
                //System.out.printf("%d %f %f %f %f : ",j,f0_cos[j],f0_sin[j],f1_cos[j_f1],f1_sin[j_f1]);

                //float previous_fdiff = (j_corr==0) ? fdiff[fdiff.length-1] : fdiff[j_corr-1];
                //if (previous_fdiff*fdiff[j_corr] < 0 || previous_fdiff==0) {
                if (previous_fdiff * fdiff < 0 || previous_fdiff == 0)
                {
                    // we found a transition
                    int p = t - last_transition;
                    last_transition = t;

                    int bits = (int)Math.Round((double)p / (double)samples_per_bit);
                    //System.out.printf("$ %f %d\n",(double) p / (double)samples_per_bit,bits);

                    if (bits == 0 || bits > 7)
                    {
                        state = State.WAITING;
                        data_carrier = false;
                        flag_count = 0;
                    }
                    else
                    {
                        if (bits == 7)
                        {
                            flag_count++;
                            flag_separator_seen = false;
                            //System.out.printf("Seen %d flags in a row\n",flag_count);

                            data = 0;
                            bitcount = 0;
                            switch (state)
                            {
                                case State.WAITING:
                                    state = State.JUST_SEEN_FLAG;
                                    data_carrier = true;
                                    break;
                                case State.JUST_SEEN_FLAG:
                                    break;
                                case State.DECODING:
                                    if (packet != null && packet.terminate())
                                    {
                                        packetsDecoded++;
                                        lastPacket = packet.bytesWithoutCRC();
                                        if (handler != null)
                                            handler.handlePacket(packet.bytesWithoutCRC());                                        
                                    };
                                    packet = null;
                                    state = State.JUST_SEEN_FLAG;
                                    break;
                            };
                        }
                        else
                        {
                            switch (state)
                            {
                                case State.WAITING:
                                    break;
                                case State.JUST_SEEN_FLAG:
                                    state = State.DECODING;
                                    break;
                                case State.DECODING:
                                    break;
                            };
                            if (state == State.DECODING)
                            {
                                if (bits != 1)
                                    flag_count = 0;
                                else
                                {
                                    if (flag_count > 0 && !flag_separator_seen)
                                        flag_separator_seen = true;
                                    else
                                        flag_count = 0;
                                };

                                for (int k = 0; k < bits - 1; k++)
                                {
                                    bitcount++;
                                    data >>= 1;
                                    data += 128;
                                    if (bitcount == 8)
                                    {
                                        if (packet == null)
                                            packet = new Packet();
                                        //if (data==0xAA) packet.terminate();
                                        if (!packet.addByte((sbyte)data))
                                        {
                                            state = State.WAITING;
                                            data_carrier = false;
                                        };
                                        //System.out.printf(">>> %02x %c %c\n", data, (char)data, (char)(data>>1));
                                        data = 0;
                                        bitcount = 0;
                                    };
                                };
                                if (bits - 1 != 5) // the zero after the ones is not a stuffing
                                {
                                    bitcount++;
                                    data >>= 1;
                                    if (bitcount == 8)
                                    {
                                        if (packet == null)
                                            packet = new Packet();
                                        //if (data==0xAA) packet.terminate();
                                        if (!packet.addByte((sbyte)data))
                                        {
                                            state = State.WAITING;
                                            data_carrier = false;
                                        };
                                        //System.out.printf(">>> %02x %c %c\n", data, (char)data, (char)(data>>1));
                                        data = 0;
                                        bitcount = 0;
                                    };
                                };
                            };
                        };
                    };
                };

                previous_fdiff = fdiff;

                t++;

                j_td++;
                if (j_td == td_filter.Length)
                    j_td = 0;

                j_cd++;
                if (j_cd == cd_filter.Length)
                    j_cd = 0;

                j_corr++;
                if (j_corr == c0_real.Length) // samples_per_bit
                    j_corr = 0;

                //j++;
                //if (j==samples_per_bit) j=0;

                //j_f1++;
                //if (j_f1==6*samples_per_bit) j_f1=0;
            }
        }
    }

}