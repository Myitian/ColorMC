using ColorMC.Core.Helpers;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Loader;
using ColorMC.Core.Utils;
using Newtonsoft.Json;
using System.Xml;

namespace ColorMC.Core.Net.Apis;

/// <summary>
/// Forge网络请求
/// </summary>
public static class ForgeAPI
{
    private static List<string>? s_supportVersion;
    private static readonly Dictionary<string, List<string>> s_forgeVersion = new();

    private static List<string>? s_neoSupportVersion;
    private static readonly Dictionary<string, List<string>> s_neoForgeVersion = new();

    /// <summary>
    /// 获取支持的版本
    /// </summary>
    /// <returns></returns>
    public static async Task<List<string>?> GetSupportVersion(bool neo, SourceLocal? local = null)
    {
        try
        {
            if ((neo ? s_neoSupportVersion : s_supportVersion) != null)
                return neo ? s_neoSupportVersion : s_supportVersion;

            if (local == SourceLocal.BMCLAPI
                || local == SourceLocal.MCBBS)
            {
                if (neo)
                {
                    if (s_neoSupportVersion == null)
                    {
                        await LoadFromSource(true);
                    }
                    return s_neoSupportVersion;
                }
                string url = UrlHelper.ForgeVersion(local);
                var data = await BaseClient.GetString(url);
                if (data.Item1 == false)
                {
                    ColorMCCore.OnError?.Invoke(LanguageHelper.Get("Core.Http.Error7"),
                        new Exception(url), false);
                    return null;
                }
                var obj = JsonConvert.DeserializeObject<List<string>>(data.Item2!);
                if (obj == null)
                    return null;

                if (neo)
                    s_neoSupportVersion = obj;
                else
                    s_supportVersion = obj;

                return obj;
            }
            else
            {
                await LoadFromSource(neo);

                return neo ? s_neoSupportVersion : s_supportVersion;
            }
        }
        catch (Exception e)
        {
            Logs.Error(LanguageHelper.Get("Core.Http.Forge.Error5"), e);
            return null;
        }
    }

    /// <summary>
    /// 获取版本列表
    /// </summary>
    /// <param name="mc">游戏版本</param>
    /// <param name="local">下载源</param>
    /// <returns>版本列表</returns>
    public static async Task<List<string>?> GetVersionList(bool neo, string mc, SourceLocal? local = null)
    {
        try
        {
            List<string> list = new();
            if (local == SourceLocal.BMCLAPI
                || local == SourceLocal.MCBBS)
            {
                string url = neo
                    ? UrlHelper.NeoForgeVersions(mc, local)
                    : UrlHelper.ForgeVersions(mc, local);
                var data = await BaseClient.GetString(url);
                if (data.Item1 == false)
                {
                    ColorMCCore.OnError?.Invoke(LanguageHelper.Get("Core.Http.Error7"),
                        new Exception(url), false);
                    return null;
                }
                var list1 = new List<Version>();

                if (neo)
                {
                    var obj = JsonConvert.DeserializeObject<List<NeoForgeVersionObj>>(data.Item2!);
                    if (obj == null)
                        return null;

                    foreach (var item in obj)
                    {
                        list1.Add(new(item.version));
                    }
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<List<ForgeVersionObj1>>(data.Item2!);
                    if (obj == null)
                        return null;

                    foreach (var item in obj)
                    {
                        list1.Add(new(item.version));
                    }
                }

                list1.Reverse();

                var list2 = new List<string>();
                foreach (var item in list1)
                {
                    list2.Add(item.ToString());
                }

                return list2;
            }
            else
            {
                if (!neo && s_forgeVersion.TryGetValue(mc, out var list1))
                {
                    var list2 = new List<string>();
                    foreach (var item in list1)
                    {
                        list2.Add(item.ToString());
                    }
                    return list2;
                }
                else if (neo && s_neoForgeVersion.TryGetValue(mc, out list1))
                {
                    var list2 = new List<string>();
                    foreach (var item in list1)
                    {
                        list2.Add(item.ToString());
                    }
                    return list2;
                }

                await LoadFromSource(neo);

                if (neo)
                {
                    if (s_neoForgeVersion.TryGetValue(mc, out var list4))
                    {
                        var list2 = new List<string>();
                        foreach (var item in list4)
                        {
                            list2.Add(item.ToString());
                        }
                        return list2;
                    }
                }
                else
                {
                    if (s_forgeVersion.TryGetValue(mc, out var list4))
                    {
                        var list2 = new List<string>();
                        foreach (var item in list4)
                        {
                            list2.Add(item.ToString());
                        }
                        return list2;
                    }
                }
                return null;
            }
        }
        catch (Exception e)
        {
            Logs.Error(LanguageHelper.Get("Core.Http.Forge.Error6"), e);
            return null;
        }
    }

    /// <summary>
    /// 从xml获取信息
    /// </summary>
    /// <param name="neo">是否为NeoForge</param>
    /// <returns></returns>
    public static async Task LoadFromSource(bool neo)
    {
        var url = neo ? UrlHelper.NeoForgeVersion(SourceLocal.Offical) :
                    UrlHelper.ForgeVersion(SourceLocal.Offical);
        var html = await BaseClient.GetString(url);
        if (html.Item1 == false)
        {
            ColorMCCore.OnError?.Invoke(LanguageHelper.Get("Core.Http.Error7"),
                new Exception(url), false);
            return;
        }

        var xml = new XmlDocument();
        xml.LoadXml(html.Item2!);

        var list = new List<string>();

        var node = xml.SelectNodes("//metadata/versioning/versions/version");
        if (node == null)
        {
            return;
        }

        if (neo)
        {
            s_neoForgeVersion.Clear();
        }
        else
        {
            s_forgeVersion.Clear();
        }
        foreach (XmlNode item in node)
        {
            var str = item.InnerText;
            var args = str.Split('-');
            var mc1 = args[0];
            var version = args[1];

            if (!list.Contains(mc1))
            {
                list.Add(mc1);
            }

            if (neo)
            {
                if (s_neoForgeVersion.TryGetValue(mc1, out var list2))
                {
                    list2.Add(new(version));
                }
                else
                {
                    var list3 = new List<string>() { version };
                    s_neoForgeVersion.Add(mc1, list3);
                }
            }
            else
            {
                if (s_forgeVersion.TryGetValue(mc1, out var list2))
                {
                    list2.Add(new(version));
                }
                else
                {
                    var list3 = new List<string>() { version };
                    s_forgeVersion.Add(mc1, list3);
                }
            }
        }

        foreach (var item in s_neoForgeVersion.Values)
        {
            item.Reverse();
        }
        foreach (var item in s_forgeVersion.Values)
        {
            item.Reverse();
        }

        if (neo)
        {
            s_neoSupportVersion = list;
        }
        else
        {
            s_supportVersion = list;
        }

        if (neo)
        {
            url = UrlHelper.NeoForgeNewVersion(SourceLocal.Offical);

            html = await BaseClient.GetString(url);
            if (html.Item1 == false)
            {
                ColorMCCore.OnError?.Invoke(LanguageHelper.Get("Core.Http.Error7"),
                    new Exception(url), false);
                return;
            }

            xml = new XmlDocument();
            xml.LoadXml(html.Item2!);

            node = xml.SelectNodes("//metadata/versioning/versions/version");
            if (node?.Count > 0)
            {
                foreach (XmlNode item in node)
                {
                    var str = item.InnerText;
                    var args = str.Split('.');
                    var mc1 = "1." + args[0] + "." + args[1];
                    var version = str;

                    if (!s_neoSupportVersion!.Contains(mc1))
                    {
                        s_neoSupportVersion.Add(mc1);
                    }

                    if (s_neoForgeVersion.TryGetValue(mc1, out var list2))
                    {
                        list2.Add(version);
                    }
                    else
                    {
                        var list3 = new List<string>() { version };
                        s_neoForgeVersion.Add(mc1, list3);
                    }
                }

                foreach (var item in s_neoForgeVersion.Values)
                {
                    item.Reverse();
                }
            }
        }
    }
}
