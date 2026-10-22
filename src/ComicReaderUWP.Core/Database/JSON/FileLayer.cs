// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.DebugTools;

namespace ComicReaderUWP.Core.Database.JSON;

public class FileLayer(string filepath) : IConfigBackingLayer
{
    private const string TAG = nameof(FileLayer);

    private readonly string _filepath = filepath;

    public string? ReadConfig()
    {
        try
        {
            return File.ReadAllText(_filepath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, ex);
            return null;
        }
    }

    public void WriteConfig(string value)
    {
        try
        {
            File.WriteAllText(_filepath, value);
        }
        catch (DirectoryNotFoundException)
        {
            string? folderPath = Path.GetDirectoryName(_filepath);
            if (string.IsNullOrEmpty(folderPath))
            {
                Logger.F(TAG, "Directory not found and folder is null or empty.");
                return;
            }

            try
            {
                Directory.CreateDirectory(folderPath);
                File.WriteAllText(_filepath, value);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, ex);
            }
        }
        catch (Exception ex)
        {
            Logger.F(TAG, ex);
        }
    }
}
