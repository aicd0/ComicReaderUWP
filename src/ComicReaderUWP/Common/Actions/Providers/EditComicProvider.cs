// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.Views.Dialogs.EditComicInfo;

namespace ComicReaderUWP.Common.Actions.Providers;

internal class EditComicProvider : IActionProvider
{
    public const string NAME = "EditComic";
    public const string PARAM_COMIC_ID = "ComicId";

    public string Name => NAME;

    public void Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        IMainWindowComponent? mainWindowCom = context.GetComponent<IMainWindowComponent>();
        if (mainWindowCom is null)
        {
            context.SetError("No IMainWindowComponent component found.");
            return;
        }

        string idList = parameters[PARAM_COMIC_ID] ?? string.Empty;
        string[] idsRaw = idList.Split(',', StringSplitOptions.RemoveEmptyEntries);
        List<long> ids = [];
        foreach (string idRaw in idsRaw)
        {
            if (!long.TryParse(idRaw, out long id))
            {
                context.SetError($"Invalid ComicId: {idRaw}");
                return;
            }

            ids.Add(id);
        }

        if (ids.Count == 0)
        {
            context.SetError("No valid comic ID found.");
            return;
        }

        CoroutineUtils.Run(async () =>
        {
            List<ComicModel> comics = await ComicModel.BatchFromId("EditComicProvider", ids);
            if (comics.Count > 0)
            {
                var dialog = new EditComicInfoDialog(comics);
                CoroutineUtils.Run(() => dialog.ShowAsync(mainWindowCom.WindowId));
            }

            context.SetSuccess();
        });
    }
}
