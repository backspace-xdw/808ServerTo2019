using System.Text;
using JT808.Protocol;

namespace JT808.Server;

/// <summary>
/// 位置数据存储器 - 以手机号为文件名保存最新位置信息 (XML格式)
/// </summary>
public class LocationDataStore
{
    private readonly string _dataDirectory;
    private readonly object _lockObj = new();

    public LocationDataStore(string dataDirectory = "LocationData")
    {
        _dataDirectory = dataDirectory;

        // 确保数据目录存在
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }

    /// <summary>
    /// 保存位置信息到XML文件（以手机号为文件名，不足12位前面补0）
    /// </summary>
    /// <param name="plateNumber">车牌号</param>
    /// <param name="phoneNumber">终端手机号</param>
    /// <param name="location">位置信息</param>
    public void SaveLocation(string plateNumber, string phoneNumber, LocationInfo location)
    {
        // 手机号不足12位前面补0
        var paddedPhone = phoneNumber.PadLeft(12, '0');
        var filePath = Path.Combine(_dataDirectory, $"{paddedPhone}.xml");

        var content = FormatLocationXml(paddedPhone, location);

        lock (_lockObj)
        {
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
    }

    /// <summary>
    /// 格式化位置信息为XML
    /// </summary>
    private string FormatLocationXml(string phoneNumber, LocationInfo location)
    {
        var sb = new StringBuilder();

        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append("<NewDataSet>");
        sb.Append("<table>");
        sb.Append($"<Identifier>{phoneNumber}</Identifier>");
        sb.Append($"<Time>{location.GpsTime:yyyy-MM-dd HH:mm:ss}</Time>");
        sb.Append($"<Longitude>{location.GetLongitude():F6}</Longitude>");
        sb.Append($"<Latitude>{location.GetLatitude():F6}</Latitude>");
        sb.Append($"<speed>{location.Speed}</speed>");
        sb.Append($"<mileage>{GetMileage(location)}</mileage>");
        sb.Append($"<direction>{location.Direction}</direction>");
        sb.Append($"<altitude>{location.Altitude}</altitude>");
        sb.Append($"<OverspeedAlarm>{(HasOverspeedAlarm(location) ? "是" : "否")}</OverspeedAlarm>");
        sb.Append("</table>");
        sb.Append("</NewDataSet>");

        return sb.ToString();
    }

    /// <summary>
    /// 获取里程值 (从附加信息中解析)
    /// </summary>
    private uint GetMileage(LocationInfo location)
    {
        foreach (var info in location.AdditionalInfoList)
        {
            // 0x01 为里程，单位 1/10km
            if (info.Id == 0x01 && info.Length == 4 && info.Content != null)
            {
                return ReadUInt32(info.Content) / 10;
            }
        }
        return 0;
    }

    /// <summary>
    /// 检查是否有超速报警
    /// </summary>
    private bool HasOverspeedAlarm(LocationInfo location)
    {
        // 报警标志 bit1 为超速报警
        return (location.AlarmFlag & 0x02) != 0;
    }

    private uint ReadUInt32(byte[] buffer)
    {
        return (uint)((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]);
    }

    /// <summary>
    /// 获取数据目录路径
    /// </summary>
    public string GetDataDirectory() => _dataDirectory;
}
