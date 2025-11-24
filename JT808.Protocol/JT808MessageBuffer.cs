namespace JT808.Protocol;

/// <summary>
/// JT808消息缓冲区 - 处理粘包/半包问题
/// </summary>
public class JT808MessageBuffer
{
    private readonly List<byte> _buffer = new();
    private readonly object _lock = new();

    /// <summary>
    /// 添加接收到的数据
    /// </summary>
    public void Append(byte[] data)
    {
        lock (_lock)
        {
            _buffer.AddRange(data);
        }
    }

    /// <summary>
    /// 提取完整的消息
    /// </summary>
    public List<byte[]> ExtractMessages()
    {
        var messages = new List<byte[]>();

        lock (_lock)
        {
            while (true)
            {
                // 查找第一个标识位
                int startIndex = _buffer.IndexOf(JT808Constants.FLAG);
                if (startIndex < 0)
                {
                    // 没有找到起始标识位，清空缓冲区
                    _buffer.Clear();
                    break;
                }

                // 移除起始标识位之前的数据
                if (startIndex > 0)
                {
                    _buffer.RemoveRange(0, startIndex);
                }

                // 查找第二个标识位（消息结束）
                if (_buffer.Count < 2)
                    break; // 数据不足

                int endIndex = _buffer.IndexOf(JT808Constants.FLAG, 1);
                if (endIndex < 0)
                    break; // 没有找到结束标识位，等待更多数据

                // 提取完整消息（包含起始和结束标识位）
                int messageLength = endIndex + 1;
                var message = _buffer.GetRange(0, messageLength).ToArray();
                messages.Add(message);

                // 从缓冲区移除已提取的消息
                _buffer.RemoveRange(0, messageLength);
            }
        }

        return messages;
    }

    /// <summary>
    /// 清空缓冲区
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
        }
    }

    /// <summary>
    /// 获取缓冲区大小
    /// </summary>
    public int BufferSize
    {
        get
        {
            lock (_lock)
            {
                return _buffer.Count;
            }
        }
    }
}
