﻿using gui.app.gpucontroller.amd;
using gui.app.gpucontroller.amd32;
using gui.app.gpucontroller.amd64;
using gui.app.mvvm.model;
using gui.app.mvvm.viewmodel;
using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using vibrance.GUI.AMD.vendor;

namespace vibrance.GUI
{
    public partial class AmdVibranceGUI : Form
    {
        private AmdVibranceAdapter v;
        private RegistryController registryController;
        private AutoResetEvent resetEvent;
        public bool silenced = false;
        private const string appName = "vibranceGUI";
        private const string twitterLink = "https://twitter.com/juvlarN";
        private const string paypalDonationLink = "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=JDQFNKNNEW356";

        private bool allowVisible;

        public AmdVibranceGUI()
        {
            InitializeComponent();
            allowVisible = true;
            v = new AmdVibranceAdapter(silenced, this.AddLogItem);

            resetEvent = new AutoResetEvent(false);
            backgroundWorker.WorkerReportsProgress = true;
            settingsBackgroundWorker.WorkerReportsProgress = true;

            backgroundWorker.RunWorkerAsync();
        }

        private void AddLogItem(string logItem)
        {
            Debug.WriteLine(logItem);
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!allowVisible)
            {
                value = false;
                if (!this.IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        public void SetAllowVisible(bool value)
        {
            allowVisible = value;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            setGuiEnabledFlag(false);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.notifyIcon.Visible = true;
                this.notifyIcon.ShowBalloonTip(250);
                this.Hide();
            }
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            int vibranceIngameLevel = v.MaxLevel, vibranceWindowsLevel = v.DefaultLevel, refreshRate = 5000;
            bool keepActive = false;

            while (!this.IsHandleCreated)
            {
                System.Threading.Thread.Sleep(500);
            }


            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    readVibranceSettings(out vibranceIngameLevel, out vibranceWindowsLevel, out keepActive, out refreshRate);
                });
            }
            else
            {
                readVibranceSettings(out vibranceIngameLevel, out vibranceWindowsLevel, out keepActive, out refreshRate);
            }

            if (v.amdViewModel != null)
            {
                backgroundWorker.ReportProgress(1);

                setGuiEnabledFlag(true);

                v.setKeepActive(keepActive);
                v.setVibranceIngameLevel(vibranceIngameLevel);
                v.setVibranceWindowsLevel(vibranceWindowsLevel);
                v.setSleepInterval(refreshRate);
                v.setShouldRun(true);

                bool unload = v.unloadLibraryEx();

                backgroundWorker.ReportProgress(2, unload);
                resetEvent.Set();
                Application.DoEvents();
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (v.amdViewModel != null)
            {
                setGuiEnabledFlag(true);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            cleanUp();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void trackBarIngameLevel_Scroll(object sender, EventArgs e)
        {
            labelIngameLevel.Text = trackBarIngameLevel.Value.ToString();

            if (!settingsBackgroundWorker.IsBusy)
            {
                settingsBackgroundWorker.RunWorkerAsync();
            }
        }

        private void trackBarWindowsLevel_Scroll(object sender, EventArgs e)
        {
            labelWindowsLevel.Text = trackBarWindowsLevel.Value.ToString();

            if (!settingsBackgroundWorker.IsBusy)
            {
                settingsBackgroundWorker.RunWorkerAsync();
            }
        }

        private void settingsBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.Sleep(1000);
            int ingameLevel = 0, windowsLevel = 0, refreshRate = 0;
            bool keepActive = false;
            this.Invoke((MethodInvoker)delegate
            {
                ingameLevel = trackBarIngameLevel.Value;
                windowsLevel = trackBarWindowsLevel.Value;
                keepActive = checkBoxKeepActive.Checked;
                refreshRate = int.Parse(textBoxRefreshRate.Text);
            });
            saveVibranceSettings(ingameLevel, windowsLevel, keepActive, refreshRate);
        }

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 1)
            {
                listBoxLog.Items.Add("vibranceInfo.isInitialized: " + (v.amdViewModel != null));

                listBoxLog.Items.Add("vibranceInfo.userVibranceSettingActive: " + v.amdViewModel.VibranceSettingsViewModel.Model.IngameVibranceLevel);
                listBoxLog.Items.Add("vibranceInfo.userVibranceSettingDefault: " + v.amdViewModel.VibranceSettingsViewModel.Model.WindowsVibranceLevel);
                listBoxLog.Items.Add("");
                listBoxLog.Items.Add("");
                this.statusLabel.Text = "Running!";
                this.statusLabel.ForeColor = Color.Green;
            }
        }

        private void settingsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            allowVisible = true;
            this.Show();

            this.WindowState = FormWindowState.Normal;
            this.Visible = true;

            this.Refresh();
            this.ShowInTaskbar = true;
        }

        private void textBoxRefreshRate_TextChanged(object sender, EventArgs e)
        {
            if (v != null)
            {
                int refreshRate = -1;

                if (!int.TryParse(textBoxRefreshRate.Text, out refreshRate))
                {
                    textBoxRefreshRate.Text = "5000";
                }
                else if (refreshRate > 200)
                {
                    if (v != null)
                    {
                        v.setSleepInterval(refreshRate);
                        if (!settingsBackgroundWorker.IsBusy)
                        {
                            settingsBackgroundWorker.RunWorkerAsync();
                        }
                        listBoxLog.Items.Add("Refresh rate has been set to: " + refreshRate + " ms");
                    }
                }
                else
                {
                    listBoxLog.Items.Add("The refresh rate must be greater than 200 ms!");
                }
            }
        }

        private void checkBoxKeepActive_CheckedChanged(object sender, EventArgs e)
        {
            if (v != null)
            {
                v.setKeepActive(checkBoxKeepActive.Checked);
                if (!settingsBackgroundWorker.IsBusy)
                {
                    settingsBackgroundWorker.RunWorkerAsync();
                }
                if (checkBoxKeepActive.Checked)
                    listBoxLog.Items.Add("Vibrance stays at ingame level when tabbed out.");
            }
        }

        private void checkBoxAutostart_CheckedChanged(object sender, EventArgs e)
        {
            RegistryController autostartController = new RegistryController();
            if (this.checkBoxAutostart.Checked)
            {
                string pathToExe = "\"" + Application.ExecutablePath.ToString() + "\" -minimized";
                if (!autostartController.isProgramRegistered(appName))
                {
                    if (autostartController.registerProgram(appName, pathToExe))
                        listBoxLog.Items.Add("Registered to Autostart!");
                    else
                        listBoxLog.Items.Add("Registering to Autostart failed!");
                }
                else if (!autostartController.isStartupPathUnchanged(appName, pathToExe))
                {
                    if (autostartController.registerProgram(appName, pathToExe))
                        listBoxLog.Items.Add("Updated Autostart Path!");
                    else
                        listBoxLog.Items.Add("Updating Autostart Path failed!");
                }
            }
            else
            {
                if (autostartController.unregisterProgram(appName))
                    listBoxLog.Items.Add("Unregistered from Autostart!");
                else
                    listBoxLog.Items.Add("Unregistering from Autostart failed!");
            }
        }

        private void twitterToolStripTextBox_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(AmdVibranceGUI.twitterLink);
        }

        private void linkLabelTwitter_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(AmdVibranceGUI.twitterLink);
        }

        private void setGuiEnabledFlag(bool flag)
        {
            this.Invoke((MethodInvoker)delegate
            {
                this.checkBoxKeepActive.Enabled = flag;
                this.trackBarWindowsLevel.Enabled = flag;
                this.trackBarIngameLevel.Enabled = flag;
                this.textBoxRefreshRate.Enabled = flag;
                this.checkBoxAutostart.Enabled = flag;
            });
        }

        private void cleanUp()
        {
            this.statusLabel.Text = "Closing...";
            this.statusLabel.ForeColor = Color.Red;
            this.Update();
            listBoxLog.Items.Add("Initiating observer thread exit... ");
            if (v != null && v.amdViewModel != null)
            {
                v.setShouldRun(false);
                resetEvent.WaitOne();
                listBoxLog.Items.Add("Unloading NVAPI... ");
            }
        }

        private void readVibranceSettings(out int vibranceIngameLevel, out int vibranceWindowsLevel, out bool keepActive, out int refreshRate)
        {
            registryController = new RegistryController();
            this.checkBoxAutostart.Checked = registryController.isProgramRegistered(appName);

            vibranceIngameLevel = v.amdViewModel.VibranceSettingsViewModel.Model.IngameVibranceLevel;
            vibranceWindowsLevel = v.amdViewModel.VibranceSettingsViewModel.Model.WindowsVibranceLevel;
            keepActive = v.amdViewModel.VibranceSettingsViewModel.Model.KeepVibranceOnWhenCsGoIsStarted;
            refreshRate = v.amdViewModel.VibranceSettingsViewModel.Model.RefreshRate;

            labelWindowsLevel.Text = vibranceWindowsLevel.ToString();
            labelIngameLevel.Text = vibranceIngameLevel.ToString();

            trackBarWindowsLevel.Value = vibranceWindowsLevel;
            trackBarIngameLevel.Value = vibranceIngameLevel;
            checkBoxKeepActive.Checked = keepActive;
            textBoxRefreshRate.Text = refreshRate.ToString();
        }

        private void saveVibranceSettings(int ingameLevel, int windowsLevel, bool keepActive, int refreshRate)
        {
            v.amdViewModel.VibranceSettingsViewModel.Model.IngameVibranceLevel = ingameLevel;
            v.amdViewModel.VibranceSettingsViewModel.Model.WindowsVibranceLevel = windowsLevel;
            v.amdViewModel.VibranceSettingsViewModel.Model.KeepVibranceOnWhenCsGoIsStarted = keepActive;
            v.amdViewModel.VibranceSettingsViewModel.Model.RefreshRate = refreshRate;
            v.amdViewModel.VibranceSettingsViewModel.SaveVibranceSettings();
        }

        private void buttonPaypal_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(AmdVibranceGUI.paypalDonationLink);
        }

    }

    public class AmdVibranceAdapter
    {
        public AmdViewModel amdViewModel;

        private bool silenced;

        public AmdVibranceAdapter(bool silenced, Action<string> addLogItem)
        {
            this.amdViewModel = new AmdViewModel(addLogItem, Environment.Is64BitOperatingSystem ? (AmdAdapter)new AmdAdapter64() : (AmdAdapter)new AmdAdapter32());
            this.silenced = silenced;
        }

        public int DefaultLevel
        {
            get
            {
                return this.amdViewModel.MinimumVibranceLevel;
            }
        }

        public int MaxLevel
        {
            get
            {
                return this.amdViewModel.MaximumVibranceLevel;
            }
        }

        public void setKeepActive(bool value)
        {
            this.amdViewModel.VibranceSettingsViewModel.Model.KeepVibranceOnWhenCsGoIsStarted = value;
        }

        CancellationTokenSource cancellationTokenSource;

        private Task task;

        public void setShouldRun(bool value)
        {
            if (value)
            {
                this.cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = this.cancellationTokenSource.Token;

                task = Task.Factory.StartNew(new Action(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool moreToDo = true;
                    while (moreToDo)
                    {
                        this.amdViewModel.VibranceSettingsViewModel.RefreshVibranceStatus(VibranceSettingsViewModel.GetForegroundWindow());

                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        Thread.Sleep(this.amdViewModel.VibranceSettingsViewModel.Model.RefreshRate);
                    }
                }), cancellationTokenSource.Token);
            }
            else if (this.cancellationTokenSource != null)
            {
                this.cancellationTokenSource.Cancel();

                try
                {
                    task.Wait();
                }
                catch (Exception ex)
                {
                    this.amdViewModel.VibranceSettingsViewModel.ResetVibrance();
                }
            }
        }

        public void setSleepInterval(int refreshRate)
        {
            this.amdViewModel.VibranceSettingsViewModel.Model.RefreshRate = refreshRate;
        }


        internal void setVibranceWindowsLevel(int vibranceWindowsLevel)
        {
            this.amdViewModel.VibranceSettingsViewModel.Model.WindowsVibranceLevel = vibranceWindowsLevel;
        }

        internal void setVibranceIngameLevel(int vibranceIngameLevel)
        {
            this.amdViewModel.VibranceSettingsViewModel.Model.IngameVibranceLevel = vibranceIngameLevel;
        }

        internal bool unloadLibraryEx()
        {
            return false;
        }
    }
}