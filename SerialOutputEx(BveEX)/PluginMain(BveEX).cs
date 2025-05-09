using System;
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
using TypeWrapping;
//using ObjectiveHarmonyPatch;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using SerialOutputEx;
using System.Diagnostics.Eventing.Reader;

namespace SerialOutputEx
{
    [Plugin(PluginType.Extension)]
    [HideExtensionMain]
    internal class PluginMain : AssemblyPluginBase, IExtension
    {

        private SerialPort serialPort;

        private bool Debug = false;
        private OutputInfo outputInfo = new OutputInfo();

        private string str_send_latch = "";
        private DateTime dateTime;

        private bool flgScenarioOpened = false;

        private bool flgFirstSend = false;

        private bool flgConsoleOpened = false;
        /// <summary>
        /// デバッグコンソールのインスタンス
        /// </summary>
        private DebugConsole dc = new DebugConsole();
        /// <summary>
        /// SerialOutputEx連動メニュー 
        /// </summary>
        private ToolStripMenuItem tsiOutput;
        /// <summary>
        /// デバッグコンソール表示メニュー
        /// </summary>
        private ToolStripMenuItem tsiConsole;
        /// <summary>
        /// ポート選択メニュー
        /// </summary>
        private ToolStripMenuItem tsiPorts;
        /// <summary>
        /// シリアル出力エディタ起動メニュー
        /// </summary>
        private ToolStripMenuItem tsiOpenEditor;
        /// <summary>
        /// 開始時ノッチセットメニュー
        /// </summary>
        private ToolStripMenuItem tsiStartingNotchSet;
        /// <summary>
        /// 自動ブレーキ帯設定メニュー
        /// </summary>
        private ToolStripMenuItem tsiAutoBrakeSettings;
        /// <summary>
        /// 自動ブレーキ帯使用メニュー
        /// </summary>
        private ToolStripMenuItem tsiAutoBrakeSet;
        /// <summary>
        /// BveEX使用メニュー
        /// </summary>
        private ToolStripMenuItem tsiAutoAirEX;
        /// <summary>
        /// 自動ノッチ合わせ設定メニュー
        /// </summary>
        private ToolStripMenuItem tsiAutoNotchFit;
        /// <summary>
        /// SerialOutputEx設定メニュー
        /// </summary>
        private ToolStripMenuItem tsiSettings;
        /// <summary>
        /// 設定ファイルの選択メニュー
        /// </summary>
        private ToolStripMenuItem tsiSetSettingFile;
        private ToolStripSeparator tsiSeperator;

        private string portName = "";
        private string editorPath = "";
        private string editorPathV1_1 = "";

        private bool IsStartingNotchSet = false;

        private bool flgAutoBrakeSetChange = false;
        private bool flgAutoNotchFitChange = false;
        private bool flgAutoAirEXChange = false;
        private bool IsUseAutoBrake = false;
        private bool IsUseAutoNotchFit = false;
        private bool IsUseAutoAirEX = false;
        private string xmlpath;

        private string dir;

        private readonly HarmonyPatch Patch;
        //PatchInvokedEventHandler BcChangeHandler;
        private int targetBcValue = 0;
        private bool flgBcChangeMode = false;
        private bool flgFirstBoot = true;

        public PluginMain(PluginBuilder builder) : base(builder)
        {
            /*ClassMemberSet carBrakeMembers = BveHacker.BveTypes.GetClassInfoOf<CarBrake>();
            MethodInfo tickMethod = carBrakeMembers.OriginalType.GetMethods().First(m =>
            {
                ParameterInfo[] parameters = m.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(double);
            });

            Patch = HarmonyPatch.Patch(null, tickMethod, PatchType.Prefix);
            BcChangeHandler = (sender, e) =>
            {
                CarBrake instance = CarBrake.FromSource(e.Instance);
                instance.BcValve.TargetPressure.Value = targetBcValue * 1000;

                return PatchInvokationResult.DoNothing(e);
            };*/

            //0.4.31219.1 追記
            Extensions.AllExtensionsLoaded += Extensions_AllExtensionsLoaded;

        }


        private void Extensions_AllExtensionsLoaded(object sender, EventArgs e)
        {
            tsiOutput = Extensions.GetExtension<IContextMenuHacker>().AddCheckableMenuItem("SerialOutputEx 連動", SerialOutputEx_Change, ContextMenuItemType.CoreAndExtensions);
            tsiConsole = Extensions.GetExtension<IContextMenuHacker>().AddCheckableMenuItem("デバッグコンソール 表示", DebugConsoleDisp_Change, ContextMenuItemType.CoreAndExtensions);
            tsiOpenEditor = Extensions.GetExtension<IContextMenuHacker>().AddClickableMenuItem("SerialOutputEx 設定", SerialOutputExEdit_Open, ContextMenuItemType.CoreAndExtensions);
            //tsiStartingNotchSet = Extensions.GetExtension<IContextMenuHacker>().AddCheckableMenuItem("シナリオ開始時 ノッチ設定転送", SerialOutputExStartingNotchSet_Change, ContextMenuItemType.CoreAndExtensions);
            //tsiAutoBrakeSettings = Extensions.GetExtension<IContextMenuHacker>().AddClickableMenuItem("自動ブレーキ帯 設定", null, ContextMenuItemType.CoreAndExtensions);
            //tsiAutoBrakeSet = Extensions.GetExtension<IContextMenuHacker>().AddCheckableMenuItem("自動ブレーキ帯 使用", AutoBrakeSet_Change, ContextMenuItemType.CoreAndExtensions);
            //tsiAutoAirEX = Extensions.GetExtension<IContextMenuHacker>().AddCheckableMenuItem("BveEX使用", AutoBrakeEX_Change, ContextMenuItemType.CoreAndExtensions);
            //tsiAutoNotchFit = Extensions.GetExtension<IContextMenuHacker>().AddCheckableMenuItem("自動ノッチ合わせ", AutoNotchFit_Change, ContextMenuItemType.CoreAndExtensions);
            tsiSettings = Extensions.GetExtension<IContextMenuHacker>().AddClickableMenuItem("SerialOutputEx 設定", null, ContextMenuItemType.CoreAndExtensions);
            tsiSetSettingFile = Extensions.GetExtension<IContextMenuHacker>().AddClickableMenuItem("設定ファイルの選択", SetSettingFile_Click, ContextMenuItemType.CoreAndExtensions);
            tsiSeperator = Extensions.GetExtension<IContextMenuHacker>().AddSeparator(0);

            //"シナリオを開く"イベント
            BveHacker.ScenarioOpened += BveHacker_ScenarioOpened;

            BveHacker.ScenarioCreated += BveHacker_ScenarioCreated;

            //コンテクストメニューを開くイベント
            BveHacker.MainForm.ContextMenu.Opened += ContextMenu_Opened;

            //コンテクストメニューのアイテムクリックイベント
            BveHacker.MainForm.ContextMenu.ItemClicked += ContextMenu_ItemClicked;

            //0.3.31218.1 追記ここまで

            Task task = Open(portName, false);

        }

        private void SetSettingFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            MessageShow("鋭意実装中です");
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
        /// <summary>
        /// シリアル出力エディタの起動
        /// </summary>
        private void SerialOutputExEdit_Open(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process p = System.Diagnostics.Process.Start(editorPath);
            }
            catch (Exception ex)
            {
                MessageShow("例外:" + ex.Message);
                Dispose();
            }
        }
        /// <summary>
        /// ポート選択メニュー操作
        /// </summary>
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

                    Task open = Open(portName, true);
                }
                catch (Exception ex)
                {
                    MessageShow(ex.Message);
                }
            }
        }

        /// <summary>
        /// シリアル出力(ON/OFF)メニュー変更
        /// </summary>
        private void SerialOutputEx_Change(object sender, EventArgs e)
        {
            if (tsiOutput.Checked)
            {
                if (!serialPort.IsOpen)
                {
                    Task open = Open(portName, false);
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

        /// <summary>
        /// 自動ブレーキ設定(ON/OFF)メニュー変更
        /// </summary>
        private void AutoBrakeSet_Change(object sender, EventArgs e)
        {
            flgAutoBrakeSetChange = true;
            IsUseAutoBrake = tsiAutoBrakeSet.Checked;
            Properties.Settings.Default.UseAutoBrake = IsUseAutoBrake;
            tsiAutoAirEX.Enabled = IsUseAutoBrake;
        }
        /// <summary>
        /// BveEX使用(On/Off)メニュー変更
        /// </summary>
       private void AutoBrakeEX_Change(object sender, EventArgs e)
        {
            flgAutoAirEXChange = true;
            IsUseAutoAirEX = tsiAutoAirEX.Checked;
            Properties.Settings.Default.UseAutoAirEX = IsUseAutoAirEX;
        }

        /// <summary>
        /// ブレーキ段数自動設定(ON/OFF)メニュー変更
        /// </summary>
        /*private void AutoNotchFit_Change(object sender, EventArgs e)
        {
            flgAutoNotchFitChange = true;
            IsUseAutoNotchFit = tsiAutoNotchFit.Checked;
            Properties.Settings.Default.IsUseAutoNotchFit = IsUseAutoNotchFit;
        }*/
        /// <summary>
        /// デバッグコンソール表示(ON/OFF)変更
        /// </summary>
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
        /// <summary>
        /// シナリオ開始時 ノッチ設定転送 変更メソッド
        /// </summary>
        /*private void SerialOutputExStartingNotchSet_Change(object sender, EventArgs e)
        {
            IsStartingNotchSet = tsiStartingNotchSet.Checked;
            Properties.Settings.Default.IsStartingNotchSet = IsStartingNotchSet;

        }*/

        /// <summary>
        /// コンテクストメニューを開くイベント
        /// </summary>
        private void ContextMenu_Opened(object sender, EventArgs e)
        {
            tsiSettings.DropDownItems.Clear();

            ToolStripSeparator tss = new ToolStripSeparator();
            tsiSettings.DropDownItems.Add(tsiOpenEditor);
            tsiSettings.DropDownItems.Add(tss);
            tsiSettings.DropDownItems.Add(tsiConsole);
            //tsiSettings.DropDownItems.Add(tsiStartingNotchSet);
            //tsiSettings.DropDownItems.Add(tsiAutoBrakeSettings);
            //tsiSettings.DropDownItems.Add(tsiAutoNotchFit);
            tsiOpenEditor.DropDownItems.Add(tsiSetSettingFile);
            //tsiAutoBrakeSettings.DropDownItems.Add(tsiAutoBrakeSet);
            //tsiAutoBrakeSettings.DropDownItems.Add(tsiAutoAirEX);

            tsiOutput.Enabled = !flgScenarioOpened;
            tsiOutput.Checked = serialPort.IsOpen;
            tsiOutput.Text = serialPort.IsOpen ? "SerialOutputEx 連動 (" + serialPort.PortName + ")" : "SerialOutputEx 連動";

            /*tsiStartingNotchSet.Enabled = tsiOutput.Checked;
            tsiAutoBrakeSet.Enabled = tsiOutput.Checked;
            tsiAutoNotchFit.Enabled = tsiOutput.Checked;
            tsiAutoBrakeSettings.Enabled = tsiOutput.Checked;
            tsiAutoAirEX.Enabled = tsiOutput.Checked;*/

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
                            if ((_portName.Contains("(" + serialPort.PortName + ")")) && serialPort.IsOpen)
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
                            MessageShow(ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// コンテクストメニューのクリックイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            //シナリオを閉じるメニューをクリックした時のイベント
            if (e.ClickedItem.Name == "closeToolStripMenuItem")
            {
                flgScenarioOpened = false;
            }
        }

        /// <summary>
        /// シリアル通信開始処理
        /// </summary>
        /// <param name="portName">ポート名</param>
        /// <param name="anotherPortOpen">他のポートを開く場合の処理</param>
        /// <returns></returns>
        private async Task Open(string portName, bool anotherPortOpen)
        {
            //初回起動時500ms遅延(Win11でMessageBoxがNullReferenceExceptionを起こす)
            if (flgFirstBoot)
            {
                flgFirstBoot = false;
                await Task.Delay(500);

                string _stTarget = Assembly.GetExecutingAssembly().CodeBase;
                string _dllPath = "";
                //ローカルファイルのパスを表すURI
                string _uriPath = _stTarget;
                //Uriオブジェクトを作成する
                Uri _u = new Uri(_uriPath);
                //変換するURIがファイルを表していることを確認する
                if (_u.IsFile)
                {
                    //Windows形式のパス表現に変換する
                    _dllPath = _u.LocalPath + Uri.UnescapeDataString(_u.Fragment);
                }
                else
                {
                    MessageShow("ファイルURIではありません。");
                }

                dir = Path.GetDirectoryName(_dllPath);
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
                    MessageShow("例外:" + ex.Message);
                }


                IsStartingNotchSet = Properties.Settings.Default.IsStartingNotchSet;
                IsUseAutoBrake = Properties.Settings.Default.UseAutoBrake;
                IsUseAutoNotchFit = Properties.Settings.Default.IsUseAutoNotchFit;
                IsUseAutoAirEX = Properties.Settings.Default.UseAutoAirEX;

                /*tsiStartingNotchSet.Checked = IsStartingNotchSet;
                tsiAutoBrakeSet.Checked = IsUseAutoBrake;
                tsiAutoNotchFit.Checked = IsUseAutoNotchFit;
                tsiAutoAirEX.Checked = IsUseAutoAirEX;

                tsiStartingNotchSet.Enabled = tsiOutput.Checked;
                tsiAutoBrakeSet.Enabled = tsiOutput.Checked;
                tsiAutoNotchFit.Enabled = tsiOutput.Checked;
                tsiAutoBrakeSettings.Enabled = tsiOutput.Checked;
                tsiAutoAirEX.Enabled = tsiOutput.Checked;*/
                //Properties.Settings.Default.Reload();
            }
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
                MessageShow("ファイルURIではありません。");
            }

            string xmldir = dir;
            string xmlfileName = Path.GetFileNameWithoutExtension(dllPath);　//0.2.31217.1 追記 設定ファイル名をdllと同じとする。
            xmlpath = xmldir + @"\" + xmlfileName + ".xml";

            try
            {
                if (!File.Exists(xmlpath))
                {
                    MessageShow("設定ファイル " + xmlfileName + ".xml が見つかりません"); //0.2.31217.1 変更 設定ファイル名をdllと同じとする。
                }
                else
                {
                    portName = OutputOpen(xmlpath, portName, anotherPortOpen);
                }

            }
            catch (IOException ex)
            {
                MessageShow("IOの例外:" + ex.Message);
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }


            }
            catch (Exception ex)
            {
                MessageShow("例外:" + ex.Message);
                Dispose();
            }

            if (Debug && !flgConsoleOpened)
            {
                dc.Open();
                flgConsoleOpened = true;
            }
            if (Debug)
            {
                tsiConsole.Checked = true;
            }
            if (serialPort.IsOpen)
            {
                tsiOutput.Checked = true;
            }
            dateTime = DateTime.Now;
            //return portName;
        }

        /// <summary>
        /// 設定XMLファイルを読込み、シリアル通信を開始sるメソッド
        /// </summary>
        /// <param name="path">設定XMLファイルパス</param>
        /// <param name="_portName">ポート名</param>
        /// <param name="_anotherPortOpen">他のポートを開く場合</param>
        /// <returns></returns>
        private string OutputOpen(string path, string _portName, bool _anotherPortOpen)
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
            catch (IOException)
            {
                MessageShow("シリアルポート " + serialPort.PortName + " が存在しません。\nシナリオ選択画面を閉じ、右クリックメニューの[SerialOutputEX 連動]メニューから別のポートを選択してください。");
            }
            catch (Exception ex)
            {
                MessageShow(ex.ToString());
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

            Properties.Settings.Default.Save();

            /*
            if (flgBcChangeMode)
            {
                Patch.Invoked -= BcChangeHandler;
            }
            Patch.Dispose();*/
        }

        public override void Tick(TimeSpan elapsed)
        {
            if (serialPort.IsOpen)
            {

                if (flgFirstSend && IsStartingNotchSet)
                {
                    string strBrakeNotchCount = BveHacker.Scenario.Vehicle.Instruments.Cab.Handles.NotchInfo.BrakeNotchCount.ToString();
                    string strPowerNotchCount = BveHacker.Scenario.Vehicle.Instruments.Cab.Handles.NotchInfo.PowerNotchCount.ToString();
                    SerialWrite(serialPort, "WR 004 " + strBrakeNotchCount, "ブレーキ段数         WR 004 " + strBrakeNotchCount);
                    SerialWrite(serialPort, "WR 072 " + strPowerNotchCount, "マスコンノッチ数     WR 072 " + strPowerNotchCount);
                    flgFirstSend = false;
                }

                if (flgAutoBrakeSetChange || flgAutoAirEXChange)
                {
                    if (serialPort.IsOpen)
                    {

                        int n = 0;
                        string s0 = "無効";
                        string s2 = "無効";
                        if (IsUseAutoBrake) n |= (1 << 0); s0 = "有効";
                        //if (cbRealAutoAir.Checked) n |= (1 << 1);
                        if (IsUseAutoAirEX) n |= (1 << 2); s2 = "有効";
                        SerialWrite(serialPort, "WR 068 " + n.ToString(), "自動ブレーキ" + s0 + "\r\nBveEX" + s2 + "            WR 068 " + n.ToString());
                    }
                    flgAutoBrakeSetChange = false;
                    flgAutoAirEXChange = false;
                }

                if (flgAutoNotchFitChange)
                {
                    if (IsUseAutoNotchFit)
                    {
                        SerialWrite(serialPort, "WR 078 1", "自動ノッチ合わせ有効 WR 078 1");
                    }
                    else
                    {
                        SerialWrite(serialPort, "WR 078 0", "自動ノッチ合わせ無効 WR 078 0");
                    }
                    flgAutoNotchFitChange = false;
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
        /// <summary>
        /// 送信コマンド生成
        /// </summary>
        /// <param name="_data"></param>
        /// <param name="handles"></param>
        /// <param name="ats"></param>
        /// <param name="vehicleStateStore"></param>
        /// <returns></returns>
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
                /*if (indata.StartsWith("BC"))
                {
                    int.TryParse(indata.Substring(3), out targetBcValue);
                }
                if (indata.StartsWith("AAB"))
                {
                    int.TryParse(indata.Substring(4), out int eventBcChange);
                    ConsoleWrite("BC" + eventBcChange.ToString());
                    //BC変更シーケンス
                    if (eventBcChange == 1)
                    {
                        flgBcChangeMode = true;
                        Patch.Invoked += BcChangeHandler;
                        ConsoleWrite("BC変更　設定");
                    }
                    else
                    {
                        flgBcChangeMode = false;
                        Patch.Invoked -= BcChangeHandler;
                        ConsoleWrite("BC変更　解除");
                    }
                }*/
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
        /// <summary>
        /// メッセージダイアログ表示メソッド
        /// </summary>
        /// <param name="str"></param>
        private void MessageShow(string str)
        {
            BveEx.Diagnostics.ErrorDialogInfo edi = new BveEx.Diagnostics.ErrorDialogInfo("SeldEX", "", str);
            BveEx.Diagnostics.ErrorDialog.Show(edi);
            //MessageBox.Show(str);
        }

    }
}
