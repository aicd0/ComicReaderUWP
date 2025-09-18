// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Providers;
using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Common;

internal class EventRecorder
{
    public static EventRecorder Create(string tag)
    {
        return new(tag);
    }

    public bool Successful { get; private set; } = true;
    public string ErrorMessage { get; private set; } = string.Empty;
    public bool LastChildSuccessful => _children.Count == 0 || _children[^1].Successful;

    public string DetailedErrorMessage
    {
        get
        {
            if (Successful)
            {
                return string.Empty;
            }

            List<string> messages = [];
            void CollectMessages(int depth, EventRecorder recorder)
            {
                if (recorder.Successful)
                {
                    return;
                }

                string content = recorder.ErrorMessage;
                if (string.IsNullOrEmpty(content))
                {
                    content = "(no message)";
                }

                StringBuilder sb = new();
                if (depth > 0)
                {
                    sb.Append(' ', depth * 2);
                    sb.Append("- ");
                }

                sb.Append(content);
                messages.Add(sb.ToString());

                lock (recorder._children)
                {
                    for (int i = recorder._children.Count - 1; i >= 0; i--)
                    {
                        CollectMessages(depth + 1, recorder._children[i]);
                    }
                }
            }

            CollectMessages(0, this);
            return string.Join('\n', messages);
        }
    }

    private readonly string _tag;
    private readonly List<EventRecorder> _children = [];

    private EventRecorder(string tag)
    {
        _tag = tag;
    }

    public EventRecorder New(string tag)
    {
        EventRecorder child = new(tag);
        lock (_children)
        {
            _children.Add(child);
        }

        return child;
    }

    public void SetError(string message, Exception? exception = null, bool fatal = false)
    {
        Successful = false;
        ErrorMessage = message;

        if (fatal)
        {
            Logger.F(_tag, message, exception);
        }
        else
        {
            Logger.E(_tag, message, exception);
        }
    }

    public void SetError(Exception exception, bool fatal = false)
    {
        Successful = false;
        ErrorMessage = exception.Message;

        if (fatal)
        {
            Logger.F(_tag, exception);
        }
        else
        {
            Logger.E(_tag, exception);
        }
    }

    public void DisplayErrorMessage(ActionHandler actionHandler)
    {
        if (Successful)
        {
            return;
        }

        string detailedMessage = DetailedErrorMessage;
        ActionModel actionModel = ActionModel.Builder.Create(MessageDialogProvider.NAME)
            .AddParameter(MessageDialogProvider.PARAM_TITLE, "Error")
            .AddParameter(MessageDialogProvider.PARAM_MESSAGE, detailedMessage)
            .Build();
        actionHandler.Handle(actionModel);
    }
}
