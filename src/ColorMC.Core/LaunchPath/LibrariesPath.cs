using ColorMC.Core.Config;
using ColorMC.Core.Helpers;
using ColorMC.Core.Net;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Minecraft;
using ColorMC.Core.Utils;
using System.Collections.Concurrent;

namespace ColorMC.Core.LaunchPath;

/// <summary>
/// 运行库路径
/// </summary>
public static class LibrariesPath
{
    public const string Name = "libraries";
    public static string BaseDir { get; private set; }
    public static string NativeDir { get; private set; }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="dir">运行路径</param>
    public static void Init(string dir)
    {
        BaseDir = dir + "/" + Name;
        NativeDir = $"{BaseDir}" + $"/native-{SystemInfo.Os}-{SystemInfo.SystemArch}".ToLower();

        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(NativeDir);
    }

    /// <summary>
    /// native路径
    /// </summary>
    /// <param name="version">游戏版本</param>
    /// <returns>路径</returns>
    public static string GetNativeDir(string version)
    {
        string dir = $"{NativeDir}/{version}";
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 检查游戏运行库
    /// </summary>
    /// <param name="obj">游戏数据</param>
    /// <returns>丢失的库</returns>
    public static async Task<List<DownloadItemObj>> CheckGameLib(this GameArgObj obj, CancellationToken cancel)
    {
        var list = new List<DownloadItemObj>();
        var list1 = await DownloadItemHelper.BuildGameLibs(obj);

        await Parallel.ForEachAsync(list1, cancel, async (item, cancel) =>
        {
            if (cancel.IsCancellationRequested)
                return;

            if (!File.Exists(item.Local))
            {
                list.Add(item);
                return;
            }
            if (ConfigUtils.Config.GameCheck.CheckLibSha1)
            {
                using var stream = new FileStream(item.Local, FileMode.Open, FileAccess.Read,
                    FileShare.Read);
                var sha1 = await HashHelper.GenSha1Async(stream);
                if (!string.IsNullOrWhiteSpace(item.SHA1) && item.SHA1 != sha1)
                {
                    list.Add(item);
                    return;
                }
            }
            if (item.Later != null)
            {
                using var stream = new FileStream(item.Local, FileMode.Open, FileAccess.Read,
                        FileShare.Read);
                item.Later.Invoke(stream);
            }

            return;
        });

        return list;
    }

    /// <summary>
    /// 检查Forge的运行库
    /// </summary>
    /// <param name="obj">游戏实例</param>
    /// <returns>丢失的库</returns>
    public static async Task<ConcurrentBag<DownloadItemObj>?> CheckForgeLib(this GameSettingObj obj, bool neo, CancellationToken cancel)
    {
        var version1 = VersionPath.GetVersion(obj.Version)!;
        var v2 = CheckHelpers.ISGameVersionV2(version1);
        if (v2)
        {
            GameHelper.ReadyForgeWrapper();
        }

        var forge = neo ?
            VersionPath.GetNeoForgeObj(obj.Version, obj.LoaderVersion!) :
            VersionPath.GetForgeObj(obj.Version, obj.LoaderVersion!);
        if (forge == null)
            return null;

        //forge本体
        var list1 = DownloadItemHelper.BuildForgeLibs(forge, obj.Version, obj.LoaderVersion!, neo);

        var forgeinstall = neo ?
            VersionPath.GetNeoForgeInstallObj(obj.Version, obj.LoaderVersion!) :
            VersionPath.GetForgeInstallObj(obj.Version, obj.LoaderVersion!);
        if (forgeinstall == null && v2)
            return null;

        //forge安装器
        if (forgeinstall != null)
        {
            var list2 = DownloadItemHelper.BuildForgeLibs(forgeinstall, obj.Version,
                obj.LoaderVersion!, neo);
            list1.AddRange(list2);
        }

        var list = new ConcurrentBag<DownloadItemObj>();

        await Parallel.ForEachAsync(list1, cancel, async (item, cancel) =>
        {
            if (cancel.IsCancellationRequested)
                return;

            if (!File.Exists(item.Local))
            {
                list.Add(item);
                return;
            }
            if (item.SHA1 == null)
                return;

            if (!ConfigUtils.Config.GameCheck.CheckLibSha1)
                return;
            using var stream = new FileStream(item.Local, FileMode.Open, FileAccess.ReadWrite,
                FileShare.ReadWrite);
            var sha1 = await HashHelper.GenSha1Async(stream);
            if (item.SHA1 != sha1)
            {
                list.Add(item);
            }
        });

        return list;
    }

    /// <summary>
    /// 检查Fabric的运行库
    /// </summary>
    /// <param name="obj">游戏实例</param>
    /// <returns>丢失的库</returns>
    public static List<DownloadItemObj>? CheckFabricLib(this GameSettingObj obj, CancellationToken cancel)
    {
        var fabric = VersionPath.GetFabricObj(obj.Version, obj.LoaderVersion!);
        if (fabric == null)
            return null;

        var list = new List<DownloadItemObj>();

        foreach (var item in fabric.libraries)
        {
            if (cancel.IsCancellationRequested)
                break;

            var name = PathHelper.ToPathAndName(item.name);
            string file = $"{BaseDir}/{name.Path}";
            if (!File.Exists(file))
            {
                list.Add(new()
                {
                    Url = UrlHelper.DownloadFabric(item.url + name.Path, BaseClient.Source),
                    Name = name.Name,
                    Local = $"{BaseDir}/{name.Path}"
                });
                continue;
            }
        }

        return list;
    }

    /// <summary>
    /// 检查Quilt的运行库
    /// </summary>
    /// <param name="obj">游戏实例</param>
    /// <returns>丢失的库</returns>
    public static List<DownloadItemObj>? CheckQuiltLib(this GameSettingObj obj, CancellationToken cancel)
    {
        var quilt = VersionPath.GetQuiltObj(obj.Version, obj.LoaderVersion!);
        if (quilt == null)
            return null;

        var list = new List<DownloadItemObj>();

        foreach (var item in quilt.libraries)
        {
            if (cancel.IsCancellationRequested)
                return null;

            var name = PathHelper.ToPathAndName(item.name);
            string file = $"{BaseDir}/{name.Path}";
            if (!File.Exists(file))
            {
                list.Add(new()
                {
                    Url = UrlHelper.DownloadQuilt(item.url + name.Path, BaseClient.Source),
                    Name = name.Name,
                    Local = $"{BaseDir}/{name.Path}"
                });
                continue;
            }
        }

        return list;
    }

    /// <summary>
    /// 获取游戏核心路径
    /// </summary>
    /// <param name="version">游戏版本</param>
    /// <returns>游戏路径</returns>
    public static string GetGameFile(string version)
    {
        return Path.GetFullPath($"{BaseDir}/net/minecraft/client/{version}/client-{version}.jar");
    }

    /// <summary>
    /// 获取OptiFine路径
    /// </summary>
    /// <param name="obj">游戏实例</param>
    /// <returns></returns>
    public static string GetOptiFineLib(this GameSettingObj obj)
    {
        return GetOptiFineLib(obj.Version, obj.LoaderVersion!);
    }

    /// <summary>
    /// 获取OptiFine路径
    /// </summary>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">optifine版本</param>
    /// <returns></returns>
    public static string GetOptiFineLib(string mc, string version)
    {
        return $"{BaseDir}/optifine/installer/OptiFine-{mc}-{version}.jar";
    }

    /// <summary>
    /// 检测OptiFine是否存在
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static bool CheckOptifineLib(this GameSettingObj obj)
    {
        return File.Exists(GetOptiFineLib(obj));
    }
}
