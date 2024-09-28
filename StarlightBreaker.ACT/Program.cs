using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Zodiark;
using Zodiark.MemoryPatch;

namespace StarlightBreaker
{
    class MainClass : IActPluginV1
    {
        private Label statusLabel;
        private Process FFXIV;
        private Patch StarCheckPatch;
        private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin ffxivPlugin;
        private BackgroundWorker _processSwitcher;
        private ZodiarkProcess Mordion;
        private const string PluginName = "StarlightBreaker";

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText) {
            statusLabel = pluginStatusText;
            pluginScreenSpace.Text = PluginName;
            statusLabel.Text = "Looking for FFXIV...";

            ffxivPlugin = GetFFXIVPlugin();

            _processSwitcher = new BackgroundWorker { WorkerSupportsCancellation = true };
            _processSwitcher.DoWork += ProcessSwitcher;
            _processSwitcher.RunWorkerAsync();
            pluginScreenSpace.Hide();
            HideTab();

         }

        public void HideTab() {
            var oFormActMain = ActGlobals.oFormActMain;
            var tcPlugins = (TabControl)typeof(FormActMain).GetField("tcPlugins", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(oFormActMain);
            if (tcPlugins == null) return;
            foreach (TabPage tab in tcPlugins.TabPages) {
                if (string.Equals(tab.Text, PluginName, StringComparison.CurrentCultureIgnoreCase))
                    tcPlugins.TabPages.Remove(tab);
            }
        }

        //private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin GetFfxivPlugin() {
        //    FFXIV_ACT_Plugin.FFXIV_ACT_Plugin ffxivActPlugin = null;
        //    foreach (var actPluginData in ActGlobals.oFormActMain.ActPlugins)
        //        if (actPluginData.pluginFile.Name.ToUpper().Contains("FFXIV_ACT_Plugin".ToUpper()) &&
        //            actPluginData.lblPluginStatus.Text.ToUpper().Contains("FFXIV Plugin Started.".ToUpper()))
        //            ffxivActPlugin = (FFXIV_ACT_Plugin.FFXIV_ACT_Plugin)actPluginData.pluginObj;
        //    return ffxivActPlugin ?? throw new Exception("找不到FFXIV解析插件，请确保其加载顺序位于Starlight Breaker之前。");
        //}

        private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin GetFFXIVPlugin()
        {
            var plugin = ActGlobals.oFormActMain.ActPlugins.FirstOrDefault(x => x.pluginObj?.GetType().ToString() == "FFXIV_ACT_Plugin.FFXIV_ACT_Plugin")?.pluginObj;
            return (FFXIV_ACT_Plugin.FFXIV_ACT_Plugin)plugin ?? throw new Exception("找不到FFXIV解析插件，请确保其加载顺序位于鲶鱼精邮差之前。");
        }

        private void ProcessSwitcher(object sender, DoWorkEventArgs e) {
            while (true) {
                if (_processSwitcher.CancellationPending) {
                    e.Cancel = true;
                    break;
                }

                if (FFXIV != GetFFXIVProcess()) {
                    Detach();
                    FFXIV = GetFFXIVProcess();
                    if (FFXIV != null)
                        Attach();
                    else statusLabel.Text = "Looking for FFXIV...";
                }

                Thread.Sleep(3000);
            }
        }

        private void Detach() {
            if (Mordion != null) {
                if (StarCheckPatch != null) {
                    StarCheckPatch.Disable();
                }
            }
            statusLabel.Text = "反和谐已关闭";
        }

        private void Attach() {
            try {
                if (FFXIV.ProcessName == "ffxiv_dx11") {
                    Mordion = new ZodiarkProcess(FFXIV);
                    var starCheck = Mordion.Scanner.ScanText("85 C9 0F 84 ?? ?? ?? ?? 41 83 7C 0B");
                    //ActGlobals.oFormActMain.WriteDebugLog(starCheck.ToHex().ToString());
                    StarCheckPatch = Mordion.SetPatch(starCheck, new byte?[] { 0x31 });
                    StarCheckPatch.Enable();
                    statusLabel.Text = "反和谐已开启";

                }
                else {
                    var thisYear = new string(DateTime.Now.Year.ToString().ToCharArray().Reverse().ToArray());
                    MessageBox.Show($"{thisYear}年了，别用Dx9了\n请勿盲目相信登录器设置中的Dx11设置，检查客户端是否工作在Dx11模式时最好使用任务管理器查看进程名是否为ffxiv_dx11.exe确定。\n可以尝试退出游戏后删除sdo\\sdologin\\GamePlugin\\GameSetting.xml，然后重新开启Dx11模式", "幹，老兄你的游戏好雞掰怪啊", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw new Exception("不支持FFXIV Dx9");
                }
            }
            catch (Exception ex) {
#if DEBUG
                MessageBox.Show($"反和谐开启失败！\n{ex.Message}");
#endif
                ActGlobals.oFormActMain.WriteExceptionLog(ex, "反和谐异常");

                statusLabel.Text = "反和谐开启失败";
            }

        }

        private Process GetFFXIVProcess() {
            return ffxivPlugin.DataRepository.GetCurrentFFXIVProcess();
        }

        public void DeInitPlugin() {
            try {
                Detach();
            }
            catch (Exception) {
                // ignored
            }
        }


    }
}
