namespace Skyline.DataMiner.Sdk.Shell
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// Allows running commands on the shell.
    /// </summary>
    internal interface IShell
    {
        /// <summary>
        /// Runs the given command on the shell.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="output">Any output from running the command.</param>
        /// <param name="errors">Any error from the command.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> that controls the cancellation of the command.</param>
        /// <param name="workingDirectory">Optional working directory in which the command should be run.</param>
        /// <returns><see cref="bool.TrueString"/> if there were no errors with the command.</returns>
        bool RunCommand(string command, out string output, out string errors, CancellationToken cancellationToken, string workingDirectory = "");
    }

    /// <summary>
    /// Helper methods for <see cref="IShell"/>.
    /// </summary>
    internal static class ShellFactory
    {
        /// <summary>
        /// Get your shiny shells here! Tailored specifically to your OS!
        /// </summary>
        /// <returns>Shiny shell.</returns>
        /// <exception cref="NotSupportedException">Can't create a shell for this OS.</exception>
        public static IShell GetShell()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsShell();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new UnixShell();
            }

            throw new NotSupportedException($"The current operating system ({System.Runtime.InteropServices.RuntimeInformation.OSDescription}) is not supported.");
        }
    }
}
