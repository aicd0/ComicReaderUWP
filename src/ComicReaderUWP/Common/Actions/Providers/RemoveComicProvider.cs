// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.SDK.Models;

namespace ComicReaderUWP.Common.Actions.Providers;

internal class RemoveComicProvider : IActionProvider
{
    public const string NAME = "DeleteComic";
    public const string PARAM_COMIC_ID = "ComicId";

    public string Name => NAME;

    public async Task<ActionResult> Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        IMainWindowComponent? mainWindowCom = context.GetComponent<IMainWindowComponent>();
        if (mainWindowCom is null)
        {
            return ActionResult.FromFailure("IMainWindowComponent component not found.");
        }

        string idList = parameters[PARAM_COMIC_ID] ?? string.Empty;
        string[] idsRaw = idList.Split(',', StringSplitOptions.RemoveEmptyEntries);
        List<long> ids = [];
        foreach (string idRaw in idsRaw)
        {
            if (!long.TryParse(idRaw, out long id))
            {
                return ActionResult.FromFailure($"Invalid ComicId: {idRaw}");
            }

            ids.Add(id);
        }

        if (ids.Count == 0)
        {
            return ActionResult.FromFailure("No valid comic ID found.");
        }

        List<ComicModel> comics = await ComicModel.BatchFromId(ids);
        if (comics.Count == 0)
        {
            return ActionResult.FromSuccess();
        }

        string promptContent = StringResourceProvider.Instance.ComicRemovalPromptContent
            .Replace("$count", comics.Count.ToString())
            .Replace("$comics", string.Join('\n', comics.Select(x => x.Location)));
        DialogOptions options = new DialogOptions.Builder()
            .SetTitle(StringResourceProvider.Instance.Warning)
            .SetContent(promptContent)
            .SetPrimaryButtonText(StringResourceProvider.Instance.Remove)
            .SetCloseButtonText(StringResourceProvider.Instance.Cancel)
            .Build();
        DialogResult result = await DialogUtils.EnqueueDialogAsync(mainWindowCom.WindowId, options);
        if (result != DialogResult.Primary)
        {
            return ActionResult.FromFailure("Cancelled by user.");
        }

        await BusyStateManager.WithBusyState(() => ComicModel.RemoveComics(comics));
        return ActionResult.FromSuccess();
    }
}
