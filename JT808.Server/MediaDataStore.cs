using System.Collections.Concurrent;
using JT808.Protocol;

namespace JT808.Server;

/// <summary>
/// 多媒体分包缓存项
/// </summary>
public class MediaPackageCache
{
    public string PhoneNumber { get; set; } = string.Empty;
    public uint MultimediaId { get; set; }
    public MultimediaType Type { get; set; }
    public MultimediaFormat Format { get; set; }
    public MultimediaEvent Event { get; set; }
    public byte ChannelId { get; set; }
    public ushort TotalPackages { get; set; }
    public Dictionary<ushort, byte[]> Packages { get; set; } = new();
    public DateTime CreateTime { get; set; } = DateTime.Now;
    public LocationInfo? Location { get; set; }
}

/// <summary>
/// 多媒体数据存储器 - 支持分包组装
/// </summary>
public class MediaDataStore
{
    private readonly string _dataDirectory;
    private readonly object _lockObj = new();

    // 分包缓存: key = "手机号_多媒体ID"
    private readonly ConcurrentDictionary<string, MediaPackageCache> _packageCache = new();

    // 缓存超时时间（分钟）
    private const int CacheTimeoutMinutes = 10;

    public MediaDataStore(string dataDirectory = "MediaData")
    {
        _dataDirectory = dataDirectory;

        // 确保数据目录存在
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }

        // 启动清理任务
        Task.Run(CleanupCacheTask);
    }

    /// <summary>
    /// 处理多媒体数据（支持分包）
    /// </summary>
    /// <param name="phoneNumber">终端手机号</param>
    /// <param name="multimedia">多媒体数据</param>
    /// <param name="totalPackages">总包数（0表示不分包）</param>
    /// <param name="packageIndex">当前包序号（从1开始）</param>
    /// <returns>如果数据完整则返回保存的文件路径，否则返回null</returns>
    public string? ProcessMedia(string phoneNumber, MultimediaDataUpload multimedia,
        ushort totalPackages, ushort packageIndex)
    {
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);

        // 不分包或只有1包，直接保存
        if (totalPackages <= 1)
        {
            return SaveMediaFile(normalizedPhone, multimedia);
        }

        // 分包处理
        var cacheKey = $"{normalizedPhone}_{multimedia.MultimediaId}";

        // 获取或创建缓存项
        var cache = _packageCache.GetOrAdd(cacheKey, _ => new MediaPackageCache
        {
            PhoneNumber = normalizedPhone,
            MultimediaId = multimedia.MultimediaId,
            Type = multimedia.Type,
            Format = multimedia.Format,
            Event = multimedia.Event,
            ChannelId = multimedia.ChannelId,
            TotalPackages = totalPackages,
            Location = multimedia.Location
        });

        // 添加当前分包数据
        lock (cache)
        {
            cache.Packages[packageIndex] = multimedia.Data;

            // 检查是否收齐所有分包
            if (cache.Packages.Count >= cache.TotalPackages)
            {
                // 组装完整数据
                var completeData = AssemblePackages(cache);

                // 创建完整的多媒体对象
                var completeMultimedia = new MultimediaDataUpload
                {
                    MultimediaId = cache.MultimediaId,
                    Type = cache.Type,
                    Format = cache.Format,
                    Event = cache.Event,
                    ChannelId = cache.ChannelId,
                    Location = cache.Location,
                    Data = completeData
                };

                // 保存文件
                var filePath = SaveMediaFile(normalizedPhone, completeMultimedia);

                // 移除缓存
                _packageCache.TryRemove(cacheKey, out _);

                return filePath;
            }
        }

        return null; // 还未收齐所有分包
    }

    /// <summary>
    /// 组装分包数据
    /// </summary>
    private byte[] AssemblePackages(MediaPackageCache cache)
    {
        // 按包序号排序并组装
        var orderedPackages = cache.Packages
            .OrderBy(p => p.Key)
            .Select(p => p.Value)
            .ToList();

        // 计算总长度
        var totalLength = orderedPackages.Sum(p => p.Length);
        var result = new byte[totalLength];

        // 复制数据
        var offset = 0;
        foreach (var package in orderedPackages)
        {
            Array.Copy(package, 0, result, offset, package.Length);
            offset += package.Length;
        }

        return result;
    }

    /// <summary>
    /// 保存多媒体文件
    /// </summary>
    private string SaveMediaFile(string phoneNumber, MultimediaDataUpload multimedia)
    {
        // 创建以日期命名的子目录 (格式: 2025-03-08)
        var dateDir = Path.Combine(_dataDirectory, DateTime.Now.ToString("yyyy-MM-dd"));
        if (!Directory.Exists(dateDir))
        {
            Directory.CreateDirectory(dateDir);
        }

        // 生成文件名: 手机号_多媒体格式编码_事件项编码_时-分-秒.扩展名
        var timeStr = DateTime.Now.ToString("HH-mm-ss");
        var formatCode = (int)multimedia.Format;
        var eventCode = (int)multimedia.Event;
        var extension = multimedia.GetFileExtensionUpper();
        var fileName = $"{phoneNumber}_{formatCode}_{eventCode}_{timeStr}{extension}";
        var filePath = Path.Combine(dateDir, fileName);

        // 如果文件已存在，添加毫秒避免覆盖
        if (File.Exists(filePath))
        {
            timeStr = DateTime.Now.ToString("HH-mm-ss-fff");
            fileName = $"{phoneNumber}_{formatCode}_{eventCode}_{timeStr}{extension}";
            filePath = Path.Combine(dateDir, fileName);
        }

        lock (_lockObj)
        {
            File.WriteAllBytes(filePath, multimedia.Data);
        }

        return filePath;
    }

    /// <summary>
    /// 标准化手机号为12位
    /// </summary>
    private string NormalizePhoneNumber(string phoneNumber)
    {
        // 去掉前导0
        var trimmed = phoneNumber.TrimStart('0');

        // 如果全是0或空，返回12个0
        if (string.IsNullOrEmpty(trimmed))
        {
            return "000000000000";
        }

        // 补齐到12位（不足前面补0）
        return trimmed.PadLeft(12, '0');
    }

    /// <summary>
    /// 清理超时缓存
    /// </summary>
    private async Task CleanupCacheTask()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

                var expiredKeys = _packageCache
                    .Where(kv => (DateTime.Now - kv.Value.CreateTime).TotalMinutes > CacheTimeoutMinutes)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _packageCache.TryRemove(key, out _);
                }
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }

    /// <summary>
    /// 获取当前缓存的分包数量
    /// </summary>
    public int GetPendingPackageCount() => _packageCache.Count;

    /// <summary>
    /// 获取数据目录路径
    /// </summary>
    public string GetDataDirectory() => _dataDirectory;
}
