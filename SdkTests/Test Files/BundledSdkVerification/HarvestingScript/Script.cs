namespace HarvestingScript
{
    using System;

    using Skyline.DataMiner.Automation;

    public sealed class Script
    {
        public void Run(IEngine engine)
        {
            try
            {
                engine.Log("Run|" + Harvesting.Probe.Entry.GetValue());
            }
            catch (ScriptAbortException)
            {
                throw;
            }
            catch (ScriptForceAbortException)
            {
                throw;
            }
            catch (ScriptTimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                engine.Log("Run|Unexpected exception: " + ex.ToString());
                engine.ExitFail("Runtime operation failed.");
            }
        }
    }
}
