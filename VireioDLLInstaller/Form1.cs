using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VireioDLLInstaller
{
    public partial class Form1 : Form
    {

        
        //This needs to link to the list of games that is supported, the C++ code you use references DxProxy/ProxyHelper.h
        public static string[] SupportedGameList = { "vrcmd.exe" };

        //The Files used to Symlink
        private string[] files32 = { "d3d9.dll", "hijackdll.dll", "VRBoost.dll", "libfreespace.dll" };
        private string[] files64 = { "d3d9_64.dll", "hijackdll64.dll", "VRBoost64.dll" };


        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool wow64Process
        );
        /// <summary>
        /// checking to see if the computer is 64bit or 32bit
        /// </summary>
        /// <returns></returns>
        static bool is64BitProcess = (IntPtr.Size == 8);
        public static bool is64BitOperatingSystem = is64BitProcess || InternalCheckIsWow64();
        public static bool InternalCheckIsWow64()
        {
            if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
                Environment.OSVersion.Version.Major >= 6)
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    bool retVal;
                    if (!IsWow64Process(p.Handle, out retVal))
                    {
                        return false;
                    }
                    return retVal;
                }
            }
            else
            {
                return false;
            }
        }

        //Default Directory(setting to common steamapp folder)
        public static string SteamRegPath
        {
            get { return steamRegPath(); }
        }

        private string getSteamPath()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(SteamRegPath, false))
            {
                if (key != null)
                    if (key.GetValue(steamKey, null) != null)
                        return key.GetValue(steamKey).ToString();

                //Steam Not Installed, put them in Program files
                return Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles).ToString();

            }
        }
        //Registry Key for steam
        public static string steamKey = "InstallPath";
        //Directory for common apps
        private static string steamAppPath = "SteamApps\\common";

        //using wow6432node if OS is 64bit
        private static string steamRegPath()
        {
            if (is64BitOperatingSystem)
                return "SOFTWARE\\Wow6432Node\\Valve\\Steam";
            else
                return "SOFTWARE\\Valve\\Steam";
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {            
            //Configuring the folder browser dialog
            folderBrowserDialog1.RootFolder = Environment.SpecialFolder.MyComputer;
            folderBrowserDialog1.ShowNewFolderButton = false;
            folderBrowserDialog1.Description = "Choose the Root directory of the Games you want to play";
            folderBrowserDialog1.SelectedPath = System.IO.Path.Combine(getSteamPath(), steamAppPath);
            listBox1.SelectionMode = SelectionMode.MultiExtended;
            listBox1.Items.Add("Press Browse Directory to begin.");
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            //Form finished loading lets show the folder browser
            browseForGame();
        }

        //use this to select the directory to scan
        private void browseForGame()
        {
            button1.Enabled = false;
            button2.Enabled = false;
            
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                listBox1.Items.Clear();
                foreach (string filePath in Directory.GetFiles(folderBrowserDialog1.SelectedPath, "*.exe", SearchOption.AllDirectories))
                {
                    //checking to see if game is on the supported list
                    if (isGameSupported(Path.GetFileName(filePath)))
                        listBox1.Items.Add(filePath);
                }
            }
            else
                return;
            if (listBox1.Items.Count < 1)           
                listBox1.Items.Add("No Supported games were found in the specified directory.");
            else
            {
                button1.Enabled = true;
                button2.Enabled = true;
            }

        }

        private bool isGameSupported(string fileName)
        {
            foreach (string game in SupportedGameList)
            {
                if (fileName == game)
                    return true;

            }
            return false;
        }

        //button1 installs symlinks
        private void button1_Click(object sender, EventArgs e)
        {

            if (listBox1.SelectedItems.Count < 1)
            {
                MessageBox.Show("Please select a game before pressing Install");
                return;
            }

            if (MessageBox.Show("Are you sure you want to install the Perception DLL's to the following Game(s)?\n\n" + string.Join("\n",listBox1.SelectedItems.Cast<string>()), "Installing...", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
            {
                foreach (string item in listBox1.SelectedItems)
                {
                    workwithFiles(item, true);
                }
            }
        }

        /// <summary>
        /// This method will parse the correct file names and create the symlinks in the selected games directory
        /// </summary>
        /// <param name="item">full path of game</param>
        /// <param name="install">is this an install or uninstall? true=install</param>
        /// <returns>true = success false = fail</returns>
        private bool workwithFiles(string item, bool install)
        {
            string[] files;           
            bool is64bit = CorFlagsReader.ReadAssemblyMetadata(item).ProcessorArchitecture.ToString().Contains("64");
            if (is64bit)
                files = files64;
            else
                files = files32;
            bool askedIfReinstall = false;
            foreach (string file in files)
            {
                string filePath = Path.GetDirectoryName(item);
                if (install)
                {
                    
                    string fullPath;
                    if (file == "d3d9_64.dll")
                        fullPath = Path.Combine(filePath, "d3d9.dll");
                    else fullPath = Path.Combine(filePath, file);
                    if (File.Exists(fullPath))
                    {
                        if(!askedIfReinstall)
                            if (MessageBox.Show("It looks like the DLL's already exist in the game directory, do you want to reinstall?", "Files Already Exist", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                            {
                                return true;
                            }
                        askedIfReinstall = true;
                        File.Delete(fullPath);
                    }
                }

                if (!startHidden(filePath, file, install))
                {
                    MessageBox.Show("There was an error! Please try again");
                    foreach (string delfile in files)
                    {
                        startHidden(filePath, delfile, false);
                    }
                    return false;
                }
            }
            return true;
        }

        
        /// <summary>
        ///start mklink.exe hidden and check for an errorcode 
        /// </summary>
        /// <param name="gamePath">Path to the game directory to install to </param>
        /// <param name="file"> name of the dll to symlink</param>
        /// <param name="install">are we installing the symlink or deleting it</param>
        /// <returns></returns>
        private bool startHidden(string gamePath, string file,bool install)
        {
            string fname = file;
            if (fname == "d3d9_64.dll")
                fname = "d3d9.dll";
            if (install)
            {
                ProcessStartInfo pinfo = new ProcessStartInfo("cmd");
                pinfo.WorkingDirectory = Environment.CurrentDirectory;
                pinfo.RedirectStandardError = true;
                pinfo.RedirectStandardOutput = true;
                pinfo.WindowStyle = ProcessWindowStyle.Hidden;
                pinfo.CreateNoWindow = true;
                pinfo.UseShellExecute = false;
                pinfo.Arguments = string.Format("/c Mklink \"{0}\" \"{1}\"", Path.Combine(gamePath, fname), Path.Combine(Environment.CurrentDirectory, file));

                Process p = Process.Start(pinfo);
                p.WaitForExit();
                if (p.ExitCode != 0)
                    return false;
            }
            else
            {
                try { File.Delete(Path.Combine(gamePath, fname)); } catch{}                
            }
            return true;
        }

        //this is the uninstall button
        private void button2_Click(object sender, EventArgs e)
        {

            //Lets make sure an item is selected on the listbox
            if (listBox1.SelectedItems.Count < 1)
            {
                MessageBox.Show("You need to select a game before pressing Uninstal!");
                return;
            }
            if (MessageBox.Show("Are you sure you want to Uninstall the Perception DLL from the following Game(s)?\n\n" + string.Join("\n", listBox1.SelectedItems.Cast<string>()), "Installing...", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
            {                
                foreach (string item in listBox1.SelectedItems)
                {
                    workwithFiles(item, false);
                }

            }
        }

        //Lets the user select another game
        private void button3_Click(object sender, EventArgs e)
        {
            browseForGame();
        }


        //Would be nice to have the ability to scan the entire hard drive for supported games, but it will take some time
        //This button and below code is disabled and not accessible by the user
        private void button4_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            scanEntirePC = new System.Threading.Thread(scanPCforGames);
            scanEntirePC.IsBackground = true;
            scanEntirePC.Start();
            while (scanEntirePC.IsAlive)
            {
                Application.DoEvents();
                label2.Text = currentFile;
                System.Threading.Thread.Sleep(200);
            }

          
        }
        public System.Threading.Thread scanEntirePC;
        public List<string> games = new List<string>();
        public string currentFile;
        private void scanPCforGames()
        {            
            foreach (DriveInfo drives in System.IO.DriveInfo.GetDrives())
            {
                if (drives.IsReady)
                {
                    foreach (string directory in Directory.GetDirectories(drives.RootDirectory.ToString(),"*",SearchOption.AllDirectories))
                    {
                        try
                        {

                            foreach (string filePath in Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories))
                            {
                                currentFile = filePath;
                                //checking to see if game is on the supported list
                                if (isGameSupported(Path.GetFileName(filePath)))
                                    games.Add(filePath);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }

            
        }

        private void label2_Click(object sender, EventArgs e)
        {
            while (scanEntirePC.IsAlive)
            {
                scanEntirePC.Interrupt();
                scanEntirePC.Abort();
                System.Threading.Thread.Sleep(100);
            }
        }
        //====================================================================================
    }
}

