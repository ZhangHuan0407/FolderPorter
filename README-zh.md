[English](./README.md)|[简体中文](./README-zh.md)

- 文件夹搬运工在多台电脑之间同步文件夹，差量传输数据。
- 如果当前系统为Linux，文件夹和文件的权限设置为775(rwxrwxr-x)
- 运行环境 .net 8
- 连接方式: TCP

# 设计意图
- 最开始我使用 scp -r 指令来同步文件夹，但是遇到了一些问题...
- scp 是全量传输 + 一个一个数据包传输。最大 2 mb/s 的 wifi 环境下，scp居然出现了 < 100 kb/s 的百度云速度...
- 同时，scp 是基于 ssh 实现的，如果两台设备都是 Windows，不装环境还真用不了
- Windows 的远程桌面，复制一个文件能给远程桌面卡掉线 :(
- 一怒之下做了文件切片校验和差量传输的软件

- VersionControl 仅仅是意外之举，版本控制并不是本意
- 由于我期望某个文件夹推送内容后归档，因此修改了文件夹存储结构，添加了版本控制

- 由于只开发了4天时间，**这个软件源码并不复杂，功能并不强大**，版本控制请用 git 或者 svn
- 这玩意的核心是:
  1. 切片并批次传输，在高延迟不必传1数据包等1数据包 (一次传输8个文件，或累计超过 100mb)
  2. 做了差量传输(1mb一个切片，求crc32并比较)，文件差异少时比 scp 传输更少文件
  3. 浪费远程服务器的磁盘空间添加历史版本(可选)

- 2 mb/s wifi 延迟 100ms+ 环境下，scp 传输 100 kb/s，FolderPorter 1.1 mb/s

[TOC]
- [设计意图](#设计意图)
- [架构支持](#架构支持)
  - [](#)
- [版本控制软件差异](#版本控制软件差异)
- [Windows 安装](#windows-安装)
  - [Windows 添加指令到 cmd](#windows-添加指令到-cmd)
- [Linux 安装](#linux-安装)
  - [环境配置](#环境配置)
  - [拷贝文件](#拷贝文件)
  - [Linux 开启自启服务](#linux-开启自启服务)
  - [确认端口处于监听状态](#确认端口处于监听状态)
  - [Linux 添加指令到 shell](#linux-添加指令到-shell)
- [更新 AppSettings.json](#更新-appsettingsjson)
  - [需要设置的配置](#需要设置的配置)
  - [默认参数，不调也能用](#默认参数不调也能用)
  - [AppSettings.json 模板](#appsettingsjson-模板)
- [Push 使用流程](#push-使用流程)
- [Pull 使用流程](#pull-使用流程)
- [List 使用流程](#list-使用流程)
- [忽略文件](#忽略文件)
- [集群部署](#集群部署)
- [多网段切换](#多网段切换)
- [版本控制文件夹结构](#版本控制文件夹结构)
- [启用加密传输](#启用加密传输)

# 架构支持
|                                                                   | Windows x86 | Windows x86-64 | Windows arm64 | Linux arm64 | Linux x86-64 | MacOS x86-64 | MacOS M1 |
| ----------------------------------------------------------------- | ----------- | -------------- | ------------- | ----------- | ------------ | ------------ | -------- |
| [Release](https://github.com/ZhangHuan0407/FolderPorter/releases) | ❌           | ✅              | ❌             | ✅           | ✅            | ❌            | ❌        |
| 源码安装                                                          | ❌           | ✅              | ❓             | ✅           | ✅            | ❓            | ❓        |
- ✅: 是
- ❓: 理论上支持，但没有测试
- ❌: 否

##
- 打包了无特定目标运行时的发行包，请注意此包内容未经测试
- [No.specific.target.during.runtime.not.tested.zip](https://github.com/ZhangHuan0407/FolderPorter/releases/download/v0.1.6/No.specific.target.during.runtime.not.tested.zip)

# 版本控制软件差异
|                | git        | svn           | FolderPorter(远程开启 VersionControl，本地不开启) | scp      |
| -------------- | ---------- | ------------- | ------------------------------------------------- | -------- |
| 本地历史文件   | 存在       | 不存在        | 不存在                                            | 不存在   |
| 控制系统       | 分布式版本 | 集中式        | 集中式                                            | 没有     |
| 服务器历史文件 | 压缩       | ~~我不知道~~  | 不压缩，仅与最近的版本产生硬链接文件              | 没有     |
| 分支           | 支持       | 支持          | 不支持                                            | 不支持   |
| 大文件         | Git LFS    | svn:externals | 使用了切片的方式进行文件校验、传输，直接怼上去    | 怼上去   |
| 压缩大仓库     | 困难       | 移除旧版本    | 移除旧版本                                        | 没有仓库 |
| 传输加密       | 存在       | 存在          | 明文密码+明文传输，或 Aes 低强度加密              | 存在     |

# Windows 安装
- 解压 [下载文件](https://github.com/ZhangHuan0407/FolderPorter/releases)
```
FolderPorter.exe
```
- 也许可以正常运行，也许需要自行安装.net 8

- 然后, [更新 AppSettings.json](#更新-appsettingsjson)

## Windows 添加指令到 cmd
- 修改系统环境变量
- 添加 FolderPorter.exe 所在文件夹

![WindowsEditPath](/img/WindowsEditPath.png)

- 打开cmd，输入 FinderPorter
- 启动FinderPorter进程即视为成功

# Linux 安装
## 环境配置
- 首先, [install dotnet 8](https://learn.microsoft.com/zh-cn/dotnet/core/install/linux-ubuntu-install)
```
sudo apt install dotnet-runtime-8.0
```

## 拷贝文件
- 然后, 解压[下载文件](https://github.com/ZhangHuan0407/FolderPorter/releases) 的压缩包，并将它移动到 /lib/FolderPorter
```
cd /var
wget -O /var/FolderPorter.zip download-url-here
unzip FolderPorter.zip -d FolderPorter
cd /lib
mkdir FolderPorter
mkdir /etc/FolderPorter
mv /var/FolderPorter/Linux-*/* /lib/FolderPorter/
cp /lib/FolderPorter/AppSettingsTemplate.json /etc/FolderPorter/AppSettings.json
chmod +x /lib/FolderPorter
chmod +x /lib/FolderPorter/FolderPorter
chmod +r /lib/FolderPorter/*
ls -al /lib/FolderPorter/
# drwxrwxrwx   2 root         root           4096 May 10 14:32 .
# -rwxr-xr-x   1 root         root         123942 May 10 14:32 FolderPorter
# -rw-r--r--   1 root         root            431 May 10 13:19 AppSettings.json
# 其余文件省略
```

- 其次, [更新 AppSettings.json](#更新-appsettingsjson)

## Linux 开启自启服务
- 可选，开机自启 FolderPorter server
- sudo nano /etc/systemd/system/FolderPorter.service
- 加入下列内容
```
[Unit]
Description=FolderPorter server :17979 /lib/FolderPorter
After=network.target

[Service]
# 如果环境变量 DOTNET_ROOT 或 PATH 缺少 dotnet 运行时位置，需要添加以下两行环境变量
# Environment="DOTNET_ROOT=/root/.dotnet"
# Environment="PATH=/root/.dotnet:$PATH"

WorkingDirectory=/lib/FolderPorter
ExecStart=/lib/FolderPorter/FolderPorter server

# 请注意，此处设置了进程挂掉自动重启进程
Restart=on-failure
RestartSec=120

KillSignal=SIGINT

# 最好不要设置root用户，否则文件创建时所有者是root用户
User=folderporter

[Install]
WantedBy=multi-user.target
```
- 使用systemctl来控制server启动和停用
```
# 开机自启
systemctl enable FolderPorter.service
# 关闭自启
systemctl disable FolderPorter.service
# 查看状态
systemctl status FolderPorter.service
# 启动
systemctl restart FolderPorter.service
# 停止，如果设置了Restart=on-failure，记得先disable再stop
systemctl stop FolderPorter.service
```

## 确认端口处于监听状态
```
lsof -i:17979
```

## Linux 添加指令到 shell
```
sudo ln -s /lib/FolderPorter/FolderPorter /bin/FolderPorter
```

# 更新 AppSettings.json
## 需要设置的配置
- Password 是当前驱动器上运行的应用程序的密码。长度限制: 500
- LocalFolders 列举所有绑定的文件夹，key 为文件夹名称
  - RootPath 为此文件夹的磁盘路径，Windows 和 Linux 均使用 /，否则可能执行报错
  - CanWrite 此文件夹是否接受远程设备的 Push(或本地Pull)
  - CanRead 此文件夹是否接受远程设备的 Pull(或本地Push)
  - VersionControl 文件夹是否启用版本控制。**请在空文件夹状态下启用/关闭该配置。**
- RemoteDevice 列举所有可访问的远程设备，key为远程设备名称
  - IP 为远程设备 server 模式监听的IP+端口。不配置不启用
  - IP2 当 IP 不可达时，自动尝试 IP2。不配置不启用
  - DomainPort 当 IP 与 IP2 均不可达时，自动尝试 DomainPort。不配置不启用
  - DevicePassword 为远程设备 AppSettings.json 的 Password
  - EncryptedTransmission 需要与远程设备的 AcceptEncryptedTransmission 保持一致

## 默认参数，不调也能用
- User 推送数据时，日志和记录中的推送者名称。如果为空，则使用DNS.GetHostName()
- AcceptEncryptedTransmission: "SimplePassword" or "AES_CBC".
  - string.Empty 视作 "SimplePassword", 仅在连接开始时明文传输密码并比对
  - "SimplePassword" 在传输数据时没有任何加密
  - "AES_CBC" 在连接开始时传输基于时间加盐的 MD5，并在传输数据时启用加密算法
- HardLinkInsteadOfCopy 当启用 VersionControl 时，如果与上一版本文件完全相同，则使用硬链接以节省存储空间
  - exFAT 不支持文件硬链接!
  - 失败时退回二进制拷贝
- MaxWorkerThreadCount 线程池的运算线程数量上限
- MaxIOThreadCount 线程池的IO线程数量上限
- RemoteBuzyRetrySeconds 当远程设备处于繁忙状态，延迟此时间后重试
- ConnectTimeoutSeconds 连接超时时间
- ListernPort server 模式下监听的端口

- LogDebug 输出调试日志
- LogProtocal 输出协议日志

## AppSettings.json 模板
```
{
  "Password": "c7ce0d8e-4985-4464-9146-0767be889a45",
  "User": "xxx@gmail.com",
  "AcceptEncryptedTransmission": "",
  "LocalFolders": {
    "RegexGameWebGL": {
      "RootPath": "D:/RegexGame/Builds/WebGL Github/RegexGame",
      "CanWrite": true,
      "CanRead": true,
      "VersionControl": false
    },
    "TestFolder": {
      "RootPath": "/var/TestFolder",
      "CanWrite": true,
      "CanRead": true,
      "VersionControl": false
    }
  },
  "RemoteDevice": {
    "raspberry": {
      "IP": "192.168.1.3:17979",
      "IP2": "192.168.2.3:17979",
      "DomainPort": "yyy.com:17979",
      "DevicePassword": "d0d642fb-b77d-4e32-b77d-2444cd8788c3",
      "EncryptedTransmission": ""
    }
  },

  "HardLinkInsteadOfCopy": true,

  "MaxWorkerThreadCount": 2,
  "MaxIOThreadCount": 3,

  "RemoteBuzyRetrySeconds": 5,
  "ConnectTimeoutSeconds": 30,

  "ListernPort": 17979,

  "LogDebug": false,
  "LogProtocal": false
}
```

# Push 使用流程
- 假定 192.168.1.2 需要传递文件到 192.168.1.1

- PC 192.168.1.1 AppSettings.json
```
{
  "Password": "123",
  "LocalFolders": {
    "TestFolder": {
      "RootPath": "/var/TestFolder",
      "CanRead": true,
      "CanWrite": true
    }
  }
  "ListernPort": 17979,
  ...
}
```

- PC 192.168.1.2 AppSettings.json
```
{
  "LocalFolders": {
    "TestFolder": {
      "RootPath": "d:/TestFolder",
      "CanRead": true,
      "CanWrite": true
    }
  }
  "RemoteDevice": {
    "PC_1": {
      "IP": "192.168.1.1:17979",
      "DevicePassword": "123"
    }
  },
  ...
}
```

- PC 192.168.1.1
```
FolderPorter server
```

- PC 192.168.1.2
```
FolderPorter push@PC_1:TestFolder
```

- 此时，192.168.1.2 会将它的 d:/TestFolder 文件夹推送到 192.168.1.1 /var/TestFolder 文件夹
- 192.168.1.1 会先写入差异的部分，再移除多出的文件

# Pull 使用流程
- 使用[Push 使用流程](#Push 使用流程)中的配置，使用下列指令即可将文件的同步方向反转
- PC 192.168.1.2
```
FolderPorter pull@PC_1:TestFolder
```
- 此时，192.168.1.1 会将它的 /var/TestFolder 文件夹推送到 192.168.1.2 d:/TestFolder 文件夹
- 192.168.1.2 会先写入差异的部分，再移除多出的文件

- 如果存在一般工件传递方向，比如打包机一般只产出不需要回读
- 可以修改此打包机的配置, CanWrite: false, 让写入总是不成功

# List 使用流程
- 远程设备已配置 VersionControl 的情况下
- 使用下列指令查看对方的历史版本
```
# 输入
FolderPorter list@PC_1:TestFolder
# 输出
List
VerifyRemotePassword success
TestFolder
ValidVersionCount: 2

{
  "Version": "7b60003232dd4a42aa869e883b7233cd",
  "DateTime": "2025-05-12T21:48:25.8395206+08:00",
  "RemoteUser": "PC_2"
}
{
  "Version": "f12da32aaf8f4171934ce3869d0a2b40",
  "DateTime": "2025-05-12T21:36:52.9075853+08:00",
  "RemoteUser": "PC_2"
}
```

# 忽略文件
- 在 RootPath 文件夹添加 .Ignore 文件
```
# 忽略目标文件
abc.txt
# 忽略目标文件夹下的文件，包含：
# director/a.txt
# director/b/a.txt
# director/b/c
directory/*
```

# 集群部署
```mermaid
graph TB
PC_0[Build Archive]--FolderPorter push-->PC_1
PC_1[FolderPorter server]
PC_2[Computer A]--pull-->PC_1
PC_3[Computer B]--pull-->PC_1
PC_4[Computer C]--pull-->PC_1
```

- 为了避免多线程操作文件报错
- 当前 Computer A B C 无法并行作业，会自动排队等待

# 多网段切换
- 可能当前设备和目标设备，同时连接了多个网段，比如同时连接有线eth0和无线wifi
- 可以将有线配置在 RemoteDevice.PC_1.IP
- 将无线配置在 RemoteDevice.PC_1.IP2
- 每次 pull/push 时优先尝试使用 IP
- 如果 IP 不可达(有线传输断开) 则尝试 IP2

# 版本控制文件夹结构
```
# 一个json文件，记录历史版本
- .VersionControl.json
# 一个版本所在的文件夹，文件夹名为版本号前8位
- abcd1234
  # 若干文件
  - *
# 一个版本所在的文件夹，文件夹名为版本号前8位
- 1234abcd
  # 若干文件
  - *
# 文件夹链接到最后一个成功生成版本。Windows 下需要 administrator 才能生成文件夹链接
- Head
```

# 启用加密传输
- 查看是否支持硬件级 aes，如果支持的话加密解密会很快
```
# windows 下可用使用 git bash 查看
grep -m1 -o aes /proc/cpuinfo


# 测试 AES-128-CBC 性能
openssl speed -evp aes-128-cbc

# Raspberry 4b 大约 40 m/s
# type             16 bytes     64 bytes    256 bytes   1024 bytes   8192 bytes  16384 bytes
# AES-128-CBC(k/s) 36865.97k    40292.69k    41630.31k    41780.91k    41882.97k    42023.04k
```

- 使用 AES-CBC 对称式加密

```
# server AppSettings.json
"AcceptEncryptedTransmission": "AES_CBC"

# another drive AppSettings.json
"RemoteDevice": {
    "PC_1": {
        "IP": "192.168.1.1:17979",
        "DevicePassword": "123",
        "EncryptedTransmission": "AES_CBC"
    }
},
```