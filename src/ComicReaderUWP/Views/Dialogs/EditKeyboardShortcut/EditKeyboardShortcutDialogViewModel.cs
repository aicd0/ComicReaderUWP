// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel;

using ComicReaderUWP.Common.HotKey;
using ComicReaderUWP.Common.Localization;

namespace ComicReaderUWP.Views.Dialogs.EditKeyboardShortcut;

internal partial class EditKeyboardShortcutDialogViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    private List<KeyboardShortcutActionEntry> _actions = [];
    public List<KeyboardShortcutActionEntry> Actions
    {
        get => _actions;
        private set
        {
            _actions = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Actions)));
        }
    }

    private int _actionIndex = -1;
    public int ActionIndex
    {
        get => _actionIndex;
        set
        {
            _actionIndex = value;
            if (value >= 0 && value < Actions.Count)
            {
                _action = Actions[value].Id;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActionIndex)));
            Validate();
        }
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
        }
    }

    private bool _hasError = false;
    public bool HasError
    {
        get => _hasError;
        set
        {
            _hasError = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
        }
    }

    private bool _okEnabled = false;
    public bool OkEnabled
    {
        get => _okEnabled;
        set
        {
            _okEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OkEnabled)));
        }
    }

    public KeyboardShortcutModel? Result { get; private set; }

    private KeyboardShortcutModel? _original;
    private KeyboardShortcutModel? _key;
    private string _action = string.Empty;

    public void Initialize(KeyboardShortcutModel? original)
    {
        _original = original;

        Title = original is null ? StringResourceProvider.Instance.AddKeyboardShortcut : StringResourceProvider.Instance.EditKeyboardShortcut;
        Actions = KeyboardShortcutActions.GetAllEntries();

        int actionIndex = 0;
        if (original is not null)
        {
            int index = Actions.FindIndex(entry => entry.Id == original.Action);
            if (index >= 0)
            {
                actionIndex = index;
            }
        }

        ActionIndex = actionIndex;

        Validate();
    }

    public void UpdateShortcut(KeyboardShortcutModel? shortcut)
    {
        _key = shortcut;
        Validate();
    }

    public KeyboardShortcutModel? CreateResult()
    {
        Validate();
        if (!OkEnabled || _key is null || string.IsNullOrEmpty(_action))
        {
            return null;
        }

        Result = new KeyboardShortcutModel
        {
            Action = _action,
            Key = _key.Key,
            Modifiers = _key.Modifiers,
        };
        return Result;
    }

    private void Validate()
    {
        string error = string.Empty;
        bool ok = false;
        if (_key is not null && !string.IsNullOrEmpty(_action))
        {
            if (KeyboardShortcutManager.IsReserved(_key.Key, _key.Modifiers))
            {
                error = StringResourceProvider.Instance.KeyboardShortcutReserved;
            }
            else if (KeyboardShortcutManager.IsDuplicate(_key, _original))
            {
                error = StringResourceProvider.Instance.KeyboardShortcutInUse;
            }
            else
            {
                ok = true;
            }
        }

        ErrorMessage = error;
        HasError = !string.IsNullOrEmpty(error);
        OkEnabled = ok;
    }
}
