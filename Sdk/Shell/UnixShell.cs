namespace Skyline.DataMiner.Sdk.Shell
{
    using System.Diagnostics;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Allows running commands on the unix shell.
    /// </summary>
    internal class UnixShell : IShell
    {
        /// <inheritdoc/>
        public bool RunCommand(string command, out string output, out string errors, CancellationToken cancellationToken, string workingDirectory = "")
        {
            StringBuilder outputStream = new StringBuilder();
            StringBuilder errorStream = new StringBuilder();
            string escapedArgs = command.Replace("\"", "\\\"");
            bool success = true;
            using (Process cmd = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
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
