using System.Text;
using JT808.Protocol;

namespace JT808.Server;

/// <summary>
/// 位置数据存储器 - 以车牌号为文件名保存最新位置信息
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
    /// 保存位置信息到文件（以车牌号为文件名，覆盖之前的数据）
    /// </summary>
    /// <param name="plateNumber">车牌号</param>
    /// <param name="phoneNumber">终端手机号</param>
    /// <param name="location">位置信息</param>
    public void SaveLocation(string plateNumber, string phoneNumber, LocationInfo location)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            // 如果没有车牌号，使用手机号作为文件名
            plateNumber = $"未知车牌_{phoneNumber}";
        }

        // 清理文件名中的非法字符
        var safeFileName = GetSafeFileName(plateNumber);
        var filePath = Path.Combine(_dataDirectory, $"{safeFileName}.txt");

        var content = FormatLocationInfo(plateNumber, phoneNumber, location);

        lock (_lockObj)
        {
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
    }

    /// <summary>
    /// 格式化位置信息为文本
    /// </summary>
    private string FormatLocationInfo(string plateNumber, string phoneNumber, LocationInfo location)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine($"                    车辆位置信息");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();

        // 基本信息
        sb.AppendLine("【基本信息】");
        sb.AppendLine($"  车牌号码: {plateNumber}");
        sb.AppendLine($"  终端手机: {phoneNumber}");
        sb.AppendLine($"  更新时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 位置信息
        sb.AppendLine("【位置信息】");
        sb.AppendLine($"  经    度: {location.GetLongitude():F6}°{(location.IsWestLongitude ? " (西经)" : " (东经)")}");
        sb.AppendLine($"  纬    度: {location.GetLatitude():F6}°{(location.IsSouthLatitude ? " (南纬)" : " (北纬)")}");
        sb.AppendLine($"  海拔高度: {location.Altitude} 米");
        sb.AppendLine($"  行驶速度: {location.GetSpeed():F1} km/h");
        sb.AppendLine($"  行驶方向: {location.Direction}° ({GetDirectionName(location.Direction)})");
        sb.AppendLine($"  GPS时间 : {location.GpsTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 状态信息
        sb.AppendLine("【车辆状态】");
        sb.AppendLine($"  ACC状态 : {(location.IsAccOn ? "开启" : "关闭")}");
        sb.AppendLine($"  定位状态: {(location.IsPositioned ? "已定位" : "未定位")}");
        sb.AppendLine($"  报警标志: 0x{location.AlarmFlag:X8}");
        sb.AppendLine($"  状态字  : 0x{location.Status:X8}");
        sb.AppendLine();

        // 附加信息
        if (location.AdditionalInfoList.Count > 0)
        {
            sb.AppendLine("【附加信息】");
            foreach (var info in location.AdditionalInfoList)
            {
                var infoName = GetAdditionalInfoName(info.Id);
                var infoValue = ParseAdditionalInfoValue(info);
                sb.AppendLine($"  {infoName}: {infoValue}");
            }
            sb.AppendLine();
        }

        // 原始数据
        sb.AppendLine("【原始数据】");
        sb.AppendLine($"  报警标志: {location.AlarmFlag}");
        sb.AppendLine($"  状态值  : {location.Status}");
        sb.AppendLine($"  经度原值: {location.Longitude}");
        sb.AppendLine($"  纬度原值: {location.Latitude}");
        sb.AppendLine();

        sb.AppendLine("═══════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    /// <summary>
    /// 获取方向名称
    /// </summary>
    private string GetDirectionName(ushort direction)
    {
        return direction switch
        {
            >= 0 and < 23 => "北",
            >= 23 and < 68 => "东北",
            >= 68 and < 113 => "东",
            >= 113 and < 158 => "东南",
            >= 158 and < 203 => "南",
            >= 203 and < 248 => "西南",
            >= 248 and < 293 => "西",
            >= 293 and < 338 => "西北",
            _ => "北"
        };
    }

    /// <summary>
    /// 获取附加信息名称
    /// </summary>
    private string GetAdditionalInfoName(byte id)
    {
        return id switch
        {
            0x01 => "里程(km)",
            0x02 => "油量(L)",
            0x03 => "行驶记录速度(km/h)",
            0x04 => "需人工确认报警ID",
            0x05 => "胎压",
            0x06 => "车厢温度",
            0x11 => "超速报警附加信息",
            0x12 => "进出区域/路线报警",
            0x13 => "路段行驶时间不足/过长",
            0x14 => "扩展车辆信号状态位",
            0x15 => "IO状态位",
            0x16 => "模拟量",
            0x17 => "无线通信网络信号强度",
            0x18 => "GNSS定位卫星数",
            0x25 => "扩展车辆信号状态位",
            0x2A => "IO状态位",
            0x2B => "模拟量",
            0x30 => "无线通信网络信号强度",
            0x31 => "GNSS定位卫星数",
            _ => $"未知(0x{id:X2})"
        };
    }

    /// <summary>
    /// 解析附加信息值
    /// </summary>
    private string ParseAdditionalInfoValue(LocationAdditionalInfo info)
    {
        if (info.Content == null || info.Content.Length == 0)
            return "无数据";

        return info.Id switch
        {
            0x01 when info.Length == 4 => $"{ReadUInt32(info.Content) / 10.0:F1}",  // 里程
            0x02 when info.Length == 2 => $"{ReadUInt16(info.Content) / 10.0:F1}",  // 油量
            0x03 when info.Length == 2 => $"{ReadUInt16(info.Content) / 10.0:F1}",  // 行驶记录速度
            0x18 when info.Length == 1 => $"{info.Content[0]}",  // GNSS卫星数
            0x31 when info.Length == 1 => $"{info.Content[0]}",  // GNSS卫星数(2019)
            _ => BitConverter.ToString(info.Content).Replace("-", " ")
        };
    }

    private uint ReadUInt32(byte[] buffer)
    {
        return (uint)((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]);
    }

    private ushort ReadUInt16(byte[] buffer)
    {
        return (ushort)((buffer[0] << 8) | buffer[1]);
    }

    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    private string GetSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var result = fileName;

        foreach (var c in invalidChars)
        {
            result = result.Replace(c, '_');
        }

        return result;
    }

    /// <summary>
    /// 获取数据目录路径
    /// </summary>
    public string GetDataDirectory() => _dataDirectory;
}
