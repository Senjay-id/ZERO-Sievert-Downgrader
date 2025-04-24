using Serilog.Events;
using Serilog;
using xdelta3.net;
using System.Diagnostics;
using Microsoft.Win32;

class Program
{
    static bool patchException = false;

    [STAThread]
    static void Main()
    {
        try
        {
            if (File.Exists("Downgrader.log")) File.Delete("Downgrader.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.File("Downgrader.log")
                .CreateLogger();

            // Get the game path using the AppID or default folder name
            string gamePath = GetSteamGamePath("1782120"); // the game steamID

            if (string.IsNullOrEmpty(gamePath))
            {
                string gameFolder = SelectFolder("Select the game folder");
                if (string.IsNullOrEmpty(gameFolder)) return;

                gamePath = gameFolder;
            }
            Log.Information($"Detected Game Path: {gamePath}");

            // Copy resources to the game directory
            CopyAllFilesAndDirectories(Path.Combine(Environment.CurrentDirectory, "resources", "GameData"), gamePath);

            string patchPath = Path.Combine(Environment.CurrentDirectory, "resources", "GameDelta");

            List<string> fileList = new List<string>
            {
                "audiogroup1.dat",
                "audiogroup10.dat",
                "audiogroup11.dat",
                "audiogroup12.dat",
                "audiogroup15.dat",
                "audiogroup2.dat",
                "audiogroup3.dat",
                "audiogroup4.dat",
                "audiogroup6.dat",
                "audiogroup7.dat",
                "audiogroup8.dat",
                "data.win",
                "GameAnalyticsExt.ext",
                "gamedata_order.json",
                "options.ini",
                "r_b_mall_layout.bin",
                "r_b_swamp_layout.bin",
                "r_i_mall.bin",
                "r_i_mall_undergound.bin",
                "template_index.json",
                "ZERO Sievert.exe",
                "ZS_vanilla\\gamedata\\ammo.json",
                "ZS_vanilla\\gamedata\\armor.json",
                "ZS_vanilla\\gamedata\\backpack.json",
                "ZS_vanilla\\gamedata\\barter.json",
                "ZS_vanilla\\gamedata\\base_storage_use.json",
                "ZS_vanilla\\gamedata\\book.json",
                "ZS_vanilla\\gamedata\\book_r.json",
                "ZS_vanilla\\gamedata\\caliber.json",
                "ZS_vanilla\\gamedata\\chest.json",
                "ZS_vanilla\\gamedata\\consumable.json",
                "ZS_vanilla\\gamedata\\documents.json",
                "ZS_vanilla\\gamedata\\grenade.json",
                "ZS_vanilla\\gamedata\\headset.json",
                "ZS_vanilla\\gamedata\\injector.json",
                "ZS_vanilla\\gamedata\\key.json",
                "ZS_vanilla\\gamedata\\languages.json",
                "ZS_vanilla\\gamedata\\medication.json",
                "ZS_vanilla\\gamedata\\npc.json",
                "ZS_vanilla\\gamedata\\npc_preset.json",
                "ZS_vanilla\\gamedata\\repair_armor.json",
                "ZS_vanilla\\gamedata\\repair_weapon.json",
                "ZS_vanilla\\gamedata\\stat.json",
                "ZS_vanilla\\gamedata\\trader.json",
                "ZS_vanilla\\gamedata\\upgrade_base_kit.json",
                "ZS_vanilla\\gamedata\\weapon.json",
                "ZS_vanilla\\gamedata\\weapon_glance_stat.json",
                "ZS_vanilla\\gamedata\\w_mod.json",
                "ZS_vanilla\\languages\\chinese_simplified\\chinese_simplified.csv",
                "ZS_vanilla\\languages\\english\\english.csv",
                "ZS_vanilla\\languages\\german\\german.csv",
                "ZS_vanilla\\languages\\japanese\\japanese.csv",
                "ZS_vanilla\\languages\\korean\\korean.csv",
                "ZS_vanilla\\languages\\pt_br\\pt_br.csv",
                "ZS_vanilla\\languages\\spanish_es\\spanish_es.csv",
                "ZS_vanilla\\languages\\spanish_latam\\spanish_latam.csv",
                "ZS_vanilla\\ui\\mm_difficulty.ui",
                "ZS_vanilla\\ui\\mm_difficulty_settings.ui",
                "ZS_vanilla\\ui\\mm_news.ui",
                "ZS_vanilla\\ui\\mm_new_game_items.ui",
                "ZS_vanilla\\ui\\mm_settings_languages.ui",
                "ZS_vanilla\\ui\\mm_sidebar.ui",
                "ZS_vanilla\\ui\\mm_sidebar_main.ui",
                "ZS_vanilla\\ui\\mm_sidebar_settings.ui",
            };
            List<string> vanillaFilePaths = fileList.Select(file => Path.Combine(gamePath, file)).ToList();
            List<string> patchFilePaths = fileList.Select(file => Path.Combine(patchPath, file)).ToList();

            Stopwatch stopwatch = Stopwatch.StartNew();
            ApplyDeltaPatches(vanillaFilePaths, patchFilePaths);
            string moddedPath = "foobar";
            string outputPath = "foobar";
            //bulkCreateDeltaPatch(gamePath, moddedPath, outputPath);
            stopwatch.Stop();
            Console.WriteLine("");
            Log.Information($"Done, Elapsed time: {stopwatch.Elapsed.TotalSeconds:F2} seconds ({stopwatch.ElapsedMilliseconds} ms)\n\n Press any key to close.");

            if (patchException)
            {
                Console.WriteLine("");
                Log.Error("Some files failed to be patched, only genuine steam files are accepted.");
            }

            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Log.Error($"An error has occurred: {ex}");
            Console.ReadKey();
        }
    }

    static string GetSteamGamePath(string appId)
    {
        string steamPath = GetSteamInstallPath();
        if (string.IsNullOrEmpty(steamPath))
        {
            return null;
        }

        List<string> steamLibraryFolders = GetSteamLibraryFolders(steamPath);

        foreach (var libraryFolder in steamLibraryFolders)
        {
            string gamePath = Path.Combine(libraryFolder, "steamapps", "common", "ZERO Sievert");
            if (Directory.Exists(gamePath))
            {
                return gamePath;
            }

            // Also check for games installed via the "appmanifest" system
            string appManifestPath = Path.Combine(libraryFolder, "steamapps", $"appmanifest_{appId}.acf");
            if (File.Exists(appManifestPath))
            {
                string installDir = ParseAppManifestForInstallDir(appManifestPath);
                if (!string.IsNullOrEmpty(installDir))
                {
                    gamePath = Path.Combine(libraryFolder, "steamapps", "common", installDir);
                    if (Directory.Exists(gamePath))
                    {
                        return gamePath;
                    }
                }
            }
        }

        return null;
    }

    static List<string> GetSteamLibraryFolders(string steamPath)
    {
        var libraryFolders = new List<string>();

        libraryFolders.Add(steamPath);

        // Parse the libraryfolders.vdf file to find additional library folders
        string libraryFoldersVdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFoldersVdfPath))
        {
            try
            {
                string[] lines = File.ReadAllLines(libraryFoldersVdfPath);
                foreach (var line in lines)
                {
                    if (line.Contains("path"))
                    {
                        string path = line.Split('"')[3]; // Extract the path from the line
                        libraryFolders.Add(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error reading libraryfolders.vdf: {ex.Message}");
            }
        }

        return libraryFolders;
    }

    static string ParseAppManifestForInstallDir(string appManifestPath)
    {
        try
        {
            string[] lines = File.ReadAllLines(appManifestPath);
            foreach (var line in lines)
            {
                if (line.Contains("installdir"))
                {
                    return line.Split('"')[3]; // Extract the installdir value
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error reading appmanifest: {ex.Message}");
        }

        return null;
    }

    static string GetSteamInstallPath()
    {
        string[] registryPaths = new[]
        {
            @"SOFTWARE\Wow6432Node\Valve\Steam",
            @"SOFTWARE\Valve\Steam"
        };

        foreach (var path in registryPaths)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
            {
                if (key != null)
                {
                    object installPath = key.GetValue("InstallPath");
                    object installPathAlt = key.GetValue("SteamPath");
                    if (installPath != null)
                    {
                        return installPath.ToString();
                    }
                    else if (installPathAlt != null)
                    {
                        return installPathAlt.ToString();
                    }
                }
            }
        }

        return null;
    }

    static string SelectFolder(string description)
    {
        using (var folderDialog = new FolderBrowserDialog())
        {
            folderDialog.Description = description;
            folderDialog.SelectedPath = Environment.CurrentDirectory;

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                return folderDialog.SelectedPath;
            }
        }
        return null;
    }

    static void CopyAllFilesAndDirectories(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(targetDir, fileName);
            File.Copy(file, destFile, true);
            Log.Debug($"Copied {file} into {destFile}");
        }

        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyAllFilesAndDirectories(subDir, destSubDir);
        }
    }

    static void ApplyDeltaPatches(List<string> vanillaFilePaths, List<string> patchFilePaths)
    {
        for (int i = 0; i < vanillaFilePaths.Count; i++)
        {
            string vanillaFilePath = vanillaFilePaths[i];
            string patchFilePath = patchFilePaths[i] + ".xdelta";
            string outputFilePath = vanillaFilePath;

            if (File.Exists(vanillaFilePath) && File.Exists(patchFilePath))
            {
                try
                {
                    applyDeltaPatch(vanillaFilePath, patchFilePath, outputFilePath);
                    Log.Information($"Patched {vanillaFilePath}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to apply patch for {vanillaFilePath} : {ex}");

                    if (!patchException)
                        patchException = true;
                }
            }
            else
            {
                Log.Warning($"File not found: {vanillaFilePath} or {patchFilePath}");
            }
        }
    }

    static void applyDeltaPatch(string sourceFilePath, string patchFilePath, string outputFilePath)
    {
        byte[] originalData = File.ReadAllBytes(sourceFilePath);
        byte[] patchData = File.ReadAllBytes(patchFilePath);

        byte[] patchedData = Xdelta3Lib.Decode(source: originalData, delta: patchData).ToArray();

        File.WriteAllBytes(outputFilePath, patchedData);
    }

    static string EscapeBackslashes(string input)
    {
        return input.Replace("\\", "\\\\");
    }
}
