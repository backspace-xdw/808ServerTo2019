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
    /// 最大并发连接数，默认为 10000
    /// </summary>
    public int MaxConnections { get; set; } = 10000;

    /// <summary>
    /// 位置数据存储目录，默认为 LocationData
    /// </summary>
    public string LocationDataDirectory { get; set; } = "LocationData";

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
    /// </summary>
    public string LogLevel { get; set; } = "Information";
}
