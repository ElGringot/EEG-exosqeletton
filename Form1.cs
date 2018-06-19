using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace gUSBampSyncDemoCS
{
    public partial class Form1 : Form
    {
        private delegate void CommonTaskDelegate();

        private Thread DAQ_hThread;
        private Thread Filter_hThread;

        private const int DAQ_hThread_PERIOD = 20;
        private const int Filter_hThread_PERIOD = 20;

        private bool DAQ_isRunning = false;
        private bool Filter_isRunning = false;

        int NumSecondsRunning = 1;
        DataAcquisitionUnit acquisitionUnit;
        FilterButterworth Filter;
        double[][] f;
        int numScans;
        int numChannels;
        int numValuesAtOnce;
        int x;

        public Form1()
        {
            InitializeComponent();
        }

        public void DAQ_TaskInit()
        {
            DAQ_isRunning = true;
            //data = new double[] { 1.0, 2.0, 3.3, 4.7 };
            acquisitionUnit = new DataAcquisitionUnit();

            //create device configurations
            Dictionary<string, DeviceConfiguration> devices = CreateDefaultDeviceConfigurations("UB-2011.11.15");

            //determine how many bytes should be read and processed at once by the processing thread (not the acquisition thread!)
            numScans = 512;
            numChannels = 0;

            foreach (DeviceConfiguration deviceConfiguration in devices.Values)
                numChannels += (deviceConfiguration.SelectedChannels.Count + Convert.ToInt32(deviceConfiguration.TriggerLineEnabled));

            numValuesAtOnce = numScans * numChannels;
            acquisitionUnit.StartAcquisition(devices);

            f = new double[numChannels][];
            for (int j = 0; j < numChannels; ++j)
            {
                f[j] = new double[numScans * NumSecondsRunning];
            }
            x = 0;

        }

        public void DAQ_TaskLoop()
        {
            CommonTaskDelegate LocalTaskInstance;
            while (true)
            {

                //this is the data processing thread; data received from the devices will be written out to a file here
                if (DAQ_isRunning) // Start, Stop
                {
                    DAQ_TaskFunction();

                    LocalTaskInstance = new CommonTaskDelegate(DAQ_DisplayFunction);
                    this.BeginInvoke(LocalTaskInstance);
                }
                Thread.Sleep(DAQ_hThread_PERIOD);
            }
        }

        public void DAQ_TaskFunction()
        {
            float[] data = acquisitionUnit.ReadData(numValuesAtOnce);

            if (data.Length > 0)
            {
                for (int i = 0; i < data.Length; ++i)
                {
                    //int chan = i % numChannels;
                    f[i % numChannels][(i / numChannels) + x] = data[i];
                }
                x = (data.Length / numChannels) % (numScans * NumSecondsRunning);
            }
        }

        public void DAQ_DisplayFunction()
        {
            chart1.Series["TF"].Points.Clear();
            Complex[] Cf = new Complex[numScans * NumSecondsRunning];
            Complex[][] TF = new Complex[numScans * NumSecondsRunning][];
            for (int i = 0; i < numChannels; ++i)
            {
                for (int j = 0; j < f[i].Length; ++j)
                {
                    Cf[j] = new Complex(f[i][j], 0);
                }
                TF[i] = fourier.FFT(Cf);
            }
            double Amp, Amp0;
            Amp0 = Math.Sqrt(Math.Pow(TF[0][0].Real, 2) + Math.Pow(TF[0][0].Imaginary, 2));
            for (int i = 0; i < numScans * NumSecondsRunning / 2; ++i)
            {
                //Console.Write(TF[1][i].Real + "  " + TF[1][i].Imaginary + "\n");
                Amp = Math.Sqrt(Math.Pow(TF[0][i].Real, 2) + Math.Pow(TF[0][i].Imaginary, 2));
                //Console.Write(Amp + "\n");
                chart1.Series["TF"].Points.AddY(Amp);
                if (Amp > Amp0)
                {
                    Console.Write(i + "\n");
                    Console.Write(Amp + "\n");
                    Console.Write("f= " + (float)i / NumSecondsRunning + "Hz" + "\n\n");
                }

            }
            Console.Write("0Hz : " + Amp0 + "\n");
            Console.Write("2Hz : " + Math.Sqrt(Math.Pow(TF[0][2 * NumSecondsRunning].Real, 2) + Math.Pow(TF[0][2].Imaginary, 2)) + "\n");
            Console.Write("5Hz : " + Math.Sqrt(Math.Pow(TF[0][5 * NumSecondsRunning].Real, 2) + Math.Pow(TF[0][5].Imaginary, 2)) + "\n");
            Console.Write("50Hz : " + Math.Sqrt(Math.Pow(TF[0][50 * NumSecondsRunning].Real, 2) + Math.Pow(TF[0][50].Imaginary, 2)) + "\n");
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------

        public void Filter_TaskInit()
        {
            int freq = 100;
            double samplerate = 1 / Filter_hThread_PERIOD;
            bool type = true;
            Filter = new FilterButterworth(freq, samplerate, type);
            Filter_isRunning = true;
        }



        public void Filter_TaskLoop()
        {
            CommonTaskDelegate LocalTaskInstance;
            while (true)
            {

                //this is the data processing thread; data received from the devices will be written out to a file here
                if (Filter_isRunning) // Start, Stop
                {
                    Filter_TaskFunction();

                    LocalTaskInstance = new CommonTaskDelegate(Filter_DisplayFunction);
                    this.BeginInvoke(LocalTaskInstance);
                }
                Thread.Sleep(Filter_hThread_PERIOD);
            }
        }

        public void Filter_TaskFunction()
        {
            
        }

        public void Filter_DisplayFunction()
        {
            chart1.Series["Fil"].Points.Clear();
            for (int i = 0; i < numChannels; ++i)
            {
                for (int j = 0; j < numScans * NumSecondsRunning; j++)
                {
                    Filter.Update(f[i][j]);
                    chart1.Series["Fil"].Points.AddY(Filter.Value);
                }
            }
        }


        /*
        private void gtecTask()
        {
            const uint NumSecondsRunning = 1;
            DataAcquisitionUnit acquisitionUnit = new DataAcquisitionUnit();

            //create device configurations
            Dictionary<string, DeviceConfiguration> devices = CreateDefaultDeviceConfigurations("UB-2011.11.15");

            //determine how many bytes should be read and processed at once by the processing thread (not the acquisition thread!)
            int numScans = 512;
            int numChannels = 0;

            foreach (DeviceConfiguration deviceConfiguration in devices.Values)
                numChannels += (deviceConfiguration.SelectedChannels.Count + Convert.ToInt32(deviceConfiguration.TriggerLineEnabled));

            int numValuesAtOnce = numScans * numChannels;

            try
            {
                //create file stream
                using (FileStream fileStream = new FileStream("receivedData.bin", FileMode.Create))
                {
                    using (BinaryWriter writer = new BinaryWriter(fileStream))
                    {
                        //start acquisition thread
                        acquisitionUnit.StartAcquisition(devices);

                        //to stop the application after a specified time, get start time
                        DateTime startTime = DateTime.Now;
                        DateTime stopTime = startTime.AddSeconds(NumSecondsRunning);

                        //this is the data processing thread; data received from the devices will be written out to a file here
                        while (DateTime.Now < stopTime)
                        {
                            float[] data = acquisitionUnit.ReadData(numValuesAtOnce);

                            //write data to file
                            for (int i = 0; i < data.Length; i++)
                            {
                                writer.Write(data[i]);
                                // Console.Write(data[i] + "\n");
                                //Console.ReadKey();
                            }

                        }
                    }
                }
            }


            catch (Exception ex)
            {
                Console.WriteLine("\t{0}", ex.Message);
            }
            finally
            {
                //stop data acquisition
                acquisitionUnit.StopAcquisition();

                //                Console.WriteLine("Press any key exit...");
                //                Console.ReadKey(true);
            }

            if (File.Exists("receivedData.bin"))
            {
                using (BinaryReader reader = new BinaryReader(File.Open("receivedData.bin", FileMode.Open)))
                {
                    double[][] f = new double[numChannels][];
                    for (int j = 0; j < numChannels; ++j)
                    {
                        f[j] = new double[numScans * NumSecondsRunning];
                    }
                    int i = 0;
                    while (reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        for (int j = 0; j < numChannels; ++j)
                            f[j][i] = reader.ReadSingle();
                        i++;
                    }
                    reader.Close();

                    /*for (i = 0; i < 100; ++i)
                    {
                        Console.Write(f[0][i] + "\n");

                    }
                    Console.ReadKey();

                    Complex[] signal = new Complex[256];
                    Complex[] test = new Complex[256];
                    for (int i = 0; i < 256; ++i)
                    {
                        test[i] = new Complex(Math.Sin(16 * 3.14159 * i / 256), 0);
                    }
                    signal = fourier.FFT(test);
                    for (int i = 0; i < 256; ++i)
                    {
                        Console.Write(Math.Sqrt(Math.Pow(signal[i].Real, 2) + Math.Pow(signal[i].Imaginary, 2)) + "\n");
                    }

                    Complex[] Cf = new Complex[numScans * NumSecondsRunning];
                    Complex[][] TF = new Complex[numScans * NumSecondsRunning][];
                    for (i = 0; i < numChannels; ++i)
                    {
                        for (int j = 0; j < f[i].Length; ++j)
                        {
                            Cf[j] = new Complex(f[i][j], 0);
                        }
                        TF[i] = fourier.FFT(Cf);
                    }
                    double Amp, Amp0;
                    Amp0 = Math.Sqrt(Math.Pow(TF[0][0].Real, 2) + Math.Pow(TF[0][0].Imaginary, 2));
                    for (i = 0; i < numScans * NumSecondsRunning / 2; ++i)
                    {
                        //Console.Write(TF[1][i].Real + "  " + TF[1][i].Imaginary + "\n");
                        Amp = Math.Sqrt(Math.Pow(TF[0][i].Real, 2) + Math.Pow(TF[0][i].Imaginary, 2));
                        //Console.Write(Amp + "\n");
                        chart1.Series["TF"].Points.AddY(Amp);
                        if (Amp > Amp0)
                        {
                            Console.Write(i + "\n");
                            Console.Write(Amp + "\n");
                            Console.Write("f= " + (float)i / NumSecondsRunning + "Hz" + "\n\n");
                        }

                    }
                    Console.Write("0Hz : " + Amp0 + "\n");
                    Console.Write("2Hz : " + Math.Sqrt(Math.Pow(TF[0][2 * NumSecondsRunning].Real, 2) + Math.Pow(TF[0][2].Imaginary, 2)) + "\n");
                    Console.Write("5Hz : " + Math.Sqrt(Math.Pow(TF[0][5 * NumSecondsRunning].Real, 2) + Math.Pow(TF[0][5].Imaginary, 2)) + "\n");
                    Console.Write("50Hz : " + Math.Sqrt(Math.Pow(TF[0][50 * NumSecondsRunning].Real, 2) + Math.Pow(TF[0][50].Imaginary, 2)) + "\n");
                    //Console.ReadKey();
                    //}
                }
            }
        }*/


        private static long Length(string v)
        {
            throw new NotImplementedException();
        }
        static Dictionary<string, DeviceConfiguration> CreateDefaultDeviceConfigurations(params string[] serialNumbers)
        {
            Dictionary<string, DeviceConfiguration> deviceConfigurations = new Dictionary<string, DeviceConfiguration>();

            for (int i = 0; i < serialNumbers.Length; i++)
            {
                DeviceConfiguration deviceConfiguration = new DeviceConfiguration();
                deviceConfiguration.SelectedChannels = new List<byte>(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
                deviceConfiguration.NumberOfScans = 16;
                deviceConfiguration.SampleRate = 512;
                deviceConfiguration.IsSlave = (i > 0);

                deviceConfiguration.TriggerLineEnabled = false;
                deviceConfiguration.SCEnabled = false;
                deviceConfiguration.Mode = gUSBampWrapper.OperationModes.Normal;
                deviceConfiguration.BandpassFilters = new Dictionary<byte, int>();
                deviceConfiguration.NotchFilters = new Dictionary<byte, int>();
                deviceConfiguration.BipolarSettings = new gUSBampWrapper.Bipolar();
                deviceConfiguration.CommonGround = new gUSBampWrapper.Gnd();
                deviceConfiguration.CommonReference = new gUSBampWrapper.Ref();
                deviceConfiguration.Drl = new gUSBampWrapper.Channel();

                deviceConfiguration.Dac = new gUSBampWrapper.DAC();
                deviceConfiguration.Dac.WaveShape = gUSBampWrapper.WaveShapes.Sine;
                deviceConfiguration.Dac.Amplitude = 2000;
                deviceConfiguration.Dac.Frequency = 10;
                deviceConfiguration.Dac.Offset = 2047;

                deviceConfigurations.Add(serialNumbers[i], deviceConfiguration);
            }

            return deviceConfigurations;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CommonTaskDelegate DAQ_ThreadInstance = new CommonTaskDelegate(DAQ_TaskLoop);
            DAQ_hThread = new Thread(new ThreadStart(DAQ_ThreadInstance));
            DAQ_TaskInit();
            DAQ_hThread.Start();

            CommonTaskDelegate Filter_ThreadInstance = new CommonTaskDelegate(Filter_TaskLoop);
            Filter_hThread = new Thread(new ThreadStart(Filter_ThreadInstance));
            Filter_TaskInit();
            Filter_hThread.Start();
        }
    }
}
