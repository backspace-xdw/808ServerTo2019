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
        // 创建以手机号命名的子目录
        var phoneDir = Path.Combine(_dataDirectory, phoneNumber);
        if (!Directory.Exists(phoneDir))
        {
            Directory.CreateDirectory(phoneDir);
        }

        // 生成文件名: 时间戳_多媒体ID_通道ID.扩展名
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{timestamp}_{multimedia.MultimediaId}_CH{multimedia.ChannelId}{multimedia.GetFileExtension()}";
        var filePath = Path.Combine(phoneDir, fileName);

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
