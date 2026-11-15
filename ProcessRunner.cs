using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StrmPosterForJellyfin
{
    /// <summary>
    /// 外部进程执行结果。
    /// </summary>
    public class ProcessResult
    {
        public int ExitCode { get; init; }

        public string StandardOutput { get; init; } = string.Empty;

        public string StandardError { get; init; } = string.Empty;

        /// <summary>
        /// 获取一个值，指示进程是否因超时被终止。
        /// </summary>
        public bool TimedOut { get; init; }
    }

    /// <summary>
    /// 统一的外部进程执行器：使用 ArgumentList 避免参数注入，并保证超时/取消时结束进程。
    /// </summary>
    public static class ProcessRunner
    {
        public static async Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            try
            {
                var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(timeoutCts.Token)).ConfigureAwait(false);

                return new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = await outputTask.ConfigureAwait(false),
                    StandardError = await errorTask.ConfigureAwait(false)
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                throw;
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return new ProcessResult
                {
                    ExitCode = -1,
                    TimedOut = true
                };
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // 进程可能已自行退出，忽略
            }
        }
    }
}
