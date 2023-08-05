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
    await Task.Delay(1000);
}
