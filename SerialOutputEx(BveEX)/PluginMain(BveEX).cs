﻿using System;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using BveTypes.ClassWrappers;
using BveEx.PluginHost.Plugins;
using BveEx.PluginHost.Plugins.Extensions;
using BveEx.Extensions.ContextMenuHacker;
using BveEx.PluginHost;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;

namespace SerialOutputEx
{
    [Plugin(PluginType.Extension)]
    [HideExtensionMain]
    internal class PluginMain : AssemblyPluginBase, IExtension
    {
        //シリアルポート
        private SerialPort serialPort;
        private string portName = "";

        //デバッグコンソール
        private DebugConsole dc = new DebugConsole();
        private bool Debug = false;

        //出力設定
        private OutputInfo outputInfo = new OutputInfo();
        private string str_send_latch = "";
        private DateTime dateTime;
        private bool flgScenarioOpened = false;
        private bool flgFirstSend = false;
        private bool flgConsoleOpened = false;

        //設定ファイル
        private string editorPath = "";
        private string editorPathV1_1 = "";
        private string xmlpath;
        private string dir;

        //コンテクストメニュー
        private ToolStripMenuItem tsiOutput;
        private ToolStripMenuItem tsiConsole;
        private ToolStripMenuItem tsiPorts;
        private ToolStripMenuItem tsiOpenEditor;
        private ToolStripMenuItem tsiSettings;
        //private ToolStripMenuItem tsiSetSettingFile;
        private ToolStripSeparator tsiSeperator;

        public PluginMain(PluginBuilder builder) : base(builder)
        {

            Extensions.AllExtensionsLoaded += Extensions_AllExtensionsLoaded;

            string stTarget = Assembly.GetExecutingAssembly().CodeBase;
            string dllPath = "";
            //ローカルファイルのパスを表すURI
            string uriPath = stTarget;
            //Uriオブジェクトを作成する
            Uri u = new Uri(uriPath);
            //変換するURIがファイルを表していることを確認する
            if (u.IsFile)
            {
                //Windows形式のパス表現に変換する
                dllPath = u.LocalPath + Uri.UnescapeDataString(u.Fragment);
            }
            else
            {
                MessageBox.Show("ファイルURIではありません。");
            }

            dir = Path.GetDirectoryName(dllPath);
            if (dir.Contains("2.0"))
            {
                //MessageBox.Show("BveEX 2.0 Mode");
            }
            else if (dir.Contains("Legacy"))
            {
                //MessageBox.Show("Legacy Mode");
                dir = Path.GetFullPath(dir + @"\..\..\..\BveEx\2.0\Extensions");
            }
            else
            {
                //MessageBox.Show("AtsEX Mode");
            }

            string fileName = "シリアル出力エディタ";
            string path = dir + @"\" + fileName + ".exe";
            string pathV1_1 = dir + @"\" + fileName + "_V1.1.exe";

            editorPath = path;
            editorPathV1_1 = pathV1_1;

        }

       

        private void Extensions_AllExtensionsLoaded(object sender, EventArgs e)
        {
            string openedPortName = Open(portName, false);
            tsiOutput = Extensions.GetExtension<IContextMenuHacker>().AddCheckableMenuItem("SerialOutputEx 連動", SerialOutputEx_Change, ContextMenuItemType.CoreAndExtensions);
            tsiConsole = Extensions.GetExtension<IContextMenuHacker>().AddCheckableMenuItem("デバッグコンソール 表示", DebugConsoleDisp_Change, ContextMenuItemType.CoreAndExtensions);
            tsiOpenEditor = Extensions.GetExtension<IContextMenuHacker>().AddClickableMenuItem("SerialOutputEx 設定", SerialOutputExEdit_Open, ContextMenuItemType.CoreAndExtensions);
            tsiSettings = Extensions.GetExtension<IContextMenuHacker>().AddClickableMenuItem("SerialOutputEx 設定", null, ContextMenuItemType.CoreAndExtensions);
            //tsiSetSettingFile = Extensions.GetExtension<IContextMenuHacker>().AddClickableMenuItem("設定ファイルの選択", SetSettingFile_Click, ContextMenuItemType.CoreAndExtensions);
            tsiSeperator = Extensions.GetExtension<IContextMenuHacker>().AddSeparator(0);

            if (Debug)
            {
                tsiConsole.Checked = true;
            }
            if (serialPort.IsOpen)
            {
                tsiOutput.Checked = true;
            }

            //"シナリオを開く"イベント
            BveHacker.ScenarioOpened += BveHacker_ScenarioOpened;

            BveHacker.ScenarioCreated += BveHacker_ScenarioCreated;

            //コンテクストメニューを開くイベント
            BveHacker.MainForm.ContextMenu.Opened += ContextMenu_Opened;

            //コンテクストメニューのアイテムクリックイベント
            BveHacker.MainForm.ContextMenu.ItemClicked += ContextMenu_ItemClicked;

            //0.3.31218.1 追記ここまで

            try
            {
                if (File.Exists(editorPathV1_1))
                {
                    editorPath = editorPathV1_1;
                }
                else
                {
                    if (!File.Exists(editorPath))
                    {
                        tsiOpenEditor.Enabled = false;
                        tsiOpenEditor.Text = "SerialOutputEx 設定 (シリアル出力エディタが見つかりません)";
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("例外:" + ex.Message);
            }
        }

        private void SetSettingFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            MessageBox.Show("鋭意実装中です");
            ofd.InitialDirectory = @"C:\";
            ofd.Filter = "XMLファイル(*.xml)|*.xml|すべてのファイル(*.*)|*.*";
            ofd.FilterIndex = 1;
            ofd.Title = "開くファイルを選択してください";
            ofd.RestoreDirectory = true;

            //ダイアログを表示する
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                //OKボタンがクリックされたとき、選択されたファイル名を表示する
                //xmlpath = ofd.FileName;
            }
        }

        private void BveHacker_ScenarioCreated(ScenarioCreatedEventArgs e)
        {

        }

        private void BveHacker_ScenarioOpened(ScenarioOpenedEventArgs e)
        {
            flgScenarioOpened = true;
            flgFirstSend = true;
        }

        //シリアル出力エディタの起動
        private void SerialOutputExEdit_Open(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process p = System.Diagnostics.Process.Start(editorPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("例外:" + ex.Message);
                Dispose();
            }
        }

        //ポート選択メニュー操作
        private void TsiPorts_Click(object sender, EventArgs e)
        {
            // sender にはクリックされたメニューの ToolStripMenuItem が入っている
            // ポートを開きなおす処理
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            if (mi.Text != serialPort.PortName)
            {
                try
                {
                    if (serialPort.IsOpen)
                    {
                        serialPort.Close();
                    }
                    Regex regexPortName = new Regex(@"(COM\d+)");
                    portName = regexPortName.Match(mi.Text).Groups[1].ToString();

                    int.TryParse(portName.Substring(3), out int iPortNum);
                    outputInfo.PortNum = iPortNum;

                    //XmlSerializerオブジェクトを作成
                    //オブジェクトの型を指定する
                    XmlSerializer serializer = new XmlSerializer(typeof(OutputInfo));
                    //書き込むファイルを開く（UTF-8 BOM無し）
                    StreamWriter sw = new StreamWriter(xmlpath, false, new UTF8Encoding(false));
                    //シリアル化し、XMLファイルに保存する
                    serializer.Serialize(sw, outputInfo);
                    sw.Close();

                    Open(portName,true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        //シリアル出力(ON/OFF)メニュー変更
        private void SerialOutputEx_Change(object sender, EventArgs e)
        {
            if (tsiOutput.Checked)
            {
                if (!serialPort.IsOpen)
                {
                    Open(portName,false);
                }
            }
            else
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
        }

        //デバッグコンソール表示(ON/OFF)変更
        private void DebugConsoleDisp_Change(object sender, EventArgs e)
        {
            Debug = tsiConsole.Checked;
            flgConsoleOpened = tsiConsole.Checked;
            if (tsiConsole.Checked)
            {
                dc.Open();
            }
            else
            {
                dc.Close();
            }
            outputInfo.Debug = Debug;

            //XmlSerializerオブジェクトを作成
            //オブジェクトの型を指定する
            XmlSerializer serializer = new XmlSerializer(typeof(OutputInfo));
            //書き込むファイルを開く（UTF-8 BOM無し）
            StreamWriter sw = new StreamWriter(xmlpath, false, new UTF8Encoding(false));
            //シリアル化し、XMLファイルに保存する
            serializer.Serialize(sw, outputInfo);
            sw.Close();
        }

        private void ContextMenu_Opened(object sender, EventArgs e)
        {
            tsiSettings.DropDownItems.Clear();

            ToolStripSeparator tss = new ToolStripSeparator();
            tsiSettings.DropDownItems.Add(tsiOpenEditor);
            tsiSettings.DropDownItems.Add(tss);
            tsiSettings.DropDownItems.Add(tsiConsole);
            //tsiOpenEditor.DropDownItems.Add(tsiSetSettingFile);

            tsiOutput.Enabled = !flgScenarioOpened;
            tsiOutput.Checked = serialPort.IsOpen;

            if (!flgScenarioOpened)
            {

                tsiOutput.DropDownItems.Clear();

                GetDeviceNames gdn = new GetDeviceNames(out string[] ports);


                if (ports != null)
                {
                    foreach (var _portName in ports)
                    {
                        try
                        {
                            tsiPorts = new ToolStripMenuItem(_portName);

                            // クリックイベントを追加
                            tsiPorts.Click += TsiPorts_Click;
                            //使用中ポートにチェック、Enableとする
                            if ((_portName.Contains("("+serialPort.PortName+")")) && serialPort.IsOpen)
                            {
                                tsiPorts.Checked = true;
                                tsiPorts.Enabled = false;
                            }
                            else
                            {
                                tsiPorts.Checked = false;
                                tsiPorts.Enabled = true;
                            }
                            tsiOutput.DropDownItems.Add(tsiPorts);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
        }
        private void ContextMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            //シナリオを閉じるメニューをクリックした時のイベント
            if (e.ClickedItem.Name == "closeToolStripMenuItem")
            {
                flgScenarioOpened = false;
            }
        }
        private string Open(string portName, bool anotherPortOpen)
        {
            string stTarget = Assembly.GetExecutingAssembly().CodeBase;
            string dllPath = "";
            //ローカルファイルのパスを表すURI
            string uriPath = stTarget;
            //Uriオブジェクトを作成する
            Uri u = new Uri(uriPath);
            //変換するURIがファイルを表していることを確認する
            if (u.IsFile)
            {
                //Windows形式のパス表現に変換する
                dllPath = u.LocalPath + Uri.UnescapeDataString(u.Fragment);
            }
            else
            {
                MessageBox.Show("ファイルURIではありません。");
            }

            string xmldir = dir;
            string xmlfileName = Path.GetFileNameWithoutExtension(dllPath);　//0.2.31217.1 追記 設定ファイル名をdllと同じとする。
            xmlpath = xmldir + @"\" + xmlfileName + ".xml";

            try
            {
                if (!File.Exists(xmlpath))
                {
                    MessageBox.Show("設定ファイル " + xmlfileName + ".xml が見つかりません");　//0.2.31217.1 変更 設定ファイル名をdllと同じとする。
                }
                else
                {
                    portName = OutputOpen(xmlpath, portName, anotherPortOpen);
                }

            }
            catch (IOException ex)
            {
                MessageBox.Show("IOの例外:" + ex.Message);
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show("例外:" + ex.Message);
                Dispose();
            }

            if (Debug && !flgConsoleOpened)
            {
                dc.Open();
                flgConsoleOpened = true;
            }
            dateTime = DateTime.Now;
            return portName;
        }

        private string OutputOpen(string path , string _portName , bool _anotherPortOpen)
        {

            //XmlSerializerオブジェクトを作成
            XmlSerializer serializer = new XmlSerializer(typeof(OutputInfo));

            //読み込むファイルを開く
            XmlReader reader = XmlReader.Create(path);

            //XMLファイルから読み込み、逆シリアル化する
            outputInfo = (OutputInfo)serializer.Deserialize(reader);
            reader.Close();

            using (serialPort = new SerialPort()

            {
                BaudRate = outputInfo.BaudRate,
                DataBits = outputInfo.DataBits,
                Parity = outputInfo.Parity,
                StopBits = outputInfo.StopBits,
                DtrEnable = false,
                RtsEnable = true,//Arduino Microとの通信で受信イベントを発生させるにはRTSをTrueにすること
                ReadBufferSize = 256,
                WriteBufferSize = 256,
                WriteTimeout = 1000,
                ReadTimeout = 1000,
                Handshake = Handshake.None,
        })
                serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

            //同じを開くかポート番号がnullのとき
            if (!_anotherPortOpen || _portName == "")
            {
                serialPort.PortName = "COM" + outputInfo.PortNum.ToString();
            }

            //別のポートを開く指示があるとき
            else
            {
                serialPort.PortName = _portName;
            }

            Debug = outputInfo.Debug;

            //シリアルポートを開く
            try
            {
                serialPort.Open();
            }
            catch(IOException)
            {
                MessageBox.Show("シリアルポート "+ serialPort.PortName + " が存在しません。\nシナリオ選択画面を閉じ、右クリックメニューの[SerialOutputEX 連動]メニューから別のポートを選択してください。" );
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            return serialPort.PortName;

        }

        public override void Dispose()
        { 
            //シリアルポートを閉じる
            //try
            //{
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
            //}
            //catch { }

            //AtsEXを選択解除したときに
            //コンソールを非表示とする //0.2.31217.1 追記
            if (Debug || flgConsoleOpened)
            {
                dc.Close();
                flgConsoleOpened = false;
            }
        }

        public override void Tick(TimeSpan elapsed)
        {

            if (serialPort.IsOpen)
            {

                if (flgFirstSend)
                {

                    flgFirstSend = false;
                }

                //If you want to change the Handle state, please access to Ats.Handle
                HandleSet handles = BveHacker.Scenario.Vehicle.Instruments.Cab.Handles;
                AtsPlugin ats = BveHacker.Scenario.Vehicle.Instruments.AtsPlugin;
                VehicleStateStore vehicleStateStore = ats.StateStore;


                //送信コマンド用
                string str_send = "";
                //コマンド解析用
                string str = "";
                //コマンド連結
                if (outputInfo.outputInfoDataList != null || outputInfo.outputInfoDataList.Count != 0)
                {
                    for (int i = 0; i < outputInfo.outputInfoDataList.Count; i++)
                    {
                        str += SendCommandGenerator(outputInfo.outputInfoDataList[i], handles, ats, vehicleStateStore);
                    }
                }

                str_send = str;

                //送信コマンドが前回と異なる場合、かつ前回送信時間から0.02sec以上経過した場合のみ出力
                if ((str_send != str_send_latch) && ((DateTime.Now - dateTime).TotalSeconds > 0.02))
                {
                    dateTime = DateTime.Now;
                    SerialWrite(serialPort, str_send, str_send);
                }
                str_send_latch = str_send;
            }
        }

        //送信コマンド生成
        private string SendCommandGenerator(OutputInfoData _data, HandleSet handles, AtsPlugin ats, VehicleStateStore vehicleStateStore)
        {
            string str = "";
            string format = "{0:" + _data.Base + _data.Digit.ToString() + "}";
            switch (_data.infoId)
            {

                case 0://列車位置
                    str = Right(string.Format(format, (int)(BveHacker.Scenario.VehicleLocation.Location * Math.Pow(10, _data.Pow))), _data.Digit);
                    break;

                case 1://列車速度
                    str = Right(string.Format(format, (int)(Math.Abs(vehicleStateStore.Speed[0]) * Math.Pow(10, _data.Pow))), _data.Digit);
                    break;

                case 2://現在時刻
                    str = Right(string.Format(format, (int)((int)BveHacker.Scenario.TimeManager.Time.TotalMilliseconds * Math.Pow(10, _data.Pow))), _data.Digit);
                    break;

                case 3://ブレーキシリンダ圧力
                    str = Right(string.Format(format, (int)(vehicleStateStore.BcPressure[0] * Math.Pow(10, _data.Pow))), _data.Digit);
                    break;

                case 4://元空気溜め圧力
                    str = Right(string.Format(format, (int)(vehicleStateStore.MrPressure[0] * Math.Pow(10, _data.Pow))), _data.Digit);
                    break;

                case 5://ツリアイ空気溜め圧力
                    str = Right(string.Format(format, (int)(vehicleStateStore.ErPressure[0] * Math.Pow(10, _data.Pow))), _data.Digit);
                    break;

                case 6://ブレーキ管圧力
                    str = Right(string.Format(format, (int)(vehicleStateStore.BpPressure[0] * Math.Pow(10, _data.Pow))), _data.Digit);
                    break;

                case 7://直通管圧力
                    str = Right(string.Format(format, (int)(vehicleStateStore.SapPressure[0] * Math.Pow(10, _data.Pow))), _data.Digit);
                    break;

                case 8://電流
                    string sign = " ";
                    if (vehicleStateStore.Current[0] < 0)
                    {
                        sign = "";
                    }
                    else if (vehicleStateStore.Current[0] > 0)
                    {
                        sign = "+";
                    }
                    str = Right(sign + string.Format(format, (int)(vehicleStateStore.Current[0] * Math.Pow(10, _data.Pow))), _data.Digit + 1);
                    break;

                case 9://ブレーキノッチ
                    str = Right(string.Format(format, handles.BrakeNotch), _data.Digit);
                    break;

                case 10://力行ノッチ
                    string prefix = "N";
                    if (handles.PowerNotch > 0)
                    {
                        prefix = "P";
                    }
                    else if (handles.PowerNotch < 0)
                    {
                        prefix = "H";
                    }
                    str = prefix + Right(string.Format(format, Math.Abs(handles.PowerNotch)), _data.Digit);
                    break;

                case 11://レバーサ位置
                    str = "N";
                    if (handles.ReverserPosition > 0)
                    {
                        str = "F";
                    }
                    else if (handles.ReverserPosition < 0)
                    {
                        str = "B";
                    }
                    break;

                case 12://ドア状態
                    str = BveHacker.Scenario.Vehicle.Doors.AreAllClosed ? "0" : "1";
                    break;

                case 13://パネル状態
                    str = Right(string.Format(format, (int)(ats.PanelArray[_data.PanelNum])), _data.Digit);
                    break;

                case 14://固定文字列
                    str = _data.Value;
                    break;

                case 15://サウンド状態 　//0.2.31217.1 準備
                    str = Right(ats.SoundArray[_data.SoundNum].ToString(), 1);
                    break;

                default:
                    break;

            }

            return str;
        }

        //文字列を末尾から指定文字数切り取るメソッド 0.2.31217.1 追加
        public static string Right(string str, int len)
        {
            if (len < 0)
            {
                throw new ArgumentException("引数'len'は0以上でなければなりません。");
            }
            if (str == null)
            {
                return "";
            }
            if (str.Length <= len)
            {
                return str;
            }
            return str.Substring(str.Length - len, len);
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            //Arduino Microとの通信で受信イベントを発生させるにはRTSをTrueにすること
            SerialPort sp = (SerialPort)sender;
            //ESP32でBluetoothSPPで落ちたのでtrycatch
            try
            {
                string indata = sp.ReadLine();
                ConsoleWrite("受信:" + indata);

                /*if (indata.StartsWith("特定の文字列"))
                {
                    //"特定の文字列"で受信した場合の処理
                }
                */
            }
            catch
            {
                ConsoleWrite(e.ToString());
            }
        }

        internal void SerialWrite(SerialPort sp, string command)
        {
            if (sp.IsOpen)
            {
                sp.Write(command + "\r");
            }
        }
        internal void ConsoleWrite(string command)
        {
            if (Debug)
            {
                Console.Write(command + "\r\n");
            }
        }
        internal void SerialWrite(SerialPort sp, string command, string debug_command)
        {
            SerialWrite(sp, command);
            ConsoleWrite(debug_command);
        }

    }
}
