using System.Diagnostics;
using System.Text.Json;

namespace AutoResolution;

internal static class JsonFileStore
{
    private const int MaxAttempts = 5;
    private static readonly Mutex FileMutex = new(false, "AutoResolution.JsonFileStore");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static T? Load<T>(string path)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return default;
                }

                using var mutexLease = AcquireMutex();
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return JsonSerializer.Deserialize<T>(stream);
            }
            catch (JsonException)
            {
                return default;
            }
            catch (IOException) when (attempt < MaxAttempts - 1)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts - 1)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }

        return default;
    }

    public static bool Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                using var mutexLease = AcquireMutex();
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    JsonSerializer.Serialize(stream, value, Options);
                    stream.Flush(true);
                }

                File.Move(temporaryPath, path, true);
                return true;
            }
            catch (IOException) when (attempt < MaxAttempts - 1)
            {
                DeleteTemporaryFile(temporaryPath);
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts - 1)
            {
                DeleteTemporaryFile(temporaryPath);
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                DeleteTemporaryFile(temporaryPath);
                Debug.WriteLine($"No se pudo guardar el archivo JSON '{path}': {exception.Message}");
                return false;
            }
        }

        DeleteTemporaryFile(temporaryPath);
        Debug.WriteLine($"No se pudo guardar el archivo JSON '{path}' después de varios intentos.");
        return false;
    }

    private static IDisposable AcquireMutex()
    {
        try
        {
            FileMutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
            // The previous process ended while holding the mutex; ownership is transferred.
        }

        return new MutexLease(FileMutex);
    }

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class MutexLease(Mutex mutex) : IDisposable
    {
        public void Dispose() => mutex.ReleaseMutex();
    }
}
