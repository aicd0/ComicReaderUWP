// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Views.Dialogs.EditComicInfo;

namespace ComicReaderUWP.Common.Actions.Providers;

internal class DeleteComicProvider : IActionProvider
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

        var dialog = new EditComicInfoDialog(comics);
        await dialog.ShowAsync(mainWindowCom.WindowId);
        return ActionResult.FromSuccess();
    }
}
