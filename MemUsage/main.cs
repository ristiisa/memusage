using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Windows.Forms;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace MemUsage
{
    public partial class main : Form
    {
        [DllImport("psapi.dll", SetLastError = true)]
        static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, uint size);

        [StructLayout(LayoutKind.Sequential, Size = 40)]
        private struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public uint PeakWorkingSetSize;
            public uint WorkingSetSize;
            public uint QuotaPeakPagedPoolUsage;
            public uint QuotaPagedPoolUsage;
            public uint QuotaPeakNonPagedPoolUsage;
            public uint QuotaNonPagedPoolUsage;
            public uint PagefileUsage;
            public uint PeakPagefileUsage;
        }

        public class Item<T>
        {
            public string Text { get; set; }
            public T Value { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }

        PlotModel model;
        private Dictionary<FieldInfo, LineSeries> series;

        public main()
        {
            InitializeComponent();

            model = new PlotModel {
                PlotType = PlotType.XY,
                Background = OxyColors.White,
                LegendBackground = OxyColor.FromAColor(140, OxyColors.WhiteSmoke),
                LegendBorder = OxyColors.Black
            };

            model.Axes.Add(new LinearAxis
            {
                Key = "xAxis",
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dash,
                Title = "Time (s)"
            });

            model.Axes.Add(new LinearAxis
            {
                Key = "yAxis1",
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dash,
                Title = "Memory Usage (MiB)"
            });

            series = new Dictionary<FieldInfo, LineSeries>();
            var t = typeof(PROCESS_MEMORY_COUNTERS);
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if(f.Name == "cb") continue;
                var line = new LineSeries { Title = f.Name };
                model.Series.Add(line);
                series.Add(f, line);
            }

            plot.Model = model;
        }

        int n = 0;
        private bool Paused;
        private Item<Process> SelectedItem;

        private unsafe void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (SelectedItem == null || !Paused) return;

                var p = Process.GetProcesses().Where(process => process.Id == SelectedItem.Value.Id);

                if (!p.Any()) {
                    p = Process.GetProcesses().Where(process => process.ProcessName == SelectedItem.Value.ProcessName);
                }

                if (!p.Any()) return;

                var i = new PROCESS_MEMORY_COUNTERS {cb = (uint) sizeof (PROCESS_MEMORY_COUNTERS)};
                if (!GetProcessMemoryInfo(p.Single().Handle, out i, i.cb)) return;

                foreach (var s in series)
                {
                    var v = (uint)s.Key.GetValue(i);
                    s.Value.Points.Add(new DataPoint(n / 100.0, v / (1024.0 * 1024.0)));
                }
                n++;

                plot.InvalidatePlot(true);
            } catch(Exception ex)
            {}
        }

        private void plot_Click(object sender, EventArgs e)
        {

        }

        private void LoadProcesses(object sender, EventArgs e)
        {
            var previous = comboBox1.SelectedItem as Item<Process>;
            var ilist = new List<Item<Process>>();

            object selected = null;
            comboBox1.Items.Clear();

            var wmiQueryString = "SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process";
            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            using (var results = searcher.Get())
            {
                var query = from p in Process.GetProcesses()
                            join mo in results.Cast<ManagementObject>()
                            on p.Id equals (int)(uint)mo["ProcessId"]
                            select new
                            {
                                Process = p,
                                Path = (string)mo["ExecutablePath"],
                                CommandLine = (string)mo["CommandLine"],
                            };
                
                foreach (var p in query)
                {
                    ilist.Add(new Item<Process> { Text = string.Format("{0} ({1} {2})", p.Process.ProcessName, p.Path, p.Process.Id), Value = p.Process });
                    if (previous != null && p.Process.Id == previous.Value.Id)
                    {
                        selected = ilist.Last();
                    }
                }

                comboBox1.Items.AddRange(ilist.ToArray());
                
                if (selected == null && previous != null) {
                    selected = ilist.Single(p => p.Value.ProcessName == previous.Value.ProcessName);
                }

                comboBox1.SelectedItem = selected;
            }
        }

        private void main_Load(object sender, EventArgs e)
        {
            LoadProcesses(sender, e);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem == null) return;

            Paused = !Paused;
            button2.Image = !Paused ? Properties.Resources.play128 : Properties.Resources.pause41;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            foreach (var s in series)
                s.Value.Points.Clear();

            n = 0;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedItem = comboBox1.SelectedItem as Item<Process>;
        }
    }
}
