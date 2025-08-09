using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing CommandExists('java') method...");
        Console.WriteLine("======================================");
        
        bool result = CommandExists("java");
        Console.WriteLine($"Result: {result}");
    }
    
    private static bool CommandExists(string cmd)
    {
        try
        {
            Console.WriteLine($"Testing command: {cmd}");
            
            // Try running the command to see if it exists
            var psi = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("cmd.exe", $"/c where {cmd}")
                : new ProcessStartInfo("which", cmd);
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            
            Console.WriteLine($"Starting process: {psi.FileName} {psi.Arguments}");
            
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Console.WriteLine("Failed to start process");
                throw new Exception("Process.Start returned null");
            }
            
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            
            Console.WriteLine($"Exit code: {proc.ExitCode}");
            Console.WriteLine($"StdOut: '{stdout}'");
            Console.WriteLine($"StdErr: '{stderr}'");
            
            bool success = proc.ExitCode == 0;
            Console.WriteLine($"Primary method result: {success}");
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in primary method: {ex.Message}");
            Console.WriteLine("Falling back to PATH search...");
            
            // Fallback to PATH search
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            var exe = OperatingSystem.IsWindows() && Path.GetExtension(cmd) != ".exe" ? cmd + ".exe" : cmd;
            
            Console.WriteLine($"Looking for: {exe}");
            Console.WriteLine($"Searching {paths.Length} PATH entries:");
            
            foreach (var path in paths.Take(5)) // Show first 5 paths
            {
                Console.WriteLine($"  {path}");
                var fullPath = Path.Combine(path, exe);
                if (File.Exists(fullPath))
                {
                    Console.WriteLine($"    ✓ Found: {fullPath}");
                    return true;
                }
            }
            
            bool fallbackResult = paths.Any(p => File.Exists(Path.Combine(p, exe)));
            Console.WriteLine($"Fallback method result: {fallbackResult}");
            return fallbackResult;
        }
    }
}
