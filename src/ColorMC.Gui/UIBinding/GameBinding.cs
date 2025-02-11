using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ColorMC.Core.Chunk;
using ColorMC.Core.Downloader;
using ColorMC.Core.Game;
using ColorMC.Core.Helpers;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Nbt;
using ColorMC.Core.Net;
using ColorMC.Core.Net.Apis;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Chunk;
using ColorMC.Core.Objs.CurseForge;
using ColorMC.Core.Objs.Login;
using ColorMC.Core.Objs.Minecraft;
using ColorMC.Core.Objs.Modrinth;
using ColorMC.Core.Objs.ServerPack;
using ColorMC.Core.Utils;
using ColorMC.Gui.Objs;
using ColorMC.Gui.UI.Model;
using ColorMC.Gui.UI.Model.Items;
using ColorMC.Gui.Utils;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorMC.Gui.UIBinding;

public static class GameBinding
{
    public static bool IsNotGame => InstancesPath.IsNotGame;
    public static List<GameSettingObj> GetGames()
    {
        return InstancesPath.Games;
    }

    public static async Task<List<string>> GetGameVersion(bool? type1, bool? type2, bool? type3)
    {
        var list = new List<string>();
        var ver = await VersionPath.GetVersions();
        if (ver == null)
        {
            return list;
        }

        foreach (var item in ver.versions)
        {
            if (item.type == "release")
            {
                if (type1 == true)
                {
                    list.Add(item.id);
                }
            }
            else if (item.type == "snapshot")
            {
                if (type2 == true)
                {
                    list.Add(item.id);
                }
            }
            else
            {
                if (type3 == true)
                {
                    list.Add(item.id);
                }
            }
        }

        return list;
    }

    public static async Task<bool> AddGame(string name, string version,
        Loaders loaders, string? loaderversion = null, string? group = null)
    {
        var game = new GameSettingObj()
        {
            Name = name,
            Version = version,
            Loader = loaders,
            LoaderVersion = loaderversion,
            GroupName = group
        };

        game = await InstancesPath.CreateGame(game);

        if (game != null)
        {
            ConfigBinding.SetLastLaunch(game.UUID);
        }

        return game != null;
    }

    public static async Task<bool> AddGame(string name, string local, List<string> unselect, string? group = null)
    {
        var game = new GameSettingObj()
        {
            Name = name,
            Version = (await GetGameVersion(true, false, false))[0],
            Loader = Loaders.Normal,
            LoaderVersion = null,
            GroupName = group
        };

        game = await InstancesPath.CreateGame(game);
        if (game == null)
        {
            return false;
        }

        var res = await game.CopyFile(local, unselect);

        if (!res.Item1)
        {
            await game.Remove();
            App.ShowError(App.Lang("Gui.Error26"), res.Item2);
        }

        var files = Directory.GetFiles(local);
        foreach (var item in files)
        {
            if (item.EndsWith(".json"))
            {
                try
                {
                    var obj = JObject.Parse(PathHelper.ReadText(item)!);
                    if (obj.TryGetValue("patches", out var patch) && patch is JArray array)
                    {
                        foreach (var item1 in array)
                        {
                            var id = item1["id"]?.ToString();
                            var version = item1["version"]?.ToString() ?? "";
                            if (id == "game")
                            {
                                game.Version = version;
                            }
                            else if (id == "forge")
                            {
                                game.LoaderVersion = version;
                                game.Loader = Loaders.Forge;
                            }
                            else if (id == "fabric")
                            {
                                game.LoaderVersion = version;
                                game.Loader = Loaders.Fabric;
                            }
                            else if (id == "quilt")
                            {
                                game.LoaderVersion = version;
                                game.Loader = Loaders.Quilt;
                            }
                            else if (id == "neoforge")
                            {
                                game.LoaderVersion = version;
                                game.Loader = Loaders.NeoForge;
                            }
                        }
                        game.Save();
                        break;
                    }
                }
                catch
                {

                }
            }
        }

        App.ShowGameEdit(game);

        return true;
    }

    public static Task<(bool, GameSettingObj?)> AddPack(string dir, PackType type, string? name, string? group)
    {
        return InstancesPath.InstallZip(dir, type, name, group);
    }

    public static Dictionary<string, List<GameSettingObj>> GetGameGroups()
    {
        return InstancesPath.Groups;
    }

    public static Task<List<string>?> GetCurseForgeGameVersions()
    {
        return CurseForgeAPI.GetGameVersions();
    }

    public static Task<List<string>?> GetModrinthGameVersions()
    {
        return ModrinthAPI.GetGameVersion();
    }

    private static CurseForgeCategoriesObj? CurseForgeCategories;

    public static async Task<Dictionary<string, string>?> GetCurseForgeCategories(
        FileType type = FileType.ModPack)
    {
        if (CurseForgeCategories == null)
        {
            var list6 = await CurseForgeAPI.GetCategories();
            if (list6 == null)
            {
                return null;
            }

            CurseForgeCategories = list6;
        }

        var list7 = from item2 in CurseForgeCategories.data
                    where item2.classId == type switch
                    {
                        FileType.Mod => CurseForgeAPI.ClassMod,
                        FileType.World => CurseForgeAPI.ClassWorld,
                        FileType.Resourcepack => CurseForgeAPI.ClassResourcepack,
                        FileType.Shaderpack => CurseForgeAPI.ClassShaderpack,
                        _ => CurseForgeAPI.ClassModPack
                    }
                    orderby item2.name descending
                    select (item2.name, item2.id);

        return list7.ToDictionary(a => a.id.ToString(), a => a.name);
    }

    private static List<ModrinthCategoriesObj>? ModrinthCategories;

    public static async Task<Dictionary<string, string>?> GetModrinthCategories(
        FileType type = FileType.ModPack)
    {
        if (ModrinthCategories == null)
        {
            var list6 = await ModrinthAPI.GetCategories();
            if (list6 == null)
            {
                return null;
            }

            ModrinthCategories = list6;
        }

        var list7 = from item2 in ModrinthCategories
                    where item2.project_type == type switch
                    {
                        FileType.Shaderpack => ModrinthAPI.ClassShaderpack,
                        FileType.Resourcepack => ModrinthAPI.ClassResourcepack,
                        FileType.ModPack => ModrinthAPI.ClassModPack,
                        _ => ModrinthAPI.ClassMod
                    }
                    && item2.header == "categories"
                    orderby item2.name descending
                    select item2.name;

        return list7.ToDictionary(a => a);
    }

    public static async Task<bool> InstallCurseForge(CurseForgeModObj.Data data,
        CurseForgeObjList.Data data1, string? name, string? group)
    {
        var res = await InstancesPath.InstallCurseForge(data, name, group);
        if (!res.Item1)
        {
            return false;
        }
        if (data1.logo != null)
        {
            await SetGameIconFromUrl(res.Item2!, data1.logo.url);
        }

        return true;
    }

    public static async Task<bool> InstallModrinth(ModrinthVersionObj data,
        ModrinthSearchObj.Hit data1, string? name, string? group)
    {
        var res = await InstancesPath.InstallModrinth(data, name, group);
        if (!res.Item1)
        {
            return false;
        }
        if (data1.icon_url != null)
        {
            await SetGameIconFromUrl(res.Item2!, data1.icon_url);
        }

        return true;
    }

    public static async Task SetGameIconFromUrl(GameSettingObj obj, string url)
    {
        try
        {
            var data = await BaseClient.GetBytes(url);
            if (data.Item1)
            {
                await File.WriteAllBytesAsync(obj.GetIconFile(), data.Item2!);
            }
        }
        catch (Exception e)
        {
            Logs.Error(App.Lang("Gui.Error45"), e);
            App.ShowError(App.Lang("Gui.Error45"), e);
        }
    }

    public static async Task SetGameIconFromFile(BaseModel model, GameSettingObj obj)
    {
        try
        {
            var file = await PathBinding.SelectFile(FileType.Icon);
            if (file != null)
            {
                model.Progress(App.Lang("Gui.Info30"));
                using var info = SKBitmap.Decode(PathHelper.OpenRead(file)!);
                if (info.Width > 200 || info.Height > 200)
                {
                    using var image = await Task.Run(() =>
                    {
                        return ImageUtils.Resize(info, 200, 200);
                    });
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    PathHelper.WriteBytes(obj.GetIconFile(), data.AsSpan().ToArray());
                }
                else
                {
                    using var data = info.Encode(SKEncodedImageFormat.Png, 100);
                    PathHelper.WriteBytes(obj.GetIconFile(), data.AsSpan().ToArray());
                }

                model.ProgressClose();
                model.Notify(App.Lang("Gui.Info29"));
            }
        }
        catch (Exception e)
        {
            Logs.Error(App.Lang("Gui.Error43"), e);
            App.ShowError(App.Lang("Gui.Error43"), e);
        }
    }

    private static async Task<(LoginObj?, string?)> GetUser(BaseModel model)
    {
        var login = UserBinding.GetLastUser();
        if (login == null)
        {
            return (null, App.Lang("Gui.Error40"));
        }
        if (login.AuthType == AuthType.Offline)
        {
            var have = AuthDatabase.Auths.Keys.Any(a => a.Item2 == AuthType.OAuth);
            if (!have)
            {
                BaseBinding.OpUrl(UrlHelper.Minecraft);
                return (null, App.Lang("Gui.Error44"));
            }
        }

        if (UserBinding.IsLock(login))
        {
            var res = await model.ShowWait(App.Lang("Gui.Info36"));
            if (!res)
                return (null, App.Lang("Gui.Error41"));
        }

        return (login, null);
    }

    public static async Task<(bool, string?)> Launch(BaseModel model, GameSettingObj? obj, WorldObj? world = null, bool wait = false)
    {
        if (obj == null)
        {
            return (false, App.Lang("Gui.Error39"));
        }

        if (BaseBinding.IsGameRun(obj))
        {
            return (false, App.Lang("Gui.Error42"));
        }

        var user = await GetUser(model);
        if (user.Item1 == null)
        {
            return (false, user.Item2);
        }

        var res1 = await BaseBinding.Launch(model, obj, user.Item1, world, wait);
        if (res1.Item1)
        {
            ConfigBinding.SetLastLaunch(obj.UUID);
        }
        return res1;
    }


    public static bool AddGameGroup(string name)
    {
        return InstancesPath.AddGroup(name);
    }

    public static void MoveGameGroup(GameSettingObj obj, string? now)
    {
        obj.MoveGameGroup(now);
        App.MainWindow?.LoadMain();
    }

    public static async Task<bool> ReloadVersion()
    {
        await VersionPath.GetFromWeb();

        return await VersionPath.Have();
    }

    public static async void SaveGame(GameSettingObj obj, string? versi, Loaders loader, string? loadv)
    {
        if (!string.IsNullOrWhiteSpace(versi))
        {
            obj.Version = versi;
            var ver = await VersionPath.GetVersions();
            var version1 = ver!.versions.FirstOrDefault(a => a.id == versi);
            if (version1 != null)
            {
                if (version1.type == "release")
                {
                    obj.GameType = GameType.Release;
                }
                else if (version1.type == "snapshot")
                {
                    obj.GameType = GameType.Snapshot;
                }
                else
                {
                    obj.GameType = GameType.Other;
                }
            }
        }
        obj.Loader = loader;
        if (!string.IsNullOrWhiteSpace(loadv))
        {
            obj.LoaderVersion = loadv;
        }
        obj.Save();
    }

    public static void SetGameJvmMemArg(GameSettingObj obj, uint? min, uint? max)
    {
        obj.JvmArg ??= new();
        obj.JvmArg.MinMemory = min;
        obj.JvmArg.MaxMemory = max;
        obj.Save();
    }

    public static void SetGameJvmArg(GameSettingObj obj, JvmArgObj obj1)
    {
        obj.JvmArg = obj1;
        obj.Save();
    }

    public static void SetGameWindow(GameSettingObj obj, WindowSettingObj obj1)
    {
        obj.Window = obj1;
        obj.Save();
    }

    public static void SetGameServer(GameSettingObj obj, ServerObj obj1)
    {
        obj.StartServer = obj1;
        obj.Save();
    }

    public static void SetGameProxy(GameSettingObj obj, ProxyHostObj obj1)
    {
        obj.ProxyHost = obj1;
        obj.Save();
    }

    public static async Task<List<ModDisplayModel>> GetGameMods(GameSettingObj obj,
        bool sha256 = false)
    {
        var list = new List<ModDisplayModel>();
        var list1 = await obj.GetMods(sha256);
        if (list1 == null)
        {
            return list;
        }

        list1.ForEach(item =>
        {
            var obj1 = new ModDisplayModel()
            {
                Name = item.ReadFail ? App.Lang("GameEditWindow.Tab4.Info5")
                : item.name,
                Obj = item
            };

            var item1 = obj.Mods.Values.FirstOrDefault(a => a.SHA1 == item.Sha1);
            if (item1 != null)
            {
                obj1.Obj1 = item1;
            }

            obj1.Enable = !item.Disable;

            list.Add(obj1);
        });
        return list;
    }

    public static (bool, string?) ModEnDi(ModObj obj)
    {
        try
        {
            if (obj.Disable)
            {
                obj.Enable();
            }
            else
            {
                obj.Disable();
            }

            return (true, null);
        }
        catch (Exception e)
        {
            string temp = string.Format(App.Lang("GameEditWindow.Tab4.Error3"), obj.Local);
            Logs.Error(temp, e);
            return (false, temp);
        }
    }

    public static void DeleteMod(ModObj mod)
    {
        string name = new FileInfo(mod.Local).Name;
        foreach (var item in mod.Game.Mods)
        {
            if (item.Value.File == name)
            {
                mod.Game.Mods.Remove(item.Key);
                mod.Game.SaveModInfo();
                break;
            }
        }
        mod.Delete();
    }

    public static Task<bool> AddMods(GameSettingObj obj, IReadOnlyList<IStorageFile> file)
    {
        var list = new List<string>();
        foreach (var item in file)
        {
            var item1 = item.GetPath();
            if (item1 != null)
            {
                list.Add(item1);
            }
        }
        return obj.AddMods(list);
    }

    public static List<string> GetAllConfig(GameSettingObj obj)
    {
        var list = new List<string>();
        var dir = obj.GetGamePath().Length + 1;

        var file = obj.GetOptionsFile();
        if (!File.Exists(file))
        {
            File.Create(file).Dispose();
        }

        list.Add(obj.GetOptionsFile()[dir..]);
        string con = obj.GetConfigPath();

        var list1 = PathHelper.GetAllFile(con);
        foreach (var item in list1)
        {
            list.Add(item.FullName[dir..].Replace("\\", "/"));
        }

        return list;
    }

    public static List<string> GetAllConfig(WorldObj obj)
    {
        var list = new List<string>();
        var dir = obj.Local.Length + 1;

        var list1 = PathHelper.GetAllFile(obj.Local);
        foreach (var item in list1)
        {
            if (item.Extension is ".png" or ".lock")
                continue;
            list.Add(item.FullName[dir..].Replace("\\", "/"));
        }

        return list;
    }

    public static List<string> GetAllTopConfig(GameSettingObj obj)
    {
        var list = new List<string>();
        var dir = obj.GetGamePath();

        var dir1 = new DirectoryInfo(dir);
        var list1 = dir1.GetFileSystemInfos();
        foreach (var item in list1)
        {
            string name = item.Name;
            if (item.Attributes == FileAttributes.Directory)
            {
                if (name == "mods" || name == "resourcepacks")
                    continue;
                name += "/";
            }

            list.Add(name);
        }

        return list;
    }

    public static async Task<ChunkDataObj?> ReadMca(WorldObj obj, string name)
    {
        var dir = obj.Local;

        return await ChunkMca.Read(Path.GetFullPath(dir + "/" + name));
    }

    public static async Task<ChunkDataObj?> ReadMca(GameSettingObj obj, string name)
    {
        var dir = obj.GetGamePath();

        return await ChunkMca.Read(Path.GetFullPath(dir + "/" + name));
    }

    public static async Task<NbtBase?> ReadNbt(WorldObj obj, string name)
    {
        var dir = obj.Local;

        return await NbtBase.Read(Path.GetFullPath(dir + "/" + name));
    }

    public static async Task<NbtBase?> ReadNbt(GameSettingObj obj, string name)
    {
        var dir = obj.GetGamePath();

        return await NbtBase.Read(Path.GetFullPath(dir + "/" + name));
    }

    public static string ReadConfigFile(WorldObj obj, string name)
    {
        var dir = obj.Local;

        return File.ReadAllText(Path.GetFullPath(dir + "/" + name));
    }

    public static string ReadConfigFile(GameSettingObj obj, string name)
    {
        var dir = obj.GetGamePath();

        return File.ReadAllText(Path.GetFullPath(dir + "/" + name));
    }

    public static void SaveConfigFile(WorldObj obj, string name, string? text)
    {
        var dir = obj.Local;

        File.WriteAllText(Path.GetFullPath(dir + "/" + name), text);
    }

    public static void SaveConfigFile(GameSettingObj obj, string name, string? text)
    {
        var dir = obj.GetGamePath();

        File.WriteAllText(Path.GetFullPath(dir + "/" + name), text);
    }

    public static void SaveNbtFile(WorldObj obj, string file, NbtBase nbt)
    {
        var dir = obj.Local;

        nbt.Save(Path.GetFullPath(dir + "/" + file));
    }

    public static void SaveNbtFile(GameSettingObj obj, string file, NbtBase nbt)
    {
        var dir = obj.GetGamePath();

        nbt.Save(Path.GetFullPath(dir + "/" + file));
    }

    public static void SaveMcaFile(WorldObj obj, string file, ChunkDataObj data)
    {
        var dir = obj.Local;

        data.Save(Path.GetFullPath(dir + "/" + file));
    }

    public static void SaveMcaFile(GameSettingObj obj, string file, ChunkDataObj data)
    {
        var dir = obj.GetGamePath();

        data.Save(Path.GetFullPath(dir + "/" + file));
    }

    public static Task<List<WorldObj>> GetWorlds(GameSettingObj obj)
    {
        return obj.GetWorlds();
    }

    public static async Task<bool> AddWorld(GameSettingObj obj, string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return false;
        }
        var res = await obj.AddWorldZip(file);
        if (!res)
        {
            PathBinding.OpFile(file);
        }

        return res;
    }

    public static void DeleteWorld(WorldObj world)
    {
        world.Remove();
    }

    public static Task ExportWorld(WorldObj world, string? file)
    {
        if (file == null)
            return Task.CompletedTask;

        return world.ExportWorldZip(file);
    }

    public static Task<List<ResourcepackObj>> GetResourcepacks(GameSettingObj obj,
        bool sha256 = false)
    {
        return obj.GetResourcepacks(sha256);
    }

    public static void DeleteResourcepack(ResourcepackObj obj)
    {
        PathHelper.Delete(obj.Local);
    }

    public static Task<bool> AddResourcepack(GameSettingObj obj, IReadOnlyList<IStorageFile> file)
    {
        var list = new List<string>();
        foreach (var item in file)
        {
            var item1 = item.GetPath();
            if (item1 != null)
            {
                list.Add(item1);
            }
        }
        return obj.AddResourcepack(list);
    }

    public static void DeleteScreenshot(string file)
    {
        PathHelper.Delete(file);
    }

    public static void ClearScreenshots(GameSettingObj obj)
    {
        obj.ClearScreenshots();
    }

    public static List<string> GetScreenshots(GameSettingObj obj)
    {
        return obj.GetScreenshots();
    }

    public static GameSettingObj? GetGame(string? uuid)
    {
        return InstancesPath.GetGame(uuid);
    }

    public static void OpPath(GameSettingObj obj)
    {
        PathBinding.OpPath(obj, PathType.GamePath);
    }

    public static async Task<IEnumerable<ServerInfoObj>> GetServers(GameSettingObj obj)
    {
        return await obj.GetServerInfos();
    }

    public static Task<List<ShaderpackObj>> GetShaderpacks(GameSettingObj obj)
    {
        return obj.GetShaderpacks();
    }

    public static Task AddServer(GameSettingObj obj, string name, string ip)
    {
        return Task.Run(() =>
        {
            obj.AddServer(name, ip);
        });
    }

    public static Task DeleteServer(GameSettingObj obj, ServerInfoObj server)
    {
        return Task.Run(() =>
        {
            obj.RemoveServer(server.Name, server.IP);
        });
    }

    public static void DeleteConfig(GameSettingObj obj)
    {
        obj.JvmArg = null;
        obj.JvmName = "";
        obj.JvmLocal = "";
        obj.StartServer = null;
        obj.ProxyHost = null;
        obj.AdvanceJvm = null;

        obj.Save();
    }

    public static void SetAdvanceJvmArg(GameSettingObj obj, AdvanceJvmObj obj1)
    {
        obj.AdvanceJvm = obj1;

        obj.Save();
    }

    public static Task<bool> AddShaderpack(GameSettingObj obj, IReadOnlyList<IStorageFile> file)
    {
        var list = new List<string>();
        foreach (var item in file)
        {
            var item1 = item.GetPath();
            if (item1 != null)
            {
                list.Add(item1);
            }
        }

        return obj.AddShaderpack(list);
    }

    public static void DeleteShaderpack(ShaderpackObj obj)
    {
        obj.Delete();
    }

    public static async Task<List<SchematicObj>> GetSchematics(GameSettingObj obj)
    {
        var list = await obj.GetSchematics();
        var list1 = new List<SchematicObj>();
        foreach (var item in list)
        {
            if (item.Broken)
            {
                list1.Add(new()
                {
                    Name = App.Lang("Gui.Error14"),
                    Local = item.Local,
                });
            }
            else
            {
                list1.Add(item);
            }
        }

        return list1;
    }

    public static bool AddSchematic(GameSettingObj obj, IReadOnlyList<IStorageFile> file)
    {
        var list = new List<string>();
        foreach (var item in file)
        {
            var item1 = item.GetPath();
            if (item1 != null)
            {
                list.Add(item1);
            }
        }

        return obj.AddSchematic(list);
    }

    public static void DeleteSchematic(SchematicObj obj)
    {
        obj.Delete();
    }

    public static void SetModInfo(GameSettingObj obj, CurseForgeModObj.Data? data)
    {
        if (data == null)
            return;

        data.FixDownloadUrl();

        var obj1 = new ModInfoObj()
        {
            FileId = data.id.ToString(),
            ModId = data.modId.ToString(),
            File = data.fileName,
            Name = data.displayName,
            Url = data.downloadUrl,
            SHA1 = data.hashes.Where(a => a.algo == 1)
                .Select(a => a.value).FirstOrDefault()
        };
        if (!obj.Mods.TryAdd(obj1.ModId, obj1))
        {
            obj.Mods[obj1.ModId] = obj1;
        }

        obj.SaveModInfo();
    }

    public static void SetModInfo(GameSettingObj obj, ModrinthVersionObj? data)
    {
        if (data == null)
        {
            return;
        }

        var file = data.files.FirstOrDefault(a => a.primary) ?? data.files[0];
        var obj1 = new ModInfoObj()
        {
            FileId = data.id.ToString(),
            ModId = data.project_id,
            File = file.filename,
            Name = data.name,
            Url = file.url,
            SHA1 = file.hashes.sha1
        };
        if (!obj.Mods.TryAdd(obj1.ModId, obj1))
        {
            obj.Mods[obj1.ModId] = obj1;
        }

        obj.SaveModInfo();
    }

    public static async Task<bool> BackupWorld(WorldObj world)
    {
        try
        {
            await world.Backup();
            return true;
        }
        catch (Exception e)
        {
            string text = App.Lang("Gui.Error20");
            Logs.Error(text, e);
            App.ShowError(text, e);
            return false;
        }
    }

    public static Task<bool> BackupWorld(GameSettingObj obj, FileInfo item1)
    {
        return obj.UnzipBackupWorld(item1);
    }


    public static void SetGameName(GameSettingObj obj, string data)
    {
        obj.Name = data;
        obj.Save();

        App.MainWindow?.LoadMain();
    }

    public static async Task<bool> CopyGame(GameSettingObj obj, string data)
    {
        if (BaseBinding.IsGameRun(obj))
        {
            return false;
        }

        if (await obj.Copy(data) == null)
        {
            return false;
        }

        App.MainWindow?.LoadMain();

        return true;
    }

    public static void SaveServerPack(ServerPackObj obj1)
    {
        obj1.Save();
    }

    public static ServerPackObj? GetServerPack(GameSettingObj obj)
    {
        return obj.GetServerPack().Item2;
    }

    public static Task<bool> GenServerPack(ServerPackObj obj, string local)
    {
        return obj.GenServerPack(local);
    }

    public static async void CopyServer(ServerInfoObj obj)
    {
        await BaseBinding.CopyTextClipboard($"{obj.Name}\n{obj.IP}");
    }

    public static Task<bool> ModCheck(List<ModDisplayModel> list)
    {
        return Task.Run(() =>
        {
            var modid = new List<string>();
            var mod = new List<ModDisplayModel>();
            foreach (var item in list)
            {
                if (item.Obj.modid == null || item.Obj.Disable)
                    continue;
                modid.Add(item.Obj.modid);
                if (item.Obj.InJar?.Count > 0)
                {
                    foreach (var item1 in item.Obj.InJar)
                    {
                        modid.Add(item1.modid);
                    }
                }
                mod.Add(item);
            }
            modid.Add("forge");
            modid.Add("fabric");
            modid.Add("minecraft");
            modid.Add("java");
            modid.Add("fabricloader");

            var lost = new ConcurrentBag<(string, List<string>)>();

            Parallel.ForEach(mod, item =>
            {
                if (item == null)
                {
                    return;
                }

                var list1 = new List<string>();
                if (item.Obj.requiredMods != null)
                {
                    foreach (var item1 in item.Obj.requiredMods)
                    {
                        var list2 = item1.Split(",");
                        list1.AddRange(list2);
                        foreach (var item2 in list2)
                        {
                            if (modid.Contains(item2))
                            {
                                list1.Remove(item2);
                            }
                        }
                    }
                }
                if (item.Obj.dependencies != null)
                {
                    foreach (var item1 in item.Obj.dependencies)
                    {
                        var list2 = item1.Split(",");
                        list1.AddRange(list2);
                        foreach (var item2 in list2)
                        {
                            if (modid.Contains(item2))
                            {
                                list1.Remove(item2);
                            }
                        }
                    }
                }

                if (list1.Count > 0)
                {
                    lost.Add((item.Name, list1));
                }
            });

            if (!lost.IsEmpty)
            {
                static string GetString(List<string> list)
                {
                    var str = new StringBuilder();
                    foreach (var item in list)
                    {
                        str.Append(item).Append(',');
                    }

                    return str.ToString()[..^1];
                }

                var str = new StringBuilder();
                foreach (var item in lost)
                {
                    str.Append(string.Format(App.Lang("Gui.Info25"), item.Item1,
                        GetString(item.Item2))).Append(Environment.NewLine);
                }

                App.ShowError(App.Lang("Gui.Info26"), str.ToString());
                return false;
            }

            return true;
        });
    }

    public static List<string> GetLogList(GameSettingObj obj)
    {
        return obj.GetLogFiles();
    }

    public static async Task<string?> ReadLog(GameSettingObj obj, string name)
    {
        if (BaseBinding.IsGameRun(obj))
        {
            if (name.EndsWith("latest.log") || name.EndsWith("debug.log"))
                return null;
        }
        return await Task.Run(() => obj.ReadLog(name));
    }

    public static Task<bool> ModPackUpdate(GameSettingObj obj, CurseForgeModObj.Data fid)
    {
        return obj.UpdateModPack(fid);
    }

    public static Task<bool> ModPackUpdate(GameSettingObj obj, ModrinthVersionObj fid)
    {
        return obj.UpdateModPack(fid);
    }

    public static List<ModDisplayModel> ModDisable(ModDisplayModel item, List<ModDisplayModel> items)
    {
        var list = new List<ModDisplayModel>();
        foreach (var item1 in items)
        {
            if (!item1.Enable || item1.Obj.modid == item.Obj.modid)
            {
                continue;
            }
            if (item1.Obj.dependencies != null)
            {
                if (item1.Obj.dependencies.Contains(item.Obj.modid))
                {
                    list.Add(item1);
                    continue;
                }
                else if (item.Obj.InJar != null)
                {
                    foreach (var item2 in item.Obj.InJar)
                    {
                        if (item1.Obj.dependencies.Contains(item2.modid))
                        {
                            list.Add(item1);
                            break;
                        }
                    }
                }
            }
            else if (item1.Obj.requiredMods != null)
            {
                if (item1.Obj.requiredMods.Contains(item.Obj.modid))
                {
                    list.Add(item1);
                    continue;
                }
                else if (item.Obj.InJar != null)
                {
                    foreach (var item2 in item.Obj.InJar)
                    {
                        if (item1.Obj.requiredMods.Contains(item2.modid))
                        {
                            list.Add(item1);
                            break;
                        }
                    }
                }
            }
        }

        return list;
    }

    public static void GameStateUpdate(GameSettingObj obj)
    {
        if (App.GameLogWindows.TryGetValue(obj.UUID, out var win1))
        {
            win1.Update();
        }
    }

    public static async Task<bool> AddFile(GameSettingObj obj, IDataObject data, FileType type)
    {
        if (!data.Contains(DataFormats.Files))
        {
            return false;
        }
        var list = data.GetFiles();
        if (list == null)
        {
            return false;
        }
        switch (type)
        {
            case FileType.Mod:
                var list1 = new List<string>();
                foreach (var item in list)
                {
                    var file = item.TryGetLocalPath();
                    if (string.IsNullOrWhiteSpace(file))
                    {
                        continue;
                    }
                    if (File.Exists(file) && file.ToLower().EndsWith(".jar"))
                    {
                        list1.Add(file);
                    }
                }

                return await obj.AddMods(list1);
            case FileType.World:
                foreach (var item in list)
                {
                    var file = item.TryGetLocalPath();
                    if (string.IsNullOrWhiteSpace(file))
                    {
                        continue;
                    }
                    if (File.Exists(file) && file.ToLower().EndsWith(".zip"))
                    {
                        return await obj.AddWorldZip(file);
                    }
                }
                return false;
            case FileType.Resourcepack:
                list1 = [];
                foreach (var item in list)
                {
                    var file = item.TryGetLocalPath();
                    if (string.IsNullOrWhiteSpace(file))
                    {
                        continue;
                    }
                    if (File.Exists(file) && file.ToLower().EndsWith(".zip"))
                    {
                        list1.Add(file);
                    }
                }
                return await obj.AddResourcepack(list1);
            case FileType.Shaderpack:
                list1 = [];
                foreach (var item in list)
                {
                    var file = item.TryGetLocalPath();
                    if (string.IsNullOrWhiteSpace(file))
                        continue;
                    if (File.Exists(file) && file.ToLower().EndsWith(".zip"))
                    {
                        list1.Add(file);
                    }
                }
                return await obj.AddShaderpack(list1);
            case FileType.Schematic:
                list1 = [];
                foreach (var item in list)
                {
                    var file = item.TryGetLocalPath();
                    if (string.IsNullOrWhiteSpace(file))
                        continue;
                    var file1 = file.ToLower();
                    if (File.Exists(file) &&
                        (file1.EndsWith(Schematic.Name1) || file1.EndsWith(Schematic.Name2)))
                    {
                        list1.Add(file);
                    }
                }
                return obj.AddSchematic(list1);
        }

        return false;
    }

    public static PackType CheckType(string local)
    {
        Stream? stream = PathHelper.OpenRead(local);

        if (stream == null)
        {
            return PackType.ColorMC;
        }

        try
        {
            if (local.EndsWith(".mrpack"))
            {
                return PackType.Modrinth;
            }
            if (local.EndsWith(".zip"))
            {
                using ZipFile zFile = new(stream);
                foreach (ZipEntry item in zFile)
                {
                    if (item.Name.EndsWith("game.json"))
                    {
                        return PackType.ColorMC;
                    }
                    else if (item.Name.EndsWith("mcbbs.packmeta"))
                    {
                        return PackType.HMCL;
                    }
                    else if (item.Name.EndsWith("instance.cfg"))
                    {
                        return PackType.MMC;
                    }
                    else if (item.Name.EndsWith("manifest.json"))
                    {
                        return PackType.CurseForge;
                    }
                    else if (item.Name.Contains(".minecraft/"))
                    {
                        return PackType.ZipPack;
                    }
                }
            }
        }
        finally
        {
            stream.Close();
            stream.Dispose();
        }

        return PackType.ColorMC;
    }

    public static async Task<bool> UnZipCloudConfig(GameSettingObj obj, CloudDataObj data, string local)
    {
        data.Config.Clear();
        return await Task.Run(() =>
        {
            try
            {
                using var s = new ZipInputStream(PathHelper.OpenRead(local));
                ZipEntry theEntry;
                while ((theEntry = s.GetNextEntry()) != null)
                {
                    string filename = $"{obj.GetBasePath()}/{theEntry.Name}";
                    data.Config.Add(theEntry.Name);
                    var directoryName = Path.GetDirectoryName(filename);
                    string fileName = Path.GetFileName(theEntry.Name);

                    if (directoryName?.Length > 0)
                    {
                        Directory.CreateDirectory(directoryName);
                    }

                    if (fileName != string.Empty)
                    {
                        using var streamWriter = PathHelper.OpenWrite(filename);

                        s.CopyTo(streamWriter);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Logs.Error(App.Lang("AddGameWindow.Tab1.Error17"), e);
            }
            return false;
        });
    }
    public static async Task<(bool, string?)> DownloadCloud(CloundListObj obj, string? group)
    {
        var game = await InstancesPath.CreateGame(new()
        {
            Name = obj.Name,
            UUID = obj.UUID,
            GroupName = group
        });
        if (game == null)
        {
            return (false, App.Lang("AddGameWindow.Tab1.Error10"));
        }

        var cloud = new CloudDataObj()
        {
            Config = []
        };

        GameCloudUtils.SetCloudData(game, cloud);
        string local = Path.GetFullPath(game.GetBasePath() + "/config.zip");
        var res = await GameCloudUtils.DownloadConfig(obj, local);
        if (res != 100)
        {
            return (false, App.Lang("AddGameWindow.Tab1.Error11"));
        }
        await UnZipCloudConfig(game, cloud, local);
        var temp = await GameCloudUtils.HaveCloud(game);
        cloud.ConfigTime = DateTime.Parse(temp.Item3!);
        GameCloudUtils.SetCloudData(game, cloud);

        game = game.Reload();

        if (game.Mods != null)
        {
            var list = new List<DownloadItemObj>();
            foreach (var item in game.Mods.Values)
            {
                list.Add(new()
                {
                    Url = item.Url,
                    Name = item.File,
                    Local = game.GetGamePath() + "/" + item.Path + "/" + item.File,
                    SHA1 = item.SHA1
                });
            }

            if (list.Count > 0)
            {
                var res1 = await DownloadManager.Start(list);
                if (!res1)
                {
                    return (false, App.Lang("AddGameWindow.Tab1.Error12"));
                }
            }
        }

        return (true, null);
    }

    public static GameSettingObj? GetGameByName(string name)
    {
        return InstancesPath.GetGameByName(name);
    }

    public static async void CheckCloudAndOpen(GameSettingObj obj)
    {
        var res = await GameCloudUtils.HaveCloud(obj);
        if (res.Item1 == 100 && res.Item2)
        {
            Dispatcher.UIThread.Post(() =>
            {
                App.ShowGameCloud(obj, true);
            });
        }
    }

    public static async Task<bool> DeleteGame(BaseModel model, GameSettingObj obj)
    {
        InfoBinding.Window = model;
        var res = await obj.Remove();
        App.MainWindow?.LoadMain();
        if (res)
        {
            Dispatcher.UIThread.Post(() =>
            {
                App.CloseGameWindow(obj);
            });
        }

        return res;
    }

    public static async Task<(bool, string?)> DownloadServerPack(BaseModel model,
        string? name, string? group, string text)
    {
        InfoBinding.Window = model;
        try
        {
            var data = await BaseClient.GetString(text + "server.json");
            if (!data.Item1)
            {
                return (false, App.Lang("AddGameWindow.Tab1.Error15"));
            }
            var obj = JsonConvert.DeserializeObject<ServerPackObj>(data.Item2!);
            if (obj == null)
            {
                return (false, App.Lang("AddGameWindow.Tab1.Error16"));
            }

            var game = obj.Game;
            if (!string.IsNullOrWhiteSpace(name))
            {
                game.Name = name;
            }
            if (!string.IsNullOrWhiteSpace(group))
            {
                game.GroupName = group;
            }
            game.UUID = null!;
            game.LaunchData = null!;
            game.ServerUrl = text;
            game.ModPackType = SourceType.ColorMC;
            game = await InstancesPath.CreateGame(game);

            if (game == null)
            {
                return (false, App.Lang("AddGameWindow.Tab1.Error10"));
            }

            model.Progress(App.Lang("AddGameWindow.Tab1.Info15"));

            var res1 = await obj.Update(game);
            if (!res1)
            {
                model.ProgressClose();
                model.ShowOk(App.Lang("AddGameWindow.Tab1.Error12"), async () =>
                {
                    await game.Remove();
                });

                return (false, null);
            }

            PathHelper.WriteText(game.GetServerPackFile(), data.Item2!);

            return (true, null);
        }
        catch (Exception e)
        {
            string temp = App.Lang("AddGameWindow.Tab1.Error16");
            Logs.Error(temp, e);
            return (false, temp);
        }
    }

    public static bool DataPackDisE(DataPackObj obj)
    {
        if (BaseBinding.IsGameRun(obj.World.Game))
        {
            return false;
        }
        return DataPack.DisE(new List<DataPackObj>() { obj }, obj.World);
    }

    public static bool DataPackDisE(IEnumerable<DataPackModel> pack)
    {
        var list = new List<DataPackObj>();
        foreach (var item in pack)
        {
            list.Add(item.Pack);
        }
        if (BaseBinding.IsGameRun(list[0].World.Game))
        {
            return false;
        }
        return DataPack.DisE(list, list[0].World);
    }

    public static async Task<bool> DeleteDataPack(DataPackModel item)
    {
        if (BaseBinding.IsGameRun(item.Pack.World.Game))
        {
            return false;
        }
        return await DataPack.Delete([item.Pack], item.Pack.World);
    }

    public static async Task<bool> DeleteDataPack(IEnumerable<DataPackModel> items)
    {
        var list = new List<DataPackObj>();
        foreach (var item in items)
        {
            list.Add(item.Pack);
        }
        if (BaseBinding.IsGameRun(list[0].World.Game))
        {
            return false;
        }
        return await DataPack.Delete(list, list[0].World);
    }

    public static async Task<List<Loaders>> GetSupportLoader(string version)
    {
        var loaders = new List<Loaders>();
        Task[] list =
        [
            Task.Run(async () =>
            {
                var list = await WebBinding.GetForgeSupportVersion();
                if (list != null && list.Contains(version))
                {
                    loaders.Add(Loaders.Forge);
                }
            }),
            Task.Run(async () =>
            {
                var list = await WebBinding.GetFabricSupportVersion();
                if (list != null && list.Contains(version))
                {
                    loaders.Add(Loaders.Fabric);
                }
            }),
            Task.Run(async () =>
            {
                var list = await WebBinding.GetQuiltSupportVersion();
                if (list != null && list.Contains(version))
                {
                    loaders.Add(Loaders.Quilt);
                }
            }),
            Task.Run(async () =>
            {
                var list = await WebBinding.GetNeoForgeSupportVersion();
                if (list != null && list.Contains(version))
                {
                    loaders.Add(Loaders.NeoForge);
                }
            }),
            Task.Run(async () =>
            {
                var list = await WebBinding.GetOptifineSupportVersion();
                if (list != null && list.Contains(version))
                {
                    loaders.Add(Loaders.OptiFine);
                }
            })
        ];

        await Task.WhenAll(list);

        loaders.Sort();

        return loaders;
    }
}