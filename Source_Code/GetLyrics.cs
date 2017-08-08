using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Web;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();
        private int AutoFlag = 1;

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);
            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "GetLyrics";
            about.Description = "Get Lyrics From Internet";
            about.Author = "Dixeran";
            about.TargetApplication = "";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            about.Type = PluginType.LyricsRetrieval;
            about.VersionMajor = 1;  // your plugin version
            about.VersionMinor = 0;
            about.Revision = 1;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = ReceiveNotificationFlags.DownloadEvents;
            about.ConfigurationPanelHeight = 40;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
            dataPath += "GetLyrics_Config.conf";
            if(!File.Exists(dataPath))
            {
                string confString = String.Format("Automatically_chose={0}", AutoFlag.ToString());
                File.WriteAllText(dataPath, confString);
            }
            else
            {
                FileStream confFile = new FileStream(dataPath, FileMode.Open);
                StreamReader confRead = new StreamReader(confFile);
                string conf_auto = confRead.ReadLine();
                AutoFlag = conf_auto[20] - '0';
            }
            Form test = new Form();
            test.Text = AutoFlag.ToString();
            test.ShowDialog();
            return about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            // save any persistent settings in a sub-folder of this path
            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
            dataPath += "GetLyrics_Config.conf";
            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
            if (panelHandle != IntPtr.Zero)
            {
                Panel configPanel = (Panel)Panel.FromHandle(panelHandle);
                Label prompt = new Label();
                prompt.AutoSize = true;
                prompt.Location = new Point(20, 20);
                prompt.Text = "Automatically select the lyrics (自动选择歌词）";

                CheckBox autoFlag_checked = new CheckBox();
                if (AutoFlag == 0) autoFlag_checked.Checked = false;
                else autoFlag_checked.Checked = true;
                autoFlag_checked.Bounds = new Rectangle(0, 20, autoFlag_checked.Width, autoFlag_checked.Height);

                autoFlag_checked.CheckedChanged += (_object, _event) =>
                {
                    if (autoFlag_checked.Checked == true) AutoFlag = 1;
                    else AutoFlag = 0;
                    string confString = String.Format("Automatically_chose={0}", AutoFlag.ToString());
                    File.WriteAllText(dataPath, confString);
                };

                configPanel.Controls.AddRange(new Control[] { prompt, autoFlag_checked });
            }
            return false;
        }
       
        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            // save any persistent settings in a sub-folder of this path
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                    // perform startup initialisation
                    switch (mbApiInterface.Player_GetPlayState())
                    {
                        case PlayState.Playing:
                        case PlayState.Paused:
                            // ...
                            break;
                    }
                    break;
                case NotificationType.TrackChanged:
                    string artist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
                    // ...
                    break;
            }
        }

        // return an array of lyric or artwork provider names this plugin supports
        // the providers will be iterated through one by one and passed to the RetrieveLyrics/ RetrieveArtwork function in order set by the user in the MusicBee Tags(2) preferences screen until a match is found
        public string[] GetProviders()
        {
            return new string[]
            {
                "QQ Music"
            };
        }

        // return lyrics for the requested artist/title from the requested provider
        // only required if PluginType = LyricsRetrieval
        // return null if no lyrics are found
        public string RetrieveLyrics(string sourceFileUrl, string artist, string trackTitle, string album, bool synchronisedPreferred, string provider)
        {
            string SearchUrl = String.Format("http://c.y.qq.com/soso/fcgi-bin/client_search_cp?ct=24&qqmusic_ver=1298&new_json=1&remoteplace=txt.yqq.song&t=0&aggr=1&cr=1&catZhida=1&lossless=0&flag_qc=0&p=1&n=20&w={0} {1}&g_tk=5381&hostUin=0&format=jsonp&inCharset=utf8&outCharset=utf-8&notice=0&platform=yqq&needNewCode=0", trackTitle, artist);
            var request = (HttpWebRequest)WebRequest.Create(SearchUrl);
            var response = (HttpWebResponse)request.GetResponse();
            var SearchString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            SearchString = SearchString.Replace("callback(", "");
            SearchString = SearchString.Replace("})", "}");//删除回调中的多余字符

            JObject SearchResult = JObject.Parse(SearchString);//解析搜索结果
            JArray SongList = (JArray)SearchResult["data"]["song"]["list"];//搜索结果曲目列表

            int ID = SongList[0]["id"].ToObject<int>();//从曲目列表得到歌曲唯一id（默认首选）

            if(AutoFlag == 0)
            { 
                Form frm = new Form();
                frm.Width = 640;
                frm.Height = 400;
                frm.Text = "GetLyrics";

                ListView list = new ListView();
                list.Bounds = new Rectangle(new Point(10, 10), new Size(600, 300));
                list.View = View.Details;
                list.Columns.Add("name", 200, HorizontalAlignment.Left);
                list.Columns.Add("album", 200, HorizontalAlignment.Left);
                list.Columns.Add("singer", 200, HorizontalAlignment.Left);

                for (int t = 0; t < SongList.Count; t++)
                {
                    ListViewItem _song = list.Items.Add(SongList[t]["name"].ToObject<string>());
                    _song.SubItems.Add(SongList[t]["album"]["name"].ToObject<string>());
                    string SingerName = "";
                    JArray singers = (JArray)SongList[t]["singer"];
                    for (int i = 0; i < singers.Count; i++)
                    {
                        if (i == 0) SingerName += singers[i]["name"].ToObject<string>();
                        else SingerName = SingerName + "&" + singers[i]["name"].ToObject<string>();
                    }
                    _song.SubItems.Add(SingerName);
                }

                Button submit = new Button();
                submit.Text = "Select";
                submit.Bounds = new Rectangle(new Point(10, 320), new Size(600, 30));
                submit.Click += (_object, _event) =>
                {
                    ListView.SelectedIndexCollection selected = new ListView.SelectedIndexCollection(list);
                    if (selected.Count > 0)
                    {
                        ID = SongList[selected[0]]["id"].ToObject<int>();
                    }
                    frm.Close();
                };

                frm.Controls.Add(list);
                frm.Controls.Add(submit);
                frm.ShowDialog();
            }

            var LyricsUrl = String.Format("http://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric.fcg?nobase64=1&musicid={0}&callback=jsonp1&g_tk=5381&jsonpCallback=jsonp1&loginUin=0&hostUin=0&format=jsonp&inCharset=utf8&outCharset=utf-8&notice=0&platform=yqq&needNewCode=0", ID.ToString());
            var Lyrequest = (HttpWebRequest)WebRequest.Create(LyricsUrl);
            Lyrequest.Referer = "https://y.qq.com/";//QQ音乐查询歌词必带
            var Lyresponse = (HttpWebResponse)Lyrequest.GetResponse();
            var LyricsRawString = new StreamReader(Lyresponse.GetResponseStream()).ReadToEnd();
            LyricsRawString = LyricsRawString.Replace("jsonp1(", "");
            LyricsRawString = LyricsRawString.Replace("})", "}");//删除回调中的多余字符

            JObject LyricsResult = JObject.Parse(LyricsRawString);//解析得到的JSON
            int Lycode = LyricsResult["retcode"].ToObject<int>();//判断是否存在歌词
            if(Lycode != 0)
            {
                return null;
            }
            else
            {
                string LyricsString = LyricsResult["lyric"].ToObject<string>();//解析JSON中的歌词
                LyricsString = HttpUtility.HtmlDecode(LyricsString);
                return LyricsString;
            }
        }

        // return Base64 string representation of the artwork binary data from the requested provider
        // only required if PluginType = ArtworkRetrieval
        // return null if no artwork is found
        public string RetrieveArtwork(string sourceFileUrl, string albumArtist, string album, string provider)
        {
            //Return Convert.ToBase64String(artworkBinaryData)
            return null;
        }
   }
}