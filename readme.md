中文 | [English](./readme.eng.md)

### 介绍

如果你的 Windows 程序需要在后台长期运行，而且你希望它在开机后用户登录之前就自动运行、且在用户注销之后也不停止，那么你需要将程序注册为一个系统服务。

然而，在 Windows 下编写一个可注册为系统服务的程序并不是一件简单的事情。首先，程序必须是二进制的可执行程序，这就排除了脚本语言和虚拟机语言；其次，程序必须按系统服务的格式编写，过程繁琐，编写示例可见：[MS 官方文档](https://code.msdn.microsoft.com/windowsapps/CppWindowsService-cacf4948) 。

[EasyService](https://github.com/pandolia/easy-service) 是一个可以将常规程序注册为系统服务的工具，体积只有 16KB 。你可以按常规的方法编写程序，然后用 EasyService 注册为一个系统服务，这样你的程序就可以在开机后用户登录之前自动运行、且在用户注销之后也不会停止。

如果你需要在 Windows 系统下部署网站、API 或其他需要长期在后台运行的服务， EasyService 将是一个很有用的工具。

### 安装

下载 [源码及程序](https://github.com/pandolia/easy-service/archive/master.zip)，解压。右键单击 bin 目录下的 register-this-path.bat ，以管理员身份运行，将 bin 目录加入至系统路径中。

重新打开 “我的电脑” ，在任意位置打开一个命令行窗口，输入 ***svc -v*** ，如果正常输出版本信息，则表明安装成功。

### 使用方法

（1） 编写、测试你的程序，EasyService 对程序仅有一个强制要求和一个建议：

```
强制要求： 程序应持续运行

建议： 当程序的标准输入接收到 “exit” 或 “回车” 后在 10 秒之内退出
```

其中建议要求是非强制性的，程序不满足此要求也可以。

典型的程序见 [index.js](https://github.com/pandolia/easy-service/blob/master/samples/nodejs-version/worker/index.js) （nodejs 版）， [main.py](https://github.com/pandolia/easy-service/blob/master/samples/python-version/worker/main.py) （python 版） 或 [SampleWorker.cs](https://github.com/pandolia/easy-service/blob/master/src/SampleWorker.cs) （C# 版），这三个程序都是每隔 1 秒打印一行信息，键入回车后退出。

（2） 打开命令行窗口，输入 ***svc create hello-svc*** ，将创建一个样板工程 hello-svc 。

（3） 打开 hello-svc/svc.conf 文件，修改配置：

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

（4） 用管理员身份打开命令行窗口， cd 到 hello-svc 目录：

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

### 注意事项

为保证的数据的一致性，要求：

* （1） 运行 ***svc install*** 安装服务后，不应对 svc.conf 文件进行修改，删除，也不得移动或重命名目录，除非再次运行 ***svc remove*** 删除了服务

* （2） 不应在服务管理控制台中对采用 EasyService 安装的服务进行修改或操作，也不应采用除 svc 命令以外的其他方式进行修改或操作

### 内部实现

EasyService 实质是将自己（svc.exe）注册为一个系统服务，此服务启动时，会读取 svc.conf 中的配置，创建一个子进程运行 Worker 中指定的程序及命令行参数，之后，监视该子进程，如果发现子进程停止运行，会重新启动一个子进程。而当此服务停止时，会向子进程的标准输入中写入数据 “exit” ，并等待子进程退出，如果等待时间超过 10 秒，则直接终止子进程。

EasyService 源码见 [src/main.cs](https://github.com/pandolia/easy-service/blob/master/src/Main.cs) 。

### 与 NSSM 的对比

Windows 下部署服务的同类型的工具还有 NSSM ，与 EasyService 相比， NSSM 主要优点有：

* 提供了图形化安装、管理服务的界面

* 可以自定义环境变量

* 可以设置服务的依赖服务 dependencies

NSSM 主要缺点是界面和文档都是英文的，对新手也不见得更友好，另外在远程通过命令行编辑和管理服务稍微麻烦一些，需要记住它的命令的参数。

总体而言， EasyService 已实现了大部分服务程序需要的功能，主要优点有：

* 在命令行模式下编辑、管理和查看服务更方便

* 日志自动按日期输出到不同文件

* 停止服务时，先向工作进程的标准输入写入 "exit" ，并等待工作进程自己退出（但等待时间不超过 10 秒），这个 “通知退出” 的机制对于需要进行清理工作的程序来说是非常关键的

### v1.0.1 版的新功能

* （1） 原版 EasyService 需要对每个服务拷贝一个 svc.exe 作为服务的二进制文件。 v1.0.1 版去掉了此限制，所有 EasyService 服务共用同一个 svc.exe 。

* （2） 增加一个 svc create $project_name 命令，可以快速创建样板工程目录。

* （3） 增加 register-this-path.bat ，可以自动注册 svc.exe 所在的目录，在任意位置都可以使用 svc 命令。

### 典型用例

Appin 网站介绍了用 EasyService 部署 frp 内网穿透服务的方法，请看 [这里](https://www.appinn.com/easyservice-for-windows/) 。
