using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MCPForUnity.Editor.Constants;
using UnityEditor;

namespace MCPForUnity.Editor.Helpers
{
    internal static class ExecPath
    {
        private const string PrefClaude = EditorPrefKeys.ClaudeCliPathOverride;

        // Resolve Claude CLI absolute path. Pref → env → common locations → PATH.
        internal static string ResolveClaude()
        {
            try
            {
                string pref = EditorPrefs.GetString(PrefClaude, string.Empty);
                if (!string.IsNullOrEmpty(pref) && File.Exists(pref)) return pref;
            }
            catch { }

            string env = Environment.GetEnvironmentVariable("CLAUDE_CLI");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
                string[] candidates =
                {
                    "/opt/homebrew/bin/claude",
                    "/usr/local/bin/claude",
                    Path.Combine(home, ".local", "bin", "claude"),
                };
                foreach (string c in candidates) { if (File.Exists(c)) return c; }
                // Try NVM-installed claude under ~/.nvm/versions/node/*/bin/claude
                string nvmClaude = ResolveClaudeFromNvm(home);
                if (!string.IsNullOrEmpty(nvmClaude)) return nvmClaude;
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                return Which("claude", "/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin");
#else
                return null;
#endif
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#if UNITY_EDITOR_WIN
                // Common npm global locations
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? string.Empty;
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) ?? string.Empty;
                string[] candidates =
                {
                    // Prefer .cmd (most reliable from non-interactive processes)
                    Path.Combine(appData, "npm", "claude.cmd"),
                    Path.Combine(localAppData, "npm", "claude.cmd"),
                    // Fall back to PowerShell shim if only .ps1 is present
                    Path.Combine(appData, "npm", "claude.ps1"),
                    Path.Combine(localAppData, "npm", "claude.ps1"),
                };
                foreach (string c in candidates) { if (File.Exists(c)) return c; }
                string fromWhere = Where("claude.exe") ?? Where("claude.cmd") ?? Where("claude.ps1") ?? Where("claude");
                if (!string.IsNullOrEmpty(fromWhere)) return fromWhere;
#endif
                return null;
            }

            // Linux
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
                string[] candidates =
                {
                    "/usr/local/bin/claude",
                    "/usr/bin/claude",
                    Path.Combine(home, ".local", "bin", "claude"),
                };
                foreach (string c in candidates) { if (File.Exists(c)) return c; }
                // Try NVM-installed claude under ~/.nvm/versions/node/*/bin/claude
                string nvmClaude = ResolveClaudeFromNvm(home);
                if (!string.IsNullOrEmpty(nvmClaude)) return nvmClaude;
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                return Which("claude", "/usr/local/bin:/usr/bin:/bin");
#else
                return null;
#endif
            }
        }

        // Attempt to resolve claude from NVM-managed Node installations, choosing the newest version
        private static string ResolveClaudeFromNvm(string home)
        {
            try
            {
                if (string.IsNullOrEmpty(home)) return null;
                string nvmNodeDir = Path.Combine(home, ".nvm", "versions", "node");
                if (!Directory.Exists(nvmNodeDir)) return null;

                string bestPath = null;
                Version bestVersion = null;
                foreach (string versionDir in Directory.EnumerateDirectories(nvmNodeDir))
                {
                    string name = Path.GetFileName(versionDir);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (name.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract numeric portion: e.g., v18.19.0-nightly -> 18.19.0
                        string versionStr = name.Substring(1);
                        int dashIndex = versionStr.IndexOf('-');
                        if (dashIndex > 0)
                        {
                            versionStr = versionStr.Substring(0, dashIndex);
                        }
                        if (Version.TryParse(versionStr, out Version parsed))
                        {
                            string candidate = Path.Combine(versionDir, "bin", "claude");
                            if (File.Exists(candidate))
                            {
                                if (bestVersion == null || parsed > bestVersion)
                                {
                                    bestVersion = parsed;
                                    bestPath = candidate;
                                }
                            }
                        }
                    }
                }
                return bestPath;
            }
            catch { return null; }
        }

        // Explicitly set the Claude CLI absolute path override in EditorPrefs
        internal static void SetClaudeCliPath(string absolutePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
                {
                    EditorPrefs.SetString(PrefClaude, absolutePath);
                }
            }
            catch { }
        }

        // Clear any previously set Claude CLI override path
        internal static void ClearClaudeCliPath()
        {
            try
            {
                if (EditorPrefs.HasKey(PrefClaude))
                {
                    EditorPrefs.DeleteKey(PrefClaude);
                }
            }
            catch { }
        }

        internal static bool TryRun(
            string file,
            string args,
            string workingDir,
            out string stdout,
            out string stderr,
            int timeoutMs = 15000,
            string extraPathPrepend = null)
        {
            stdout = string.Empty;
            stderr = string.Empty;
            try
            {
                // Handle PowerShell scripts on Windows by invoking through powershell.exe
                bool isPs1 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                             file.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);

                var psi = new ProcessStartInfo
                {
                    FileName = isPs1 ? "powershell.exe" : file,
                    Arguments = isPs1
                        ? $"-NoProfile -ExecutionPolicy Bypass -File \"{file}\" {args}".Trim()
                        : args,
                    WorkingDirectory = string.IsNullOrEmpty(workingDir) ? Environment.CurrentDirectory : workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                if (!string.IsNullOrEmpty(extraPathPrepend))
                {
                    string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    psi.EnvironmentVariables["PATH"] = string.IsNullOrEmpty(currentPath)
                        ? extraPathPrepend
                        : (extraPathPrepend + System.IO.Path.PathSeparator + currentPath);
                }

                using var process = new Process { StartInfo = psi, EnableRaisingEvents = false };

                var so = new StringBuilder();
                var se = new StringBuilder();
                process.OutputDataReceived += (_, e) => { if (e.Data != null) so.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) se.AppendLine(e.Data); };

                if (!process.Start()) return false;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    // Timeout occurred - kill the process (child processes may remain due to Unity .NET limitations)
                    try 
                    { 
                        if (!process.HasExited)
                        {
                            int pid = process.Id;
                            try
                            {
                                // Kill process (entireProcessTree not supported in Unity .NET profile)
                                process.Kill();
                                
                                // Wait a bit to ensure the process actually terminates
                                if (!process.WaitForExit(1000))
                                {
                                    McpLog.Warn($"Process {pid} did not exit after Kill command");
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                // Process already exited - that's fine
                            }
                            catch (Exception killEx)
                            {
                                McpLog.Warn($"Failed to kill process {pid}: {killEx.Message}");
                            }
                        }
                    } 
                    catch (Exception ex) 
                    { 
                        McpLog.Debug($"Error during process cleanup: {ex.Message}");
                    }
                    return false;
                }

                // Ensure async buffers are flushed
                process.WaitForExit();

                stdout = so.ToString();
                stderr = se.ToString();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        private static string Which(string exe, string prependPath)
        {
            try
            {
                var psi = new ProcessStartInfo("/usr/bin/which", exe)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true, // Consume stderr
                    CreateNoWindow = true,
                };
                string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                psi.EnvironmentVariables["PATH"] = string.IsNullOrEmpty(path) ? prependPath : (prependPath + Path.PathSeparator + path);
                
                using var p = new Process { StartInfo = psi };
                var outputBuilder = new StringBuilder();
                
                p.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                p.ErrorDataReceived += (sender, e) => { }; // Drain stderr

                if (!p.Start()) return null;

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                if (p.WaitForExit(2000))
                {
                    string output = outputBuilder.ToString().Trim();
                    return (!string.IsNullOrEmpty(output) && File.Exists(output)) ? output : null;
                }
                else
                {
                    try { p.Kill(); } catch { }
                    return null;
                }
            }
            catch { return null; }
        }
#endif

#if UNITY_EDITOR_WIN
        private static string Where(string exe)
        {
            try
            {
                var psi = new ProcessStartInfo("where", exe)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true, // Consume stderr
                    CreateNoWindow = true,
                };
                
                using var p = new Process { StartInfo = psi };
                var outputBuilder = new StringBuilder();

                p.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                p.ErrorDataReceived += (sender, e) => { }; // Drain stderr

                if (!p.Start()) return null;

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                if (p.WaitForExit(2000))
                {
                    string first = outputBuilder.ToString()
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault();
                    return (!string.IsNullOrEmpty(first) && File.Exists(first)) ? first : null;
                }
                else
                {
                    try { p.Kill(); } catch { }
                    return null;
                }
            }
            catch { return null; }
        }
#endif

        /// <summary>
        /// Creates a ProcessStartInfo for opening a terminal window with the given command
        /// Works cross-platform: macOS, Windows, and Linux
        /// </summary>
        public static ProcessStartInfo CreateTerminalProcessStartInfo(string command, string pathPrepend = null)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be empty", nameof(command));

            command = command.Replace("\r", "").Replace("\n", "");

#if UNITY_EDITOR_OSX
            // macOS: Use osascript directly to avoid shell metacharacter injection via bash
            // Escape for AppleScript: backslash and double quotes
            string escapedCommand = command.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                Arguments = $"-e \"tell application \\\"Terminal\\\" to do script \\\"{escapedCommand}\\\" activate\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
#elif UNITY_EDITOR_WIN
            // Windows: Use cmd.exe with start command to open new window
            // Wrap in quotes for /k and escape internal quotes
            string escapedCommandWin = command.Replace("\"", "\\\"");
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // We need to inject PATH into the new cmd window.
                // Since 'start' launches a separate process, we'll try to set PATH before running the command.
                // Note: 'start' inherits environment variables, so setting them on this ProcessStartInfo should work.
                Arguments = $"/c start \"MCP Server\" cmd.exe /k \"{escapedCommandWin}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Inject PATH
            if (!string.IsNullOrEmpty(pathPrepend))
            {
                string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                psi.EnvironmentVariables["PATH"] = pathPrepend + Path.PathSeparator + currentPath;
            }
            return psi;
#else
            // Linux: Try common terminal emulators
            // We use bash -c to execute the command, so we must properly quote/escape for bash
            // Escape single quotes for the inner bash string
            string escapedCommandLinux = command.Replace("'", "'\\''");
            // Wrap the command in single quotes for bash -c
            string script = $"'{escapedCommandLinux}; exec bash'";
            // Escape double quotes for the outer Process argument string
            string escapedScriptForArg = script.Replace("\"", "\\\"");
            string bashCmdArgs = $"bash -c \"{escapedScriptForArg}\"";
            
            string[] terminals = { "gnome-terminal", "xterm", "konsole", "xfce4-terminal" };
            string terminalCmd = null;
            
            foreach (var term in terminals)
            {
                try
                {
                    // Use 'which' to find the terminal emulator
                    string found = null;
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                     found = Which(term, "/usr/bin:/bin:/usr/local/bin");
#endif
                    if (!string.IsNullOrEmpty(found))
                    {
                        terminalCmd = term;
                        break;
                    }
                }
                catch { }
            }
            
            if (terminalCmd == null)
            {
                terminalCmd = "xterm"; // Fallback
            }
            
            // Different terminals have different argument formats
            string args;
            if (terminalCmd == "gnome-terminal")
            {
                args = $"-- {bashCmdArgs}";
            }
            else if (terminalCmd == "konsole")
            {
                args = $"-e {bashCmdArgs}";
            }
            else if (terminalCmd == "xfce4-terminal")
            {
                // xfce4-terminal expects -e "command string" or -e command arg
                args = $"--hold -e \"{bashCmdArgs.Replace("\"", "\\\"")}\"";
            }
            else // xterm and others
            {
                args = $"-hold -e {bashCmdArgs}";
            }
            
            return new ProcessStartInfo
            {
                FileName = terminalCmd,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };
#endif
        }
    }
}
