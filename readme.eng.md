[中文](https://github.com/pandolia/easy-service) | English

### Introduction

If your Windows program needs to run permanently in the background, and you want it to be started automatically after system booting, then you need to register it as a system service.

Unfortunately, making a program that can be registered as a system service is not an easy task. First, the propgram must be an executable binary, so you can't write it with script language like Python or virtual machine language like Java. Second, you must write it in accordance with the format of Windows Service Program, which is complecated. Refer to [MS official document](https://code.msdn.microsoft.com/windowsapps/CppWindowsService-cacf4948) to see how to make a Windows Service Program.

[EasyService](https://github.com/pandolia/easy-service) is a small tool which can register normal program as a system service. It's only 25KB. You can write your program in normal way with whatever language you like, and register it as a system servie, then it will be started automatically after system booting, runs permanently in the background, and keeps runnning even after you logout the system.

If you need to deploy website server, api server or other server that runs permanently in the background in Windows, EasyService will be a very usefull tool.

### Setup

Download [the source and binary of EasyService](https://github.com/pandolia/easy-service/archive/master.zip), extract it. Then right click ***bin/register-this-path.bat*** , run with Administrator to add the path of ***bin*** directory to system path. Or add it manual.

Reopen My Computer, open a terminal somewhere, run command *** svc -v *** to check whether the installation is successful.

### Usage

(1) Write and test your program. EasyService has only one mandatory requirement and a recommendation to your program:

```
mandatory requirement: the program runs permanently

recommendation: the program exits in 5 seconds when receives data "exit" in its stdin
```

Typical programs are: [index.js](https://github.com/pandolia/easy-service/blob/master/samples/nodejs-version/worker/index.js) (nodejs version), [main.py](https://github.com/pandolia/easy-service/blob/master/samples/python-version/worker/main.py) (Python version), and [SampleWorker.cs](https://github.com/pandolia/easy-service/blob/master/src/SampleWorker.cs)。

(2) Open a terminal with administrator, run ***svc create hello-svc** to create a template project directory hello-svc.

(3) Open **hello-svc/svc.conf** , edit configurations:

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

(4) Cd to hello-svc:

a. run ***svc check*** to check configurations

b. run ***svc test-worker*** to test your program (the Worker)

If everything are fine:

c. run ***svc install*** to register and start a system service. Now your program is running in the background.

d. run ***svc stop|start|restart|remove*** to stop, start, restart and remove the service.

e. run ***svc log*** to display output of the service.

### Register multiple services

To register multiple services, just run ***svc create your-project-name*** to create multiple template project directories, edit configurations, and run **svc check|test-worker|install|...** .

### Internal Implementation

Actually, EasyService registers himself (**svc.exe**) as a system service. When this service starts, he reads configurations in **svc.conf** and creates a child process to run the program (the Worker), then monitors the child process and re-creates one if it stops running. When this service stops, he writes data "exit" to the stdin of the child process and wait for it to exit, and terminates the child process if waitting time exceeds 5 seconds.

Source code of EasyService are in [src](https://github.com/pandolia/easy-service/tree/master/src) 。