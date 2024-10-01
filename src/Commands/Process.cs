﻿using System.Diagnostics;

namespace Devlooped.Sponsors;

public static class Process
{
    public static bool TryExecute(string program, string arguments, out string? output)
        => TryExecuteCore(program, arguments, null, out output);

    public static bool TryExecute(string program, string arguments, string input, out string? output)
        => TryExecuteCore(program, arguments, input, out output);

    static bool TryExecuteCore(string program, string arguments, string? input, out string? output)
    {
        var info = new ProcessStartInfo(program, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input != null,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };

        try
        {
            var proc = System.Diagnostics.Process.Start(info);
            if (proc == null)
            {
                output = null;
                return false;
            }

            var gotError = false;
            proc.ErrorDataReceived += (_, __) => gotError = true;

            if (input != null)
            {
                // Write the input to the standard input stream
                proc.StandardInput.WriteLine(input);
                proc.StandardInput.Close();
            }

            output = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(5000))
            {
                proc.Kill();
                output = null;
                return false;
            }

            var error = proc.StandardError.ReadToEnd();
            gotError |= error.Length > 0;
            output = output.Trim();
            if (string.IsNullOrEmpty(output))
                output = null;
            if (output == null && gotError && !string.IsNullOrWhiteSpace(error))
                output = error.Trim();

            return !gotError && proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            output = ex.Message;
            return false;
        }
    }
}
