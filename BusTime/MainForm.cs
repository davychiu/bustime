/*
 * BusTime
 * 
 * PocketPC application that displays bus schedules for the next
 * 4 buses of 3 bus stops and the weather.
 * 
 * Author: Davy Chiu
 * Last Modified: 9/16/2009
 *
 * Comments and suggestions can be sent to 
 * 
 * davychiu@gmail.com
 * 
 */

using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;


namespace BusTime
{
    public partial class MainForm : Form
    {
        public const String CITYCODE = "54366";
        public const String BUSNUM1 = "025";
        public const String BUSNUM2 = "027";
        public const String BUSNUM3 = "027";
        public const String BUSSTOP1 = "51555"; //25west
        //public const String BUSSTOP2 = "51560"; //nanaimo 25east
        //public const String BUSSTOP3 = "51529"; //nanaimo 25west
        public const String BUSSTOP2 = "51713";
        public const String BUSSTOP3 = "51679";
        public const String BUSSTOP1DIR = "West";
        public const String BUSSTOP2DIR = "South";
        public const String BUSSTOP3DIR = "North";

        public Boolean busThreadDone = false;
        public Boolean curThreadDone = false;
        public Boolean foreThreadDone = false;

        private int minute = 0;

        public MainForm()
        {
            //Microsoft.WindowsCE.Forms.SystemSettings.ScreenOrientation = Microsoft.WindowsCE.Forms.ScreenOrientation.Angle270;           
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Thread setBusTimesThread = new Thread(new ThreadStart(setBusTimes));
            setBusTimesThread.Start();

            Thread setCurentWeatherThread = new Thread(new ThreadStart(setCurrentWeather));
            setCurentWeatherThread.Start();

            Thread setWeatherForecastThread = new Thread(new ThreadStart(setWeatherForecast));
            setWeatherForecastThread.Start();

            Thread progressThread = new Thread(new ThreadStart(progress));
            progressThread.Start();
        }

        private void setWeatherForecast()
        {
            foreThreadDone = false;

            try
            {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load("http://api.wxbug.net/getForecastRSS.aspx?ACode=A5678056775&citycode=" + CITYCODE + "&unittype=1&outputtype=1");

                progressBar1.Invoke(new progressUpdateCallback(progressUpdate), new Object[] { progressBar1, 10 });

                if (xdoc.InnerText.Length > 0)
                {
                    XmlNodeList titles = xdoc.GetElementsByTagName("aws:title");
                    XmlNodeList icons = xdoc.GetElementsByTagName("aws:image");
                    XmlNodeList highs = xdoc.GetElementsByTagName("aws:high");
                    XmlNodeList lows = xdoc.GetElementsByTagName("aws:low");

                    Label[] dayArray = { foreDay0, foreDay1, foreDay2, foreDay3, foreDay4 };
                    Label[] highArray = { foreHigh0, foreHigh1, foreHigh2, foreHigh3, foreHigh4 };
                    Label[] lowArray = { foreLow0, foreLow1, foreLow2, foreLow3, foreLow4 };
                    PictureBox[] iconArray = { foreIcon0, foreIcon1, foreIcon2, foreIcon3, foreIcon4 };

                    for (int i = 0; i < 5; i++)
                    {
                        progressBar1.Invoke(new progressUpdateCallback(progressUpdate), new Object[] { progressBar1, 10 });

                        dayArray[i].Invoke(new labelUpdateCallback(labelUpdate), new Object[] { dayArray[i], titles[i].InnerText });
                        highArray[i].Invoke(new labelUpdateCallback(labelUpdate), new Object[] { highArray[i], highs[i].InnerText + highs[i].Attributes[0].Value });
                        lowArray[i].Invoke(new labelUpdateCallback(labelUpdate), new Object[] { lowArray[i], lows[i].InnerText + lows[i].Attributes[0].Value });


                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(icons[i].InnerText);
                        req.ReadWriteTimeout = 10000; //10 seconds
                        req.Timeout = 15000; //15 seconds
                        req.KeepAlive = false;

                        HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                        Stream pstream = resp.GetResponseStream();

                        iconArray[i].Invoke(new pictureBoxUpdateCallback(pictureBoxUpdate), new Object[] { iconArray[i], new Bitmap(pstream) });

                        pstream.Close();
                        resp.Close();
                    }
                }

            }
            catch (Exception e)
            {
                //ignore
            }

            foreThreadDone = true;
        }

        private void setCurrentWeather()
        {
            curThreadDone = false;

            try
            {
                Object[] data = new Object[3];

                XmlDocument xdoc = new XmlDocument();
                xdoc.Load("http://api.wxbug.net/getLiveWeatherRSS.aspx?ACode=A5678056775&cityCode=" + CITYCODE + "&unittype=1&outputtype=1");

                progressBar1.Invoke(new progressUpdateCallback(progressUpdate), new Object[] { progressBar1, 10 });

                if (xdoc.InnerText.Length > 0)
                {
                    XmlNodeList temp = xdoc.GetElementsByTagName("aws:temp");
                    XmlNodeList current = xdoc.GetElementsByTagName("aws:current-condition");

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(current[0].Attributes[0].Value);
                    req.ReadWriteTimeout = 10000; //10 seconds
                    req.Timeout = 15000; //15 seconds
                    req.KeepAlive = false;

                    HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                    Stream pstream = resp.GetResponseStream();

                    data[0] = temp[0].InnerText + "°C";
                    data[1] = current[0].InnerText;
                    data[2] = new Bitmap(pstream);

                    pstream.Close();
                    resp.Close();

                    progressBar1.Invoke(new progressUpdateCallback(progressUpdate), new Object[] { progressBar1, 10 });

                    label8.Invoke(new labelUpdateCallback(labelUpdate), new Object[] { label8, data[0] });
                    label7.Invoke(new labelUpdateCallback(labelUpdate), new Object[] { label7, data[1] });
                    pictureBox1.Invoke(new pictureBoxUpdateCallback(pictureBoxUpdate), new Object[] { pictureBox1, data[2] });
                }
            }
            catch (Exception e)
            {
                //ignore
            }

            curThreadDone = true;
        }

        private void setBusTimes()
        {
            busThreadDone = false;

            try
            {
                //String busStopUrl1 = "http://m.translink.ca/api/stops/?f=json&s=" + BUSSTOP1 + "&d=" + BUSSTOP1DIR;
                //String busStopUrl2 = "http://m.translink.ca/api/stops/?f=json&s=" + BUSSTOP2 + "&d=" + BUSSTOP2DIR;
                //String busStopUrl3 = "http://m.translink.ca/api/stops/?f=json&s=" + BUSSTOP3 + "&d=" + BUSSTOP3DIR;
                String busStopUrl1 = "http://m.translink.ca/stop/" + BUSSTOP1;
                String busStopUrl2 = "http://m.translink.ca/stop/" + BUSSTOP2;
                String busStopUrl3 = "http://m.translink.ca/stop/" + BUSSTOP3;
                Hashtable busStops1 = parser(BUSNUM1, getHttp(busStopUrl1));
                Hashtable busStops2 = parser(BUSNUM2, getHttp(busStopUrl2));
                Hashtable busStops3 = parser(BUSNUM3, getHttp(busStopUrl3));

                progressBar1.Invoke(new progressUpdateCallback(progressUpdate), new Object[] { progressBar1, 10 });

                if ((busStops1.Count > 0) && (busStops2.Count > 0) && (busStops3.Count > 0))
                {
                    fillValues(busStops1, BUSNUM1, BUSNUM1 + BUSSTOP1DIR, label1, label2);
                    fillValues(busStops2, BUSNUM2, BUSNUM2 + BUSSTOP2DIR, label3, label4);
                    fillValues(busStops3, BUSNUM3, BUSNUM3 + BUSSTOP3DIR, label5, label6);
                }
                progressBar1.Invoke(new progressUpdateCallback(progressUpdate), new Object[] { progressBar1, 10 });
            }
            catch (WebException e)
            {
                //ignore
            }
            catch (ArgumentException ae)
            {
                //ignore
            }
            busThreadDone = true;
        }
        private void fillValues(Hashtable busStops, String stopId, String stopDir, Label labela, Label labelb)
        {
            String name = "";
            String time = "";

            busStop busStopTmp = (busStop)busStops[stopId];
            name = "(" + stopDir.Substring(1, 3) + ") " + busStopTmp.stopName;

            foreach (String bustime in busStopTmp.times)
            {
                time += bustime + "  ";
            }

            labela.Invoke(new labelUpdateCallback(labelUpdate), new Object[] { labela, name });
            labelb.Invoke(new labelUpdateCallback(labelUpdate), new Object[] { labelb, time });

        }

        private void labelUpdate(Label labela, String lstring)
        {
            labela.Text = lstring;
        }
        private delegate void labelUpdateCallback(Label labela, String lstring);

        private void pictureBoxUpdate(PictureBox pbox, Bitmap picture)
        {
            pbox.Image = picture;
        }
        private delegate void pictureBoxUpdateCallback(PictureBox pbox, Bitmap picture);

        private String getHttp(String url)
        {
            String results = "";

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.ReadWriteTimeout = 10000; //10 seconds
                req.Timeout = 15000; //15 seconds
                req.KeepAlive = false;

                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();

                StreamReader streamReader = new StreamReader(resp.GetResponseStream());
                results = streamReader.ReadToEnd();

                streamReader.Close();
                resp.Close();
            }
            catch (Exception e)
            {
                //ignore
            }

            return results;
        }

        private Hashtable parser(String stopID, String html)
        {
            Hashtable busStops = new Hashtable();
            char[] delimiter = { '\n' };

            if (html.Length > 0)
            {
                String[] htmlArray = html.Split(delimiter);

                if (htmlArray.Length > 10)
                {
                    busStop busStopTemp = new busStop();

                    busStopTemp.id = stopID;
                    String stopNameTemp = htmlArray[10].Remove(0, 43);
                    busStopTemp.stopName = stopNameTemp.Substring(0, stopNameTemp.IndexOf("<"));
                    busStopTemp.times = htmlArray[11].Trim().Split(new char[] { ' ' });
                                                       
                    busStops.Add(stopID, busStopTemp);
                }               
            }
            return busStops;
        }        

        private class busStop
        {
            public String id = "";
            public String stopName = "";
            public String[] times;
        }

        private void progress()
        {
            try
            {
                progressBar1.Invoke(new progressShowHideCallback(progressShowHide), new Object[] { progressBar1, true });
                while (!busThreadDone && !curThreadDone && !foreThreadDone)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);
                progressBar1.Invoke(new progressSetValueCallback(progressSetValue), new Object[] { progressBar1, 0 });
                progressBar1.Invoke(new progressShowHideCallback(progressShowHide), new Object[] { progressBar1, false });

            }
            catch (Exception e)
            {
                //ignore
            }
        }

        private void progressUpdate(ProgressBar progbar, int value)
        {
            if (progbar.Value < progbar.Maximum)
            {
                progbar.Value += value;
            }
        }
        private delegate void progressUpdateCallback(ProgressBar progbar, int value);

        private void progressSetValue(ProgressBar progbar, int value)
        {
            progbar.Value = value;
        }
        private delegate void progressSetValueCallback(ProgressBar progbar, int value);

        private void progressShowHide(ProgressBar progbar, Boolean showhide)
        {
            progbar.Visible = showhide;
        }
        private delegate void progressShowHideCallback(ProgressBar progbar, Boolean showhide);

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                TimeLabel.Text = DateTime.Now.ToString("hh:mm:ss tt");

                if (!DateTime.Now.Minute.Equals(minute)) //every minute
                {
                    //setBusTimes();

                    Thread setBusTimesThread = new Thread(new ThreadStart(setBusTimes));
                    setBusTimesThread.Start();

                    if (DateTime.Now.Minute % 10 == 0) //every 10 minutes
                    {
                        //setCurrentWeather();

                        Thread setCurentWeatherThread = new Thread(new ThreadStart(setCurrentWeather));
                        setCurentWeatherThread.Start();

                        if (DateTime.Now.Minute % 30 == 0) //every 30 minutes
                        {
                            //setWeatherForecast();

                            Thread setWeatherForecastThread = new Thread(new ThreadStart(setWeatherForecast));
                            setWeatherForecastThread.Start();
                        }
                    }
                    Thread progressThread = new Thread(new ThreadStart(progress));
                    progressThread.Start();

                }
            }
            catch (Exception pe)
            {
                return;
            }
            minute = DateTime.Now.Minute;
        }

        private void menuItem2_Click(object sender, EventArgs e)
        {
            Thread setBusTimesThread = new Thread(new ThreadStart(setBusTimes));
            setBusTimesThread.Start();

            Thread setCurentWeatherThread = new Thread(new ThreadStart(setCurrentWeather));
            setCurentWeatherThread.Start();

            Thread setWeatherForecastThread = new Thread(new ThreadStart(setWeatherForecast));
            setWeatherForecastThread.Start();

            Thread progressThread = new Thread(new ThreadStart(progress));
            progressThread.Start();
        }

        private void menuItem1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}


