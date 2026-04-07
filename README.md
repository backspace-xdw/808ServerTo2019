# JT808-2019 车载终端通讯服务器

> 基于 JT/T 808-2019 协议标准的高并发 TCP 服务器
>
> 已完成高并发优化, 单实例支持 10000+ 车载终端在线
>
> 自动识别 2013 / 2019 两个协议版本, 同端口同时在线

---

## 🎯 核心特性

- **协议版本自动识别** — 通过消息体属性 bit14 自动切换 2013 / 2019 字段长度, 无需配置
- **高并发优化** — `SocketAsyncEventArgs` 池化 + `ArrayPool<byte>` 缓冲区, 热路径零分配
- **实时位置存储 (XML)** — 按手机号(12 位标准化)生成 XML 文件, 字段格式与上游平台标准一致
- **位置数据归档** — 可选启用; 按 `yyyyMMdd` 文件夹归档每天的最新位置快照
- **粘包/半包处理** — 完善的消息缓冲区
- **会话管理** — 顶号上线/超时清理/原子时间戳, 全部 lock-free
- **多媒体上传** — 支持分包组装、漏包检测、断点重传
- **可配置启动** — 全部参数走 `appsettings.json`, 不需要改代码

---

## 📊 2013 vs 2019 协议字段对比

| 项目 | 2013 版本 | 2019 版本 |
|------|---------|---------|
| 手机号长度 | 6 字节 | 10 字节 |
| 制造商 ID | 5 字节 | 11 字节 |
| 终端型号 | 20 字节 | 30 字节 |
| 终端 ID | 7 字节 | 30 字节 |
| 鉴权扩展 | 仅鉴权码 | 鉴权码 + IMEI + 软件版本 |
| 参数 ID | 1 字节 | 4 字节 |
| 版本标识 | 无 | 消息体属性 bit14 |
| 协议版本号 | 无 | 消息头新增 1 字节 |

---

## 📁 项目结构

```
JT808Server2019/
├── JT808.Protocol/                  # 协议层
│   ├── JT808Constants.cs            # 协议常量 (2019扩展)
│   ├── JT808Message.cs              # 消息数据结构 (2019扩展)
│   ├── JT808Decoder.cs              # 协议解码器 (版本自动识别)
│   ├── JT808Encoder.cs              # 协议编码器 (2019支持)
│   └── JT808MessageBuffer.cs        # 消息缓冲区 (粘包/半包)
│
├── JT808.Server/                    # 服务器层 (高并发优化版)
│   ├── JT808TcpServer.cs            # TCP 服务器主类
│   ├── SessionManager.cs            # 会话管理 (lock-free)
│   ├── LocationDataStore.cs         # 实时位置 XML 存储 + 归档双写
│   ├── MediaDataStore.cs            # 多媒体数据(分包重组)
│   ├── ServerConfig.cs              # 配置数据类
│   ├── Program.cs                   # 启动程序
│   ├── appsettings.json             # 运行时配置
│   └── JT808.Server.csproj
│
├── JT808.TestClient/                # 单连接测试客户端
├── JT808.ConcurrencyTest/           # 并发压测工具
│
├── JT808Server2019.sln
├── build.sh
└── README.md
```

---

## 🚀 快速开始

### 环境要求

- **.NET 9.0 SDK** 或更高
- Linux / Windows / macOS

### 编译

```bash
cd /home/shenzheng/JT808Server2019
export PATH="$HOME/.dotnet:$PATH"
./build.sh
# 或: dotnet build --configuration Release
```

### 启动服务器

```bash
cd JT808.Server
dotnet run
```

或后台运行:

```bash
nohup dotnet run --configuration Release > server.log 2>&1 &
```

服务器自动检测 stdin 是否被重定向, **后台模式**会每 10s 输出一次统计;
**交互模式**下按任意键查看在线终端列表, 按 `Q` 退出。

### 启动后会显示

```
============================================================
JT808-2019 车载终端通讯服务器
基于 JT/T 808-2019 协议
支持 12000+ 并发连接 (高并发优化版)
支持 2013 和 2019 版本自动识别
============================================================

当前配置:
  监听地址:       0.0.0.0:8809
  Listen Backlog: 4096
  最大并发连接:   12000
  位置目录:       LocationData
  位置归档:       LocationArchive/yyyyMMdd/
  媒体目录:       MediaData
  会话超时:       30 分钟
  日志级别:       Warning

提示: 高并发场景请确认系统配置:
  ulimit -n 65536                              # fd 上限
  sysctl -w net.core.somaxconn=8192            # listen 队列
  sysctl -w net.ipv4.tcp_max_syn_backlog=8192  # syn 队列
```

### 启动测试客户端

```bash
cd JT808.TestClient
dotnet run
```

按提示选择: 服务器地址 / 端口 / 手机号 / 协议版本 (Y=2019, n=2013)。

---

## ⚙️ 配置说明 (appsettings.json)

```json
{
  "ServerConfig": {
    "IpAddress": "0.0.0.0",
    "Port": 8809,
    "Backlog": 4096,
    "MaxConcurrentConnections": 12000,
    "LocationDataDirectory": "LocationData",
    "LocationArchiveDirectory": "LocationArchive",
    "MediaDataDirectory": "MediaData",
    "SessionTimeoutMinutes": 30,
    "LogLevel": "Warning"
  }
}
```

| 字段 | 默认值 | 说明 |
|---|---|---|
| `IpAddress` | `0.0.0.0` | 监听地址, `0.0.0.0` 表示所有网卡 |
| `Port` | `8809` | TCP 监听端口 (区别 2013 版的 8808) |
| `Backlog` | `4096` | TCP listen 队列长度, 实际值受 `net.core.somaxconn` 限制 |
| `MaxConcurrentConnections` | `12000` | 应用层连接硬上限, 超限新连接被立即拒绝 |
| `LocationDataDirectory` | `LocationData` | 实时位置 XML 主存储目录, 文件名 = 12 位手机号.xml |
| `LocationArchiveDirectory` | `LocationArchive` | 归档目录; 留空 `""` 关闭归档双写 |
| `MediaDataDirectory` | `MediaData` | 多媒体文件存储目录 |
| `SessionTimeoutMinutes` | `30` | 会话最长无活动时间 |
| `LogLevel` | `Warning` | 高并发场景推荐 `Warning`; 调试时改 `Debug` |

---

## 📦 实时位置数据存储

### 主存储

- **路径**: `LocationDataDirectory/{12位手机号}.xml`
- **行为**: 始终保留每辆车**最新的一条**, 后到的覆盖前面的
- **写入方式**: 后台 worker 异步刷盘 (调用方零阻塞), 同手机号短时多次上报会**合并去重**
- **编码**: UTF-8 with BOM (与上游下游历史一致)

### 归档双写 (可选)

- **路径**: `LocationArchiveDirectory/{yyyyMMdd}/{12位手机号}.xml`
- **触发**: `LocationArchiveDirectory` 非空时启用
- **行为**: 每次主存储写入时, 同时写一份到当天的日期文件夹下
- **跨天**: 午夜后自动建新文件夹, 旧天文件夹保留作为历史归档
- **失败隔离**: 归档写失败不影响主存储, 反之亦然

### XML 字段格式

固定 40 个字段, 顺序与上游平台标准一致:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<NewDataSet><table>
  <PhoneNumber>014818411623</PhoneNumber>
  <Time>2026-04-07 12:07:28</Time>
  <ACCZT>0</ACCZT>             <!-- bit0  -->
  <DingweiZT>1</DingweiZT>     <!-- bit1  -->
  <YunYinZT>0</YunYinZT>       <!-- bit4  -->
  <Longitude>114.313080</Longitude>
  <Latitude>23.671164</Latitude>
  <GaoDu>54.000000</GaoDu>
  <Speed>0.0</Speed>
  <direction>2.36</direction>  <!-- 弧度 -->
  <mileage>707419.687500</mileage>  <!-- km, 来自附加信息 0x01 -->
  <ilLevel>0.000000</ilLevel>       <!-- L,  来自附加信息 0x02 -->
  <!-- 28 个报警字段 (JT808 报警标志位 0~14、18~30) -->
  <JinjiBJ>0</JinjiBJ> <ChaoSuBJ>0</ChaoSuBJ> ... <CFYJ>0</CFYJ>
</table></NewDataSet>
```

---

## ⚡ 高并发优化要点

| 优化项 | 实现 |
|---|---|
| **接收 SAEA 复用** | 每个 session 持有一份 `SocketAsyncEventArgs` + `ArrayPool<byte>` 缓冲区, 整个连接生命周期复用 |
| **零分配热路径** | `JT808MessageBuffer.Append(byte[], offset, count)` 直接消费接收 buffer 片段, 不再 `new byte[]` |
| **同步内联处理** | 接收回调内同线程直接 `ProcessMessage`, 不走 `Task.Run`, 避免 ThreadPool 抖动 |
| **per-session 发送锁** | 同一 socket 的并发 Send 串行化, 防止数据错乱 |
| **同步发送 + 5s 超时** | JT808 应答包小, sync `Send` 内核 buffer 立即吸收; 慢客户端由 `SendTimeout` 兜底 |
| **位置数据异步通道** | `Channel<string>` + 后台 worker, 同车多次上报去重合并, 调用方零等待 |
| **原子时间戳** | `LastActiveTicks` 用 `Interlocked` 读写, 替代 `DateTime.Now` (后者比 UTC 慢 30~50 倍) |
| **PeriodicTimer 清理** | 每 60s 清理超时会话, 不占工作线程 |
| **连接数硬上限** | `MaxConcurrentConnections` 超限直接关闭新连接 |
| **Socket 调优** | `NoDelay = true` (关闭 Nagle), `KeepAlive = true`, `SendTimeout = 5000` |

---

## 🐧 系统配置建议 (Linux 1 万终端)

```bash
# 进程级 fd 上限
ulimit -n 65536

# 内核 listen 队列
sysctl -w net.core.somaxconn=8192
sysctl -w net.ipv4.tcp_max_syn_backlog=8192

# 持久化: 写入 /etc/sysctl.conf
echo "net.core.somaxconn=8192" >> /etc/sysctl.conf
echo "net.ipv4.tcp_max_syn_backlog=8192" >> /etc/sysctl.conf

# systemd unit 中设置 LimitNOFILE=65536
```

---

## 📝 已实现消息

### 终端通用
- ✅ `0x0001` 终端通用应答
- ✅ `0x8001` 平台通用应答
- ✅ `0x0002` 终端心跳
- ✅ `0x0100` 终端注册 (2019 扩展)
- ✅ `0x8100` 终端注册应答
- ✅ `0x0102` 终端鉴权 (2019 扩展, IMEI/软件版本)

### 位置信息
- ✅ `0x0200` 位置信息汇报 (含附加信息解析 + XML 落盘)
- ⏳ `0x0704` 定位数据批量上传 (已收应答, 待解析批量数据)

### 多媒体 (2019)
- ✅ `0x0801` 多媒体数据上传 (分包重组 + 漏包检测 + 断点重传)
- ✅ `0x8800` 多媒体数据上传应答 (重传请求)
- ⏳ `0x8801` 摄像头立即拍摄命令
- ⏳ `0x8802` 存储多媒体数据检索

---

## 🔍 调试和监控

### 在线统计 (交互模式)

启动后按任意键, 显示:
- 在线终端数 / 已鉴权数 / 2019 vs 2013 版本分布
- 终端列表: 手机号 / 版本 / 鉴权 / 收发计数 / IMEI / 最后活跃

### 后台模式

`stdin` 被重定向 (systemd / docker / nohup) 时自动进入后台模式, 每 10s 输出一行统计。

### 周期统计日志

后台 cleanup worker 每 60 秒输出一行:

```
[Stats] 在线=1234 清理超时=2 待写位置=15
```

### 调高日志级别

排查问题时把 `appsettings.json` 的 `LogLevel` 改成 `Debug` 即可看到每条消息细节; 默认 `Warning` 是为了高并发下不被日志拖累。

---

## 🧪 并发压测

详见 [`并发测试说明.md`](./并发测试说明.md)。

```bash
cd JT808.ConcurrencyTest
dotnet run --configuration Release
# 输入: 服务器地址 / 端口 / 并发数 / 协议版本
```

---

## 🛠️ 扩展开发

### 添加新消息类型

1. `JT808Constants.cs` 加消息 ID 常量
2. `JT808Message.cs` 加数据结构
3. `JT808Decoder.cs` 加解析方法
4. `JT808Encoder.cs` 加编码方法
5. `JT808TcpServer.cs` 的 `ProcessMessage` switch 加 case + Handle 方法

### 数据库集成建议

- **PostgreSQL / MySQL** — 终端档案、车辆信息、轨迹历史
- **Redis** — 在线终端缓存、鉴权码缓存
- **InfluxDB / TimescaleDB** — 位置时序数据 (适合大体量轨迹查询)

---

## 🚢 生产环境部署

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish JT808.Server/JT808.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8809
ENTRYPOINT ["dotnet", "JT808.Server.dll"]
```

### Nginx TCP 负载均衡

```nginx
stream {
    upstream jt808_backend {
        least_conn;
        server 192.168.1.10:8809;
        server 192.168.1.11:8809;
        server 192.168.1.12:8809;
    }
    server {
        listen 8809;
        proxy_pass jt808_backend;
        proxy_timeout 600s;
    }
}
```

---

## 📚 参考资料

- **JT/T 808-2013** 道路运输车辆卫星定位系统终端通讯协议及数据格式
- **JT/T 808-2019** 道路运输车辆卫星定位系统终端通讯协议及数据格式 (修订版)
- **GB/T 19056** 汽车行驶记录仪

---

## 📄 许可证

MIT License

---

**项目就绪, 单机 1 万终端可上线!** 🚀
