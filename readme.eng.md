[中文](https://github.com/pandolia/easy-service) | English

### Introduction

If your Windows program needs to run permanently in the background, and you want it to be started automatically after system booting, then you need to register it as a system service.

Unfortunately, making a program that can be registered as a system service is not an easy task. First, the propgram must be an executable binary, so you can't write it with script language like Python or virtual machine language like Java. Second, you must write it in accordance with the format of Windows Service Program, which is complecated. Refer to [MS official document](https://code.msdn.microsoft.com/windowsapps/CppWindowsService-cacf4948) to see how to make a Windows Service Program.

[EasyService](https://github.com/pandolia/easy-service) is a small tool which can register normal program as a system service. It's only 16KB. You can write your program in normal way with whatever language you like, and register it as a system servie, then it will be started automatically after system booting, runs permanently in the background, and keeps runnning even after you logout the system.

If you need to deploy website server, api server or other server that runs permanently in the background in Windows, EasyService will be a very usefull tool.

### System Requirement

EasyService requires .NetFramework 4.0 (which is already installed in most Windows distributions). Try to run [worker/sample-worker.exe](https://github.com/pandolia/easy-service/raw/master/worker/sample-worker.exe), if it runs normally, then .NetFramework 4.0 is installed in your system.

### Usage

(1) Write and test your program. EasyService has only one mandatory requirement and a recommendation to your program:

```
mandatory requirement: the program runs permanently

recommendation: the program exits in 10 seconds when receives data "exit" in its stdin
```

Typical programs are: [worker/index.js](https://github.com/pandolia/easy-service/blob/master/worker/index.js) (nodejs version), [worker/main.py](https://github.com/pandolia/easy-service/blob/master/worker/main.py) (Python version), and [src/SampleWorker.cs](https://github.com/pandolia/easy-service/blob/master/src/SampleWorker.cs) (C# version)。

(2) Download and extract [source and binary of EasyService](https://github.com/pandolia/easy-service/archive/master.zip).

(3) Open **svc.conf** , edit configurations:

```conf
# service's name, DO NOT conflict with existed services
ServiceName: An Easy Service

# program and command line arguments
Worker: node index.js

# working directory where you want to run the program, make sure this diretory exists
WorkingDir: worker

# output of the program will be written to this directory, make sure this diretory exists
OutFileDir: outfiles

# output encoding of the program, leave empty if you are not sure
WorkerEncoding: utf8
```

(4) Open a terminal with adminstration privilege in the directory which contains **svc.exe**:

a. run ***svc check*** to check configurations

b. run ***svc test-worker*** to test your program (the Worker)

If no errors happen:

c. run ***svc install*** to register and start a system service. Now your program is running in the background, and it will be started automatically after system booting.

You can see all registered services in Service Manage Console (services.msc).

d. run ***svc stop|start|restart|remove*** to stop, start, restart and remove the service.

### Register mutiple services

To register mutiple services, just create mutiple directories, copy **svc.exe** and **svc.conf** to them, edit configurations, and run **svc check|test-worker|install|...** .

### Internal Implementation

Actually, EasyService registers himself (**svc.exe**) as a system service. When this service starts, he reads configurations in **svc.conf** and creates a child process to run the program (the Worker), then monitors the child process and re-creates one if it stops running. When this service stops, he writes data "exit" to the stdin of the child process and wait for it to exit, and terminates the child process if waitting time exceeds 10 seconds.

Source code of EasyService are in [src/main.cs](https://github.com/pandolia/easy-service/blob/master/src/Main.cs) 。