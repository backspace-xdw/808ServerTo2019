using System.Net;
using System.Net.Sockets;
using JT808.Protocol;
using Microsoft.Extensions.Logging;

namespace JT808.Server;

/// <summary>
/// JT808-2019 TCP服务器(支持10000+并发连接)
/// </summary>
public class JT808TcpServer
{
    private readonly ILogger<JT808TcpServer> _logger;
    private readonly SessionManager _sessionManager;
    private Socket? _serverSocket;
    private bool _isRunning;
    private readonly int _port;
    private readonly int _backlog;

    public JT808TcpServer(ILogger<JT808TcpServer> logger, int port = 8809, int backlog = 10000)
    {
        _logger = logger;
        _sessionManager = new SessionManager();
        _port = port;
        _backlog = backlog;
    }

    /// <summary>
    /// 启动服务器
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            _logger.LogWarning("服务器已经在运行中");
            return;
        }

        try
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 设置Socket选项以支持高并发
            _serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            var endPoint = new IPEndPoint(IPAddress.Any, _port);
            _serverSocket.Bind(endPoint);
            _serverSocket.Listen(_backlog);

            _isRunning = true;
            _logger.LogInformation($"JT808-2019服务器启动成功,监听端口: {_port}");

            // 启动清理超时会话的定时任务
            Task.Run(CleanupTask);

            // 开始接受连接
            BeginAccept();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "服务器启动失败");
            throw;
        }
    }

    /// <summary>
    /// 停止服务器
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;

        try
        {
            _serverSocket?.Close();
            _logger.LogInformation("服务器已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "服务器停止时发生错误");
        }
    }

    /// <summary>
    /// 开始接受连接
    /// </summary>
    private void BeginAccept()
    {
        if (!_isRunning || _serverSocket == null)
            return;

        try
        {
            var args = new SocketAsyncEventArgs();
            args.Completed += OnAcceptCompleted;

            if (!_serverSocket.AcceptAsync(args))
            {
                ProcessAccept(args);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接受连接时发生错误");
            BeginAccept(); // 继续接受新连接
        }
    }

    /// <summary>
    /// 接受连接完成回调
    /// </summary>
    private void OnAcceptCompleted(object? sender, SocketAsyncEventArgs args)
    {
        ProcessAccept(args);
    }

    /// <summary>
    /// 处理接受的连接
    /// </summary>
    private void ProcessAccept(SocketAsyncEventArgs args)
    {
        try
        {
            if (args.SocketError == SocketError.Success && args.AcceptSocket != null)
            {
                var clientSocket = args.AcceptSocket;
                var remoteEndPoint = clientSocket.RemoteEndPoint?.ToString() ?? "Unknown";

                // 创建会话
                var session = _sessionManager.AddSession(clientSocket);
                _logger.LogInformation($"新连接: {remoteEndPoint}, SessionId: {session.SessionId}, 当前在线: {_sessionManager.GetOnlineCount()}");

                // 开始接收数据
                BeginReceive(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理连接时发生错误");
        }
        finally
        {
            args.Dispose();
            // 继续接受新连接
            BeginAccept();
        }
    }

    /// <summary>
    /// 开始接收数据
    /// </summary>
    private void BeginReceive(SessionInfo session)
    {
        try
        {
            var args = new SocketAsyncEventArgs();
            args.UserToken = session;
            args.SetBuffer(new byte[2048], 0, 2048);
            args.Completed += OnReceiveCompleted;

            if (!session.Socket.ReceiveAsync(args))
            {
                ProcessReceive(args);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"开始接收数据时发生错误, SessionId: {session.SessionId}");
            _sessionManager.RemoveSession(session.SessionId);
        }
    }

    /// <summary>
    /// 接收数据完成回调
    /// </summary>
    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs args)
    {
        ProcessReceive(args);
    }

    /// <summary>
    /// 处理接收的数据
    /// </summary>
    private void ProcessReceive(SocketAsyncEventArgs args)
    {
        var session = args.UserToken as SessionInfo;
        if (session == null)
        {
            args.Dispose();
            return;
        }

        try
        {
            if (args.SocketError == SocketError.Success && args.BytesTransferred > 0)
            {
                // 获取接收到的数据
                var receivedData = new byte[args.BytesTransferred];
                Buffer.BlockCopy(args.Buffer!, args.Offset, receivedData, 0, args.BytesTransferred);

                // 添加到消息缓冲区
                session.MessageBuffer.Append(receivedData);

                // 从缓冲区提取完整消息
                var messages = session.MessageBuffer.ExtractMessages();

                // 更新会话活跃时间
                _sessionManager.UpdateActiveTime(session.SessionId);

                // 处理每个完整消息
                foreach (var message in messages)
                {
                    session.ReceivedMessages++;
                    Task.Run(() => ProcessMessage(session, message));
                }

                // 继续接收
                args.SetBuffer(args.Offset, args.Buffer!.Length - args.Offset);
                if (!session.Socket.ReceiveAsync(args))
                {
                    ProcessReceive(args);
                }
            }
            else
            {
                // 连接断开
                _logger.LogInformation($"连接断开: SessionId: {session.SessionId}, 手机号: {session.PhoneNumber}");
                _sessionManager.RemoveSession(session.SessionId);
                args.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"处理接收数据时发生错误, SessionId: {session.SessionId}");
            _sessionManager.RemoveSession(session.SessionId);
            args.Dispose();
        }
    }

    /// <summary>
    /// 处理消息
    /// </summary>
    private void ProcessMessage(SessionInfo session, byte[] data)
    {
        try
        {
            // 解码消息
            var message = JT808Decoder.Decode(data);
            if (message == null)
            {
                _logger.LogWarning($"消息解码失败, SessionId: {session.SessionId}");
                return;
            }

            // 检测协议版本
            if (message.Header.Is2019Version && !session.Is2019Version)
            {
                session.Is2019Version = true;
                session.ProtocolVersion = message.Header.ProtocolVersion;
                _logger.LogInformation($"检测到2019版本协议, 版本号: {session.ProtocolVersion}, SessionId: {session.SessionId}");
            }

            _logger.LogInformation($"收到消息: MsgId=0x{message.Header.MessageId:X4}, " +
                                 $"Phone={message.Header.PhoneNumber}, Serial={message.Header.SerialNumber}, " +
                                 $"Version={message.Header.ProtocolVersion}");

            // 绑定手机号
            if (string.IsNullOrEmpty(session.PhoneNumber) && !string.IsNullOrEmpty(message.Header.PhoneNumber))
            {
                _sessionManager.BindPhoneNumber(session.SessionId, message.Header.PhoneNumber);
            }

            // 根据消息ID处理
            byte[]? response = null;
            switch (message.Header.MessageId)
            {
                case JT808MessageId.TerminalRegister:
                    response = HandleRegister(session, message);
                    break;

                case JT808MessageId.TerminalAuthentication:
                    response = HandleAuthentication(session, message);
                    break;

                case JT808MessageId.TerminalHeartbeat:
                    response = HandleHeartbeat(session, message);
                    break;

                case JT808MessageId.LocationReport:
                    response = HandleLocationReport(session, message);
                    break;

                case JT808MessageId.LocationBatchUpload:
                    response = HandleLocationBatchUpload(session, message);
                    break;

                default:
                    _logger.LogWarning($"未处理的消息类型: 0x{message.Header.MessageId:X4}");
                    // 返回通用应答-不支持
                    response = JT808Encoder.EncodePlatformGeneralResponse(
                        message.Header.PhoneNumber,
                        message.Header.SerialNumber,
                        message.Header.MessageId,
                        (byte)CommonResult.NotSupported,
                        session.Is2019Version);
                    break;
            }

            // 发送应答
            if (response != null)
            {
                SendData(session, response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"处理消息时发生错误, SessionId: {session.SessionId}");
        }
    }

    /// <summary>
    /// 处理终端注册
    /// </summary>
    private byte[] HandleRegister(SessionInfo session, JT808Message message)
    {
        var registerInfo = JT808Decoder.DecodeRegisterInfo(message.Body);
        if (registerInfo != null)
        {
            _logger.LogInformation($"终端注册: 手机号={message.Header.PhoneNumber}, " +
                                 $"车牌={registerInfo.PlateNumber}, 终端ID={registerInfo.TerminalId}, " +
                                 $"制造商={registerInfo.ManufacturerId}, 型号={registerInfo.TerminalModel}");

            // 保存终端信息
            session.TerminalModel = registerInfo.TerminalModel;

            // 生成鉴权码(实际应用中应该存储到数据库)
            string authCode = Guid.NewGuid().ToString("N").Substring(0, 20); // 2019版本最长50字节
            _sessionManager.SetAuthenticated(session.SessionId, false, authCode);

            // 返回注册应答
            return JT808Encoder.EncodeRegisterResponse(
                message.Header.PhoneNumber,
                message.Header.SerialNumber,
                RegisterResult.Success,
                authCode,
                session.Is2019Version);
        }

        return JT808Encoder.EncodeRegisterResponse(
            message.Header.PhoneNumber,
            message.Header.SerialNumber,
            RegisterResult.NoVehicleInDatabase,
            "",
            session.Is2019Version);
    }

    /// <summary>
    /// 处理终端鉴权 (2019版本)
    /// </summary>
    private byte[] HandleAuthentication(SessionInfo session, JT808Message message)
    {
        var authInfo = JT808Decoder.DecodeAuthenticationInfo(message.Body);
        if (authInfo != null)
        {
            _logger.LogInformation($"终端鉴权: 手机号={message.Header.PhoneNumber}, " +
                                 $"IMEI={authInfo.IMEI}, 软件版本={authInfo.SoftwareVersion}");

            // 保存终端信息 (2019新增)
            session.IMEI = authInfo.IMEI;
            session.SoftwareVersion = authInfo.SoftwareVersion;
        }

        // 标记为已鉴权(实际应用中应该验证鉴权码)
        _sessionManager.SetAuthenticated(session.SessionId, true);

        return JT808Encoder.EncodePlatformGeneralResponse(
            message.Header.PhoneNumber,
            message.Header.SerialNumber,
            message.Header.MessageId,
            (byte)CommonResult.Success,
            session.Is2019Version);
    }

    /// <summary>
    /// 处理心跳
    /// </summary>
    private byte[] HandleHeartbeat(SessionInfo session, JT808Message message)
    {
        _logger.LogDebug($"收到心跳: 手机号={message.Header.PhoneNumber}");

        return JT808Encoder.EncodePlatformGeneralResponse(
            message.Header.PhoneNumber,
            message.Header.SerialNumber,
            message.Header.MessageId,
            (byte)CommonResult.Success,
            session.Is2019Version);
    }

    /// <summary>
    /// 处理位置上报
    /// </summary>
    private byte[] HandleLocationReport(SessionInfo session, JT808Message message)
    {
        var location = JT808Decoder.DecodeLocationInfo(message.Body);
        if (location != null)
        {
            var additionalInfo = location.AdditionalInfoList.Count > 0
                ? $", 附加信息: {location.AdditionalInfoList.Count}项"
                : "";

            _logger.LogInformation($"位置上报: 手机号={message.Header.PhoneNumber}, " +
                                 $"经度={location.GetLongitude():F6}, 纬度={location.GetLatitude():F6}, " +
                                 $"速度={location.GetSpeed():F1}km/h, 时间={location.GpsTime:yyyy-MM-dd HH:mm:ss}, " +
                                 $"ACC={(location.IsAccOn ? "开" : "关")}, 定位={(location.IsPositioned ? "是" : "否")}" +
                                 additionalInfo);

            // TODO: 存储位置数据到数据库
        }

        return JT808Encoder.EncodePlatformGeneralResponse(
            message.Header.PhoneNumber,
            message.Header.SerialNumber,
            message.Header.MessageId,
            (byte)CommonResult.Success,
            session.Is2019Version);
    }

    /// <summary>
    /// 处理批量位置上传
    /// </summary>
    private byte[] HandleLocationBatchUpload(SessionInfo session, JT808Message message)
    {
        _logger.LogInformation($"批量位置上传: 手机号={message.Header.PhoneNumber}, 数据长度={message.Body.Length}");

        // TODO: 解析批量位置数据

        return JT808Encoder.EncodePlatformGeneralResponse(
            message.Header.PhoneNumber,
            message.Header.SerialNumber,
            message.Header.MessageId,
            (byte)CommonResult.Success,
            session.Is2019Version);
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    private void SendData(SessionInfo session, byte[] data)
    {
        try
        {
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(data, 0, data.Length);
            args.Completed += (sender, e) => e.Dispose();

            if (!session.Socket.SendAsync(args))
            {
                args.Dispose();
            }

            session.SentMessages++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"发送数据失败, SessionId: {session.SessionId}");
        }
    }

    /// <summary>
    /// 清理超时会话定时任务
    /// </summary>
    private async Task CleanupTask()
    {
        while (_isRunning)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                _sessionManager.CleanupTimeoutSessions(30);

                var onlineCount = _sessionManager.GetOnlineCount();
                var sessions = _sessionManager.GetAllSessions();
                var authenticatedCount = sessions.Count(s => s.IsAuthenticated);
                var version2019Count = sessions.Count(s => s.Is2019Version);

                _logger.LogInformation($"在线终端数: {onlineCount}, 已鉴权: {authenticatedCount}, 2019版本: {version2019Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理会话时发生错误");
            }
        }
    }

    /// <summary>
    /// 获取会话管理器
    /// </summary>
    public SessionManager GetSessionManager() => _sessionManager;
}
