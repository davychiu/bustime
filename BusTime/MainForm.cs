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
        private static string sessCookie = "";

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
                System.Diagnostics.Debug.WriteLine("setWeatherForecast Exception: " + e.Message);
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
                    req.Proxy = GlobalProxySelection.GetEmptyWebProxy();

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
                System.Diagnostics.Debug.WriteLine("setCurrentWeather Exception: " + e.Message);
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
                //String busStopUrl1 = "http://m.translink.ca/stop/" + BUSSTOP1;
                //String busStopUrl2 = "http://m.translink.ca/stop/" + BUSSTOP2;
                //String busStopUrl3 = "http://m.translink.ca/stop/" + BUSSTOP3;
                //String busStopUrl1 = "http://tripplanning.translink.ca/hiwire?.a=iNextBusFind&.s={%24SID}&ShowTimes=1&NumStopTimes=25&LineDirId=&GetSchedules=1&Geocode=0&FormState=0&FromTime=" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + "&PublicNum=" + BUSSTOP1;
                //String busStopUrl2 = "http://tripplanning.translink.ca/hiwire?.a=iNextBusFind&.s={%24SID}&ShowTimes=1&NumStopTimes=25&LineDirId=&GetSchedules=1&Geocode=0&FormState=0&FromTime=" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + "&PublicNum=" + BUSSTOP2;
                //String busStopUrl3 = "http://tripplanning.translink.ca/hiwire?.a=iNextBusFind&.s={%24SID}&ShowTimes=1&NumStopTimes=27&LineDirId=&GetSchedules=1&Geocode=0&FormState=0&FromTime=" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + "&PublicNum=" + BUSSTOP3;               
                //String busStopHash1 = "http://nb.translink.ca/Text/Stop/" + BUSSTOP1;
                //String busStopHash2 = "http://nb.translink.ca/Text/Stop/" + BUSSTOP2;
                //String busStopHash3 = "http://nb.translink.ca/Text/Stop/" + BUSSTOP3;
                //String busStopUrl1 = "http://nb.translink.ca/rideapi.ashx?cp=gsas%2F" + getHttp(busStopHash1).Replace("+", "%2B").Replace("/", "%2F").Replace("=", "%3D");
                //String busStopHtml1 = getHttp("http://nb.translink.ca/Text/Stop/" + BUSSTOP1);
                //String busStopUrl2 = "http://nb.translink.ca/rideapi.ashx?cp=gsas%2F" + getHttp(busStopHash2).Replace("+", "%2B").Replace("/", "%2F").Replace("=", "%3D");
                //String busStopHtml2 = getHttp("http://nb.translink.ca/Text/Stop/" + BUSSTOP2);
                //String busStopUrl3 = "http://nb.translink.ca/rideapi.ashx?cp=gsas%2F" + getHttp(busStopHash3).Replace("+", "%2B").Replace("/", "%2F").Replace("=", "%3D");
                //String busStopHtml3 = getHttp("http://nb.translink.ca/Text/Stop/" + BUSSTOP3);
                //Hashtable busStops1 = parser(BUSNUM1, busStopHtml1);
                //Hashtable busStops2 = parser(BUSNUM2, busStopHtml2);
                //Hashtable busStops3 = parser(BUSNUM3, busStopHtml3);
                String busStop1 = getHttp("http://192.168.0.5/bustime_helper/bustime_helper.php?stop=" + BUSSTOP1);
                String busStop2 = getHttp("http://192.168.0.5/bustime_helper/bustime_helper.php?stop=" + BUSSTOP2);
                String busStop3 = getHttp("http://192.168.0.5/bustime_helper/bustime_helper.php?stop=" + BUSSTOP3);
                //System.Diagnostics.Debug.WriteLine("URL: " + busStopUrl1 + " " + busStopUrl2 + " " + busStopUrl3);
                System.Diagnostics.Debug.WriteLine("HTML: " + busStop1 + " " + busStop2 + " " + busStop3);

                progressBar1.Invoke(new progressUpdateCallback(progressUpdate), new Object[] { progressBar1, 10 });

                if ((busStop1.Length > 0) && (busStop2.Length > 0) && (busStop3.Length > 0))
                {
                    //fillValues(busStops1, BUSNUM1, BUSNUM1 + BUSSTOP1DIR, label1, label2);
                    //fillValues(busStops2, BUSNUM2, BUSNUM2 + BUSSTOP2DIR, label3, label4);
                    //fillValues(busStops3, BUSNUM3, BUSNUM3 + BUSSTOP3DIR, label5, label6);
                    fillValues(busStop1, BUSNUM1, BUSNUM1 + BUSSTOP1DIR, label1, label2);
                    fillValues(busStop2, BUSNUM2, BUSNUM2 + BUSSTOP2DIR, label3, label4);
                    fillValues(busStop3, BUSNUM3, BUSNUM3 + BUSSTOP3DIR, label5, label6);
                }
                progressBar1.Invoke(new progressUpdateCallback(progressUpdate), new Object[] { progressBar1, 10 });
            }
            catch (WebException e)
            {
                System.Diagnostics.Debug.WriteLine("setBusTimes WebException: " + e.Message);
            }
            catch (ArgumentException ae)
            {
                System.Diagnostics.Debug.WriteLine("setBusTimes ArgumentException: " + ae.Message);
            }
            busThreadDone = true;
        }
        private void fillValues(String time, String stopId, String stopDir, Label labela, Label labelb)
        {
            //String name = "";
            //String time = "";

            //busStop busStopTmp = (busStop)busStops[stopId];
            //name = "(" + stopDir.Substring(1, 3) + ") " + busStopTmp.stopName;

            //foreach (String bustime in busStopTmp.times)
            //{
            //    time += bustime + "  ";
            //}

            //labela.Invoke(new labelUpdateCallback(labelUpdate), new Object[] { labela, name });
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

            if (url.Length > 0)
            {
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "Mozilla/5.0 (Windows NT 5.1; rv:6.0.1) Gecko/20100101 Firefox/6.0.1";
                    req.ReadWriteTimeout = 5000; //2 seconds
                    req.Timeout = 5000; //2 seconds
                    req.KeepAlive = false;
                    req.Proxy = GlobalProxySelection.GetEmptyWebProxy();
                    req.Method = "GET";
                    req.AllowAutoRedirect = true;
                    if (sessCookie.Length > 0)
                    {
                        req.Headers.Add("Cookie", sessCookie);
                    }

                    HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                    if (resp.Headers["Set-Cookie"] != null)
                    {
                        sessCookie = resp.Headers["Set-Cookie"];
                    }

                    StreamReader streamReader = new StreamReader(resp.GetResponseStream());
                    results = streamReader.ReadToEnd();

                    streamReader.Close();
                    resp.Close();
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("getHttp Exception: " + e.Message);
                }
            }

            return results;
        }

        private Hashtable parser(String stopID, String html)
        {
            Hashtable busStops = new Hashtable();
            char[] delimiter = { '\n' };
            System.Diagnostics.Debug.WriteLine("Parsing " + stopID + ": " + html);
            //MatchCollection matches = Regex.Matches(html, ";;\\s\">(.*?)</span>");

            if (html.Length > 0)
            { 
                //MatchCollection matches = Regex.Matches(html, "</em>\\sat\\s(.*?)[am|pm]<br/>");
                MatchCollection matches = Regex.Matches(html, "Time\":\"\\s(.*?)[AM|PM]\",\"");

                if (matches.Count > 0)
                {
                    busStop busStoptemp = new busStop();
                    String busTimestemp = "";

                    busStoptemp.id = stopID;                   
                    foreach (Match match in matches)
                    {                        
                        busTimestemp += match.Groups[1].Value.Trim() + " ";
                        System.Diagnostics.Debug.WriteLine("time is" + match.Groups[1].Value.Trim());
                    }
                    System.Diagnostics.Debug.WriteLine("time is" + busTimestemp);
                    busStoptemp.times = busTimestemp.Trim().Split(new char[] {' '});
                    busStops.Add(stopID, busStoptemp);
                }

                //String[] htmlArray = html.Split(delimiter);

                //if (htmlArray.Length > 10)
                //{
                //    busStop busStopTemp = new busStop();

                //    busStopTemp.id = stopID;
                //    String stopNameTemp = htmlArray[10].Remove(0, 43);
                //    busStopTemp.stopName = stopNameTemp.Substring(0, stopNameTemp.IndexOf("<"));
                //    busStopTemp.times = htmlArray[11].Trim().Split(new char[] { ' ' });
                                                       
                //    busStops.Add(stopID, busStopTemp);
                //}               
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
                System.Diagnostics.Debug.WriteLine("progress Exception: " + e.Message);
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

                if ((!DateTime.Now.Minute.Equals(minute))&&(busThreadDone)&&(curThreadDone)&&(foreThreadDone)) //every minute
                {
                    //setBusTimes();
                    System.Diagnostics.Debug.WriteLine("timer: " + sessCookie);

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
                System.Diagnostics.Debug.WriteLine("timer Exception: " + pe.Message);
                return;
            }
            minute = DateTime.Now.Minute;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
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

        private void pictureBox1_DoubleClick(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}


