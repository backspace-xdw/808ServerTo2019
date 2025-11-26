using JT808.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// 加载配置文件
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// 绑定配置
var serverConfig = new ServerConfig();
configuration.GetSection("ServerConfig").Bind(serverConfig);

// 解析日志级别
var logLevel = Enum.TryParse<LogLevel>(serverConfig.LogLevel, true, out var level)
    ? level
    : LogLevel.Information;

// 创建日志工厂
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(logLevel);
});

var logger = loggerFactory.CreateLogger<JT808TcpServer>();

// 创建并启动服务器
var server = new JT808TcpServer(
    logger,
    ipAddress: serverConfig.IpAddress,
    port: serverConfig.Port,
    backlog: serverConfig.MaxConnections,
    locationDataDir: serverConfig.LocationDataDirectory,
    mediaDataDir: serverConfig.MediaDataDirectory,
    sessionTimeoutMinutes: serverConfig.SessionTimeoutMinutes);

Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("JT808-2019 车载终端通讯服务器");
Console.WriteLine("基于 JT/T 808-2019 协议");
Console.WriteLine($"支持 {serverConfig.MaxConnections}+ 并发连接");
Console.WriteLine("支持 2013 和 2019 版本自动识别");
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine();
Console.WriteLine("当前配置:");
Console.WriteLine($"  监听地址: {serverConfig.IpAddress}:{serverConfig.Port}");
Console.WriteLine($"  位置目录: {serverConfig.LocationDataDirectory}");
Console.WriteLine($"  媒体目录: {serverConfig.MediaDataDirectory}");
Console.WriteLine($"  会话超时: {serverConfig.SessionTimeoutMinutes} 分钟");
Console.WriteLine($"  日志级别: {serverConfig.LogLevel}");
Console.WriteLine();

try
{
    server.Start();

    // 检查是否在后台运行
    bool isBackgroundMode = Console.IsInputRedirected || !Console.KeyAvailable;

    if (isBackgroundMode)
    {
        Console.WriteLine("后台模式运行，将每10秒显示一次统计...");
        Console.WriteLine("按 Ctrl+C 退出");
        Console.WriteLine();

        // 后台模式：定时显示统计
        while (true)
        {
            Thread.Sleep(10000);
            ShowStatisticsSimple(server);
        }
    }
    else
    {
        Console.WriteLine("按任意键查看统计信息, 按 Q 退出...");
        Console.WriteLine();

        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Q)
            {
                break;
            }

            // 显示统计信息
            ShowStatistics(server);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"服务器运行错误: {ex.Message}");
}
finally
{
    server.Stop();
    Console.WriteLine("服务器已停止");
}

static void ShowStatistics(JT808TcpServer server)
{
    Console.Clear();
    Console.WriteLine("=".PadRight(100, '='));
    Console.WriteLine("服务器统计信息 (JT/T 808-2019)");
    Console.WriteLine("=".PadRight(100, '='));
    Console.WriteLine();

    var sessionManager = server.GetSessionManager();
    var sessions = sessionManager.GetAllSessions().ToList();

    Console.WriteLine($"在线终端数: {sessions.Count}");
    Console.WriteLine($"已鉴权终端: {sessions.Count(s => s.IsAuthenticated)}");
    Console.WriteLine($"2019版本: {sessions.Count(s => s.Is2019Version)}");
    Console.WriteLine($"2013版本: {sessions.Count(s => !s.Is2019Version)}");
    Console.WriteLine();

    Console.WriteLine("终端列表:");
    Console.WriteLine("-".PadRight(100, '-'));
    Console.WriteLine($"{"手机号",-20} {"版本",-6} {"鉴权",-6} {"收/发消息",-15} {"IMEI",-18} {"最后活跃",-20}");
    Console.WriteLine("-".PadRight(100, '-'));

    foreach (var session in sessions.Take(15))
    {
        var version = session.Is2019Version ? $"2019" : "2013";
        var imei = session.IMEI ?? "-";

        Console.WriteLine($"{session.PhoneNumber,-20} " +
                         $"{version,-6} " +
                         $"{(session.IsAuthenticated ? "是" : "否"),-6} " +
                         $"{session.ReceivedMessages}/{session.SentMessages,-15} " +
                         $"{imei,-18} " +
                         $"{session.LastActiveTime:yyyy-MM-dd HH:mm:ss,-20}");
    }

    if (sessions.Count > 15)
    {
        Console.WriteLine($"... 还有 {sessions.Count - 15} 个终端未显示");
    }

    Console.WriteLine();
    Console.WriteLine("按任意键刷新, 按 Q 退出...");
}

static void ShowStatisticsSimple(JT808TcpServer server)
{
    var sessionManager = server.GetSessionManager();
    var sessions = sessionManager.GetAllSessions().ToList();

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 在线: {sessions.Count}, " +
                     $"已鉴权: {sessions.Count(s => s.IsAuthenticated)}, " +
                     $"2019版本: {sessions.Count(s => s.Is2019Version)}, " +
                     $"2013版本: {sessions.Count(s => !s.Is2019Version)}");
}
