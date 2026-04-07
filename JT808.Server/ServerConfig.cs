namespace JT808.Server;

/// <summary>
/// 服务器配置类
/// </summary>
public class ServerConfig
{
    /// <summary>
    /// 服务器监听IP地址，默认为 0.0.0.0 (监听所有网卡)
    /// </summary>
    public string IpAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// 服务器监听端口，默认为 8809
    /// </summary>
    public int Port { get; set; } = 8809;

    /// <summary>
    /// TCP listen 队列长度 (kernel SOMAXCONN), 默认 4096
    /// 注意: Linux 实际值会被 /proc/sys/net/core/somaxconn 限制
    /// </summary>
    public int Backlog { get; set; } = 4096;

    /// <summary>
    /// 应用层最大并发连接数 (硬上限), 默认 12000
    /// 超过此数的新连接会被立即拒绝
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 12000;

    /// <summary>
    /// 位置数据存储目录，默认为 LocationData
    /// </summary>
    public string LocationDataDirectory { get; set; } = "LocationData";

    /// <summary>
    /// 位置数据归档目录 (可选, 留空则不启用归档)
    /// 启用后会在该目录下按 yyyyMMdd 创建当日子文件夹, 把最新的位置 XML 同时写一份过去
    /// 文件名仍按手机号 12 位命名, 只保留最新一条
    /// 不影响 LocationDataDirectory 的原有写入
    /// </summary>
    public string LocationArchiveDirectory { get; set; } = "";

    /// <summary>
    /// 多媒体数据存储目录，默认为 MediaData
    /// </summary>
    public string MediaDataDirectory { get; set; } = "MediaData";

    /// <summary>
    /// 会话超时时间(分钟)，默认为 30 分钟
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// 日志级别: Trace, Debug, Information, Warning, Error, Critical
    /// 高并发场景推荐 Warning, 调试时再调成 Debug
    /// </summary>
    public string LogLevel { get; set; } = "Warning";
}
