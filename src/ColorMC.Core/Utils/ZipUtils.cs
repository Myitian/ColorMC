using ColorMC.Core.Helpers;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using System.Text;
using Crc32 = ICSharpCode.SharpZipLib.Checksum.Crc32;

namespace ColorMC.Core.Utils;

/// <summary>
/// 压缩包处理
/// </summary>
public class ZipUtils
{
    private int Size = 0;
    private int Now = 0;

    /// <summary>
    /// 压缩文件
    /// </summary>
    /// <param name="zipDir">路径</param>
    /// <param name="zipFile">文件名</param>
    /// <param name="filter">过滤</param>
    /// <returns></returns>
    public async Task ZipFile(string zipDir, string zipFile, List<string>? filter = null)
    {
        if (zipDir[^1] != Path.DirectorySeparatorChar)
            zipDir += Path.DirectorySeparatorChar;
        using var s = new ZipOutputStream(PathHelper.OpenWrite(zipFile));
        s.SetLevel(9);
        Size = PathHelper.GetAllFile(zipDir).Count;
        Now = 0;
        await Zip(zipDir, s, zipDir, filter);
        await s.FinishAsync(CancellationToken.None);
        s.Close();
    }

    /// <summary>
    /// 打包Zip
    /// </summary>
    /// <param name="strFile"></param>
    /// <param name="s"></param>
    /// <param name="staticFile"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    private async Task Zip(string strFile, ZipOutputStream s,
        string staticFile, List<string>? filter)
    {
        if (strFile[^1] != Path.DirectorySeparatorChar) strFile += Path.DirectorySeparatorChar;
        var crc = new Crc32();
        string[] filenames = Directory.GetFileSystemEntries(strFile);
        foreach (string file in filenames)
        {
            if (filter != null && filter.Contains(file))
            {
                continue;
            }
            if (Directory.Exists(file))
            {
                await Zip(file, s, staticFile, filter);
            }
            else
            {
                Now++;
                ColorMCCore.UnZipItem?.Invoke(Path.GetFileName(file), Now, Size);
                using var fs = PathHelper.OpenRead(file)!;

                byte[] buffer = new byte[fs.Length];
                await fs.ReadAsync(buffer);
                string tempfile = file[(staticFile.LastIndexOf("\\") + 1)..];
                var entry = new ZipEntry(tempfile)
                {
                    DateTime = DateTime.Now,
                    Size = fs.Length
                };
                crc.Reset();
                crc.Update(buffer);
                entry.Crc = crc.Value;
                await s.PutNextEntryAsync(entry);
                await s.WriteAsync(buffer);
            }
        }
    }

    /// <summary>
    /// 压缩文件
    /// </summary>
    /// <param name="zipFile">压缩包路径</param>
    /// <param name="zipList">压缩的文件</param>
    /// <param name="path">替换的前置路径</param>
    /// <returns></returns>
    public async Task ZipFile(string zipFile, List<string> zipList, string path)
    {
        using var s = new ZipOutputStream(PathHelper.OpenWrite(zipFile));
        s.SetLevel(9);
        Size = zipList.Count;
        Now = 0;
        var crc = new Crc32();

        foreach (var item in zipList)
        {
            string tempfile = item[(path.Length + 1)..];
            if (Directory.Exists(item))
            {
                var entry = new ZipEntry(tempfile + "/")
                {
                    DateTime = DateTime.Now
                };
                await s.PutNextEntryAsync(entry);
            }
            else
            {
                Now++;
                ColorMCCore.UnZipItem?.Invoke(item, Now, Size);
                using var fs = PathHelper.OpenRead(item)!;

                byte[] buffer = new byte[fs.Length];
                await fs.ReadAsync(buffer);

                var entry = new ZipEntry(tempfile)
                {
                    DateTime = DateTime.Now,
                    Size = fs.Length
                };
                crc.Reset();
                crc.Update(buffer);
                entry.Crc = crc.Value;
                await s.PutNextEntryAsync(entry);
                await s.WriteAsync(buffer);
            }
        }
    }

    /// <summary>
    /// 解压
    /// </summary>
    /// <param name="path">解压路径</param>
    /// <param name="local">文件名</param>
    public async Task<bool> Unzip(string path, string local, Stream stream)
    {
        if (local.EndsWith("tar.gz"))
        {
            using var gzipStream = new GZipInputStream(stream);
            var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.UTF8);
            Size = tarArchive.RecordSize;
            tarArchive.ProgressMessageEvent += TarArchive_ProgressMessageEvent;
            tarArchive.ExtractContents(path);
            tarArchive.Close();
        }
        else
        {
            using var s = new ZipFile(stream);
            Size = (int)s.Count;
            foreach (ZipEntry theEntry in s)
            {
                Now++;
                ColorMCCore.UnZipItem?.Invoke(theEntry.Name, Now, Size);

                var file = $"{path}/{theEntry.Name}";
                var info = new FileInfo(file);

                info.Directory?.Create();

                if (info.Name != string.Empty)
                {
                    if (PathHelper.FileHasInvalidChars(info.Name))
                    {
                        if (ColorMCCore.GameRequest == null)
                        {
                            return false;
                        }
                        var res = await ColorMCCore.GameRequest.Invoke(string.Format(
                            LanguageHelper.Get("Core.Zip.Info1"), theEntry.Name));
                        if (!res)
                        {
                            return false;
                        }
                        file = $"{info.Directory!.FullName}/{PathHelper.ReplaceFileName(info.Name)}";
                    }
                    using var stream1 = PathHelper.OpenWrite(file);
                    using var stream2 = s.GetInputStream(theEntry);
                    await stream2.CopyToAsync(stream1);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 进度事件回调
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="entry"></param>
    /// <param name="message"></param>
    private void TarArchive_ProgressMessageEvent(TarArchive archive, TarEntry entry, string message)
    {
        if (entry != null && message == null)
        {
            Now++;
            ColorMCCore.UnZipItem?.Invoke(entry.Name, Now, Size);
        }
    }
}

