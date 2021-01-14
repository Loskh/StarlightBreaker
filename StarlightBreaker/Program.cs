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
        private Label lbl;
        private Process FFXIV;
        private Patch StarPatch;
        private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin ffxivPlugin;
        private BackgroundWorker _processSwitcher;
        private ZodiarkProcess Mordion;


        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText) {
            pluginScreenSpace.Text = "Starlight Breaker";
            statusLabel = pluginStatusText;
            ffxivPlugin = GetFfxivPlugin();
            pluginScreenSpace.Text = "Starlight Breaker";
            this.lbl = new Label {
                Location = new Point(8, 8),
                AutoSize = true,
                Text = "Looking for FFXIV..."
            };
            pluginScreenSpace.Controls.Add(this.lbl);

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
                    else lbl.Text = "Looking for FFXIV...";
                }

                Thread.Sleep(3000);
            }
        }

        private void Detach() {
            if (Mordion != null && StarPatch != null) {
                StarPatch.Disable();
                lbl.Text = "反和谐已关闭";
                statusLabel.Text = "Disable";
            }

        }

        private void Attach() {
            Mordion = new ZodiarkProcess(FFXIV);
            var SkipAddress = Mordion.Scanner.ScanText("74 ?? 48 8B D3 E8 ?? ?? ?? ?? 48 8B C3");
            StarPatch = Mordion.SetPatch(SkipAddress, new byte?[] { 0xEB });
            StarPatch.Enable();
            lbl.Text = "反和谐已开启";
            statusLabel.Text = "Enable";
        }

        private Process GetFFXIVProcess() {
            return ffxivPlugin.DataRepository.GetCurrentFFXIVProcess();
        }

        public void DeInitPlugin() {
            try {
                if (Mordion != null&&StarPatch!=null)
                    Detach();
            }
            catch (Exception) {
                // ignored
            }
        }


    }
}
