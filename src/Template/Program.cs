using NLog;

// Template console project running NLog and NLog.Loki
var logger = LogManager.GetCurrentClassLogger();

logger.Info("Starting Template application.");
logger.Debug("Some low level info {A}, {B}, {C}.", "Hello", 12, true);

int i = 0;
while(true)
{
    logger.Info("Doing some hard work... Iteration {I}.", i++);
    logger.Info("Some additional information {Info1}", "Piece of info 1", new { arg1 = "info2", value = 22 });

    if(i % 10 == 0)
    {
        logger.Warn(
            new Exception("Something went wrong", new Exception("My innerException", new Exception("Inner inner exception"))),
            "Error while running operation {OperationName}",
            "Operation A", "Step B");
    }

    await Task.Delay(1000);
}
