using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using LiveSplit.ComponentUtil;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WindowsFirewallHelper;
using System.Collections.Generic;

namespace Auto_Recorder
{
    class Program
    {
        private static MainForm MainForm = new MainForm();
        private static Process game;
        private static Watchers watchers;

        private static void Main()
        {
            Task task = new Task(() => AutoFirewallTask());
            task.Start();
            Application.EnableVisualStyles();
            Application.Run(MainForm);
            AllowOutbound();
        }

        private static void AutoFirewallTask()
        {
            while (true)
            {
                Thread.Sleep(15);

                if (game == null || game.HasExited)
                {
                    if (!HookGameProcess())
                    {
                        AllowOutbound();
                        MainForm.BeginInvoke((MethodInvoker)delegate () { MainForm.label3.Text = "Auto firewall disabled: couldn't connect to the game!"; });
                        MainForm.BeginInvoke((MethodInvoker)delegate () { MainForm.label4.Text = "not enabled"; });
                        continue;
                    }
                }

                watchers.UpdateAll(game);

                MainForm.BeginInvoke((MethodInvoker)delegate () { MainForm.label3.Text = "Auto firewall enabled!"; });

                if (!watchers.SupportedGameVersion)
                {
                    AllowOutbound();
                    MainForm.BeginInvoke((MethodInvoker)delegate () { MainForm.label3.Text = "Auto firewall disabled: unsupported game version!"; });
                    MainForm.BeginInvoke((MethodInvoker)delegate () { MainForm.label4.Text = "not enabled"; });
                    continue;
                }

                if (!watchers.Connected.Changed && !watchers.FirstUpdate)
                    continue;

                if (watchers.Connected.Current)
                {
                    BlockOutbound();
                    MainForm.BeginInvoke((MethodInvoker)delegate () { MainForm.label4.Text = "Connections blocked"; });
                }

                watchers.FirstUpdate = false;
            }
        }

        private static void BlockOutbound()
        {
            var rule = FirewallManager.Instance.CreateApplicationRule(FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public, "HauloFirewall", FirewallAction.Block, watchers.GamePath);
            rule.Direction = FirewallDirection.Outbound;
            FirewallManager.Instance.Rules.Add(rule);
        }

        private static void AllowOutbound()
        {
            var rule = FirewallManager.Instance.Rules.FirstOrDefault(r => r.Name == "HauloFirewall");
            if (rule != null)
                FirewallManager.Instance.Rules.Remove(rule);
        }

             
        private static bool HookGameProcess()
        {
            game = Process.GetProcessesByName("HaloInfinite").OrderByDescending(x => x.StartTime).FirstOrDefault(x => !x.HasExited);
            if (game == null) return false;
            
            try
            {
                watchers = new Watchers(game);
            }
            catch
            {
                game = null;
                return false;
            }
            return true;
        }
    }

    class Watchers : MemoryWatcherList
    {
        public MemoryWatcher<bool> Connected { get; }
        public string GamePath { get; }
        public bool SupportedGameVersion = true;
        public bool FirstUpdate = true;

        public Watchers(Process game)
        {
            var versions = new Dictionary<int, string> {
                { 0x1263000, "v6.10020.17952.0" }, // Season 1
                { 0x133F000, "v6.10020.19048.0" },
                { 0x1262000, "v6.10021.10921.0" },
                { 0x125D000, "v6.10021.11755.0" },
                { 0x17F7000, "v6.10021.12835.0" },
                { 0x1829000, "v6.10021.12835.0" },
                { 0x1827000, "v6.10021.16272.0" },
                { 0x17DE000, "v6.10021.18539.0" }, // Season 2
                { 0x1806000, "v6.10022.10499.0" },
            };
            var ArbiterModuleSize = game.ModulesWow64Safe().FirstOrDefault(x => x.ModuleName == "Arbiter.dll").ModuleMemorySize;
            versions.TryGetValue(ArbiterModuleSize, out var gameVersion);
            this.GamePath = game.MainModuleWow64Safe().FileName;

            switch (gameVersion)
            {
                default:
                    this.SupportedGameVersion = false;
                    break;
                case "v6.10020.17952.0":
                    this.Connected = new MemoryWatcher<bool>(game.MainModuleWow64Safe().BaseAddress + 0x43265B0);
                    break;
                case "v6.10020.19048.0":
                    // this.Connected = new MemoryWatcher<bool>(game.MainModuleWow64Safe().BaseAddress + 0x47F673C);
                    this.Connected = new MemoryWatcher<bool>(game.MainModuleWow64Safe().BaseAddress + 0x4FFDD10);
                    break;
                case "v6.10021.10921.0":
                case "v6.10021.11755.0":
                    this.Connected = new MemoryWatcher<bool>(game.MainModuleWow64Safe().BaseAddress + 0x4327570);
                    break;
                case "v6.10021.12835.0":
                    this.Connected = new MemoryWatcher<bool>(game.MainModuleWow64Safe().BaseAddress + 0x4639C50);
                    break;
                case "v6.10021.16272.0":
                    this.Connected = new MemoryWatcher<bool>(game.MainModuleWow64Safe().BaseAddress + 0x463ED10);
                    break;
                case "v6.10021.18539.0":
                    this.Connected = new MemoryWatcher<bool>(game.MainModuleWow64Safe().BaseAddress + 0x4531C60);
                    break;
                case "v6.10022.10499.0":
                    this.Connected = new MemoryWatcher<bool>(game.MainModuleWow64Safe().BaseAddress + 0x48F6D70);
                    break;
            }
            this.AddRange(this.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(p => !p.GetIndexParameters().Any()).Select(p => p.GetValue(this, null) as MemoryWatcher).Where(p => p != null));
        }
    }

}