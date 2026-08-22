// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.ErrorHandling;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.SDK.Models;

namespace ComicReaderUWP.Data.Models.Misc;

internal class ComicImportExclusionModel
{
    private const string TAG = nameof(ComicImportExclusionModel);

    public static ComicImportExclusionModel Instance { get; } = new();

    private readonly ConcurrentDictionary<string, string> _excluded = [];

    private readonly Lazy<IRegistryKey> _registryKey;

    private ComicImportExclusionModel()
    {
        _registryKey = new(() =>
        {
            IRegistryKey registryKey = AppDB.MainRegistry.CreateKey(RegistryNames.COMIC_IMPORT_EXCLUSIONS);
            foreach (string location in registryKey.Keys)
            {
                _excluded[location] = string.Empty;
            }

            return registryKey;
        });
    }

    public bool Contains(string location)
    {
        EnsureInitialized();
        return _excluded.ContainsKey(location);
    }

    public void Add(IEnumerable<ComicModel> comics)
    {
        EnsureInitialized();

        IRegistryKey registryKey = _registryKey.Value;

        foreach (ComicModel comic in comics)
        {
            string location = comic.Location;
            if (string.IsNullOrEmpty(location))
            {
                Logger.F(TAG, "Comic location is empty.");
                continue;
            }

            _excluded[location] = string.Empty;
            registryKey.Set(location, string.Empty);
        }
    }

    public async Task<ErrorResult<bool>> EditWithNotepad()
    {
        EnsureInitialized();

        var err = ErrorLogger<bool>.Create(TAG);

        string[] lines = [.. _excluded.Keys.OrderBy(location => location, StringComparer.Ordinal)];

        ErrorResult<string> editErr = await ThirdPartyLauncher.EditTemporaryTextFileAsync("ComicImportExclusions.txt", lines);
        if (!editErr.IsSuccessful)
        {
            return err.SetError(editErr);
        }

        string filePath = editErr.Result;
        List<string> locations = [];
        try
        {
            string[] resultLines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
            foreach (string line in resultLines)
            {
                string location = line.Trim();
                if (string.IsNullOrEmpty(location))
                {
                    continue;
                }

                locations.Add(location);
            }
        }
        catch (Exception ex)
        {
            return err.SetError(ex);
        }

        Refresh(locations);

        return err.SetResult(default);
    }

    private void Refresh(IEnumerable<string> locations)
    {
        HashSet<string> newLocations = [.. locations];

        foreach (string location in _excluded.Keys)
        {
            if (newLocations.Contains(location))
            {
                continue;
            }

            _excluded.TryRemove(location, out _);
            _registryKey.Value.Remove(location);
        }

        foreach (string location in newLocations)
        {
            if (_excluded.ContainsKey(location))
            {
                continue;
            }

            _excluded[location] = string.Empty;
            _registryKey.Value.Set(location, string.Empty);
        }
    }

    private void EnsureInitialized()
    {
        _ = _registryKey.Value;
    }
}
