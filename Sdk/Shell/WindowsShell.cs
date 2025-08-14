namespace Skyline.DataMiner.Sdk.Shell
{
    using System.Diagnostics;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Allows running commands on the windows shell.
    /// </summary>
    internal class WindowsShell : IShell
    {
        /// <inheritdoc/>
        public bool RunCommand(string command, out string output, out string errors, CancellationToken cancellationToken, string workingDirectory = "")
        {
            StringBuilder outputStream = new StringBuilder();
            StringBuilder errorStream = new StringBuilder();

            bool success = true;
            using (Process cmd = new Process
            {
                // You got to put the entire thing in quotes so the WindowsShell can remove the quotes
                // and think there's no quotes while there are quotes which get removed.
                // If you don't put the quotes, you have to put more quotes which is too confusing.
                // See https://ss64.com/nt/cmd.html
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas",
                    FileName = "cmd.exe",
                    Arguments = $"/S /C \"{command}\"",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                }
            })
            {

                cmd.OutputDataReceived += (_, args) => { outputStream.AppendLine(args.Data); };
                cmd.ErrorDataReceived += (_, args) => { errorStream.AppendLine(args.Data); };
                cmd.Start();
                cmd.BeginOutputReadLine();
                cmd.BeginErrorReadLine();
                cmd.WaitForExit(300000);// 5 min max wait
                if (!cmd.HasExited)
                {
                    success = false;
                    cmd.Kill();
                }

                output = outputStream.ToString();
                errors = errorStream.ToString();

                success &= cmd.ExitCode == 0;
                return success;
            }
        }
    }
}
