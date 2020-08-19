using DWS.lang;
using DWS.lib;
using DWS.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DWS
{
    public sealed partial class MainDwsForm : Form
    {
        private const string LogFileName = "DWS.log";
        // ReSharper disable once InconsistentNaming
        public const int WM_NCLBUTTONDOWN = 0xA1;
        // ReSharper disable once InconsistentNaming
        public const int HT_CAPTION = 0x2;
        // ReSharper disable once InconsistentNaming
        private const int CS_DROPSHADOW = 0x00020000;
        private readonly string _systemPath = Path.GetPathRoot(Environment.SystemDirectory);
        private bool _destroyFlag;
        private readonly List<string> _errorsList = new List<string>();
        private int _fatalErrors;
        private ResourceManager _rm;
        private string _system32Location;
        private bool _win10 = true;

        public MainDwsForm(string[] args)
        {
            InitializeComponent();
            DoubleBuffered = true;
            // Re create log file
            RecreateLogFile(LogFileName);
            // Check windows version
            CheckWindowsVersion();
            //Check SYSNATIVE (x64)
            _SetShellSys32Path();
            ProfessionalModeSet(false);
            CheckEnableOrDisableUac();
            /*
             * Get icon
             */
            try
            {
                Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
                Text += @"Final Vesion";
            }
            catch
            {
                _OutPut("Error get icon or version.", LogLevel.Error);
            }
            SetLanguage(_GetLang(args)); // set language
            ChangeLanguage(); // change language
            StealthMode(args); //check args
            new Thread(AnimateBackground).Start(); // animate border (new thread)
            //new Thread(AutoUpdate).Start(); // auto update
        }
        /*
        void AutoUpdate()
        {
            try
            {
                var assemblyInfo =
                        new WebClient().DownloadString(
                    $"http://raw.githubusercontent.com/Nummer/Destroy-Windows-10-Spying/master/DWS/Properties/AssemblyInfo.cs?rnd={new Random().Next(0, 9999999)}");
                var readText = assemblyInfo.Split('\n');
                var versionInfoLines = readText.Where(t => t.Contains("[assembly: AssemblyFileVersion"));
                var version = "";
                foreach (var item in versionInfoLines)
                {
                    version = item.Substring(item.IndexOf('(') + 2, item.LastIndexOf(')') - item.IndexOf('(') - 3);
                }
                if (version == FileVersionInfo
                        .GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location)
                        .FileVersion) return;
                Invoke(new MethodInvoker(() => { Enabled = false; }));
                MessageBox.Show(string.Format(Resources.MainDwsForm_AutoUpdate1_, version), @"Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Process.Start("https://github.com/Nummer/Destroy-Windows-10-Spying/releases/latest");
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Resources.MainDwsForm_AutoUpdate_, ex.Message), @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }*/

        public override string Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                CaptionWindow.Text = value;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        /* 
         * Feature animation is calculated borders.
         */

        private void AnimateBackground()
        {
            try
            {
                while (true)
                {
                    for (float i = 0; i < 1f; i += 0.01f)
                    {
                        Thread.Sleep(50); // thread slleep. Too fast.
                        ChangeBorderColor(Rainbow(i)); //call change border function
                    }
                }
            }
            catch (Exception ex)
            {
                _OutPut($"Error in AnimateBackground: {ex.Message}", LogLevel.Debug);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            /*
             * Check windows system restore Enabled status
             */
            if (WindowsUtil.SystemRestore_Status() == 0) _OutPut("Windows Restore DISABLE", LogLevel.Warning);
        }

        /*
         * Check function arguments
         */

        private void StealthMode(IEnumerable<string> args)
        {
            var currentArgs = args as string[] ?? args.ToArray();
            foreach (var currentArg in currentArgs)
            {
                if (currentArg.IndexOf("/deleteapp=", StringComparison.Ordinal) > -1)
                {
                    DeleteWindows10MetroApp(currentArg.Replace("/deleteapp=", null));
                }
                if (currentArg.IndexOf("/destroy", StringComparison.Ordinal) > -1)
                {
                    WindowState = FormWindowState.Minimized;
                    ShowInTaskbar = false;
                    _destroyFlag = true;
                    //Windows 10
                    if (_win10) DestroyWindowsSpyingMainThread();
                    else Dws78MainThread();
                }
                if (currentArg.IndexOf("/deleteonedrive", StringComparison.Ordinal) > -1)
                {
                    DeleteOneDrive();
                }
            }
            if (currentArgs.Any())
                Process.GetCurrentProcess().Kill();
        }

        /*
         * Constant
         */

        private void _SetShellSys32Path()
        {
            if (File.Exists(_systemPath + @"Windows\Sysnative\cmd.exe"))
            {
                _system32Location = _systemPath + @"Windows\Sysnative\";
            }
            else
            {
                _system32Location = _systemPath + @"Windows\System32\";
            }
        }

        /*
         * Check windows build number (win 10 or 7/8)
         */

        private void CheckWindowsVersion()
        {
            var windowsBuildNumber = WindowsUtil.GetWindowsBuildNumber();
            if (windowsBuildNumber < 7600)
            {
                if (MessageBox.Show(@"Minimum windows version - 7\nExit from the program?", @"Error",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error) == DialogResult.Yes)
                    Process.GetCurrentProcess().Kill();
            }
            // check Win 7 or 8.1
            if (windowsBuildNumber >= 10000) return;
            _win10 = false;
            Windows78Panel.Enabled = true;
            Windows78Panel.Visible = true;
            Win10SettingsPanel.Enabled = false;
            Win10SettingsPanel.Visible = false;
            checkBoxDeleteWindows10Apps.Enabled = false;
            btnDestroyWindowsSpying.Visible = false;
            btnDestroyWindows78Spy.Visible = true;
            for (var i = 0; i < _updatesnumberlist.Length; i++)
            {
                checkedListBoxUpdatesW78.Items.Add("KB" + _updatesnumberlist[i]);
                checkedListBoxUpdatesW78.SetItemChecked(i, true);
            }
            //------------------------------------------
        }

        private void CheckEnableOrDisableUac()
        {
            if (WindowsUtil.UAC_Status())
            {
                btnEnableUac.Enabled = true;
                btnDisableUac.Enabled = false;
            }
            else
            {
                btnEnableUac.Enabled = false;
                btnDisableUac.Enabled = true;
            }

            if (WindowsUtil.SystemRestore_Status() != 0) return;
            checkBoxCreateSystemRestorePoint.Checked = false;
            checkBoxCreateSystemRestorePoint.Enabled = false;
        }

        private void btnDestroyWindowsSpying_Click(object sender, EventArgs e)
        {
            StartDestroyWindowsSpying();
        }

        private void Progressbaradd(int numberadd)
        {
            try
            {
                Invoke(new MethodInvoker(delegate
                {
                    try
                    {
                        ProgressBarStatus.Value += numberadd;
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }));
            }
            catch (Exception)
            {
                try
                {
                    ProgressBarStatus.Value += numberadd;
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        private void EnableOrDisableTab(bool enableordisable)
        {
            try
            {
                Invoke(new MethodInvoker(delegate
                {
                    ControlBox = enableordisable;
                    _CloseButton.Enabled = enableordisable;
                    btnDestroyWindowsSpying.Enabled = enableordisable;
                    tabPageSettings.Enabled = enableordisable;
                    tabPageUtilites.Enabled = enableordisable;
                }));
            }
            catch (Exception)
            {
                ControlBox = enableordisable;
                tabPageMain.Enabled = enableordisable;
                tabPageSettings.Enabled = enableordisable;
                tabPageUtilites.Enabled = enableordisable;
            }
        }

        public void RecreateLogFile(string logfilename)
        {
            try
            {
                if (!File.Exists(logfilename))
                {
                    File.Create(logfilename).Close();
                }
                else
                {
                    File.Delete(logfilename);
                    File.Create(logfilename).Close();
                }
            }
            catch (Exception ex)
            {
                _OutPut(ex.Message, LogLevel.Debug);
            }
        }

        public void DeleteFile(string filepath)
        {
            RunCmd($"/c del /F /Q \"{filepath}\"");
        }

        public void RunCmd(string args)
        {
            ProcStartargs(Paths.ShellCmdLocation, args);
        }

        private void ProcStartargs(string name, string args)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = name,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.GetEncoding(866)
                    }
                };
                proc.Start();
                string line = null;
                while (!proc.StandardOutput.EndOfStream)
                {
                    line += Environment.NewLine + proc.StandardOutput.ReadLine();
                }
                proc.WaitForExit();
                _OutPut($"Start: {name} {args}{Environment.NewLine}Output: {line}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _OutPut($"Error start prog {name} {args}", LogLevel.Error);
                _OutPut(ex.Message, LogLevel.Debug);
                _fatalErrors++;
                _errorsList.Add($"Error start prog {name} {args}");
            }
        }

        private static void CreateRestorePoint(string description)
        {
            var oScope = new ManagementScope("\\\\localhost\\root\\default");
            var oPath = new ManagementPath("SystemRestore");
            var oGetOp = new ObjectGetOptions();
            var oProcess = new ManagementClass(oScope, oPath, oGetOp);

            var oInParams =
                oProcess.GetMethodParameters("CreateRestorePoint");
            oInParams["Description"] = description;
            oInParams["RestorePointType"] = 12; // MODIFY_SETTINGS
            oInParams["EventType"] = 100;

            oProcess.InvokeMethod("CreateRestorePoint", oInParams, null);
        }

        private static string GetWindowsBuildVersion()
        {
            // в value массив из байт
            var value = $"\r\nWindows Version: {WindowsUtil.GetProductName()}\r\nBuild: {WindowsUtil.GetSystemBuild()}";
            return value;
        }

        private void DeleteWindows10MetroApp(string appname)
        {
            ProcStartargs("powershell",
                $"-command \"Get-AppxPackage *{appname}* | Remove-AppxPackage\"");
        }

        private void StartDestroyWindowsSpying()
        {
            _errorsList.Clear();
            _fatalErrors = 0;
            EnableOrDisableTab(false);
            SetCompleteText(true);
            _OutPut($"Starting: {DateTime.Now}.");
            _OutPut(GetWindowsBuildVersion());
            _OutPutSplit();
            ProgressBarStatus.Value = 0;
            new Thread(DestroyWindowsSpyingMainThread).Start();
        }

        private void DisableSpyServices(string serviceName)
        {
            new Thread(() =>
            {
                RunCmd($"/c net stop {serviceName}");
                ProcStartargs("powershell", $"-command \"Set-Service -Name {serviceName} -StartupType Disabled\"");
                _OutPut($"Disable {serviceName} service");
            }).Start();
        }

        private void DestroyWindowsSpyingMainThread()
        {
            if (checkBoxCreateSystemRestorePoint.Checked)
            {
                try
                {
                    var restorepointName = $"DestroyWindowsSpying {DateTime.Now}";
                    _OutPut($"Creating restore point {restorepointName}...");
                    CreateRestorePoint(restorepointName);
                    _OutPut($"Restore point {restorepointName} created.");
                }
                catch (Exception ex)
                {
                    _OutPut("Error creating restore point.");
                    _OutPut(ex.Message, LogLevel.Debug);
                }
            }
            Progressbaradd(10);
            if (checkBoxKeyLoggerAndTelemetry.Checked)
            {
                // DISABLE TELEMETRY
                _OutPut("Disable telemetry...");
                DigTrackFullRemove();
                // DELETE KEYLOGGER
                _OutPut("Delete keylogger...");
                RunCmd(
                    "/c reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v \"AllowCortana\" /t REG_DWORD /d 0 /f ");
                // disable Cortana;
                _OutPut("Cortana disable #1");

                var servicesList = new List<string>(new[]
                {
                    "DcpSvc", // Data Collection and Publishing Service
                    "diagnosticshub.standardcollector.service", // Microsoft (R) Diagnostics Hub Standard Collector Service
                    "DiagTrack", // Diagnostics Tracking Service
                    "SensrSvc", // Monitors Various Sensors
                    "dmwappushservice", // WAP Push Message Routing Service
                    "lfsvc", // Geolocation Service
                    "MapsBroker", // Downloaded Maps Manager
                    "NetTcpPortSharing", // Net.Tcp Port Sharing Service
                    "RemoteAccess", // Routing and Remote Access
                    "SharedAccess", // Internet Connection Sharing (ICS)
                    "TrkWks", // Distributed Link Tracking Client
                    "WbioSrvc", // Windows Biometric Service
                    "WMPNetworkSvc", // Windows Media Player Network Sharing Service
                    "XblAuthManager", // Xbox Live Auth Manager
                    "XblGameSave", // Xbox Live Game Save Service
                    "XboxNetApiSvc", // Xbox Live Networking Service
                    "HomeGroupListener", // HomeGroup Listener
                    "HomeGroupProvider", // HomeGroup Provider
                    "bthserv", // Bluetooth Support Service
                    "wscsvc", // Security Center Service
                    "OneSyncSvc", // Sync Host Service
                    "AeLookupSvc", // Application Experience Service
                    "PcaSvc", // Program Compatibility Assistant
                    "WinHttpAutoProxySvc", // WinHTTP Web Proxy Auto-Discovery
                    "UPNPHOST", // Universal Plug & Play Host
                    "ERSVC", // Error Reporting Service
                    "WERSVC", // Windows Error Reporting Service
                    "SSDPSRV", // SSDP Discovery Service
                    "CDPSvc", // Connected Devices Platform Service
                    "DsSvc", // Data Sharing Service
                    "DcpSvc", // Data Collection and Publishing Service
                    "lfsvc" // Geolocation service
                });
                foreach (var service in servicesList)
                {
                    DisableSpyServices(service);
                }


            }
            Progressbaradd(15); //25
            if (checkBoxAddToHosts.Checked)
            {
                AddToHostsAndFirewall();
            }
            Progressbaradd(20); //45
            if (checkBoxDisablePrivateSettings.Checked)
            {
                string[] regkeyvalandother =
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{21157C1F-2651-4CC1-90CA-1F28B02263F6}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{2EEF81BE-33FA-4800-9670-1CD474972C3F}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{7D7E8402-7C54-4821-A34E-AEEFD62DED93}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{992AFA70-6F47-4148-B3E9-3003349C1548}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{9D9E0118-1807-4F2E-96E4-2CE57142E196}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{A8804298-2D5F-42E3-9531-9C8C39EB29CE}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{B19F89AF-E3EB-444B-8DEA-202575A71599}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{C1D23ACC-752B-43E5-8448-8D0E519CD6D6}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{D89823BA-7180-4B81-B50C-7E471E6121A3}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{E5323777-F976-4f5b-9B55-B94699C46E44}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{E6AD100E-5F4E-44CD-BE0F-2265D88D14F5}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\{E83AF229-8640-4D18-A213-E22675EBB2C3}",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeviceAccess\Global\LooselyCoupled"
                };
                foreach (var currentRegKey in regkeyvalandother)
                {
                    SetRegValueHkcu(currentRegKey, "Value", "Deny", RegistryValueKind.String);
                }
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "CortanaEnabled", "0",
                    RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", "1",
                    RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", "1",
                    RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", "0",
                    RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", "1",
                    RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                    "DisableWindowsLocationProvider", "1", RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocationScripting",
                    "1", RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableSensors", "1",
                    RegistryValueKind.DWord);
                SetRegValueHklm(@"SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration", "Status", "0",
                    RegistryValueKind.DWord);
                SetRegValueHklm(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}",
                    "SensorPermissionState", "0", RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", "0", RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds", "0", RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", "0",
                    RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\TabletPC", "PreventHandwritingDataSharing", "1",
                    RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\HandwritingErrorReports",
                    "PreventHandwritingErrorReports", "1", RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableInventory", "1",
                    RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\Personalization", "NoLockScreenCamera", "1",
                    RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", "0",
                    RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", "0",
                    RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Input\TIPC", "Enabled", "0", RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Biometrics", "Enabled", "0", RegistryValueKind.DWord);
                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\CredUI", "DisablePasswordReveal", "1",
                    RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync", "SyncPolicy", "5",
                    RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync\Groups\Personalization",
                    "Enabled", "0", RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync\Groups\BrowserSettings",
                    "Enabled", "0", RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync\Groups\Credentials", "Enabled",
                    "0", RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync\Groups\Language", "Enabled", "0",
                    RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync\Groups\Accessibility", "Enabled",
                    "0", RegistryValueKind.DWord);
                SetRegValueHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync\Groups\Windows", "Enabled", "0",
                    RegistryValueKind.DWord);
                _OutPut("Disable private settings");
            }
            Progressbaradd(10); //55
            if (checkBoxDisableWindowsDefender.Checked)
            {
                try
                {
                    SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", "1",
                        RegistryValueKind.DWord);
                    SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet", "SpyNetReporting", "0",
                        RegistryValueKind.DWord);
                    SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet", "SubmitSamplesConsent", "2",
                        RegistryValueKind.DWord);
                    SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\MRT", "DontReportInfectionInformation", "1",
                        RegistryValueKind.DWord);
                    _OutPut("Disable Windows Defender.");
                    SetRegValueHklm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "Off",
                        RegistryValueKind.String);
                    _OutPut("Disable smart screen.");
                }
                catch (Exception ex)
                {
                    _OutPut("Error disable windows Defender or Smart Screen", LogLevel.Error);
#if DEBUG
                    _OutPut(ex.Message, LogLevel.Debug);
#endif
                    _fatalErrors++;
                    _errorsList.Add($"Error disable Windows Defender or Smart Screen. Message: {ex.Message}");
                }
            }
            Progressbaradd(5); //60
            if (checkBoxSetDefaultPhoto.Checked)
            {
                SetRegValueHkcu(@"Software\Classes\.ico", null, "PhotoViewer.FileAssoc.Tiff", RegistryValueKind.String);
                SetRegValueHkcu(@"Software\Classes\.tiff", null, "PhotoViewer.FileAssoc.Tiff", RegistryValueKind.String);
                SetRegValueHkcu(@"Software\Classes\.bmp", null, "PhotoViewer.FileAssoc.Tiff", RegistryValueKind.String);
                SetRegValueHkcu(@"Software\Classes\.png", null, "PhotoViewer.FileAssoc.Tiff", RegistryValueKind.String);
                SetRegValueHkcu(@"Software\Classes\.gif", null, "PhotoViewer.FileAssoc.Tiff", RegistryValueKind.String);
                SetRegValueHkcu(@"Software\Classes\.jpeg", null, "PhotoViewer.FileAssoc.Tiff", RegistryValueKind.String);
                SetRegValueHkcu(@"Software\Classes\.jpg", null, "PhotoViewer.FileAssoc.Tiff", RegistryValueKind.String);
                _OutPut("Set Default PhotoViewer");
            }
            Progressbaradd(10); //70
            if (checkBoxSPYTasks.Checked)
            {
                DisableSpyingTasks();
            }
            Progressbaradd(10); //80
            if (checkBoxDeleteWindows10Apps.Checked)
            {
                RemoveWindows10Apps();
            }
            Progressbaradd(20); //100
            EnableOrDisableTab(true);
            try
            {
                Invoke(new MethodInvoker(delegate { SetCompleteText(); }));
            }
            catch (Exception)
            {
                try
                {
                    SetCompleteText();
                }
                catch (Exception ex)
                {
                    _OutPut(ex.Message, LogLevel.Debug);
                }
            }
        }

        private void DigTrackFullRemove()
        {
            RunCmd("/c net stop DiagTrack ");
            RunCmd("/c net stop diagnosticshub.standardcollector.service ");
            RunCmd("/c net stop dmwappushservice ");
            RunCmd("/c net stop WMPNetworkSvc ");
            RunCmd("/c sc config DiagTrack start=disabled ");
            RunCmd("/c sc config diagnosticshub.standardcollector.service start=disabled ");
            RunCmd("/c sc config dmwappushservice start=disabled ");
            RunCmd("/c sc config WMPNetworkSvc start=disabled ");
            RunCmd(
                "/c REG ADD HKLM\\SYSTEM\\ControlSet001\\Control\\WMI\\AutoLogger\\AutoLogger-Diagtrack-Listener /v Start /t REG_DWORD /d 0 /f");
            RunCmd("/c sc delete dmwappushsvc");
            RunCmd("/c sc delete \"Diagnostics Tracking Service\"");
            RunCmd("/c sc delete diagtrack");
            RunCmd("/c reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\FirewallRules\"  /v \"{60E6D465-398E-4850-BE86-7EF7620A2377}\" /t REG_SZ /d  \"v2.24|Action=Block|Active=TRUE|Dir=Out|App=C:\\windows\\system32\\svchost.exe|Svc=DiagTrack|Name=Windows  Telemetry|\" /f");
            RunCmd("/c reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\FirewallRules\"  /v \"{2765E0F4-2918-4A46-B9C9-43CDD8FCBA2B}\" /t REG_SZ /d  \"v2.24|Action=Block|Active=TRUE|Dir=Out|App=C:\\windows\\systemapps\\microsoft.windows.cortana_cw5n1h2txyewy\\searchui.exe|Name=Search  and Cortana  application|AppPkgId=S-1-15-2-1861897761-1695161497-2927542615-642690995-327840285-2659745135-2630312742|\"  /f");
            RunCmd(
                "/c reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Device Metadata\" /v \"PreventDeviceMetadataFromNetwork\" /t REG_DWORD /d 1 /f ");
            RunCmd(
                "/c reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection\" /v \"AllowTelemetry\" /t REG_DWORD /d 0 /f ");
            RunCmd(
                "/c reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\MRT\" /v \"DontOfferThroughWUAU\" /t REG_DWORD /d 1 /f ");
            RunCmd(
                "/c reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\SQMClient\\Windows\" /v \"CEIPEnable\" /t REG_DWORD /d 0 /f ");
            RunCmd(
                "/c reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat\" /v \"AITEnable\" /t REG_DWORD /d 0 /f ");
            RunCmd(
                "/c reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat\" /v \"DisableUAR\" /t REG_DWORD /d 1 /f ");
            RunCmd(
                "/c reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v \"AllowTelemetry\" /t REG_DWORD /d 0 /f ");
            RunCmd(
                "/c reg add \"HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\WMI\\AutoLogger\\AutoLogger-Diagtrack-Listener\" /v \"Start\" /t REG_DWORD /d 0 /f ");
            RunCmd(
                "/c reg add \"HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\WMI\\AutoLogger\\SQMLogger\" /v \"Start\" /t REG_DWORD /d 0 /f ");
            RunCmd(
                "/c reg add \"HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Siuf\\Rules\" /v \"NumberOfSIUFInPeriod\" /t REG_DWORD /d 0 /f ");
            RunCmd(
                "/c reg delete \"HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Siuf\\Rules\" /v \"PeriodInNanoSeconds\" /f ");
        }

        private void SetCompleteText(bool start = false)
        {
            if (_destroyFlag)
                return;
            if (start)
            {
                StatusCommandsLable.Text = @"Destroy Windows 10 Spying";
                StatusCommandsLable.ForeColor = Color.Black;
            }
            else
            {
                if (_fatalErrors == 0)
                {
                    StatusCommandsLable.Text = string.Format(Resources.MainDwsForm_SetCompleteText_Destroy_Windows_10_Spying____0__, GetTranslateText("Complete"));
                    StatusCommandsLable.ForeColor = Color.DarkGreen;
                    if (MessageBox.Show(GetTranslateText("CompleteMSG"), GetTranslateText("Info"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    Process.Start("shutdown.exe", "-r -t 0");
                    Process.GetCurrentProcess().Kill();
                }
                else
                {
                    StatusCommandsLable.Text = string.Format(Resources.MainDwsForm_SetCompleteText_Destroy_Windows_10_Spying___errors___0_, _fatalErrors);
                    StatusCommandsLable.ForeColor = Color.Red;
                    try
                    {
                        var errorsMsg = _errorsList.Aggregate<string, string>(null,
                            (current, errorMsg) => current + (errorMsg + "\r\n"));
                        var errorFilePath = Path.GetTempPath() + @"\errors.log";
                        File.Create(errorFilePath).Close();
                        File.WriteAllText(errorFilePath, errorsMsg);
                        Process.Start(errorFilePath);
                    }
                    catch
                    {
                        // ignored
                    }
                    if (MessageBox.Show(string.Format(GetTranslateText("ErrorMSG"), _fatalErrors),
                        GetTranslateText("Info"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    Process.Start("shutdown.exe", "-r -t 0");
                    Process.GetCurrentProcess().Kill();
                }
            }
        }

        private void DisableSpyingTasks()
        {
            string[] disabletaskslist =
            {
                @"Microsoft\Office\Office 15 Subscription Heartbeat",
                @"Microsoft\Office\Office ClickToRun Service Monitor",
                @"Microsoft\Office\OfficeTelemetry\AgentFallBack2016",
                @"Microsoft\Office\OfficeTelemetry\OfficeTelemetryAgentLogOn2016",
                @"Microsoft\Office\OfficeTelemetryAgentFallBack",
                @"Microsoft\Office\OfficeTelemetryAgentFallBack2016",
                @"Microsoft\Office\OfficeTelemetryAgentLogOn",
                @"Microsoft\Office\OfficeTelemetryAgentLogOn2016",
                @"Microsoft\Windows\AppID\SmartScreenSpecific",
                @"Microsoft\Windows\Application Experience\AitAgent",
                @"Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                @"Microsoft\Windows\Application Experience\ProgramDataUpdater",
                @"Microsoft\Windows\Application Experience\StartupAppTask",
                @"Microsoft\Windows\Autochk\Proxy",
                @"Microsoft\Windows\CloudExperienceHost\CreateObjectTask",
                @"Microsoft\Windows\Customer Experience Improvement Program\BthSQM",
                @"Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                @"Microsoft\Windows\Customer Experience Improvement Program\KernelCeipTask",
                @"Microsoft\Windows\Customer Experience Improvement Program\Uploader",
                @"Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                @"Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
                @"Microsoft\Windows\DiskFootprint\Diagnostics",
                @"Microsoft\Windows\FileHistory\File History (maintenance mode)",
                @"Microsoft\Windows\Maintenance\WinSAT",
                @"Microsoft\Windows\Media Center\ActivateWindowsSearch",
                @"Microsoft\Windows\Media Center\ConfigureInternetTimeService",
                @"Microsoft\Windows\Media Center\DispatchRecoveryTasks",
                @"Microsoft\Windows\Media Center\ehDRMInit",
                @"Microsoft\Windows\Media Center\InstallPlayReady",
                @"Microsoft\Windows\Media Center\mcupdate",
                @"Microsoft\Windows\Media Center\MediaCenterRecoveryTask",
                @"Microsoft\Windows\Media Center\ObjectStoreRecoveryTask",
                @"Microsoft\Windows\Media Center\OCURActivate",
                @"Microsoft\Windows\Media Center\OCURDiscovery",
                @"Microsoft\Windows\Media Center\PBDADiscovery",
                @"Microsoft\Windows\Media Center\PBDADiscoveryW1",
                @"Microsoft\Windows\Media Center\PBDADiscoveryW2",
                @"Microsoft\Windows\Media Center\PvrRecoveryTask",
                @"Microsoft\Windows\Media Center\PvrScheduleTask",
                @"Microsoft\Windows\Media Center\RegisterSearch",
                @"Microsoft\Windows\Media Center\ReindexSearchRoot",
                @"Microsoft\Windows\Media Center\SqlLiteRecoveryTask",
                @"Microsoft\Windows\Media Center\UpdateRecordPath",
                @"Microsoft\Windows\PI\Sqm-Tasks",
                @"Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem",
                @"Microsoft\Windows\Shell\FamilySafetyMonitor",
                @"Microsoft\Windows\Shell\FamilySafetyRefresh",
                @"Microsoft\Windows\Shell\FamilySafetyUpload",
                @"Microsoft\Windows\Windows Error Reporting\QueueReporting"
            };
            foreach (var currentTask in disabletaskslist)
            {
                ProcStartargs("SCHTASKS", $"/Change /TN \"{currentTask}\" /disable");
                _OutPut("Disabled task: " + currentTask);
            }
        }

        private void AddToHostsAndFirewall()
        {
            try
            {
                string[] hostsdomains = {
                    "ars.smartscreen.microsoft.com",
                    "az361816.vo.msecnd.net",
                    "az512334.vo.msecnd.net",
                    "blob.weather.microsoft.com",
                    "candycrushsoda.king.com",
                    "cdn.content.prod.cms.msn.com",
                    "cdn.onenote.net",
                    "choice.microsoft.com",
                    "choice.microsoft.com.nsatc.net",
                    "client.wns.windows.com",
                    "client-s.gateway.messenger.live.com",
                    "clientconfig.passport.net",
                    "deploy.static.akamaitechnologies.com",
                    "device.auth.xboxlive.com",
                    "dmd.metaservices.microsoft.com",
                    "dns.msftncsi.com",
                    "feedback.microsoft-hohm.com",
                    "feedback.search.microsoft.com",
                    "feedback.windows.com",
                    "img-s-msn-com.akamaized.net",
                    "insiderppe.cloudapp.net",
                    "licensing.mp.microsoft.com",
                    "mediaredirect.microsoft.com",
                    "msftncsi.com",
                    "officeclient.microsoft.com",
                    "oneclient.sfx.ms",
                    "pti.store.microsoft.com",
                    "query.prod.cms.rt.microsoft.com",
                    "register.cdpcs.microsoft.com",
                    "s0.2mdn.net",
                    "sO.2mdn.net",
                    "search.msn.com",
                    "settings-ssl.xboxlive.com",
                    "static.2mdn.net",
                    "store-images.s-microsoft.com",
                    "storeedgefd.dsx.mp.microsoft.com",
                    "support.microsoft.com",
                    "tile-service.weather.microsoft.com",
                    "time.windows.com",
                    "tk2.plt.msn.com",
                    "urs.smartscreen.microsoft.com",
                    "wdcp.microsoft.com",
                    "wdcpalt.microsoft.com",
                    "win10-trt.msedge.net",
                    "wscont.apps.microsoft.com",
                    "www.msftconnecttest.com",
                    "www.msftncsi.com",
                    "a-0001.a-msedge.net",
                    "a-0001.dc-msedge.net",
                    "a-0002.a-msedge.net",
                    "a-0003.a-msedge.net",
                    "a-0004.a-msedge.net",
                    "a-0005.a-msedge.net",
                    "a-0006.a-msedge.net",
                    "a-0007.a-msedge.net",
                    "a-0008.a-msedge.net",
                    "a-0009.a-msedge.net",
                    "a-0010.a-msedge.net",
                    "a-0011.a-msedge.net",
                    "a-0012.a-msedge.net",
                    "a-msedge.net",
                    "a.ads1.msn.com",
                    "a.ads2.msads.net",
                    "a.ads2.msn.com",
                    "a.rad.msn.com",
                    "ac3.msn.com",
                    "activity.windows.com",
                    "adnexus.net",
                    "adnxs.com",
                    "ads.msn.com",
                    "ads1.msads.net",
                    "ads1.msn.com",
                    "aidps.atdmt.com",
                    "aka-cdn-ns.adtech.de",
                    "array101-prod.do.dsp.mp.microsoft.com",
                    "array102-prod.do.dsp.mp.microsoft.com",
                    "array103-prod.do.dsp.mp.microsoft.com",
                    "array104-prod.do.dsp.mp.microsoft.com",
                    "array201-prod.do.dsp.mp.microsoft.com",
                    "array202-prod.do.dsp.mp.microsoft.com",
                    "array203-prod.do.dsp.mp.microsoft.com",
                    "array204-prod.do.dsp.mp.microsoft.com",
                    "array401-prod.do.dsp.mp.microsoft.com",
                    "array402-prod.do.dsp.mp.microsoft.com",
                    "array403-prod.do.dsp.mp.microsoft.com",
                    "array404-prod.do.dsp.mp.microsoft.com",
                    "array405-prod.do.dsp.mp.microsoft.com",
                    "array406-prod.do.dsp.mp.microsoft.com",
                    "array407-prod.do.dsp.mp.microsoft.com",
                    "array408-prod.do.dsp.mp.microsoft.com",
                    "b.ads1.msn.com",
                    "b.ads2.msads.net",
                    "b.rad.msn.com",
                    "bingads.microsoft.com",
                    "bl3301-a.1drv.com",
                    "bl3301-c.1drv.com",
                    "bl3301-g.1drv.com",
                    "bn1304-e.1drv.com",
                    "bn1306-a.1drv.com",
                    "bn1306-e.1drv.com",
                    "bn1306-g.1drv.com",
                    "bn2b-cor001.api.p001.1drv.com",
                    "bn2b-cor002.api.p001.1drv.com",
                    "bn3p-cor001.api.p001.1drv.com",
                    "bn2b-cor003.api.p001.1drv.com",
                    "bn2b-cor004.api.p001.1drv.com",
                    "bn2wns1.wns.windows.com",
                    "bn3sch020022328.wns.windows.com",
                    "by3301-a.1drv.com",
                    "by3301-c.1drv.com",
                    "by3301-e.1drv.com",
                    "bs.serving-sys.com",
                    "c.atdmt.com",
                    "c.msn.com",
                    "c-0001.dc-msedge.net",
                    "ca.telemetry.microsoft.com",
                    "cache.datamart.windows.com",
                    "cdn.atdmt.com",
                    "cds1204.lon.llnw.net",
                    "cds1293.lon.llnw.net",
                    "cds20417.lcy.llnw.net",
                    "cds20431.lcy.llnw.net",
                    "cds20450.lcy.llnw.net",
                    "cds20457.lcy.llnw.net",
                    "cds20475.lcy.llnw.net",
                    "cds21244.lon.llnw.net",
                    "cds26.ams9.msecn.net",
                    "cds425.lcy.llnw.net",
                    "cds459.lcy.llnw.net",
                    "cds494.lcy.llnw.net",
                    "cds965.lon.llnw.net",
                    "ch1-cor001.api.p001.1drv.com",
                    "ch1-cor002.api.p001.1drv.com",
                    "ch3301-c.1drv.com",
                    "ch3301-e.1drv.com",
                    "ch3301-g.1drv.com",
                    "ch3302-c.1drv.com",
                    "ch3302-e.1drv.com",
                    "compatexchange.cloudapp.net",
                    "compatexchange1.trafficmanager.net",
                    "continuum.dds.microsoft.com",
                    "corp.sts.microsoft.com",
                    "corpext.msitadfs.glbdns2.microsoft.com",
                    "cp101-prod.do.dsp.mp.microsoft.com",
                    "cp201-prod.do.dsp.mp.microsoft.com",
                    "cp401-prod.do.dsp.mp.microsoft.com",
                    "cs1.wpc.v0cdn.net",
                    "db3aqu.atdmt.com",
                    "db3wns2011111.wns.windows.com",
                    "db5.wns.windows.com",
                    "db5sch101100122.wns.windows.com",
                    "db5sch101100127.wns.windows.com",
                    "db5sch101100831.wns.windows.com",
                    "db5sch101100835.wns.windows.com",
                    "db5sch101100917.wns.windows.com",
                    "db5sch101100925.wns.windows.com",
                    "db5sch101100928.wns.windows.com",
                    "db5sch101100938.wns.windows.com",
                    "db5sch101101001.wns.windows.com",
                    "db5sch101101022.wns.windows.com",
                    "db5sch101101024.wns.windows.com",
                    "db5sch101101031.wns.windows.com",
                    "db5sch101101034.wns.windows.com",
                    "db5sch101101042.wns.windows.com",
                    "db5sch101101044.wns.windows.com",
                    "db5sch101101122.wns.windows.com",
                    "db5sch101101123.wns.windows.com",
                    "db5sch101101125.wns.windows.com",
                    "db5sch101101128.wns.windows.com",
                    "db5sch101101129.wns.windows.com",
                    "db5sch101101133.wns.windows.com",
                    "db5sch101101145.wns.windows.com",
                    "db5sch101101209.wns.windows.com",
                    "db5sch101101221.wns.windows.com",
                    "db5sch101101228.wns.windows.com",
                    "db5sch101101231.wns.windows.com",
                    "db5sch101101237.wns.windows.com",
                    "db5sch101101317.wns.windows.com",
                    "db5sch101101324.wns.windows.com",
                    "db5sch101101329.wns.windows.com",
                    "db5sch101101333.wns.windows.com",
                    "db5sch101101334.wns.windows.com",
                    "db5sch101101338.wns.windows.com",
                    "db5sch101101419.wns.windows.com",
                    "db5sch101101424.wns.windows.com",
                    "db5sch101101426.wns.windows.com",
                    "db5sch101101427.wns.windows.com",
                    "db5sch101101430.wns.windows.com",
                    "db5sch101101445.wns.windows.com",
                    "db5sch101101511.wns.windows.com",
                    "db5sch101101519.wns.windows.com",
                    "db5sch101101529.wns.windows.com",
                    "db5sch101101535.wns.windows.com",
                    "db5sch101101541.wns.windows.com",
                    "db5sch101101543.wns.windows.com",
                    "db5sch101101608.wns.windows.com",
                    "db5sch101101618.wns.windows.com",
                    "db5sch101101629.wns.windows.com",
                    "db5sch101101631.wns.windows.com",
                    "db5sch101101633.wns.windows.com",
                    "db5sch101101640.wns.windows.com",
                    "db5sch101101711.wns.windows.com",
                    "db5sch101101722.wns.windows.com",
                    "db5sch101101739.wns.windows.com",
                    "db5sch101101745.wns.windows.com",
                    "db5sch101101813.wns.windows.com",
                    "db5sch101101820.wns.windows.com",
                    "db5sch101101826.wns.windows.com",
                    "db5sch101101835.wns.windows.com",
                    "db5sch101101837.wns.windows.com",
                    "db5sch101101844.wns.windows.com",
                    "db5sch101101907.wns.windows.com",
                    "db5sch101101914.wns.windows.com",
                    "db5sch101101929.wns.windows.com",
                    "db5sch101101939.wns.windows.com",
                    "db5sch101101941.wns.windows.com",
                    "db5sch101102015.wns.windows.com",
                    "db5sch101102017.wns.windows.com",
                    "db5sch101102019.wns.windows.com",
                    "db5sch101102023.wns.windows.com",
                    "db5sch101102025.wns.windows.com",
                    "db5sch101102032.wns.windows.com",
                    "db5sch101102033.wns.windows.com",
                    "db5sch101110108.wns.windows.com",
                    "db5sch101110109.wns.windows.com",
                    "db5sch101110114.wns.windows.com",
                    "db5sch101110135.wns.windows.com",
                    "db5sch101110142.wns.windows.com",
                    "db5sch101110204.wns.windows.com",
                    "db5sch101110206.wns.windows.com",
                    "db5sch101110214.wns.windows.com",
                    "db5sch101110225.wns.windows.com",
                    "db5sch101110232.wns.windows.com",
                    "db5sch101110245.wns.windows.com",
                    "db5sch101110315.wns.windows.com",
                    "db5sch101110323.wns.windows.com",
                    "db5sch101110325.wns.windows.com",
                    "db5sch101110328.wns.windows.com",
                    "db5sch101110331.wns.windows.com",
                    "db5sch101110341.wns.windows.com",
                    "db5sch101110343.wns.windows.com",
                    "db5sch101110345.wns.windows.com",
                    "db5sch101110403.wns.windows.com",
                    "db5sch101110419.wns.windows.com",
                    "db5sch101110438.wns.windows.com",
                    "db5sch101110442.wns.windows.com",
                    "db5sch101110501.wns.windows.com",
                    "db5sch101110527.wns.windows.com",
                    "db5sch101110533.wns.windows.com",
                    "db5sch101110618.wns.windows.com",
                    "db5sch101110622.wns.windows.com",
                    "db5sch101110624.wns.windows.com",
                    "db5sch101110626.wns.windows.com",
                    "db5sch101110634.wns.windows.com",
                    "db5sch101110705.wns.windows.com",
                    "db5sch101110724.wns.windows.com",
                    "db5sch101110740.wns.windows.com",
                    "db5sch101110810.wns.windows.com",
                    "db5sch101110816.wns.windows.com",
                    "db5sch101110821.wns.windows.com",
                    "db5sch101110822.wns.windows.com",
                    "db5sch101110825.wns.windows.com",
                    "db5sch101110828.wns.windows.com",
                    "db5sch101110835.wns.windows.com",
                    "db5sch101110919.wns.windows.com",
                    "db5sch101110921.wns.windows.com",
                    "db5sch101110923.wns.windows.com",
                    "db5sch101110929.wns.windows.com",
                    "db5sch103081814.wns.windows.com",
                    "db5sch103082011.wns.windows.com",
                    "db5sch103082111.wns.windows.com",
                    "db5sch103082308.wns.windows.com",
                    "db5sch103082406.wns.windows.com",
                    "db5sch103082409.wns.windows.com",
                    "db5sch103082609.wns.windows.com",
                    "db5sch103082611.wns.windows.com",
                    "db5sch103082709.wns.windows.com",
                    "db5sch103082712.wns.windows.com",
                    "db5sch103082806.wns.windows.com",
                    "db5sch103090115.wns.windows.com",
                    "db5sch103090415.wns.windows.com",
                    "db5sch103090513.wns.windows.com",
                    "db5sch103090515.wns.windows.com",
                    "db5sch103090608.wns.windows.com",
                    "db5sch103090806.wns.windows.com",
                    "db5sch103090814.wns.windows.com",
                    "db5sch103090906.wns.windows.com",
                    "db5sch103091011.wns.windows.com",
                    "db5sch103091012.wns.windows.com",
                    "db5sch103091106.wns.windows.com",
                    "db5sch103091108.wns.windows.com",
                    "db5sch103091212.wns.windows.com",
                    "db5sch103091311.wns.windows.com",
                    "db5sch103091414.wns.windows.com",
                    "db5sch103091511.wns.windows.com",
                    "db5sch103091617.wns.windows.com",
                    "db5sch103091715.wns.windows.com",
                    "db5sch103091817.wns.windows.com",
                    "db5sch103091908.wns.windows.com",
                    "db5sch103091911.wns.windows.com",
                    "db5sch103092010.wns.windows.com",
                    "db5sch103092108.wns.windows.com",
                    "db5sch103092109.wns.windows.com",
                    "db5sch103092209.wns.windows.com",
                    "db5sch103092210.wns.windows.com",
                    "db5sch103092509.wns.windows.com",
                    "db5sch103100117.wns.windows.com",
                    "db5sch103100121.wns.windows.com",
                    "db5sch103100221.wns.windows.com",
                    "db5sch103100313.wns.windows.com",
                    "db5sch103100314.wns.windows.com",
                    "db5sch103100510.wns.windows.com",
                    "db5sch103100511.wns.windows.com",
                    "db5sch103100611.wns.windows.com",
                    "db5sch103100712.wns.windows.com",
                    "db5sch103101105.wns.windows.com",
                    "db5sch103101208.wns.windows.com",
                    "db5sch103101212.wns.windows.com",
                    "db5sch103101314.wns.windows.com",
                    "db5sch103101411.wns.windows.com",
                    "db5sch103101413.wns.windows.com",
                    "db5sch103101513.wns.windows.com",
                    "db5sch103101610.wns.windows.com",
                    "db5sch103101611.wns.windows.com",
                    "db5sch103101705.wns.windows.com",
                    "db5sch103101711.wns.windows.com",
                    "db5sch103101909.wns.windows.com",
                    "db5sch103101914.wns.windows.com",
                    "db5sch103102009.wns.windows.com",
                    "db5sch103102112.wns.windows.com",
                    "db5sch103102203.wns.windows.com",
                    "db5sch103102209.wns.windows.com",
                    "db5sch103102310.wns.windows.com",
                    "db5sch103102404.wns.windows.com",
                    "db5sch103102609.wns.windows.com",
                    "db5sch103102610.wns.windows.com",
                    "db5sch103102805.wns.windows.com",
                    "db5wns1d.wns.windows.com",
                    "db6sch102090104.wns.windows.com",
                    "db6sch102090112.wns.windows.com",
                    "db6sch102090116.wns.windows.com",
                    "db6sch102090122.wns.windows.com",
                    "db6sch102090203.wns.windows.com",
                    "db6sch102090206.wns.windows.com",
                    "db6sch102090208.wns.windows.com",
                    "db6sch102090209.wns.windows.com",
                    "db6sch102090211.wns.windows.com",
                    "db6sch102090305.wns.windows.com",
                    "db6sch102090306.wns.windows.com",
                    "db6sch102090308.wns.windows.com",
                    "db6sch102090311.wns.windows.com",
                    "db6sch102090313.wns.windows.com",
                    "db6sch102090410.wns.windows.com",
                    "db6sch102090412.wns.windows.com",
                    "db6sch102090504.wns.windows.com",
                    "db6sch102090510.wns.windows.com",
                    "db6sch102090512.wns.windows.com",
                    "db6sch102090513.wns.windows.com",
                    "db6sch102090514.wns.windows.com",
                    "db6sch102090519.wns.windows.com",
                    "db6sch102090613.wns.windows.com",
                    "db6sch102090619.wns.windows.com",
                    "db6sch102090810.wns.windows.com",
                    "db6sch102090811.wns.windows.com",
                    "db6sch102090902.wns.windows.com",
                    "db6sch102090905.wns.windows.com",
                    "db6sch102090907.wns.windows.com",
                    "db6sch102090908.wns.windows.com",
                    "db6sch102090910.wns.windows.com",
                    "db6sch102090911.wns.windows.com",
                    "db6sch102091003.wns.windows.com",
                    "db6sch102091007.wns.windows.com",
                    "db6sch102091008.wns.windows.com",
                    "db6sch102091009.wns.windows.com",
                    "db6sch102091011.wns.windows.com",
                    "db6sch102091103.wns.windows.com",
                    "db6sch102091105.wns.windows.com",
                    "db6sch102091204.wns.windows.com",
                    "db6sch102091209.wns.windows.com",
                    "db6sch102091305.wns.windows.com",
                    "db6sch102091307.wns.windows.com",
                    "db6sch102091308.wns.windows.com",
                    "db6sch102091309.wns.windows.com",
                    "db6sch102091314.wns.windows.com",
                    "db6sch102091412.wns.windows.com",
                    "db6sch102091503.wns.windows.com",
                    "db6sch102091507.wns.windows.com",
                    "db6sch102091602.wns.windows.com",
                    "db6sch102091603.wns.windows.com",
                    "db6sch102091606.wns.windows.com",
                    "db6sch102091607.wns.windows.com",
                    "dev.virtualearth.net",
                    "df.telemetry.microsoft.com",
                    "disc101-prod.do.dsp.mp.microsoft.com",
                    "disc201-prod.do.dsp.mp.microsoft.com",
                    "disc401-prod.do.dsp.mp.microsoft.com",
                    "diagnostics.support.microsoft.com",
                    "ec.atdmt.com",
                    "ecn.dev.virtualearth.net",
                    "eu.vortex.data.microsoft.com",
                    "flex.msn.com",
                    "fs.microsoft.com",
                    "g.msn.com",
                    "geo-prod.do.dsp.mp.microsoft.com",
                    "geover-prod.do.dsp.mp.microsoft.com",
                    "h1.msn.com",
                    "h2.msn.com",
                    "i-bl6p-cor001.api.p001.1drv.com",
                    "i-by3p-cor001.api.p001.1drv.com",
                    "i-by3p-cor002.api.p001.1drv.com",
                    "i-ch1-cor001.api.p001.1drv.com",
                    "i-ch1-cor002.api.p001.1drv.com",
                    "i-sn2-cor001.api.p001.1drv.com",
                    "i-sn2-cor002.api.p001.1drv.com",
                    "i1.services.social.microsoft.com",
                    "i1.services.social.microsoft.com.nsatc.net",
                    "inference.location.live.net",
                    "kv101-prod.do.dsp.mp.microsoft.com",
                    "kv201-prod.do.dsp.mp.microsoft.com",
                    "kv401-prod.do.dsp.mp.microsoft.com",
                    "lb1.www.ms.akadns.net",
                    "live.rads.msn.com",
                    "ls2web.redmond.corp.microsoft.com",
                    "m.adnxs.com",
                    "mobile.pipe.aria.microsoft.com",
                    "msedge.net",
                    "msntest.serving-sys.com",
                    "oca.telemetry.microsoft.com",
                    "oca.telemetry.microsoft.com.nsatc.net",
                    "pre.footprintpredict.com",
                    "preview.msn.com",
                    "rad.msn.com",
                    "redir.metaservices.microsoft.com",
                    "reports.wes.df.telemetry.microsoft.com",
                    "schemas.microsoft.akadns.net",
                    "secure.adnxs.com",
                    "secure.flashtalking.com",
                    "services.wes.df.telemetry.microsoft.com",
                    "settings-sandbox.data.microsoft.com",
                    "settings-win.data.microsoft.com",
                    "settings-win-ppe.data.microsoft.com",
                    "settings.data.glbdns2.microsoft.com",
                    "settings.data.microsoft.com",
                    "sn3301-c.1drv.com",
                    "sn3301-e.1drv.com",
                    "sn3301-g.1drv.com",
                    "spynet2.microsoft.com",
                    "spynetalt.microsoft.com",
                    "spyneteurope.microsoft.akadns.net",
                    "sqm.df.telemetry.microsoft.com",
                    "sqm.telemetry.microsoft.com",
                    "sqm.telemetry.microsoft.com.nsatc.net",
                    "storecatalogrevocation.storequality.microsoft.com",
                    "survey.watson.microsoft.com",
                    "t0.ssl.ak.dynamic.tiles.virtualearth.net",
                    "t0.ssl.ak.tiles.virtualearth.net",
                    "telecommand.telemetry.microsoft.com",
                    "telecommand.telemetry.microsoft.com.nsatc.net",
                    "telemetry.appex.bing.net",
                    "telemetry.microsoft.com",
                    "telemetry.urs.microsoft.com",
                    "test.activity.windows.com",
                    "tsfe.trafficshaping.dsp.mp.microsoft.com",
                    "v10.vortex-win.data.metron.live.com.nsatc.net",
                    "v10.vortex-win.data.microsoft.com",
                    "version.hybrid.api.here.com",
                    "view.atdmt.com",
                    "vortex-bn2.metron.live.com.nsatc.net",
                    "vortex-cy2.metron.live.com.nsatc.net",
                    "vortex-db5.metron.live.com.nsatc.net",
                    "vortex-hk2.metron.live.com.nsatc.net",
                    "vortex-sandbox.data.microsoft.com",
                    "vortex-win.data.metron.live.com.nsatc.net",
                    "vortex-win.data.microsoft.com",
                    "vortex.data.glbdns2.microsoft.com",
                    "vortex.data.metron.live.com.nsatc.net",
                    "vortex.data.microsoft.com",
                    "watson.microsoft.com",
                    "watson.ppe.telemetry.microsoft.com",
                    "watson.telemetry.microsoft.com",
                    "watson.telemetry.microsoft.com.nsatc.net",
                    "web.vortex.data.microsoft.com",
                    "wes.df.telemetry.microsoft.com",
                    "win10.ipv6.microsoft.com",
                    "win1710.ipv6.microsoft.com",
                    "www.msedge.net"
                };
                var hostslocation = _system32Location + @"drivers\etc\hosts";
                string hosts = null;
                if (File.Exists(hostslocation))
                {
                    hosts = File.ReadAllText(hostslocation);
                    File.SetAttributes(hostslocation, FileAttributes.Normal);
                    DeleteFile(hostslocation);
                }
                File.Create(hostslocation).Close();
                File.WriteAllText(hostslocation, hosts + Environment.NewLine);
                foreach (
                    var currentHostsDomain in
                        hostsdomains.Where(
                            currentHostsDomain =>
                                hosts != null && hosts.IndexOf(currentHostsDomain, StringComparison.Ordinal) == -1))
                {
                    RunCmd($"/c echo 0.0.0.0 {currentHostsDomain} >> \"{hostslocation}\"");
                    _OutPut($"Add to hosts - {currentHostsDomain}");
                }
            }
            catch (Exception ex)
            {
                _errorsList.Add($"Error add to hosts. Message: {ex.Message}");
                _fatalErrors++;
                _OutPut("Error add HOSTS", LogLevel.Error);
#if DEBUG
                _OutPut(ex.Message, LogLevel.Debug);
#endif
            }
            RunCmd("/c ipconfig /flushdns");

            _OutPut("Add hosts MS complete.");
            BlockIpAddr();
        }

        private void RemoveWindows10Apps()
        {
            if (checkBoxDeleteApp3d.Checked)
            {
                DeleteWindows10MetroApp("3d");
                _OutPut("Delete builder 3D");
            }
            if (checkBoxDeleteAppCamera.Checked)
            {
                DeleteWindows10MetroApp("camera");
                _OutPut("Delete Camera");
            }
            if (checkBoxDeleteMailCalendarMaps.Checked)
            {
                DeleteWindows10MetroApp("communi");
                DeleteWindows10MetroApp("maps");
                _OutPut("Delete Mail, Calendar, Maps");
            }
            if (checkBoxDeleteAppBing.Checked)
            {
                DeleteWindows10MetroApp("bing");
                _OutPut("Delete Money, Sports, News and Weather");
            }
            if (checkBoxDeleteAppZune.Checked)
            {
                DeleteWindows10MetroApp("zune");
                _OutPut("Delete Groove Music and Film TV");
            }
            if (checkBoxDeleteAppPeopleOneNote.Checked)
            {
                DeleteWindows10MetroApp("people");
                DeleteWindows10MetroApp("note");
                _OutPut("Delete People and OneNote");
            }
            if (checkBoxDeleteAppPhone.Checked)
            {
                DeleteWindows10MetroApp("phone");
                _OutPut("Delete Phone Companion");
            }
            if (checkBoxDeleteAppPhotos.Checked)
            {
                DeleteWindows10MetroApp("photo");
                _OutPut("Delete Photos");
            }
            if (checkBoxDeleteAppSolit.Checked)
            {
                DeleteWindows10MetroApp("solit");
                _OutPut("Delete Solitaire Collection");
            }
            if (checkBoxDeleteAppVoice.Checked)
            {
                DeleteWindows10MetroApp("soundrec");
                _OutPut("Delete Voice Recorder");
            }
            if (checkBoxDeleteAppXBOX.Checked)
            {
                DeleteWindows10MetroApp("xbox");
                _OutPut("Delete XBOX");
            }
        }

        private void checkBoxDeleteWindows10Apps_CheckedChanged(object sender, EventArgs e)
        {
            checkBoxDeleteApp3d.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteAppBing.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteAppCamera.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteAppPeopleOneNote.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteAppPhone.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteAppPhotos.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteAppSolit.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteAppVoice.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteAppXBOX.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteAppZune.Enabled = checkBoxDeleteWindows10Apps.Checked;
            checkBoxDeleteMailCalendarMaps.Enabled = checkBoxDeleteWindows10Apps.Checked;
        }

        private void btnDeleteAllWindows10Apps_Click(object sender, EventArgs e)
        {
            if (
                MessageBox.Show(GetTranslateText("Really"), GetTranslateText("Question"), MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) ==
                DialogResult.Yes)
            {
                EnableOrDisableTab(false);
                MessageBox.Show(GetTranslateText("PressOkAndWait15"));
                new Thread(() =>
                {
                    DeleteWindows10MetroApp(null);
                    Invoke(new MethodInvoker(delegate
                    {
                        EnableOrDisableTab(true);
                        MessageBox.Show(GetTranslateText("Complete"), GetTranslateText("Info"), MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }));
                }).Start();
            }
            else
            {
                MessageBox.Show(@"=(", @"%(");
            }
        }

        private void btnRestoreSystem_Click(object sender, EventArgs e)
        {
            Process.Start(_system32Location + "rstrui.exe");
        }

        private void btnEnableUac_Click(object sender, EventArgs e)
        {
            SetRegValueHklm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\", "EnableLUA", "1",
                RegistryValueKind.DWord);
            _OutPut("Enable UAC");
            CheckEnableOrDisableUac();
            if (
                MessageBox.Show(GetTranslateText("CompleteMSG"), GetTranslateText("Info"), MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Process.Start("shutdown.exe", "-r -t 0");
            }
        }

        private void btnDisableUac_Click(object sender, EventArgs e)
        {
            SetRegValueHklm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\", "EnableLUA", "0",
                RegistryValueKind.DWord);
            _OutPut("Disable UAC");
            CheckEnableOrDisableUac();
            if (
                MessageBox.Show(GetTranslateText("CompleteMSG"), GetTranslateText("Info"), MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Process.Start("shutdown.exe", "-r -t 0");
            }
        }

        private void btnEnableWindowsUpdate_Click(object sender, EventArgs e)
        {
            ProcStartargs("powershell", "-command \"Set-Service -Name wuauserv -StartupType Automatic\"");
            RunCmd("/c net start wuauserv");
            RunCmd("/c netsh advfirewall firewall delete rule name=\"WindowsUpdateBlock\"");
            _OutPut("Windows Update enabled");
        }

        private void btnDisableWindowsUpdate_Click(object sender, EventArgs e)
        {
            RunCmd("/c net stop wuauserv");
            ProcStartargs("powershell", "-command \"Set-Service -Name wuauserv -StartupType Disabled\"");
            RunCmd("/c netsh advfirewall firewall delete rule name=\"WindowsUpdateBlock\"");
            RunCmd(
                "/c netsh advfirewall firewall add rule name=\"WindowsUpdateBlock\" dir=out interface=any action=block service=wuauserv");

            _OutPut("Windows Update disabled");
        }

        private void btnOpenAndEditHosts_Click(object sender, EventArgs e)
        {
            ProcStartargs("notepad", _system32Location + @"drivers\etc\hosts");
        }

        private void DestroyWindowsSpyingMainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }

        private void DestroyWindowsSpyingMainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://goo.gl/EpFSzj"); //https://twitter.com/nummerok
        }

        private void btnProfessionalMode_Click(object sender, EventArgs e)
        {
            ProfessionalModeSet(btnProfessionalMode.Checked);
            Text = btnProfessionalMode.Checked
                ? $"{Text}  !Professional mode!"
                : Text.Replace("  !Professional mode!", string.Empty);
        }

        private void ProfessionalModeSet(bool enableordisable)
        {
            checkBoxKeyLoggerAndTelemetry.Enabled = enableordisable;
            checkBoxAddToHosts.Enabled = enableordisable;
            checkBoxAddToHosts78.Enabled = enableordisable;
            checkBoxDisablePrivateSettings.Enabled = enableordisable;
            checkBoxDisableWindowsDefender.Enabled = enableordisable;
            checkBoxSetDefaultPhoto.Enabled = enableordisable;
            checkBoxSPYTasks.Enabled = enableordisable;
            checkBoxSPYTasks78.Enabled = enableordisable;
            checkBoxDeleteWindows78Updates.Enabled = enableordisable;
            checkBoxDeleteGWX.Enabled = enableordisable;
            btnDeleteAllWindows10Apps.Enabled = _win10 && enableordisable;
            groupBoxUACEdit.Enabled = enableordisable;
            btnDeleteMetroAppsInfo.Enabled = enableordisable;
            btnDeleteOneDrive.Enabled = enableordisable;
            checkBoxDelKeyloggerTW78.Enabled = enableordisable;
            checkedListBoxUpdatesW78.Enabled = enableordisable && checkBoxDeleteWindows78Updates.Checked;
            if (WindowsUtil.SystemRestore_Status() == 0)
            {
                checkBoxCreateSystemRestorePoint.Checked = false;
                checkBoxCreateSystemRestorePoint.Enabled = false;
            }
            else
            {
                checkBoxCreateSystemRestorePoint.Enabled = enableordisable;
            }
        }

        private void btnDeleteMetroAppsInfo_Click(object sender, EventArgs e)
        {
            MessageBox.Show(@"Delete apps: Calculator, Windows Store, Windows Feedback, and other METRO apps.", @"Info",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnDeleteOneDrive_Click(object sender, EventArgs e)
        {
            new Thread(DeleteOneDrive).Start();
        }

        void DeleteOneDrive()
        {

            EnableOrDisableTab(false);
            try
            {
                RunCmd("/c taskkill /f /im OneDrive.exe > NUL 2>&1");
                RunCmd("/c ping 127.0.0.1 -n 5 > NUL 2>&1");
                if (File.Exists(_systemPath + @"Windows\System32\OneDriveSetup.exe"))
                    ProcStartargs(_systemPath + @"Windows\System32\OneDriveSetup.exe", "/uninstall");
                if (File.Exists(_systemPath + @"Windows\SysWOW64\OneDriveSetup.exe"))
                    ProcStartargs(_systemPath + @"Windows\SysWOW64\OneDriveSetup.exe", "/uninstall");
                RunCmd("/c ping 127.0.0.1 -n 5 > NUL 2>&1");
                RunCmd("/c rd \"%USERPROFILE%\\OneDrive\" /Q /S > NUL 2>&1");
                RunCmd("/c rd \"C:\\OneDriveTemp\" /Q /S > NUL 2>&1");
                RunCmd("/c rd \"%LOCALAPPDATA%\\Microsoft\\OneDrive\" /Q /S > NUL 2>&1");
                RunCmd("/c rd \"%PROGRAMDATA%\\Microsoft OneDrive\" /Q /S > NUL 2>&1");
                RunCmd(
                    "/c REG DELETE \"HKEY_CLASSES_ROOT\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}\" /f > NUL 2>&1");
                RunCmd(
                    "/c REG DELETE \"HKEY_CLASSES_ROOT\\Wow6432Node\\CLSID\\{018D5C66-4533-4307-9B53-224DE2ED1FE6}\" /f > NUL 2>&1");

                SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", "1",
                    RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Invoke(new MethodInvoker(delegate
                {
                    MessageBox.Show(ex.Message, GetTranslateText("Error"), MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }));
#if DEBUG
                    _OutPut(ex.Message, LogLevel.Debug);
#endif
            }
            Invoke(new MethodInvoker(delegate
            {
                MessageBox.Show(GetTranslateText("Complete"), GetTranslateText("Info"), MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }));
            EnableOrDisableTab(true);
        }

        private void linkLabelSourceCode_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/Nummer/Destroy-Windows-10-Spying");
        }

        private void btnRemoveOldFirewallRules_Click(object sender, EventArgs e)
        {
            string[] rulename =
            {
                "MS Spynet block 1",
                "MS Spynet block 2",
                "MS telemetry block 1",
                "MS telemetry block 2",
                "185.13.160.61_Block",
                "184.86.56.12_Block",
                "204.79.197.200_Block" // bing.com
            };
            foreach (var hostname in rulename)
            {
                RunCmd($"/c netsh advfirewall firewall delete rule name=\"{hostname}\"");
            }

            MessageBox.Show(GetTranslateText("Complete"), GetTranslateText("Info"));
        }

        private void comboBoxLanguageSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboBoxLanguageSelect.Text.Split('|')[0].Replace(" ", string.Empty))
            {
                case "ru-RU":
                    _rm = ru_RU.ResourceManager;
                    ChangeLanguage();
                    break;
                case "fa-IR":
                    _rm = fa_IR.ResourceManager;
                    ChangeLanguage();
                    break;
                case "fr-FR":
                    _rm = fr_FR.ResourceManager;
                    ChangeLanguage();
                    break;
                case "es-ES":
                    _rm = es_ES.ResourceManager;
                    ChangeLanguage();
                    break;
                case "pt-BR":
                    _rm = pt_BR.ResourceManager;
                    ChangeLanguage();
                    break;
                case "de-DE":
                    _rm = de_DE.ResourceManager;
                    ChangeLanguage();
                    break;
                case "pl-PL":
                    _rm = pl_PL.ResourceManager;
                    ChangeLanguage();
                    break;
                case "it-CH":
                    _rm = it_IT.ResourceManager;
                    ChangeLanguage();
                    break;
                case "cs-CZ":
                    _rm = cs_CZ.ResourceManager;
                    ChangeLanguage();
                    break;
                case "zh-CN":
                    _rm = zh_CN.ResourceManager;
                    ChangeLanguage();
                    break;
                case "tr-TR":
                    _rm = tr_TR.ResourceManager;
                    ChangeLanguage();
                    break;
                case "ar-LY":
                    _rm = ar_LY.ResourceManager;
                    ChangeLanguage();
                    break;
                case "nl-NL":
                    _rm = nl_NL.ResourceManager;
                    ChangeLanguage();
                    break;
                case "uk-UA":
                    _rm = uk_UA.ResourceManager;
                    ChangeLanguage();
                    break;
                case "lt-LT":
                    _rm = lt_LT.ResourceManager;
                    ChangeLanguage();
                    break;
                case "ja-JP":
                    _rm = ja_JP.ResourceManager;
                    ChangeLanguage();
                    break;
                default:
                    _rm = en_US.ResourceManager;
                    ChangeLanguage();
                    break;
            }
        }

        private void btnFixRotateScreen_Click(object sender, EventArgs e)
        {
            SetRegValueHklm(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableSensors", "0", RegistryValueKind.DWord);
            MessageBox.Show(GetTranslateText("Complete"), @"Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnDestroyWindows78Spy_Click(object sender, EventArgs e)
        {
            btnDestroyWindows78Spy.Enabled = false;
            _CloseButton.Enabled = false;
            _fatalErrors = 0;
            new Thread(Dws78MainThread).Start();
        }

        private void Dws78MainThread()
        {
            if (checkBoxAddToHosts78.Checked)
            {
                AddToHostsAndFirewall();
            }
            if (checkBoxSPYTasks78.Checked)
            {
                DisableSpyingTasks();
            }
            if (checkBoxDeleteWindows78Updates.Checked)
            {
                DeleteUpdatesWin78();
            }
            if (checkBoxDeleteGWX.Checked)
            {
                GwxDelete();
            }
            if (checkBoxDelKeyloggerTW78.Checked)
            {
                DigTrackFullRemove();
            }
            if (_destroyFlag)
                return;
            Invoke(new MethodInvoker(delegate
            {
                btnDestroyWindows78Spy.Enabled = true;
                _CloseButton.Enabled = true;
                MessageBox.Show(GetTranslateText("Complete"), GetTranslateText("Info"), MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }));
        }

        private void GwxDelete()
        {
            try
            {
                var windowsIdentityUser = WindowsIdentity.GetCurrent();
                var gwxDir = Environment.SystemDirectory + @"\GWX";
                var userName = windowsIdentityUser.Name.Split('\\')[1];
                if (Directory.Exists(gwxDir))
                {
                    RunCmd("/c TASKKILL /F /IM gwx.exe");
                    RunCmd($"/c takeown /f \"{gwxDir}\" /d y");
                    RunCmd($"/c icacls \"{gwxDir}\" /grant {userName}:F /q");
                    RunCmd($"/c rmdir /s /q {gwxDir}");
                    _OutPut("Delete GWX");
                }
                else
                {
                    _OutPut("GWX NOT FOUND", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                _fatalErrors++;
                _OutPut(ex.Message, LogLevel.Error);
            }
        }

        private readonly string[] _updatesnumberlist =
        {
            "2952664",
            "2976978",
            "2990214",
            "3021917",
            "3035583",
            "3042058",
            "3044374",
            "3050265",
            "3065987",
            "3065988",
            "3068708",
            "3075249",
            "3075851",
            "3075853",
            "3080149",
            "3083324",
            "3083325",
            "3083710",
            "3083711",
            "3088195",
            "3093513",
            "3093983",
            "3102810",
            "3112336",
            "971033",
            "976932"
            // THX rgadguard
        };

        private void DeleteUpdatesWin78()
        {
            for (var i = 0; i < checkedListBoxUpdatesW78.Items.Count; i++)
            {
                if (!checkedListBoxUpdatesW78.GetItemChecked(i)) continue;
                var updateNumber = Convert.ToInt32(checkedListBoxUpdatesW78.Items[i].ToString().Replace("KB", null));
                RunCmd($"/c start /wait wusa /uninstall /norestart /quiet /kb:{updateNumber}");
                _OutPut($"Remove and Hide update KB{updateNumber}");
            }
        }


        private void BlockIpAddr()
        {
            string[] ipAddr =
            {
                "104.87.88.177",
                "104.89.242.39",
                "104.96.147.3",
                "111.221.29.177",
                "111.221.29.253",
                "111.221.64.0-111.221.127.255",
                "131.253.34.230",
                "131.253.40.37",
                "131.253.61.100",
                "131.253.61.64",
                "131.253.61.68",
                "131.253.61.96",
                "134.170.115.60",
                "134.170.165.248",
                "134.170.165.253",
                "134.170.185.70",
                "134.170.30.202",
                "137.116.81.24",
                "137.117.235.16",
                "157.55.129.21",
                "157.55.130.0-157.55.130.255",
                "157.55.133.204",
                "157.55.235.0-157.55.235.255",
                "157.55.236.0-157.55.236.255",
                "157.55.240.220",
                "157.55.52.0-157.55.52.255",
                "157.55.56.0-157.55.56.255",
                "157.56.106.189",
                "157.56.121.89",
                "157.56.124.87",
                "157.56.77.148",
                "157.56.77.149",
                "157.56.91.77",
                "157.56.96.54",
                "168.63.108.233",
                "172.230.215.85",
                "191.232.139.2",
                "191.232.139.254",
                "191.232.80.58",
                "191.232.80.62",
                "191.237.208.126",
                "195.138.255.0-195.138.255.255",
                "2.22.61.43",
                "2.22.61.66",
                "204.79.197.200",
                "207.46.101.29",
                "207.46.114.58",
                "207.46.223.94",
                "207.68.166.254",
                "212.30.134.204",
                "212.30.134.205",
                "213.199.179.0-213.199.179.255",
                "23.102.21.4",
                "23.204.68.66",
                "23.205.214.76",
                "23.218.212.69",
                "23.223.20.82",
                "23.36.33.135",
                "23.48.106.243",
                "23.57.101.163",
                "23.57.107.27",
                "23.99.10.11",
                "40.77.226.221",
                "40.77.226.223",
                "52.167.222.147",
                "64.4.23.0-64.4.23.255",
                "64.4.54.22",
                "64.4.6.100",
                "65.39.117.230",
                "65.52.100.11",
                "65.52.100.7",
                "65.52.100.9",
                "65.52.100.91",
                "65.52.100.92",
                "65.52.100.93",
                "65.52.100.94",
                "65.52.108.103",
                "65.52.108.254",
                "65.52.108.29",
                "65.52.108.33",
                "65.55.108.23",
                "65.55.138.114",
                "65.55.138.126",
                "65.55.138.186",
                "65.55.223.0-65.55.223.255",
                "65.55.252.63",
                "65.55.252.71",
                "65.55.252.92",
                "65.55.252.93",
                "65.55.29.238",
                "65.55.39.10",
                "77.67.29.176"
            };
            foreach (var currentIpAddr in ipAddr)
            {
                RunCmd($"/c route -p ADD {currentIpAddr} MASK 255.255.255.255 0.0.0.0");
                RunCmd($"/c route -p change {currentIpAddr} MASK 255.255.255.255 0.0.0.0 if 1");
                RunCmd($"/c netsh advfirewall firewall delete rule name=\"{currentIpAddr}_Block\"");
                RunCmd(
                    string.Format(
                        "/c netsh advfirewall firewall add rule name=\"{0}_Block\" dir=out interface=any action=block remoteip={0}",
                        currentIpAddr));
                _OutPut($"Add Windows Firewall rule: \"{currentIpAddr}_Block\"");
            }
            RunCmd("/c netsh advfirewall firewall delete rule name=\"Explorer.EXE_BLOCK\"");
            RunCmd(
                $"/c netsh advfirewall firewall add rule name=\"Explorer.EXE_BLOCK\" dir=out interface=any action=block program=\"{_systemPath}Windows\\explorer.exe\"");
            RunCmd("/c netsh advfirewall firewall delete rule name=\"WSearch_Block\"");
            RunCmd(
                "/c netsh advfirewall firewall add rule name=\"WSearch_Block\" dir=out interface=any action=block service=WSearch");
            _OutPut("Add Windows Firewall rule: \"WSearch_Block\"");
            _OutPut("Ip list blocked");
        }

        private void btnDisableOfficeUpdate_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                @"Office 2016 may stop working after these actions.
Are you sure?", @"Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
            {
                return;
            }
            try
            {
                var windowsIdentityUser = WindowsIdentity.GetCurrent();
                var userName = windowsIdentityUser.Name.Split('\\')[1];
                MessageBox.Show(GetTranslateText("FindOffice16FileT"), GetTranslateText("Info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                var opnFileDialog = new OpenFileDialog
                {
                    InitialDirectory = @"C:\Program Files\Microsoft Office\root\Office16\",
                    Filter = @"msosync.exe|msosync.exe"
                };
                var officePath = @"C:\Program Files\Microsoft Office\root\Office16\msosync.exe";
                if (opnFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (File.Exists(opnFileDialog.FileName))
                    {
                        officePath = opnFileDialog.FileName;
                    }
                }

                RunCmd("/c TASKKILL /F /IM msosync.exe");
                RunCmd($"/c takeown /f \"{officePath}\" /d y");
                RunCmd($"/c icacls \"{officePath}\" /grant {userName}:F /q");
                var fileOffice = File.ReadAllBytes(officePath);
                fileOffice = StringToByteArray(ByteArrayToString(fileOffice).Replace("68747470", "78747470"));
                // find "http" and replace to "xttp".
                File.WriteAllBytes(officePath, fileOffice);
                _OutPut("Complete");
            }
            catch (Exception ex)
            {
                _OutPut(ex.Message, LogLevel.Error);
            }
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }

        public static string ByteArrayToString(byte[] ba)
        {
            var hex = BitConverter.ToString(ba);
            return hex.Replace("-", string.Empty);
        }

        private void CaptionWindow_Click(object sender, EventArgs e)
        {
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }

        private void MinimizeButton_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void CaptionWindow_MouseDown(object sender, MouseEventArgs e)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        [DllImport("user32.dll")]
        // ReSharper disable once InconsistentNaming
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void CloseButton_MouseEnter(object sender, EventArgs e)
        {
            _closeButtonState = 2;
            _CloseButton.Refresh();
            _CloseButton.BackColor = Color.Azure;
        }

        private void CloseButton_MouseLeave(object sender, EventArgs e)
        {
            _closeButtonState = 0;
            _CloseButton.Refresh();
            _CloseButton.BackColor = Color.White;
        }

        private void MinimizeButton_MouseEnter(object sender, EventArgs e)
        {
            _minimizeButtonState = 2;
            MinimizeButton.Refresh();
            MinimizeButton.BackColor = Color.Azure;
        }

        private void MinimizeButton_MouseLeave(object sender, EventArgs e)
        {
            _minimizeButtonState = 0;
            MinimizeButton.Refresh();
            MinimizeButton.BackColor = Color.White;
        }

        private void MinimizeButton_Paint(object sender, PaintEventArgs e)
        {
            var myCurrentPaint = _minimizeButtonState == 0
                ? Resources.minimize
                : _minimizeButtonState == 1 ? Resources.minimize1 : Resources.minimize2;
            e.Graphics.DrawImage(myCurrentPaint, MinimizeButton.Width - Resources.minimize.Width - 5,
                MinimizeButton.Height - Resources.minimize.Height - 7);
        }

        private void CloseButton_Paint(object sender, PaintEventArgs e)
        {
            var myCurrentPaint = _closeButtonState == 0
                ? Resources.close
                : _closeButtonState == 1 ? Resources.close1 : Resources.close2;
            e.Graphics.DrawImage(myCurrentPaint, _CloseButton.Width - Resources.close.Width - 5,
                _CloseButton.Height - Resources.close.Height - 7);
        }

        private void CaptionWindow_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.FillEllipse(new SolidBrush(Color.WhiteSmoke), 3, 3, 23, 23);
            e.Graphics.DrawImage(Icon.ToBitmap(), 6, 5, 19, 19);
        }

        private void ChangeBorderColor(Color cl)
        {
            try
            {
                Invoke(new MethodInvoker(delegate
                {
                    BorderDown.BackColor = cl;
                    BorderLeft.BackColor = cl;
                    BorderR.BackColor = cl;
                    BorderUP.BackColor = cl;
                }));
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public static Color Rainbow(float progress)
        {
            var div = (Math.Abs(progress % 1) * 6);
            var ascending = (int)((div % 1) * 255);
            var descending = 255 - ascending;

            switch ((int)div)
            {
                case 0:
                    return Color.FromArgb(255, 255, ascending, 0);
                case 1:
                    return Color.FromArgb(255, descending, 255, 0);
                case 2:
                    return Color.FromArgb(255, 0, 255, ascending);
                case 3:
                    return Color.FromArgb(255, 0, descending, 255);
                case 4:
                    return Color.FromArgb(255, ascending, 0, 255);
                default: // case 5:
                    return Color.FromArgb(255, 255, 0, descending);
            }
        }

        private int _closeButtonState;
        private int _minimizeButtonState;

        private void CloseButton_MouseDown(object sender, MouseEventArgs e)
        {
            _closeButtonState = 1;
            _CloseButton.Refresh();
            _CloseButton.BackColor = Color.FromArgb(255, 0, 0);
        }

        private void MinimizeButton_MouseDown(object sender, MouseEventArgs e)
        {
            _minimizeButtonState = 1;
            MinimizeButton.Refresh();
            MinimizeButton.BackColor = Color.CornflowerBlue;
        }

        #region Language

        private string _GetLang(IEnumerable<string> args)
        {
            string languageName = null;
            // check args lang
            foreach (
                var currentArg in args.Where(currentArg => currentArg.IndexOf("/lang=", StringComparison.Ordinal) > -1))
            {
                languageName = currentArg.Replace("/lang=", null);
            }
            return languageName;
        }

        private void SetLanguage(string currentlang = null)
        {
            if (currentlang == null)
            {
                currentlang = CultureInfo.CurrentUICulture.Name.ToLower();
            }
            if (currentlang.IndexOf("ru", StringComparison.Ordinal) > -1)
            {
                _rm = ru_RU.ResourceManager; // change resource language manager.
                comboBoxLanguageSelect.Text = @"ru-RU | Русский"; // set combobox text language.
            }
            else if (currentlang.IndexOf("fa", StringComparison.Ordinal) > -1)
            {
                _rm = fr_FR.ResourceManager;
                comboBoxLanguageSelect.Text = @"fa-IR | Persian";
            }
            else if (currentlang.IndexOf("fr", StringComparison.Ordinal) > -1)
            {
                _rm = fr_FR.ResourceManager;
                comboBoxLanguageSelect.Text = @"fr-FR | French";
            }
            else if (currentlang.IndexOf("es", StringComparison.Ordinal) > -1)
            {
                _rm = es_ES.ResourceManager;
                comboBoxLanguageSelect.Text = @"es-ES | Spanish";
            }
            else if (currentlang.IndexOf("pt", StringComparison.Ordinal) > -1)
            {
                _rm = pt_BR.ResourceManager;
                comboBoxLanguageSelect.Text = @"pt-BR | Portuguese";
            }
            else if (currentlang.IndexOf("de", StringComparison.Ordinal) > -1)
            {
                _rm = de_DE.ResourceManager;
                comboBoxLanguageSelect.Text = @"de-DE | German";
            }
            else if (currentlang.IndexOf("pl", StringComparison.Ordinal) > -1)
            {
                _rm = pl_PL.ResourceManager;
                comboBoxLanguageSelect.Text = @"pl-PL | Polish";
            }
            else if (currentlang.IndexOf("it", StringComparison.Ordinal) > -1)
            {
                _rm = it_IT.ResourceManager;
                comboBoxLanguageSelect.Text = @"it-CH | Italian";
            }
            else if (currentlang.IndexOf("cs", StringComparison.Ordinal) > -1)
            {
                _rm = cs_CZ.ResourceManager;
                comboBoxLanguageSelect.Text = @"cs-CZ | Czech";
            }
            else if (currentlang.IndexOf("cn", StringComparison.Ordinal) > -1)
            {
                _rm = zh_CN.ResourceManager;
                comboBoxLanguageSelect.Text = @"zh-CN | 中文(简体)";
            }
            else if (currentlang.IndexOf("tr", StringComparison.Ordinal) > -1)
            {
                _rm = tr_TR.ResourceManager;
                comboBoxLanguageSelect.Text = @"tr-TR | Turkish";
            }
            else if (currentlang.IndexOf("ar", StringComparison.Ordinal) > -1)
            {
                _rm = ar_LY.ResourceManager;
                comboBoxLanguageSelect.Text = @"ar-LY | Arabic";
            }
            else if (currentlang.IndexOf("nl", StringComparison.Ordinal) > -1)
            {
                _rm = nl_NL.ResourceManager;
                comboBoxLanguageSelect.Text = @"nl-NL | Dutch";
            }
            else if (currentlang.IndexOf("UA", StringComparison.Ordinal) > -1)
            {
                _rm = uk_UA.ResourceManager;
                comboBoxLanguageSelect.Text = @"uk-UA | Українська";
            }
            else if (currentlang.IndexOf("lt", StringComparison.Ordinal) > -1)
            {
                _rm = lt_LT.ResourceManager;
                comboBoxLanguageSelect.Text = @"lt-LT | Lithuanian";
            }
            else if (currentlang.IndexOf("ja", StringComparison.Ordinal) > -1)
            {
                _rm = ja_JP.ResourceManager;
                comboBoxLanguageSelect.Text = @"ja-JP | 日本語";
            }
            else
            {
                _rm = en_US.ResourceManager;
                comboBoxLanguageSelect.Text = @"en-US | English";
            }
        }

        private void ChangeLanguage()
        {
            // Transtale all controls
            ReadmeRichTextBox.Text = GetTranslateText("ReadMeTextBox");
            btnFixRotateScreen.Text = GetTranslateText("btnFixRotateScreen");
            tabPageMain.Text = GetTranslateText("tabPageMain");
            tabPageAbout.Text = GetTranslateText("tabPageAbout");
            tabPageReadMe.Text = GetTranslateText("tabPageReadMe");
            tabPageSettings.Text = GetTranslateText("tabPageSettings");
            tabPageUtilites.Text = GetTranslateText("tabPageUtilites");
            btnDeleteAllWindows10Apps.Text = GetTranslateText("btnDeleteAllWindows10Apps");
            btnDeleteOneDrive.Text = GetTranslateText("btnDeleteOneDrive");
            btnOpenAndEditHosts.Text = GetTranslateText("btnOpenAndEditHosts");
            btnProfessionalMode.Text = GetTranslateText("btnProfessionalMode");
            btnRestoreSystem.Text = GetTranslateText("btnRestoreSystem");
            checkBoxAddToHosts.Text = GetTranslateText("checkBoxAddToHosts");
            checkBoxAddToHosts78.Text = GetTranslateText("checkBoxAddToHosts");
            checkBoxCreateSystemRestorePoint.Text = GetTranslateText("checkBoxCreateSystemRestorePoint");
            checkBoxDeleteWindows10Apps.Text = GetTranslateText("checkBoxDeleteWindows10Apps");
            checkBoxDisablePrivateSettings.Text = GetTranslateText("checkBoxDisablePrivateSettings");
            checkBoxDisableWindowsDefender.Text = GetTranslateText("checkBoxDisableWindowsDefender");
            checkBoxKeyLoggerAndTelemetry.Text = GetTranslateText("checkBoxKeyLoggerAndTelemetry");
            checkBoxDelKeyloggerTW78.Text = GetTranslateText("checkBoxKeyLoggerAndTelemetry");
            checkBoxSetDefaultPhoto.Text = GetTranslateText("checkBoxSetDefaultPhoto");
            checkBoxSPYTasks.Text = GetTranslateText("checkBoxSPYTasks");
            checkBoxSPYTasks78.Text = GetTranslateText("checkBoxSPYTasks");
            checkBoxDeleteWindows78Updates.Text = GetTranslateText("checkBoxDeleteWindows78Updates");
            checkBoxDeleteGWX.Text = GetTranslateText("checkBoxDeleteGWX");
            labelUninstallUpdates.Text = GetTranslateText("labelUninstallUpdates");
            labelInfoDeleteMetroApps.Text = GetTranslateText("labelInfoDeleteMetroApps");
            btnEnableUac.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__UAC, GetTranslateText("Enable"));
            btnDisableUac.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__UAC, GetTranslateText("Disable"));
            btnDisableOfficeUpdate.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Office_2016_Telemetry, GetTranslateText("Disable"));
            btnDisableWindowsUpdate.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Windows_Update, GetTranslateText("Disable"));
            btnEnableWindowsUpdate.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Windows_Update, GetTranslateText("Enable"));
            checkBoxDeleteApp3d.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Builder_3D, GetTranslateText("Delete"));
            checkBoxDeleteAppCamera.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Camera, GetTranslateText("Delete"));
            checkBoxDeleteMailCalendarMaps.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Mail__Calendar__Maps, GetTranslateText("Delete"));
            checkBoxDeleteAppBing.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Money__Sports__News__Weather, GetTranslateText("Delete"));
            checkBoxDeleteAppZune.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Groove_Music__Film_TV, GetTranslateText("Delete"));
            checkBoxDeleteAppPeopleOneNote.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__People__OneNote, GetTranslateText("Delete"));
            checkBoxDeleteAppPhone.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Phone_Companion, GetTranslateText("Delete"));
            checkBoxDeleteAppPhotos.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Photos, GetTranslateText("Delete"));
            checkBoxDeleteAppSolit.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Solitaire_Collection, GetTranslateText("Delete"));
            checkBoxDeleteAppVoice.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__Voice_Recorder, GetTranslateText("Delete"));
            checkBoxDeleteAppXBOX.Text = string.Format(Resources.MainDwsForm_ChangeLanguage__0__XBOX, GetTranslateText("Delete"));
            btnRemoveOldFirewallRules.Text = GetTranslateText("RemoveAllOldFirewallRules");
        }

        private string GetTranslateText(string name)
        {
            try
            {
                var textupdate = _rm.GetString(name);
                if (textupdate != null)
                {
                    return textupdate;
                }
                else
                {
                    return en_US.ResourceManager.GetString(name);
                }
            }
            catch (Exception ex)
            {
                _OutPut($"Error get translate {name}. \nError: {ex.Message}", LogLevel.Debug);
                return en_US.ResourceManager.GetString(name);
            }
        }

        #endregion

        #region Output

        private void LogOutputTextBox_TextChanged(object sender, EventArgs e)
        {
            LogOutputTextBox.SelectionStart = LogOutputTextBox.Text.Length;
            LogOutputTextBox.ScrollToCaret();
        }

        private void _OutPutSplit()
        {
            try
            {
                Invoke(new MethodInvoker(OutPutSplitInvoke));
            }
            catch
            {
                try
                {
                    OutPutSplitInvoke();
                }
                catch (Exception ex)
                {
                    _fatalErrors++;
                    _errorsList.Add($"Error in outputsplit. Message: {ex.Message}");
                }
            }
        }

        private void _OutPut(string str, LogLevel logLevel = LogLevel.Info)
        {
            try
            {
                Invoke(new MethodInvoker(delegate { _OutPutInvoke(str, logLevel); }));
            }
            catch
            {
                try
                {
                    _OutPutInvoke(str, logLevel);
                }
                catch (Exception ex)
                {
                    _fatalErrors++;
                    _errorsList.Add($"Error in output. Message: {ex.Message}");
                }
            }
        }

        private enum LogLevel // thx TRoskop
        {
            Info,
            Warning,
            Error,
            FatalError,
            Debug
        };

        private void _OutPutInvoke(string str, LogLevel logLevel)
        {
            if (logLevel == LogLevel.Debug && string.IsNullOrEmpty(str))
                return;

            switch (logLevel)
            {
                case LogLevel.Info:
                    str = "[INFO] " + str;
                    break;
                case LogLevel.Warning:
                    str = "[WARNING] " + str;
                    break;
                case LogLevel.Error:
                    str = "[ERROR] " + str;
                    break;
                case LogLevel.FatalError:
                    str = "[!! FATAL ERROR !!] " + str;
                    break;
                case LogLevel.Debug:
#if !DEBUG
                    return;
#endif
                    // ReSharper disable once HeuristicUnreachableCode
#pragma warning disable 162
                    str = "[DEBUG] " + str;
                    break;
#pragma warning restore 162
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
            File.WriteAllText(LogFileName, File.ReadAllText(LogFileName) + str + Environment.NewLine);
            Console.WriteLine(str);
            LogOutputTextBox.Text += str + Environment.NewLine;
        }

        private void OutPutSplitInvoke()
        {
            var splittext = $"=========================={Environment.NewLine}";
            File.WriteAllText(LogFileName, File.ReadAllText(LogFileName) + splittext);
            LogOutputTextBox.Text += splittext;
        }

        #endregion

        #region registry

        private void SetRegValueHkcu(string regkeyfolder, string paramname, string paramvalue, RegistryValueKind keytype)
        {
            var registryKey = Registry.CurrentUser.CreateSubKey(regkeyfolder);
            registryKey?.Close();
            var myKey = Registry.CurrentUser.OpenSubKey(regkeyfolder, RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.FullControl);
            try
            {
                myKey?.SetValue(paramname, paramvalue, keytype);
            }
            catch (Exception ex)
            {
                _fatalErrors++;
                _errorsList.Add($"Error SetRegValueHkcu. Message: {ex.Message}");
                _OutPut($"{GetTranslateText("Error")}: {ex.Message}", LogLevel.Error);
            }

            myKey?.Close();
        }

        private void SetRegValueHklm(string regkeyfolder, string paramname, string paramvalue, RegistryValueKind keytype)
        {
            var registryKey = Registry.LocalMachine.CreateSubKey(regkeyfolder);
            registryKey?.Close();
            var myKey = Registry.LocalMachine.OpenSubKey(regkeyfolder,
                RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl);
            try
            {
                myKey?.SetValue(paramname, paramvalue, keytype);
            }
            catch (Exception ex)
            {
                _fatalErrors++;
                _errorsList.Add($"Error SetRegValueHklm. Message: {ex.Message}");
                _OutPut($"{GetTranslateText("Error")}: {ex.Message}", LogLevel.Error);
            }
            myKey?.Close();
        }

        #endregion

        private void checkBoxDeleteWindows78Updates_CheckedChanged(object sender, EventArgs e)
        {
            checkedListBoxUpdatesW78.Enabled = checkBoxDeleteWindows78Updates.Checked;
        }

        private void DestroyWindowsSpyingMainForm_Activated(object sender, EventArgs e)
        {
            CaptionWindow.ForeColor = Color.FromArgb(64, 64, 64);
        }

        private void DestroyWindowsSpyingMainForm_Deactivate(object sender, EventArgs e)
        {
            CaptionWindow.ForeColor = Color.FromArgb(164, 164, 164);
        }

    }
}