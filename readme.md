中文 | [English](./readme.eng.md)

### 介绍

如果你的 Windows 程序需要在后台长期运行，而且你希望它在开机后用户登录之前就自动运行、且在用户注销之后也不停止，那么你需要将程序注册为一个系统服务。

然而，在 Windows 下编写一个可注册为系统服务的程序并不是一件简单的事情。首先，程序必须是二进制的可执行程序，这就排除了脚本语言和虚拟机语言；其次，程序必须按系统服务的格式编写，过程繁琐，编写示例可见：[MS 官方文档](https://code.msdn.microsoft.com/windowsapps/CppWindowsService-cacf4948) 。

[EasyService](https://github.com/pandolia/easy-service) 是一个可以将常规程序注册为系统服务的工具，体积只有 16KB 。你可以按常规的方法编写程序，然后用 EasyService 注册为一个系统服务，这样你的程序就可以在开机后用户登录之前自动运行、且在用户注销之后也不会停止。

如果你需要在 Windows 系统下部署网站、API 或其他需要长期在后台运行的服务， EasyService 将是一个很有用的工具。

### 系统要求

EasyService 需要 .NetFramework 4.0 （大部分 Windows 系统都已自带）。可尝试运行 worker/sample-worker.exe ，如果正常运行，则表明系统中已安装 .NetFramework 4.0 。

### 使用方法

（1） 编写、测试你的程序，EasyService 对程序仅有一个强制要求和一个建议：

```
强制要求： 程序应持续运行

建议： 当程序的标准输入接收到 “exit” 后在 10 秒之内退出
```

其中建议要求是非强制性的，程序不满足此要求也可以。

典型的程序见 [worker/index.js](https://github.com/pandolia/easy-service/blob/master/worker/index.js) （nodejs 版）， [worker/main.py](https://github.com/pandolia/easy-service/blob/master/worker/main.py) （python 版） 或 [src/SampleWorker.cs](https://github.com/pandolia/easy-service/blob/master/src/SampleWorker.cs) （C# 版）。

（2） 下载 [源码及程序](https://github.com/pandolia/easy-service/archive/master.zip)，解压。

（3） 打开 svc.conf 文件，修改配置：

```conf
# Windows 系统服务名称、不能与系统中已有服务重名
ServiceName: An Easy Service

# 需要运行的可执行程序及命令行参数
Worker: node index.js

# 程序运行的工作目录，请确保该目录已存在
WorkingDir: worker

# 输出目录，程序运行过程的输出将会写到这个目录下面，请确保该目录已存在
OutFileDir: outfiles

# 程序输出的编码，如果不确定，请设为空
WorkerEncoding: utf8
```

（4） 用管理员账号登录系统，在 svc.exe 所在的目录下打开命令行窗口：

a. 运行 ***svc check*** 命令检查配置是否合法

b. 运行 ***svc test-worker*** 命令测试 Worker 程序是否能正常运行

若测试无误：

c. 运行 ***svc install*** 命令注册并启动系统服务，此时你的程序就已经开始运行了，即便用户注销也不会停止运行，且系统开机后、用户登录之前就会自动运行。在服务管理控制台中可以查看已注册的服务。

d. 运行 ***svc stop|start|restart|remove*** 停止、启动、重启或删除本系统服务。

Win10 系统下，需要在开始菜单中搜索 cmd 然后右键以管理员身份运行，再 cd 到 svc.conf 所在的目录，然后再执行以上命令。

### 注册多个服务

如果需要注册多个服务，可以新建多个目录，将 svc.exe 和 svc.conf 拷贝到这些目录，修改 svc.conf 中的服务名和程序名等内容，再在这些目录下打开命令行窗口执行 svc check|test-worker|install 等命令就可以了。需要注意的是：

```
a. 不同目录下的服务名不能相同，也不能和系统已有的服务同名

b. 配置文件中的 Worker/WorkingDir/OutFileDir 都是相对于该配置文件的路径

c. 注册服务之前，WorkingDir/OutFileDir 所指定的目录必须先创建好
```

### 内部实现

EasyService 实质是将自己（svc.exe）注册为一个系统服务，此服务启动时，会读取 svc.conf 中的配置，创建一个子进程运行 Worker 中指定的程序及命令行参数，之后，监视该子进程，如果发现子进程停止运行，会重新启动一个子进程。而当此服务停止时，会向子进程的标准输入中写入数据 “exit” ，并等待子进程退出，如果等待时间超过 10 秒，则直接终止子进程。

EasyService 源码见 [src/main.cs](https://github.com/pandolia/easy-service/blob/master/src/Main.cs) 。

### 典型用例

Appin 网站介绍了用 EasyService 部署 frp 内网穿透服务的方法，请看 [这里](https://www.appinn.com/easyservice-for-windows/) 。