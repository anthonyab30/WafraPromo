using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for logging

namespace WafraPromotion.API.Services
{
    public class ImageProcessingResult
    {
        public string? OcrText { get; set; }
        public string? OcrError { get; set; }
        public string? Phash { get; set; }
        public string? PhashError { get; set; }
        public string? Error { get; set; } // General error for the script execution itself
    }

    public class ImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly string _pythonScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImageProcessor", "process_image.py");
        private readonly string _pythonInterpreter;

        public ImageProcessingService(ILogger<ImageProcessingService> logger)
        {
            _logger = logger;
            // Determine Python interpreter (python3 or python). This could be configurable.
            _pythonInterpreter = GetPythonInterpreter();

            // Log the determined script path for debugging deployment/runtime issues
            _logger.LogInformation($"Python script path resolved to: {Path.GetFullPath(_pythonScriptPath)}");
            if (!File.Exists(_pythonScriptPath))
            {
                _logger.LogWarning($"Python script not found at: {Path.GetFullPath(_pythonScriptPath)}. OCR and pHash functionality will fail.");
            }
        }

        private string GetPythonInterpreter()
        {
            // Simple check, can be made more robust or configurable
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("python3") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                Process? process = Process.Start(psi);
                process?.WaitForExit(1000); // Wait 1 sec
                if (process != null && process.ExitCode == 0 || process?.ExitCode == 1) return "python3"; // Exit code 1 if called without args
            }
            catch(Exception ex) {
                 _logger.LogWarning(ex, "Python3 not found or failed to execute, falling back to 'python'.");
            }
            return "python";
        }


        public async Task<ImageProcessingResult?> ProcessImageAsync(string imagePath)
        {
            if (!File.Exists(_pythonScriptPath))
            {
                _logger.LogError($"Python script not found at {_pythonScriptPath}. Cannot process image.");
                return new ImageProcessingResult { Error = "Image processing script not configured or not found." };
            }

            if (!File.Exists(imagePath))
            {
                _logger.LogError($"Image file not found at {imagePath} for processing.");
                return new ImageProcessingResult { Error = $"Image file not found: {imagePath}" };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonInterpreter,
                Arguments = $"\"{Path.GetFullPath(_pythonScriptPath)}\" \"{Path.GetFullPath(imagePath)}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _logger.LogInformation($"Executing Python script: {_pythonInterpreter} {startInfo.Arguments}");

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    _logger.LogError("Failed to start image processing script.");
                    return new ImageProcessingResult { Error = "Failed to start image processing script." };
                }

                string output = await process.StandardOutput.ReadToEndAsync();
                string errors = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Python script execution failed with exit code {process.ExitCode}. Errors: {errors}. Output: {output}");
                    return new ImageProcessingResult { Error = $"Script error: {errors} {output}" };
                }

                _logger.LogInformation($"Python script output: {output}");
                if (string.IsNullOrWhiteSpace(output))
                {
                     _logger.LogError($"Python script returned empty output. Errors: {errors}");
                    return new ImageProcessingResult { Error = $"Script returned empty output. Errors: {errors}" };
                }

                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var result = JsonSerializer.Deserialize<ImageProcessingResult>(output, options);
                    if (result != null && result.Error == null && result.OcrError == null && result.PhashError == null && string.IsNullOrEmpty(result.OcrText) && string.IsNullOrEmpty(result.Phash))
                    {
                        // If there are no specific errors reported by the script, but ocr and phash are empty, it might be an issue
                        _logger.LogWarning($"Python script returned empty OCR text and pHash without explicit errors. Output: {output}");
                    }
                    return result;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, $"Error deserializing JSON output from Python script. Output: {output}");
                    return new ImageProcessingResult { Error = $"JSON deserialization error: {ex.Message}. Output: {output}" };
                }
            }
        }
    }
}
