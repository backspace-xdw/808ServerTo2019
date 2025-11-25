using System.Collections.Concurrent;
using System.Net.Sockets;
using JT808.Protocol;

namespace JT808.Server;

/// <summary>
/// 会话信息
/// </summary>
public class SessionInfo
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public Socket Socket { get; set; } = null!;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime ConnectTime { get; set; } = DateTime.Now;
    public DateTime LastActiveTime { get; set; } = DateTime.Now;
    public bool IsAuthenticated { get; set; } = false;
    public string? AuthCode { get; set; }
    public long ReceivedMessages { get; set; } = 0;
    public long SentMessages { get; set; } = 0;
    public JT808MessageBuffer MessageBuffer { get; set; } = new JT808MessageBuffer();

    // 2019版本特有信息
    public bool Is2019Version { get; set; } = true;
    public byte ProtocolVersion { get; set; } = 0x01;
    public string? IMEI { get; set; }
    public string? SoftwareVersion { get; set; }
    public string? TerminalModel { get; set; }

    /// <summary>
    /// 车牌号
    /// </summary>
    public string? PlateNumber { get; set; }
}

/// <summary>
/// 会话管理器
/// </summary>
public class SessionManager
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _phoneToSession = new();
    private readonly object _lockObj = new();

    /// <summary>
    /// 添加会话
    /// </summary>
    public SessionInfo AddSession(Socket socket)
    {
        var session = new SessionInfo
        {
            Socket = socket
        };

        _sessions.TryAdd(session.SessionId, session);
        return session;
    }

    /// <summary>
    /// 移除会话
    /// </summary>
    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            if (!string.IsNullOrEmpty(session.PhoneNumber))
            {
                _phoneToSession.TryRemove(session.PhoneNumber, out _);
            }

            try
            {
                session.Socket?.Shutdown(SocketShutdown.Both);
                session.Socket?.Close();
            }
            catch { }
        }
    }

    /// <summary>
    /// 通过SessionId获取会话
    /// </summary>
    public SessionInfo? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>
    /// 通过手机号获取会话
    /// </summary>
    public SessionInfo? GetSessionByPhone(string phoneNumber)
    {
        if (_phoneToSession.TryGetValue(phoneNumber, out var sessionId))
        {
            return GetSession(sessionId);
        }
        return null;
    }

    /// <summary>
    /// 绑定手机号到会话
    /// </summary>
    public void BindPhoneNumber(string sessionId, string phoneNumber)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            // 移除旧的绑定
            if (!string.IsNullOrEmpty(session.PhoneNumber))
            {
                _phoneToSession.TryRemove(session.PhoneNumber, out _);
            }

            // 添加新的绑定
            session.PhoneNumber = phoneNumber;
            _phoneToSession.TryAdd(phoneNumber, sessionId);
        }
    }

    /// <summary>
    /// 设置鉴权状态
    /// </summary>
    public void SetAuthenticated(string sessionId, bool authenticated, string? authCode = null)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsAuthenticated = authenticated;
            session.AuthCode = authCode;
        }
    }

    /// <summary>
    /// 更新最后活跃时间
    /// </summary>
    public void UpdateActiveTime(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActiveTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 获取所有会话
    /// </summary>
    public IEnumerable<SessionInfo> GetAllSessions()
    {
        return _sessions.Values;
    }

    /// <summary>
    /// 获取在线会话数
    /// </summary>
    public int GetOnlineCount()
    {
        return _sessions.Count;
    }

    /// <summary>
    /// 清理超时会话
    /// </summary>
    public void CleanupTimeoutSessions(int timeoutMinutes = 30)
    {
        var timeout = DateTime.Now.AddMinutes(-timeoutMinutes);
        var timeoutSessions = _sessions.Values
            .Where(s => s.LastActiveTime < timeout)
            .Select(s => s.SessionId)
            .ToList();

        foreach (var sessionId in timeoutSessions)
        {
            RemoveSession(sessionId);
        }
    }
}
