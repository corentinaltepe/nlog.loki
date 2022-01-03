using NLog;

// Template console project running NLog and NLog.Loki
var logger = LogManager.GetCurrentClassLogger();

logger.Info("Starting Template application.");
logger.Debug("Some low level info {A}, {B}, {C}.", "Hello", 12, true);

int i = 0;
while(true) {
    logger.Info("Doing some hard work... Iteration {I}.", i++);
    await Task.Delay(1000);
}
