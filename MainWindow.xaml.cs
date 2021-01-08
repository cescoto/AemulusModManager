﻿using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace AemulusModManager
{
    public partial class MainWindow : Window
    {
        public AemulusConfig config;
        public ConfigP3F p3fConfig;
        public ConfigP4G p4gConfig;
        public ConfigP5 p5Config;
        public Packages packages;
        public string game;
        private XmlSerializer xs;
        private XmlSerializer xp;
        private XmlSerializer xsp;
        private XmlSerializer xsm;
        public string modPath;
        private ObservableCollection<Package> PackageList;
        private ObservableCollection<DisplayedMetadata> DisplayedPackages;
        public bool emptySND;
        public bool useCpk;
        public bool messageBox;
        public string gamePath;
        public string launcherPath;
        public string elfPath;
        public string cpkLang;
        private BitmapImage bitmap;

        public DisplayedMetadata InitDisplayedMetadata(Metadata m)
        {
            DisplayedMetadata dm = new DisplayedMetadata();
            dm.name = m.name;
            dm.id = m.id;
            dm.author = m.author;
            Version v;
            if (Version.TryParse(m.version, out v))
                dm.version = m.version;
            dm.description = m.description;
            dm.link = m.link;
            return dm;
        }

        private void OnChecked(object sender, RoutedEventArgs e)
        {
            var checkBox = e.OriginalSource as CheckBox;

            DisplayedMetadata package = checkBox?.DataContext as DisplayedMetadata;

            if (package != null)
            {
                package.enabled = true;
                foreach (var p in PackageList.ToList())
                {
                    if (p.path == package.path)
                        p.enabled = true;
                }
                updatePackages();
            }
        }

        // Events for Enabled checkboxes
        private void OnUnchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = e.OriginalSource as CheckBox;

            DisplayedMetadata package = checkBox?.DataContext as DisplayedMetadata;

            if (package != null)
            {
                package.enabled = false;
                foreach (var p in PackageList.ToList())
                {
                    if (p.path == package.path)
                        p.enabled = false;
                }
                updatePackages();
            }
        }

        // Hyperlink click event
        private void OnHyperlinkClick(object sender, RoutedEventArgs e)
        {
            var destination = ((Hyperlink)e.OriginalSource).NavigateUri;

            if (destination != null)
            {
                try
                {
                    Process.Start(destination.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Invalid Mod Page link. Perhaps missing \'www\' ({ex.Message})");
                }

            }
        }

        private TextBoxOutputter outputter = new TextBoxOutputter();

        void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                ConsoleOutput.AppendText($"{e.Value}\n");
            });
        }

        void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                ConsoleOutput.AppendText(e.Value);
            });
        }

        // Autoscrolls to end whenever console updates
        private void ScrollToBottom(object sender, TextChangedEventArgs args)
        {
            ConsoleOutput.ScrollToEnd();
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            packages = new Packages();

            outputter.WriteEvent += consoleWriter_WriteEvent;
            outputter.WriteLineEvent += consoleWriter_WriteLineEvent;
            Console.SetOut(outputter);

            Directory.CreateDirectory($@"Packages");
            Directory.CreateDirectory($@"Original");
            Directory.CreateDirectory("Config");

            // Transfer all current packages to Persona 4 Golden folder
            if (!Directory.Exists($@"Packages\Persona 4 Golden") && !Directory.Exists($@"Packages\Persona 3 FES") && !Directory.Exists($@"Packages\Persona 5"))
            {
                Console.WriteLine("[INFO] Transferring current packages to Persona 4 Golden subfolder...");
                FileSystem.MoveDirectory("Packages", "Persona 4 Golden", true);
                Directory.CreateDirectory("Packages");
                FileSystem.MoveDirectory("Persona 4 Golden", @"Packages\Persona 4 Golden", true);
            }


            string[] subdirs = Directory.GetDirectories("Original")
                            .Where(x => Path.GetFileName(x).StartsWith("data"))
                            .ToArray();
            Directory.CreateDirectory(@"Original\Persona 4 Golden");
            foreach (var d in subdirs)
                FileSystem.MoveDirectory(d, $@"Original\Persona 4 Golden\{Path.GetFileName(d)}", true);

            DisplayedPackages = new ObservableCollection<DisplayedMetadata>();
            PackageList = new ObservableCollection<Package>();

            // Retrieve initial thumbnail from embedded resource
            Assembly asm = Assembly.GetExecutingAssembly();
            Stream iconStream = asm.GetManifestResourceStream("AemulusModManager.Preview.png");
            bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = iconStream;
            bitmap.EndInit();
            Preview.Source = bitmap;

            
            // Initialize config
            config = new AemulusConfig();
            p5Config = new ConfigP5();
            p4gConfig = new ConfigP4G();
            p3fConfig = new ConfigP3F();
            config.p4gConfig = p4gConfig;
            config.p3fConfig = p3fConfig;
            config.p5Config = p5Config;

            // Initialize xml serializers
            XmlSerializer oldConfigSerializer = new XmlSerializer(typeof(Config));
            xs = new XmlSerializer(typeof(AemulusConfig));
            xp = new XmlSerializer(typeof(Packages));
            xsp = new XmlSerializer(typeof(Metadata));
            xsm = new XmlSerializer(typeof(ModXmlMetadata));


            //Console.WriteLine($"[INFO] Initializing packages from {game.Replace(" ", "")}Config.xml");
            // Load in Config if it exists
            
            string file = @"Config\Config.xml";
            if (File.Exists(@"Config\Config.xml") || File.Exists(@"Config.xml"))
            {
                try
                {
                    if (File.Exists(@"Config.xml"))
                        file = @"Config.xml";
                    using (FileStream streamWriter = File.Open(file, FileMode.Open))
                    {
                        // Call the Deserialize method and cast to the object type.
                        
                        if (file == @"Config.xml")
                        {
                            Config oldConfig = (Config)oldConfigSerializer.Deserialize(streamWriter);
                            p4gConfig.reloadedPath = oldConfig.reloadedPath;
                            p4gConfig.exePath = oldConfig.exePath;
                            p4gConfig.modDir = oldConfig.modDir;
                            p4gConfig.emptySND = oldConfig.emptySND;
                            p4gConfig.cpkLang = oldConfig.cpkLang;
                            p4gConfig.useCpk = oldConfig.useCpk;

                            config.p4gConfig = p4gConfig;
                        }
                        else
                            config = (AemulusConfig)xs.Deserialize(streamWriter);
                        game = config.game;
                        if (game == null)
                        {
                            game = "Persona 4 Golden";
                            config.game = "Persona 4 Golden";
                        }
                        
                        if (config.p3fConfig != null)
                            p3fConfig = config.p3fConfig;
                        if (config.p4gConfig != null)
                            p4gConfig = config.p4gConfig;
                        if (config.p5Config != null)
                            p5Config = config.p5Config;

                        if (game == "Persona 4 Golden")
                        {
                            // Default
                            if (cpkLang == null)
                            {
                                cpkLang = "data_e.cpk";
                                config.p4gConfig.cpkLang = "data_e.cpk";
                            }
                            modPath = config.p4gConfig.modDir;
                            gamePath = config.p4gConfig.exePath;
                            launcherPath = config.p4gConfig.reloadedPath;
                            emptySND = config.p4gConfig.emptySND;
                            cpkLang = config.p4gConfig.cpkLang;
                            useCpk = config.p4gConfig.useCpk;
                            messageBox = config.p4gConfig.disableMessageBox;
                        }
                        else if (game == "Persona 3 FES")
                        {
                            modPath = config.p3fConfig.modDir;
                            gamePath = config.p3fConfig.isoPath;
                            elfPath = config.p3fConfig.elfPath;
                            launcherPath = config.p3fConfig.launcherPath;
                            messageBox = config.p3fConfig.disableMessageBox;
                            useCpk = false;
                        }
                        else if (game == "Persona 5")
                        {
                            modPath = config.p5Config.modDir;
                            gamePath = config.p5Config.gamePath;
                            launcherPath = config.p5Config.launcherPath;
                            messageBox = config.p5Config.disableMessageBox;
                            useCpk = false;
                        }
                    }
                    if (file == @"Config.xml")
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Invalid Config.xml ({ex.Message})");
                }

                switch (game)
                {
                    case "Persona 3 FES":
                        GameBox.SelectedIndex = 0;
                        break;
                    case "Persona 4 Golden":
                        GameBox.SelectedIndex = 1;
                        break;
                    case "Persona 5":
                        GameBox.SelectedIndex = 2;
                        break;
                }

                if (File.Exists($@"Config\{game.Replace(" ", "")}Packages.xml"))
                {
                    try
                    {
                        using (FileStream streamWriter = File.Open($@"Config\{game.Replace(" ", "")}Packages.xml", FileMode.Open))
                        {
                            // Call the Deserialize method and cast to the object type.
                            packages = (Packages)xp.Deserialize(streamWriter);
                            PackageList = packages.packages;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Invalid Packages.xml ({ex.Message})");
                    }
                }

                
                if (!Directory.Exists($@"Packages\{game}"))
                {
                    Console.WriteLine($@"[INFO] Creating Packages\{game}");
                    Directory.CreateDirectory($@"Packages\{game}");
                }

                // Create displayed metadata from packages in PackageList and their respective Package.xml's
                foreach (var package in PackageList)
                {
                    string xml = $@"Packages\{game}\{package.path}\Package.xml";
                    Metadata m;
                    DisplayedMetadata dm = new DisplayedMetadata();
                    try
                    {
                        if (File.Exists(xml))
                        {
                            m = new Metadata();
                            try
                            {
                                using (FileStream streamWriter = File.Open(xml, FileMode.Open))
                                {
                                    m = (Metadata)xsp.Deserialize(streamWriter);
                                    dm.name = m.name;
                                    dm.id = m.id;
                                    dm.author = m.author;
                                    dm.version = m.version;
                                    dm.link = m.link;
                                    dm.description = m.description;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message})");
                            }
                        }

                        dm.path = package.path;
                        dm.enabled = package.enabled;
                        DisplayedPackages.Add(dm);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Invalid Package.xml for package {package.id} ({ex.Message})");
                        continue;
                    }
                }
                ModGrid.ItemsSource = DisplayedPackages;
            }
            else // No config found
            {
                game = "Persona 4 Golden";
                config.game = "Persona 4 Golden";
                cpkLang = "data_e.cpk";
                config.p4gConfig.cpkLang = "data_e.cpk";
            }

            if (modPath == null)
                MergeButton.IsHitTestVisible = false;

            if (game == "Persona 4 Golden" && config.p4gConfig.modDir != null)
                modPath = config.p4gConfig.modDir;
            else if (game == "Persona 3 FES" && config.p3fConfig.modDir != null)
                modPath = config.p3fConfig.modDir;
            else if (game == "Persona 5" && config.p5Config.modDir != null)
                modPath = config.p5Config.modDir;

            // Create Packages directory if it doesn't exist
            if (!Directory.Exists("Packages"))
                Directory.CreateDirectory("Packages");

            if (!Directory.Exists("Original"))
                Directory.CreateDirectory("Original");

            Refresh();
            updateConfig();
            updatePackages();

            // Check if Original Folder is unpacked
            if (!Directory.EnumerateFileSystemEntries("Original").Any())
            {
                Console.WriteLine("[WARNING] Aemulus can't find your base files in the Original folder.");
                Console.WriteLine("Please click the Config button and select \"Unpack Base Files\" before building.");
            }

            Description.Document = ConvertToFlowDocument("Aemulus means \"Rival\" in Latin. It was chosen since it sounds cool. (You are seeing this message because no mod package is selected or the package has no description).\n\nIf you want to help support me go to:\nhttps://www.ko-fi.com/tekka");
        
        }

        public Task pacUnpack(string directory)
        {
            return Task.Run(() =>
            {
                if (game == "Persona 4 Golden")
                    PacUnpacker.Unpack(directory, cpkLang);
                else if (game == "Persona 3 FES")
                    PacUnpacker.Unzip(directory);
                else if (game == "Persona 5")
                    PacUnpacker.UnpackCPK(directory);
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    ModGrid.IsHitTestVisible = true;
                    ConfigButton.IsHitTestVisible = true;
                    MergeButton.IsHitTestVisible = true;
                    NewButton.IsHitTestVisible = true;
                    LaunchButton.IsHitTestVisible = true;
                    RefreshButton.IsHitTestVisible = true;
                    GameBox.IsHitTestVisible = true;
                });
            }
            );
        }

        private void LaunchClick(object sender, RoutedEventArgs e)
        {
            if (gamePath != null && launcherPath != null)
            {
                Console.WriteLine("[INFO] Launching game!");
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.FileName = launcherPath;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                if (game == "Persona 4 Golden")
                    startInfo.Arguments = "--launch \"" + gamePath + "\"";
                else if (game == "Persona 3 FES")
                    startInfo.Arguments = $"--nogui --elf=\"{elfPath}\"";
                else if (game == "Persona 5")
                    startInfo.Arguments = $"--no-gui \"{gamePath}\"";

                GameBox.IsHitTestVisible = false;
                ConfigButton.IsHitTestVisible = false;
                MergeButton.IsHitTestVisible = false;
                LaunchButton.IsHitTestVisible = false;
                NewButton.IsHitTestVisible = false;
                RefreshButton.IsHitTestVisible = false;

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                }

                GameBox.IsHitTestVisible = true;
                ConfigButton.IsHitTestVisible = true;
                MergeButton.IsHitTestVisible = true;
                LaunchButton.IsHitTestVisible = true;
                NewButton.IsHitTestVisible = true;
                RefreshButton.IsHitTestVisible = true;
            }
            else
                Console.WriteLine("[ERROR] Please setup shortcut in config menu.");
        }

        private void ConfigWdwClick(object sender, RoutedEventArgs e)
        {

            if (game == "Persona 4 Golden")
            {
                ConfigWindowP4G cWindow = new ConfigWindowP4G(this) { Owner = this };
                cWindow.DataContext = this;
                cWindow.ShowDialog();
            }
            else if (game == "Persona 3 FES")
            {
                ConfigWindowP3F cWindow = new ConfigWindowP3F(this) { Owner = this };
                cWindow.DataContext = this;
                cWindow.ShowDialog();
            }
            else if (game == "Persona 5")
            {
                ConfigWindowP5 cWindow = new ConfigWindowP5(this) { Owner = this };
                cWindow.DataContext = this;
                cWindow.ShowDialog();
            }
        }

        private void UpdateMetadata()
        {
            // Update metadata
            List<DisplayedMetadata> temp = DisplayedPackages.ToList();
            foreach (var package in temp)
            {
                if (File.Exists($@"Packages\{game}\{package.path}\Package.xml"))
                {
                    try
                    {
                        using (FileStream streamWriter = File.Open($@"Packages\{game}\{package.path}\Package.xml", FileMode.Open))
                        {
                            Metadata metadata = (Metadata)xsp.Deserialize(streamWriter);
                            package.name = metadata.name;
                            package.id = metadata.id;
                            package.author = metadata.author;
                            package.version = metadata.version;
                            package.link = metadata.link;
                            package.description = metadata.description;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message})");
                    }
                }
            }
            DisplayedPackages = new ObservableCollection<DisplayedMetadata>(temp);
        }

        // Refresh both PackageList and DisplayedPackages
        private void Refresh()
        {
            Metadata metadata;
            // First remove all deleted packages and update package id's to match metadata
            foreach (var package in PackageList.ToList())
            {
                if (!Directory.Exists($@"Packages\{game}\{package.path}"))
                {
                    PackageList.Remove(package);
                    List<DisplayedMetadata> temp = DisplayedPackages.ToList();
                    temp.RemoveAll(x => x.path == package.path);
                    DisplayedPackages = new ObservableCollection<DisplayedMetadata>(temp);
                }
                if (File.Exists($@"Packages\{game}\{package.path}\Package.xml"))
                {
                    try
                    {
                        using (FileStream streamWriter = File.Open($@"Packages\{game}\{package.path}\Package.xml", FileMode.Open))
                        {
                            metadata = (Metadata)xsp.Deserialize(streamWriter);
                            package.id = metadata.id;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message})");
                    }
                }
            }

            UpdateMetadata();

            // Get all packages from Packages folder (Adding packages)
            foreach (var package in Directory.EnumerateDirectories($@"Packages\{game}"))
            {
                if (File.Exists($@"{package}\Package.xml"))
                {
                    using (FileStream streamWriter = File.Open($@"{package}\Package.xml", FileMode.Open))
                    {
                        metadata = (Metadata)xsp.Deserialize(streamWriter);
                        // Add package to list if it doesn't exist
                        if (!PackageList.ToList().Any(x => x.path == Path.GetFileName(package))
                            && !DisplayedPackages.ToList().Any(x => x.path == Path.GetFileName(package)))
                        {
                            // Add new package to both collections
                            DisplayedMetadata dm = InitDisplayedMetadata(metadata);
                            Package p = new Package();
                            p.enabled = false;
                            p.id = metadata.id;
                            p.path = Path.GetFileName(package);
                            PackageList.Add(p);
                            dm.enabled = false;
                            dm.path = Path.GetFileName(package);
                            DisplayedPackages.Add(dm);
                        }
                    }
                }
                // Create Package.xml
                else
                {
                    Console.WriteLine($"[WARNING] No Package.xml found for {Path.GetFileName(package)}, creating a simple one...");
                    // Create metadata
                    Metadata newMetadata = new Metadata();
                    newMetadata.name = Path.GetFileName(package);
                    newMetadata.id = newMetadata.name.Replace(" ", "").ToLower();

                    
                    List<string> dirFiles = Directory.GetFiles(package).ToList();
                    List<string> dirFolders = Directory.GetDirectories(package, "*", System.IO.SearchOption.TopDirectoryOnly).ToList();
                    dirFiles = dirFiles.Concat(dirFolders).ToList();
                    if (File.Exists($@"{package}\Mod.xml") && Directory.Exists($@"{package}\Data"))
                    {
                        //If mod folder contains Data folder and mod.xml, import mod compendium mod.xml...
                        string modXml = $@"{package}\Mod.xml";
                        using (FileStream streamWriter = File.Open(modXml, FileMode.Open))
                        {
                            //Deserialize Mod.xml & Use metadata
                            ModXmlMetadata m = (ModXmlMetadata)xsm.Deserialize(streamWriter);
                            newMetadata.id = m.Author.ToLower().Replace(" ","") + "." + m.Title.ToLower().Replace(" ","");
                            newMetadata.author = m.Author;
                            newMetadata.version = m.Version;
                            newMetadata.link = m.Url;
                            newMetadata.description = m.Description;
                        }
                        //Move files out of Data folder
                        string dataDir = $@"{package}\Data";
                        if (Directory.Exists(dataDir))
                        {
                            FileSystem.MoveDirectory(dataDir, $@"{package}\temp", true);
                            FileSystem.MoveDirectory($@"{package}\temp", package, true);
                        }
                        //Delete prebuild.bat if exists
                        if (File.Exists($@"{package}\prebuild.bat"))
                            File.Delete($@"{package}\prebuild.bat");
                        //Make sure Data folder is gone
                        if (Directory.Exists(dataDir) && !Directory.EnumerateFileSystemEntries(dataDir).Any())
                            Directory.Delete(dataDir, true);
                        //Goodbye old friend
                        File.Delete(modXml);
                    }
                    else
                    {
                        newMetadata.author = "";
                        newMetadata.version = "";
                        newMetadata.link = "";
                        newMetadata.description = "";
                    }
                    using (FileStream streamWriter = File.Create($@"{package}\Package.xml"))
                    {
                        xsp.Serialize(streamWriter, newMetadata);
                    }
                    if (!PackageList.ToList().Any(x => x.path == Path.GetFileName(package))
                            && !DisplayedPackages.ToList().Any(x => x.path == Path.GetFileName(package)))
                    {
                        // Create package
                        Package newPackage = new Package();
                        newPackage.enabled = false;
                        newPackage.path = Path.GetFileName(package);
                        newPackage.id = newMetadata.id;
                        PackageList.Add(newPackage);
                        // Create displayedmetadata
                        DisplayedMetadata newDisplayedMetadata = InitDisplayedMetadata(newMetadata);
                        newDisplayedMetadata.enabled = false;
                        newDisplayedMetadata.path = newPackage.path;
                        DisplayedPackages.Add(newDisplayedMetadata);
                    }
                    else
                    {
                        UpdateMetadata();
                    }
                }
            }

            // Update DisplayedPackages
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                ModGrid.ItemsSource = DisplayedPackages;
                // Trigger select event to refresh description and Preview.png
                ModGrid.SetSelectedItem(ModGrid.GetSelectedItem());
            });
            Console.WriteLine($"[INFO] Refreshed!");
        }

        private void RefreshClick(object sender, RoutedEventArgs e)
        {
            Refresh();
            updateConfig();
            updatePackages();
        }

        private void NewClick(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[INFO] Creating new package!");
            CreatePackage newPackage = new CreatePackage(null);
            newPackage.ShowDialog();
            if (newPackage.metadata != null)
            {
                string path;
                if (newPackage.metadata.version != null && newPackage.metadata.version.Length > 0)
                    path = $@"Packages\{game}\{newPackage.metadata.name} {newPackage.metadata.version}";
                else
                    path = $@"Packages\{game}\{newPackage.metadata.name}";
                if (!Directory.Exists(path))
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                        using (FileStream streamWriter = File.Create($@"{path}\Package.xml"))
                        {
                            xsp.Serialize(streamWriter, newPackage.metadata);
                        }
                        if (File.Exists(newPackage.thumbnailPath))
                        {
                            string extension = Path.GetExtension(newPackage.thumbnailPath).ToLower();
                            if (extension == ".png" || extension == ".jpg")
                                File.Copy(newPackage.thumbnailPath, $@"{path}\Preview{extension}", true);
                        }
                        Refresh();
                        updateConfig();
                        updatePackages();
                        ProcessStartInfo StartInformation = new ProcessStartInfo();
                        StartInformation.FileName = path;
                        Process process = Process.Start(StartInformation);
                        Console.WriteLine("[INFO] Opened new package folder.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Couldn't create directory/Package.xml. ({ex.Message})");
                    }
                }
                else
                {
                    Console.WriteLine($"[ERROR] {newPackage.metadata.name} already exists, not creating new package.");
                }
            }
        }

        private async void MergeClick(object sender, RoutedEventArgs e)
        {
            if (!Directory.EnumerateFileSystemEntries("Original").Any())
            {
                Console.WriteLine("[WARNING] Aemulus can't find your Vanilla files in the Original folder.");
                Console.WriteLine("Please click the Config button and select \"Unpack Base Files\" before building.");
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    MessageBoxResult result = MessageBox.Show("Aemulus can't find your Vanilla files in the Original folder. Please click the Config button and select \"Unpack data00004.pac\" before building.",
                                          "Aemulus Package Manager",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Exclamation);
                });
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            GameBox.IsHitTestVisible = false;
            ConfigButton.IsHitTestVisible = false;
            MergeButton.IsHitTestVisible = false;
            LaunchButton.IsHitTestVisible = false;
            NewButton.IsHitTestVisible = false;
            RefreshButton.IsHitTestVisible = false;
            ModGrid.IsHitTestVisible = false;

            await unpackThenMerge();

            ModGrid.IsHitTestVisible = true;
            ConfigButton.IsHitTestVisible = true;
            MergeButton.IsHitTestVisible = true;
            LaunchButton.IsHitTestVisible = true;
            NewButton.IsHitTestVisible = true;
            RefreshButton.IsHitTestVisible = true;
            GameBox.IsHitTestVisible = true;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }

        private async Task unpackThenMerge()
        {
            await Task.Run(() =>
            {
                Refresh();
                List<string> packages = new List<string>();
                foreach (Package m in PackageList)
                {
                    if (m.enabled)
                    {
                        packages.Add($@"Packages\{game}\{m.path}");
                        if (game == "Persona 4 Golden" && (Directory.Exists($@"Packages\{game}\{m.path}\{Path.GetFileNameWithoutExtension(cpkLang)}")
                            || Directory.Exists($@"Packages\{game}\{m.path}\movie")) && !useCpk)
                        {
                            Console.WriteLine($"[WARNING] {m.path} is using CPK folder paths, setting Use CPK Structure to true");
                            useCpk = true;
                        }
                    }
                }
                packages.Reverse();
                if (packages.Count == 0)
                    Console.WriteLine("[ERROR] No packages to build!");
                else if (!Directory.Exists(modPath))
                    Console.WriteLine("[ERROR] Current output folder doesn't exist! Please select it again.");
                else
                {
                    string path = modPath;
                    if (game == "Persona 5")
                    {
                        path = $@"{modPath}\mod";
                        Directory.CreateDirectory(path);
                    }
                    binMerge.Restart(path, emptySND, game);
                    binMerge.Unpack(packages, path, useCpk, cpkLang, game);
                    binMerge.Merge(path, game);

                    // Only run if tblpatching is enabled and tblpatches exists
                    if (packages.Exists(x => Directory.GetFiles(x, "*.tblpatch").Length > 0 || Directory.Exists($@"{x}\tblpatches")))
                    {
                        tblPatch.Patch(packages, path, useCpk, cpkLang, game);
                    }

                    if (game == "Persona 5")
                        binMerge.MakeCpk(path);

                    if (!messageBox)
                    {
                        App.Current.Dispatcher.Invoke((Action)delegate
                        {
                            MessageBoxResult result = MessageBox.Show("Finished Building!",
                                              "Aemulus Package Manager",
                                              MessageBoxButton.OK,
                                              MessageBoxImage.Information);
                        });
                    }

                }
            });
        }

        public void updateConfig()
        {
            using (FileStream streamWriter = File.Create($@"Config\Config.xml"))
            {
                xs.Serialize(streamWriter, config);
            }
        }

        public void updatePackages()
        {
            packages.packages = PackageList;
            using (FileStream streamWriter = File.Create($@"Config\{game.Replace(" ", "")}Packages.xml"))
            {
                xp.Serialize(streamWriter, packages);
            }
        }

        private void rowSelected(object sender, SelectionChangedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null)
            {
                // Set description
                if (row.description != null && row.description.Length > 0)
                {
                    Description.Document = ConvertToFlowDocument(row.description);
                }
                else
                {
                    Description.Document = ConvertToFlowDocument("Aemulus means \"Rival\" in Latin. It was chosen since it sounds cool. (You are seeing this message because no mod package is selected or the package has no description).\n\nIf you want to help support me go to:\nhttps://www.ko-fi.com/tekka");
                }

                // Set requirement visibility
                if (Directory.Exists($@"Packages\{game}\{row.path}\patches"))
                    Inaba.Visibility = Visibility.Visible;
                else
                    Inaba.Visibility = Visibility.Collapsed;
                if (File.Exists($@"Packages\{game}\{row.path}\SND\HeeHeeHo.uwus"))
                    HHH.Visibility = Visibility.Visible;
                else
                    HHH.Visibility = Visibility.Collapsed;
                if (Directory.Exists($@"Packages\{game}\{row.path}\patches") || File.Exists($@"Packages\{game}\{row.path}\SND\HeeHeeHo.uwus"))
                    Reqs.Visibility = Visibility.Visible;
                else
                    Reqs.Visibility = Visibility.Collapsed;

                
                // Set image
                string path = $@"Packages\{game}\{row.path}";
                if (File.Exists($@"{path}\Preview.png") || File.Exists($@"{path}\Preview.jpg"))
                {
                    try
                    {
                        byte[] imageBytes = null;
                        if (File.Exists($@"{path}\Preview.png"))
                            imageBytes = File.ReadAllBytes($@"{path}\Preview.png");
                        else
                            imageBytes = File.ReadAllBytes($@"{path}\Preview.jpg");
                        var stream = new MemoryStream(imageBytes);
                        var img = new BitmapImage();

                        img.BeginInit();
                        img.StreamSource = stream;
                        img.EndInit();
                        Preview.Source = img;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] {ex.Message}");
                    }
                }
                else
                    Preview.Source = bitmap;

            }
        }

        // Update config order when rows are changed
        private void ModGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            DisplayedMetadata dm = (DisplayedMetadata)e.Row.Item;
            foreach (var p in PackageList.ToList())
            {
                if (dm.path == p.path)
                {
                    Package temp = p;
                    PackageList.Remove(p);
                    PackageList.Insert(DisplayedPackages.IndexOf(dm), temp);
                }
            }
            updateConfig();
        }

        private FlowDocument ConvertToFlowDocument(string text)
        {
            var flowDocument = new FlowDocument();

            var regex = new Regex(@"(https?:\/\/[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var matches = regex.Matches(text).Cast<Match>().Select(m => m.Value).ToList();

            var paragraph = new Paragraph();
            flowDocument.Blocks.Add(paragraph);

            foreach (var segment in regex.Split(text))
            {
                if (matches.Contains(segment))
                {
                    var hyperlink = new Hyperlink(new Run(segment))
                    {
                        NavigateUri = new Uri(segment),
                    };
                    hyperlink.RequestNavigate += (sender, args) => Process.Start(segment);

                    paragraph.Inlines.Add(hyperlink);
                }
                else
                {
                    paragraph.Inlines.Add(new Run(segment));
                }
            }

            return flowDocument;
        }
        private void Inaba_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://gamebanana.com/tools/6872");
        }

        private void HHH_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://gamebanana.com/gamefiles/12806");
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null)
            {
                if (Directory.Exists($@"Packages\{game}\{row.path}"))
                {
                    try
                    {
                        ProcessStartInfo StartInformation = new ProcessStartInfo();
                        StartInformation.FileName = $@"Packages\{game}\{row.path}";
                        Process process = Process.Start(StartInformation);
                        Console.WriteLine($@"[INFO] Opened Packages\{game}\{row.path}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($@"[ERROR] Couldn't open Packages\{game}\{row.path} ({ex.Message})");
                    }
                }
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null)
            {
                MessageBoxResult result = MessageBox.Show($@"Are you sure you want to delete Packages\{row.path}?",
                                      "Aemulus Package Manager",
                                      MessageBoxButton.YesNo,
                                      MessageBoxImage.Warning);
                if (Directory.Exists($@"Packages\{game}\{row.path}") && result == MessageBoxResult.Yes)
                {
                    Console.WriteLine($@"[INFO] Deleted Packages\{game}\{row.path}.");
                    try
                    {
                        Directory.Delete($@"Packages\{game}\{row.path}", true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($@"[ERROR] Couldn't delete Packages\{game}\{row.path} ({ex.Message})");
                    }
                    Refresh();
                    updateConfig();
                    updatePackages();
                }
            }
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null && File.Exists($@"Packages\{game}\{row.path}\Package.xml"))
            {
                Metadata m = new Metadata();
                m.name = row.name;
                m.author = row.author;
                m.id = row.id;
                m.version = row.version;
                m.link = row.link;
                m.description = row.description;
                CreatePackage createPackage = new CreatePackage(m);
                createPackage.ShowDialog();
                if (createPackage.metadata != null)
                {
                    try
                    {
                        using (FileStream streamWriter = File.Create($@"Packages\{game}\{row.path}\Package.xml"))
                        {
                            xsp.Serialize(streamWriter, createPackage.metadata);
                        }
                        if (File.Exists(createPackage.thumbnailPath))
                        {
                            string extension = Path.GetExtension(createPackage.thumbnailPath).ToLower();
                            if (extension == ".png" || extension == ".jpg")
                                File.Copy(createPackage.thumbnailPath, $@"Packages\{game}\{row.path}\Preview{extension}", true);
                        }

                        Refresh();
                        updateConfig();
                        updatePackages();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] {ex.Message}");
                    }
                }
            }
        }

        private void ModGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null)
            {
                // Enable/disable convert to 1.4.0
                if (!Directory.Exists($@"Packages\{game}\{row.path}\{Path.GetFileNameWithoutExtension(cpkLang)}") 
                    || !Directory.Exists($@"Packages\{game}\{row.path}\movie"))
                    ConvertCPK.IsEnabled = true;
                else
                    ConvertCPK.IsEnabled = false;
            }
        }

        private void ConvertCPK_Click(object sender, RoutedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            foreach (var folder in Directory.EnumerateDirectories($@"Packages\{row.path}"))
            {
                if (Path.GetFileName(folder).StartsWith("data0"))
                    FileSystem.MoveDirectory(folder, $@"Packages\{game}\{row.path}\{Path.GetFileNameWithoutExtension(cpkLang)}", true);
                else if (Path.GetFileName(folder).StartsWith("movie0"))
                    FileSystem.MoveDirectory(folder, $@"Packages\{game}\{row.path}\movie", true);
            }
            // Convert the mods.aem file too
            if (File.Exists($@"Packages\{game}\{row.path}\mods.aem"))
            {
                string text = File.ReadAllText($@"Packages\{game}\{row.path}\mods.aem");
                text = Regex.Replace(text, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                File.WriteAllText($@"Packages\{game}\{row.path}\mods.aem", text);
            }
        }

        private void ModGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space && ModGrid.CurrentColumn.Header.ToString() != "Enabled")
            {
                var checkbox = ModGrid.Columns[0].GetCellContent(ModGrid.SelectedItem) as CheckBox;
                checkbox.IsChecked = !checkbox.IsChecked;
            }
        }

        private void GameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameBox.SelectedIndex != -1 && IsLoaded)
            {
                int index = GameBox.SelectedIndex;
                game = null;
                switch (index)
                {
                    case 0:
                        game = "Persona 3 FES";
                        modPath = config.p3fConfig.modDir;
                        gamePath = config.p3fConfig.isoPath;
                        elfPath = config.p3fConfig.elfPath;
                        launcherPath = config.p3fConfig.launcherPath;
                        messageBox = config.p3fConfig.disableMessageBox;
                        useCpk = false;
                        break;
                    case 1:
                        game = "Persona 4 Golden";
                        modPath = config.p4gConfig.modDir;
                        gamePath = config.p4gConfig.exePath;
                        launcherPath = config.p4gConfig.reloadedPath;
                        emptySND = config.p4gConfig.emptySND;
                        cpkLang = config.p4gConfig.cpkLang;
                        useCpk = config.p4gConfig.useCpk;
                        messageBox = config.p4gConfig.disableMessageBox;
                        break;
                    case 2:
                        game = "Persona 5";
                        modPath = config.p5Config.modDir;
                        gamePath = config.p5Config.gamePath;
                        launcherPath = config.p5Config.launcherPath;
                        messageBox = config.p5Config.disableMessageBox;
                        useCpk = false;
                        break;
                }
                config.game = game;
                if (!Directory.Exists($@"Packages\{game}"))
                {
                    Console.WriteLine($@"[INFO] Creating Packages\{game}");
                    Directory.CreateDirectory($@"Packages\{game}");
                }
                Console.WriteLine($"[INFO] Game set to {game}.");

                if (!Directory.Exists($@"Packages\{game}"))
                {
                    Console.WriteLine($@"[INFO] Creating Packages\{game}");
                    Directory.CreateDirectory($@"Packages\{game}");
                }

                PackageList.Clear();
                DisplayedPackages.Clear();

                if (File.Exists($@"Config\{game.Replace(" ", "")}Packages.xml"))
                {
                    try
                    {
                        using (FileStream streamWriter = File.Open($@"Config\{game.Replace(" ", "")}Packages.xml", FileMode.Open))
                        {
                            // Call the Deserialize method and cast to the object type.
                            packages = (Packages)xp.Deserialize(streamWriter);
                            PackageList = packages.packages;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Invalid Packages.xml ({ex.Message})");
                    }
                }

                // Create displayed metadata from packages in PackageList and their respective Package.xml's
                foreach (var package in PackageList)
                {
                    string xml = $@"Packages\{game}\{package.path}\Package.xml";
                    Metadata m;
                    DisplayedMetadata dm = new DisplayedMetadata();
                    try
                    {
                        if (File.Exists(xml))
                        {
                            m = new Metadata();
                            try
                            {
                                using (FileStream streamWriter = File.Open(xml, FileMode.Open))
                                {
                                    m = (Metadata)xsp.Deserialize(streamWriter);
                                    dm.name = m.name;
                                    dm.id = m.id;
                                    dm.author = m.author;
                                    dm.version = m.version;
                                    dm.link = m.link;
                                    dm.description = m.description;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message})");
                            }
                        }

                        dm.path = package.path;
                        dm.enabled = package.enabled;
                        DisplayedPackages.Add(dm);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Invalid Package.xml for package {package.id} ({ex.Message})");
                        continue;
                    }
                }
                ModGrid.ItemsSource = DisplayedPackages;

                Refresh();
                updateConfig();
                updatePackages();
            }

            
        }

        private void Kofi_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://www.ko-fi.com/tekka");
        }
    }
}