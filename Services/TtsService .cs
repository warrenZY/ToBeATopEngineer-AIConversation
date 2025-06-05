using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Versioning;

namespace ToBeATopEngineer_AIConversation.Services;

public class WindowsTtsService : ITtsService
{
    [SupportedOSPlatform("windows")] // Mark the method as Windows-specific
    public Task SpeakAsync(string text)
    {
        try
        {
            using (var speechSynthesizer = new SpeechSynthesizer())
            {
                // Attempt to find a voice supporting "zh-CN" culture
                var zhCnVoice = speechSynthesizer.GetInstalledVoices()
                    .Select(v => v.VoiceInfo)
                    .FirstOrDefault(voice => voice.Culture?.Name == "zh-CN");

                if (zhCnVoice != null)
                {
                    speechSynthesizer.SelectVoice(zhCnVoice.Name);
                }
                else
                {
                    // Fallback to "en-US" voice if "zh-CN" is not found
                    var enUsVoice = speechSynthesizer.GetInstalledVoices()
                        .Select(v => v.VoiceInfo)
                        .FirstOrDefault(voice => voice.Culture?.Name == "en-US");

                    if (enUsVoice != null)
                    {
                        speechSynthesizer.SelectVoice(enUsVoice.Name);
                        Console.WriteLine("Warning: 'zh-CN' voice not found. Using 'en-US' as fallback.");
                    }
                    else
                    {
                        throw new InvalidOperationException("No installed voices supporting 'zh-CN' or 'en-US' cultures were found.");
                    }
                }

                speechSynthesizer.SetOutputToDefaultAudioDevice();
                speechSynthesizer.Speak(text);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Windows TTS error: {ex.Message}");
        }
        return Task.CompletedTask;
    }
}

// --- Linux 平台 TTS 实现 ---
public class LinuxTtsService : ITtsService
{
    [SupportedOSPlatform("linux")]
    public async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return; // 空文本不进行朗读
        }

        Console.WriteLine($"尝试在 Linux 上朗读: \"{text}\"");

        try
        {
            // 1. **优先尝试使用 `spd-say` 朗读**
            // 关键：请确保 speech-dispatcher 服务在您的树莓派上正在运行！
            bool spdSaySuccess = await SpeakWithSpdSay(text);
            if (spdSaySuccess)
            {
                Console.WriteLine("spd-say 朗读成功。");
                return; // 如果 spd-say 成功，就直接返回
            }
            else
            {
                Console.WriteLine("spd-say 朗读失败，尝试 espeak。");
            }

            // 2. **如果 `spd-say` 失败，则回退到 `espeak`**
            // `espeak` 已配置为从临时文件读取，以避免其特定的管道问题。
            bool espeakSuccess = await SpeakWithEspeak(text);
            if (espeakSuccess)
            {
                Console.WriteLine("espeak 朗读成功。");
            }
            else
            {
                Console.WriteLine("espeak 朗读也失败了。无法在 Linux 上朗读文本。");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Linux TTS 服务捕获到全局错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 辅助方法：创建临时文件，写入文本，并返回文件路径。
    /// 调用者负责删除文件。
    /// </summary>
    /// <param name="text">要写入的文本。</param>
    /// <param name="prefix">临时文件名的前缀。</param>
    /// <returns>创建的临时文件的完整路径。</returns>
    private async Task<string> CreateAndWriteTempFileAsync(string text, string prefix)
    {
        // Path.GetTempFileName() 会创建一个唯一的零字节文件，并返回其完整路径。
        string tempFilePath = Path.GetTempFileName();

        // 写入文本内容到临时文件，使用 UTF-8 编码
        await File.WriteAllTextAsync(tempFilePath, text, Encoding.UTF8);
        return tempFilePath;
    }

    /// <summary>
    /// 尝试使用 `spd-say` 朗读文本，通过写入临时文件传递文本。
    /// </summary>
    /// <param name="text">要朗读的文本。</param>
    /// <returns>如果朗读成功返回 true，否则返回 false。</returns>
    private async Task<bool> SpeakWithSpdSay(string text)
    {
        // 检查 `spd-say` 命令是否存在
        if (!IsCommandAvailable("spd-say"))
        {
            Console.WriteLine("错误: `spd-say` 命令未找到。请安装 `speech-dispatcher` 包。");
            return false;
        }

        string tempFilePath = null; // 初始化为 null

        try
        {
            // 创建并写入文本到临时文件
            tempFilePath = await CreateAndWriteTempFileAsync(text, "spd_say_");

            var processInfo = new ProcessStartInfo
            {
                FileName = "spd-say",
                // 参数：-l 表示语言，-f 表示文件输入
                Arguments = $"-l zh -f \"{tempFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = "/" // 尝试设置工作目录
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                try
                {
                    process.Start();
                    await process.WaitForExitAsync();

                    string errorOutput = await process.StandardError.ReadToEndAsync();
                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine($"spd-say exited with code {process.ExitCode}. 错误: {errorOutput.Trim()}");
                        return false;
                    }
                    else
                    {
                        string output = await process.StandardOutput.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            Console.WriteLine($"spd-say output: {output.Trim()}");
                        }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    // 捕获进程执行期间可能出现的异常
                    Console.WriteLine($"执行 `spd-say` 失败: {ex.Message}");
                    return false;
                }
            }
        }
        finally
        {
            // 确保临时文件被删除
            if (tempFilePath != null && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 无法删除临时文件 {tempFilePath}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 尝试使用 `espeak` 朗读文本，通过写入临时文件。
    /// 这解决了使用管道输入时 'Failed to stat() file -' 的问题。
    /// </summary>
    /// <param name="text">要朗读的文本。</param>
    /// <returns>如果朗读成功返回 true，否则返回 false。</returns>
    private async Task<bool> SpeakWithEspeak(string text)
    {
        // 检查 `espeak` 命令是否存在
        if (!IsCommandAvailable("espeak"))
        {
            Console.WriteLine("错误: `espeak` 命令未找到。请安装 `espeak-ng` 包。");
            return false;
        }

        string tempFilePath = null; // 初始化为 null

        try
        {
            // 创建并写入文本到临时文件
            tempFilePath = await CreateAndWriteTempFileAsync(text, "espeak_");

            var processInfo = new ProcessStartInfo
            {
                FileName = "espeak",
                // 参数：-v 表示语音，-f 表示文件输入
                Arguments = $"-v zh -f \"{tempFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = "/" // 添加工作目录
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                try
                {
                    process.Start();
                    await process.WaitForExitAsync();

                    string errorOutput = await process.StandardError.ReadToEndAsync();
                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine($"espeak exited with code {process.ExitCode}. 错误: {errorOutput.Trim()}");
                        return false;
                    }
                    else
                    {
                        string output = await process.StandardOutput.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            Console.WriteLine($"espeak output: {output.Trim()}");
                        }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"执行 `espeak` 失败: {ex.Message}");
                    return false;
                }
            }
        }
        finally
        {
            // 确保临时文件被删除
            if (tempFilePath != null && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 无法删除临时文件 {tempFilePath}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 检查指定的命令是否在系统的PATH中可执行。
    /// </summary>
    private bool IsCommandAvailable(string command)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "which", // Linux/Unix 命令，用于查找可执行文件
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0; // 如果 `which` 找到命令，返回0
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"检查命令 '{command}' 可用性时出错: {ex.Message}");
            return false;
        }
    }
}
public class NullTtsService : ITtsService
{
    public Task SpeakAsync(string text)
    {
        // Do nothing - used as fallback
        return Task.CompletedTask;
    }
}