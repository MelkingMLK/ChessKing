using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChessStrategyApp.Models;

namespace ChessStrategyApp.Services;

public class ProfileService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public ProfileService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "ChessKing");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "user_profile.json");
    }

    public async Task<UserProfile> LoadProfileAsync()
    {
        await FileLock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                var newProfile = new UserProfile();
                string defaultJson = JsonSerializer.Serialize(newProfile, JsonOptions);
                await File.WriteAllTextAsync(_filePath, defaultJson);
                return newProfile;
            }

            string json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new UserProfile();
            }

            var profile = JsonSerializer.Deserialize<UserProfile>(json, JsonOptions);
            return profile ?? new UserProfile();
        }
        catch
        {
            return new UserProfile();
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task SaveProfileAsync(UserProfile profile)
    {
        await FileLock.WaitAsync();
        try
        {
            string json = JsonSerializer.Serialize(profile, JsonOptions);
            string tempFile = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempFile, json);
            File.Move(tempFile, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore salvataggio profilo: {ex.Message}");
        }
        finally
        {
            FileLock.Release();
        }
    }

    // Formula FIDE pura senza moltiplicatori streak
    public static int CalculateNewElo(int currentElo, int opponentElo, double score, int kFactor = 32)
    {
        double exponent = (opponentElo - currentElo) / 400.0;
        double expectedScore = 1.0 / (1.0 + Math.Pow(10.0, exponent));
        int delta = (int)Math.Round(kFactor * (score - expectedScore));
        int newElo = currentElo + delta;
        return Math.Max(100, newElo);
    }
}