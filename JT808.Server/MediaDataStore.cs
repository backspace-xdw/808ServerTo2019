using JT808.Protocol;

namespace JT808.Server;

/// <summary>
/// 多媒体数据存储器 - 存储图片、音频、视频等多媒体文件
/// </summary>
public class MediaDataStore
{
    private readonly string _dataDirectory;
    private readonly object _lockObj = new();

    public MediaDataStore(string dataDirectory = "MediaData")
    {
        _dataDirectory = dataDirectory;

        // 确保数据目录存在
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }

    /// <summary>
    /// 保存多媒体数据到文件
    /// </summary>
    /// <param name="phoneNumber">终端手机号</param>
    /// <param name="multimedia">多媒体数据</param>
    /// <returns>保存的文件路径</returns>
    public string SaveMedia(string phoneNumber, MultimediaDataUpload multimedia)
    {
        // 创建以日期命名的子目录 (格式: 2025-03-08)
        var dateDir = Path.Combine(_dataDirectory, DateTime.Now.ToString("yyyy-MM-dd"));
        if (!Directory.Exists(dateDir))
        {
            Directory.CreateDirectory(dateDir);
        }

        // 生成文件名: 手机号_多媒体格式编码_事件项编码_时-分-秒.扩展名
        // 例如: 014818454246_3_1_10-21-22.JPEG
        var timeStr = DateTime.Now.ToString("HH-mm-ss");
        var formatCode = (int)multimedia.Format;
        var eventCode = (int)multimedia.Event;
        var extension = multimedia.GetFileExtensionUpper();
        var fileName = $"{phoneNumber}_{formatCode}_{eventCode}_{timeStr}{extension}";
        var filePath = Path.Combine(dateDir, fileName);

        lock (_lockObj)
        {
            File.WriteAllBytes(filePath, multimedia.Data);
        }

        return filePath;
    }

    /// <summary>
    /// 获取数据目录路径
    /// </summary>
    public string GetDataDirectory() => _dataDirectory;
}
