using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.ExtendedToolkit.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ToggleSwitch = Avalonia.ExtendedToolkit.Controls.ToggleSwitch;

namespace mkvtool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void CheckFileBtn_OnClick(object? sender, RoutedEventArgs e)
        {
            string[] files = await ShowSelectFileDialog("MKV file", new string[] {"mkv"}, false);
            if (files != null && files.Length > 0)
            {
                SetBusy(true);
                new Thread(() =>
                {
                    bool[] result = mkvlib.CheckSubset(files[0], lcb);
                    if (result[1])
                        PrintResult("Check", "Has error.");
                    else if (result[0])
                        PrintResult("Check", "This mkv file are subsetted.");
                    else
                        PrintResult("Check", "This mkv file are not subsetted.");
                    SetBusy(false);
                }).Start();
            }
        }

        private async void CheckFolderBtn_OnClick(object? sender, RoutedEventArgs e)
        {
            string dir = await new OpenFolderDialog().ShowAsync(this);
            if (!string.IsNullOrEmpty(dir))
            {
                SetBusy(true);
                new Thread(() =>
                {
                    string[] list = mkvlib.QueryFolder(dir, lcb);
                    if (list != null && list.Length > 0)
                    {
                        lcb("Not subsetted file list:");
                        lcb(" ----- Begin ----- ");
                        lcb(string.Join(Environment.NewLine, list));
                        lcb(" -----  End  ----- ");
                    }
                    else
                        lcb("All files are subsetted.");

                    SetBusy(false);
                }).Start();
            }
        }

        void lcb(string str)
        {
            DoUIThread(() =>
                this.FindControl<TextBox>("logBox").Text += str + Environment.NewLine);
        }

        private async void TopLevel_OnOpened(object? sender, EventArgs e)
        {
            this.Closing += (_, __) => Environment.Exit(0);
            SetBusy(true);
            new Thread(() =>
            {
                try
                {
                    if (!mkvlib.InitInstance(lcb))
                    {
                        PrintResult("Init", "Failed to init mkvlib.");
                    }
                    else
                    {
                        PrintResult("Init", "Init successfully.");
                        DoUIThread(() => this.FindControl<Grid>("mainBox").IsEnabled = true);
                    }
                }
                catch
                {
                    PrintResult("Init", "Missing mkvlib.");
                }

                SetBusy(false);
            }).Start();
        }

        class SubsetArg
        {
            public static string[] Asses { get; set; }
            public static string Fonts { get; set; }
            public static string Output { get; set; }
            public static bool DirSafe { get; set; }
        }

        private async void SubsetSelectBtns_OnClick(object? sender, RoutedEventArgs e)
        {
            Button btn = (Button) sender;
            string dir;
            switch (btn.Tag.ToString())
            {
                case "asses":
                    SubsetArg.Asses = null;
                    this.FindControl<TextBlock>("sa1").Text = string.Empty;
                    string[] files = await ShowSelectFileDialog("ASS file(s)", new[] {"ass"}, true);
                    if (files != null && files.Length > 0)
                    {
                        SubsetArg.Asses = files;
                        this.FindControl<TextBlock>("sa1").Text = string.Join(Environment.NewLine, files);
                    }

                    break;
                case "fonts":
                    SubsetArg.Fonts = string.Empty;
                    this.FindControl<TextBlock>("sa2").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        SubsetArg.Fonts = dir;
                        this.FindControl<TextBlock>("sa2").Text = dir;
                    }

                    break;
                case "output":
                    SubsetArg.Output = string.Empty;
                    this.FindControl<TextBlock>("sa3").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        SubsetArg.Output = dir;
                        this.FindControl<TextBlock>("sa3").Text = dir;
                    }

                    break;
            }
        }

        async Task<string[]> ShowSelectFileDialog(string name, string[] ext, bool multiple)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.AllowMultiple = multiple;
            FileDialogFilter filter = new FileDialogFilter();
            filter.Name = name;
            filter.Extensions.AddRange(ext);
            fileDialog.Filters.Add(filter);
            string[] files = await fileDialog.ShowAsync(this);
            return files;
        }

        private async void DoSubsetBtn_OnClick(object? sender, RoutedEventArgs e)
        {
            if (SubsetArg.Asses != null && SubsetArg.Asses.Length > 0 && !string.IsNullOrEmpty(SubsetArg.Fonts) &&
                !string.IsNullOrEmpty(SubsetArg.Output))
            {
                SetBusy(true);
                SubsetArg.DirSafe = this.FindControl<ToggleSwitch>("sa4").IsChecked == true;
                new Thread(() =>
                {
                    if (mkvlib.ASSFontSubset(SubsetArg.Asses, SubsetArg.Fonts, SubsetArg.Output, SubsetArg.DirSafe,
                        lcb))
                    {
                        PrintResult("Subset", "Subset successfully.");
                        SubsetArg.Asses = null;
                        SubsetArg.Fonts = string.Empty;
                        SubsetArg.Output = string.Empty;
                        DoUIThread(() =>
                        {
                            this.FindControl<TextBlock>("sa1").Text = string.Empty;
                            this.FindControl<TextBlock>("sa2").Text = string.Empty;
                            this.FindControl<TextBlock>("sa3").Text = string.Empty;
                            this.FindControl<ToggleSwitch>("sa4").IsChecked = true;
                        });
                    }
                    else
                        PrintResult("Subset", "Failed to subset.");

                    SetBusy(false);
                }).Start();
            }
        }

        class DumpArg
        {
            public static string Path { get; set; }
            public static string Output { get; set; }
            public static bool Subset { get; set; }
            public static bool Dir { get; set; }
        }

        private async void DumpSelectBtns_OnClick(object? sender, RoutedEventArgs e)
        {
            Button btn = (Button) sender;
            string dir;
            switch (btn.Tag.ToString())
            {
                case "file":
                    DumpArg.Path = string.Empty;
                    DumpArg.Dir = false;
                    this.FindControl<TextBlock>("da1").Text = string.Empty;
                    string[] files = await ShowSelectFileDialog("MKV file",
                        new[] {"mkv"},
                        false);
                    if (files != null && files.Length > 0)
                    {
                        DumpArg.Path = files[0];
                        this.FindControl<TextBlock>("da1").Text = files[0];
                    }

                    break;
                case "folder":
                    DumpArg.Path = string.Empty;
                    this.FindControl<TextBlock>("da1").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        DumpArg.Path = dir;
                        this.FindControl<TextBlock>("da1").Text = dir;
                        DumpArg.Dir = true;
                    }

                    break;
                case "output":
                    DumpArg.Output = string.Empty;
                    this.FindControl<TextBlock>("da2").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        DumpArg.Output = dir;
                        this.FindControl<TextBlock>("da2").Text = dir;
                    }

                    break;
            }
        }

        private async void DoDumpBtn_OnClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(DumpArg.Path) &&
                !string.IsNullOrEmpty(DumpArg.Output))
            {
                DumpArg.Subset = this.FindControl<ToggleSwitch>("da3").IsChecked == true;
                SetBusy(true);
                new Thread(() =>
                {
                    bool r = !DumpArg.Dir
                        ? mkvlib.DumpMKV(DumpArg.Path, DumpArg.Output, DumpArg.Subset, lcb)
                        : mkvlib.DumpMKVs(DumpArg.Path, DumpArg.Output, DumpArg.Subset, lcb);
                    if (r)
                    {
                        PrintResult("Dump", "Dump successfully.");
                        DumpArg.Path = string.Empty;
                        DumpArg.Output = string.Empty;
                        DumpArg.Dir = false;
                        DoUIThread(() =>
                        {
                            this.FindControl<TextBlock>("da1").Text = string.Empty;
                            this.FindControl<TextBlock>("da2").Text = string.Empty;
                            this.FindControl<ToggleSwitch>("da3").IsChecked = true;
                        });
                    }
                    else
                        PrintResult("Dump", "Failed to dump.");

                    SetBusy(false);
                }).Start();
            }
        }

        class MakeArg
        {
            public static string Dir { get; set; }
            public static string Data { get; set; }
            public static string Output { get; set; }
            public static string slang { get; set; }
            public static string stitle { get; set; }
        }

        private async void MakeSelectBtns_OnClick(object? sender, RoutedEventArgs e)
        {
            Button btn = (Button) sender;
            string dir;
            switch (btn.Tag.ToString())
            {
                case "dir":
                    MakeArg.Dir = string.Empty;
                    this.FindControl<TextBlock>("ma1").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        MakeArg.Dir = dir;
                        this.FindControl<TextBlock>("ma1").Text = dir;
                    }

                    break;
                case "data":
                    MakeArg.Data = string.Empty;
                    this.FindControl<TextBlock>("ma2").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        MakeArg.Data = dir;
                        this.FindControl<TextBlock>("ma2").Text = dir;
                    }

                    break;
                case "output":
                    MakeArg.Output = string.Empty;
                    this.FindControl<TextBlock>("ma3").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        MakeArg.Output = dir;
                        this.FindControl<TextBlock>("ma3").Text = dir;
                    }

                    break;
            }
        }

        private async void DoMakeBtn_OnClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(MakeArg.Dir) && !string.IsNullOrEmpty(MakeArg.Data) &&
                !string.IsNullOrEmpty(MakeArg.Output))
            {
                MakeArg.slang = this.FindControl<TextBox>("ma4").Text;
                MakeArg.stitle = this.FindControl<TextBox>("ma5").Text;
                SetBusy(true);
                new Thread(() =>
                {
                    if (mkvlib.MakeMKVs(MakeArg.Dir, MakeArg.Data, MakeArg.Output, MakeArg.slang, MakeArg.stitle, lcb))
                    {
                        PrintResult("Make", "Make successfully.");
                        MakeArg.Dir = string.Empty;
                        MakeArg.Data = string.Empty;
                        MakeArg.Output = string.Empty;
                        MakeArg.slang = string.Empty;
                        MakeArg.stitle = string.Empty;
                        DoUIThread(() =>
                        {
                            this.FindControl<TextBlock>("ma1").Text = string.Empty;
                            this.FindControl<TextBlock>("ma2").Text = string.Empty;
                            this.FindControl<TextBlock>("ma3").Text = string.Empty;
                            this.FindControl<TextBox>("ma4").Text = string.Empty;
                            this.FindControl<TextBox>("ma5").Text = string.Empty;
                        });
                    }
                    else
                        PrintResult("Make", "Failed to make.");

                    SetBusy(false);
                }).Start();
            }
        }

        class CreateArg
        {
            public static string vDir { get; set; }
            public static string sDir { get; set; }
            public static string fDir { get; set; }
            public static string oDir { get; set; }
            public static string slang { get; set; }
            public static string stitle { get; set; }
            public static bool clean { get; set; }
        }

        private async void CreateSelectBtns_OnClick(object? sender, RoutedEventArgs e)
        {
            Button btn = (Button) sender;
            string dir;
            switch (btn.Tag.ToString())
            {
                case "v":
                    CreateArg.vDir = string.Empty;
                    this.FindControl<TextBlock>("ca1").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        CreateArg.vDir = dir;
                        this.FindControl<TextBlock>("ca1").Text = dir;
                    }

                    break;
                case "s":
                    CreateArg.sDir = string.Empty;
                    this.FindControl<TextBlock>("ca2").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        CreateArg.sDir = dir;
                        this.FindControl<TextBlock>("ca2").Text = dir;
                    }

                    break;
                case "f":
                    CreateArg.fDir = string.Empty;
                    this.FindControl<TextBlock>("ca3").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        CreateArg.fDir = dir;
                        this.FindControl<TextBlock>("ca3").Text = dir;
                    }

                    break;
                case "o":
                    CreateArg.oDir = string.Empty;
                    this.FindControl<TextBlock>("ca4").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        CreateArg.oDir = dir;
                        this.FindControl<TextBlock>("ca4").Text = dir;
                    }

                    break;
            }
        }

        private async void DoCreateBtn_OnClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CreateArg.vDir) && !string.IsNullOrEmpty(CreateArg.sDir) &&
                !string.IsNullOrEmpty(CreateArg.fDir) && !string.IsNullOrEmpty(CreateArg.oDir))
            {
                CreateArg.slang = this.FindControl<TextBox>("ca5").Text;
                CreateArg.stitle = this.FindControl<TextBox>("ca6").Text;
                CreateArg.clean = this.FindControl<ToggleSwitch>("ca7").IsChecked == true;
                SetBusy(true);
                new Thread(() =>
                {
                    if (mkvlib.CreateMKVs(CreateArg.vDir, CreateArg.sDir, CreateArg.fDir, string.Empty, CreateArg.oDir,
                        CreateArg.slang, CreateArg.stitle, CreateArg.clean, lcb))
                    {
                        PrintResult("Create", "Create successfully.");
                        CreateArg.vDir = string.Empty;
                        CreateArg.sDir = string.Empty;
                        CreateArg.fDir = string.Empty;
                        CreateArg.oDir = string.Empty;
                        CreateArg.clean = false;
                        DoUIThread(() =>
                        {
                            this.FindControl<TextBlock>("ca1").Text = string.Empty;
                            this.FindControl<TextBlock>("ca2").Text = string.Empty;
                            this.FindControl<TextBlock>("ca3").Text = string.Empty;
                            this.FindControl<TextBlock>("ca4").Text = string.Empty;
                            this.FindControl<TextBox>("ca5").Text = string.Empty;
                            this.FindControl<TextBox>("ca6").Text = string.Empty;
                            this.FindControl<ToggleSwitch>("ca7").IsChecked = false;
                        });
                    }
                    else
                        PrintResult("Create", "Failed to create.");

                    SetBusy(false);
                }).Start();
            }
        }

        class WorkflowArg
        {
            public static string Dir { get; set; }
            public static string Data { get; set; }
            public static string Dist { get; set; }
        }

        private async void WorkflowSelectBtns_OnClick(object? sender, RoutedEventArgs e)
        {
            Button btn = (Button) sender;
            string dir;
            switch (btn.Tag.ToString())
            {
                case "dir":
                    WorkflowArg.Dir = string.Empty;
                    this.FindControl<TextBlock>("wa1").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        WorkflowArg.Dir = dir;
                        this.FindControl<TextBlock>("wa1").Text = dir;
                    }

                    break;
                case "data":
                    WorkflowArg.Data = string.Empty;
                    this.FindControl<TextBlock>("wa2").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        WorkflowArg.Data = dir;
                        this.FindControl<TextBlock>("wa2").Text = dir;
                    }

                    break;
                case "dist":
                    WorkflowArg.Dist = string.Empty;
                    this.FindControl<TextBlock>("wa3").Text = string.Empty;
                    dir = await new OpenFolderDialog().ShowAsync(this);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        WorkflowArg.Dist = dir;
                        this.FindControl<TextBlock>("wa3").Text = dir;
                    }

                    break;
            }
        }

        private async void DoWorkflowBtn_OnClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(WorkflowArg.Dir) && !string.IsNullOrEmpty(WorkflowArg.Data) &&
                !string.IsNullOrEmpty(WorkflowArg.Dist))
            {
                SetBusy(true);
                new Thread(() =>
                {
                    if (mkvlib.DumpMKVs(WorkflowArg.Dir, WorkflowArg.Data, true, lcb) &&
                        mkvlib.MakeMKVs(WorkflowArg.Dir, WorkflowArg.Data, WorkflowArg.Dist, "", "", lcb))
                    {
                        PrintResult("Workflow", "Workflow successfully.");
                        WorkflowArg.Dir = string.Empty;
                        WorkflowArg.Data = string.Empty;
                        WorkflowArg.Dist = string.Empty;
                        DoUIThread(() =>
                        {
                            this.FindControl<TextBlock>("wa1").Text = string.Empty;
                            this.FindControl<TextBlock>("wa2").Text = string.Empty;
                            this.FindControl<TextBlock>("wa3").Text = string.Empty;
                        });
                    }
                    else
                        PrintResult("Workflow", "Failed to workflow.");

                    SetBusy(false);
                }).Start();
            }
        }

        void PrintResult(string str1, string str2)
        {
            string str = $"##### {str1} result: \"{str2}\"";
            lcb(str);
        }

        async void DoUIThread(Action action)
        {
            await Dispatcher.UIThread.InvokeAsync(action);
        }

        void SetBusy(bool busy)
        {
            DoUIThread(() => this.FindControl<BusyIndicator>("busyBox").IsBusy = busy);
        }
    }
}