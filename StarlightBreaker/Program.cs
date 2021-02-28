using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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
        private Patch ChatLogStarPatch;
        private Patch pfinderStarPatch;
        private Patch pfinderDialogStarPatch;
        private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin ffxivPlugin;
        private BackgroundWorker _processSwitcher;
        private ZodiarkProcess Mordion;


        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText) {
            statusLabel = pluginStatusText;
            ffxivPlugin = GetFfxivPlugin();

            statusLabel.Text = "Looking for FFXIV...";

            _processSwitcher = new BackgroundWorker { WorkerSupportsCancellation = true };
            _processSwitcher.DoWork += ProcessSwitcher;
            _processSwitcher.RunWorkerAsync();
        }

        private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin GetFfxivPlugin() {
            FFXIV_ACT_Plugin.FFXIV_ACT_Plugin ffxivActPlugin = null;
            foreach (var actPluginData in ActGlobals.oFormActMain.ActPlugins)
                if (actPluginData.pluginFile.Name.ToUpper().Contains("FFXIV_ACT_Plugin".ToUpper()) &&
                    actPluginData.lblPluginStatus.Text.ToUpper().Contains("FFXIV Plugin Started.".ToUpper()))
                    ffxivActPlugin = (FFXIV_ACT_Plugin.FFXIV_ACT_Plugin)actPluginData.pluginObj;
            return ffxivActPlugin ?? throw new Exception("找不到FFXIV解析插件，请确保其加载顺序位于Starlight Breaker之前。");
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
                if (ChatLogStarPatch != null) {
                    ChatLogStarPatch.Disable();
                }
                if (pfinderStarPatch != null) {
                    pfinderStarPatch.Disable();
                }
                if (pfinderDialogStarPatch != null) {
                    pfinderDialogStarPatch.Disable();
                }
            }
            statusLabel.Text = "反和谐已关闭";
        }

        private void Attach() {
            try {
                if (FFXIV.ProcessName == "ffxiv_dx11") {
                    Mordion = new ZodiarkProcess(FFXIV);
                    var ChatLogSkipAddress = Mordion.Scanner.ScanText("74 ?? 48 8B D3 E8 ?? ?? ?? ?? 48 8B C3");
                    ChatLogStarPatch = Mordion.SetPatch(ChatLogSkipAddress, new byte?[] { 0xEB });
                    var pfinderSkipAddress = Mordion.Scanner.ScanText("48 8B D6 E8 ?? ?? ?? ?? 80 BF") + 3;
                    pfinderStarPatch = Mordion.SetPatch(pfinderSkipAddress, new byte?[] { 0x90, 0x90, 0x90, 0x90, 0x90 });
                    var pfinderDialogSkipAddress = Mordion.Scanner.ScanText("4C 8B C7 E8 ?? ?? ?? ?? 40 38 B3") + 3;
                    pfinderDialogStarPatch = Mordion.SetPatch(pfinderDialogSkipAddress, new byte?[] { 0x90, 0x90, 0x90, 0x90, 0x90 });

                    ChatLogStarPatch.Enable();
                    pfinderStarPatch.Enable();
                    pfinderDialogStarPatch.Enable();
                    statusLabel.Text = "反和谐已开启";
                }
                else {
                    MessageBox.Show($"2021年了，别用Dx9了", "幹，老兄你的游戏好雞瓣怪啊",MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw new Exception("不支持FFXIV Dx9");
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"反和谐开启失败！\n{ex.Message}");
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
