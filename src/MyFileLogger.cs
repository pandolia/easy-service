using System.Text;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

class MyFileLogger
{
    private readonly ILog Logger;

    public MyFileLogger(Conf conf)
    {
        var patternLayout = new PatternLayout
        {
            ConversionPattern = "%message%newline"
        };
        patternLayout.ActivateOptions();

        var fileAppender = new RollingFileAppender
        {
            Name = "MyFileAppenderName",
            Encoding = Encoding.UTF8,
            RollingStyle = RollingFileAppender.RollingMode.Date,
            File = conf.OutFileDir,
            DatePattern = "yyyy-MM-dd'.log'",
            StaticLogFileName = false,
            AppendToFile = true,
            Layout = patternLayout
        };
        fileAppender.ActivateOptions();

        var hierarchy = (Hierarchy)LogManager.GetRepository();
        hierarchy.Root.AddAppender(fileAppender);
        hierarchy.Root.Level = Level.All;
        hierarchy.Configured = true;

        Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }

    public void Log(string line)
    {
        Logger.Info(line);
    }
}