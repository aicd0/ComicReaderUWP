// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Expression;
using ComicReaderUWP.Common.Expression.Filter;
using ComicReaderUWP.Common.Expression.Filter.Sql;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Tables;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.Views.Dialogs.EditFilter;

internal partial class EditFilterDialogViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public MutableLiveData<string> NameLiveData = new();
    public MutableLiveData<string> ExpressionLiveData = new();
    public MutableLiveData<string> ParseResultLiveData = new();
    public MutableLiveData<bool> SaveEnableLiveData = new();
    public MutableLiveData<bool> SaveAsNewEnableLiveData = new();
    public MutableLiveData<ExpressionTokenInfo> AppendToExpressionLiveData = new();

    private ComicFilterModel.ExternalModel? _filterModel;
    private ComicFilterModel.ExternalFilterModel? _filter;
    private string? _name;
    private string? _expression;
    private bool _isNameValid = false;
    private bool _isNameExists = false;
    private bool _isExpressionValid = false;

    private ObservableCollection<TagViewModel> _expressionButtons = [];
    public ObservableCollection<TagViewModel> ExpressionButtons
    {
        get => _expressionButtons;
        set
        {
            _expressionButtons = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpressionButtons)));
        }
    }

    private bool _includeHiddenComics = false;
    public bool IncludeHiddenComics
    {
        get => _includeHiddenComics;
        set
        {
            _includeHiddenComics = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IncludeHiddenComics)));
        }
    }

    private bool _saveViewSettings = false;
    public bool SaveViewSettings
    {
        get => _saveViewSettings;
        set
        {
            _saveViewSettings = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveViewSettings)));
        }
    }

    private bool _saveSortingAndGroupingSettings = true;
    public bool SaveSortingAndGroupingSettings
    {
        get => _saveSortingAndGroupingSettings;
        set
        {
            _saveSortingAndGroupingSettings = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveSortingAndGroupingSettings)));
        }
    }

    public void Initialize(ComicFilterModel.ExternalFilterModel filter)
    {
        CoroutineUtils.Run(() => InitializeAsync(filter));
    }

    public void UpdateName(string name)
    {
        name = name.Trim();
        if (name == _name)
        {
            return;
        }
        _name = name;

        _isNameValid = name.Length > 0;
        _isNameExists = FindFilter(name) != null;
        UpdateButtonStates();
    }

    public void UpdateExpression(string expression)
    {
        if (expression == _expression)
        {
            return;
        }

        _expression = expression;

        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter == null)
        {
            return;
        }

        filter.Expression = expression;
        UpdateSQLQuery();
    }

    public void SetIncludeHiddenComics(bool includeHiddenComics)
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter is null)
        {
            return;
        }

        filter.IncludeHiddenComics = includeHiddenComics;
        UpdateSQLQuery();
    }

    public void SetSaveViewSettings(bool save)
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter is null)
        {
            return;
        }

        filter.SaveViewSettings = save;
    }

    public void SetSaveSortingAndGroupingSettings(bool save)
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter is null)
        {
            return;
        }

        filter.SaveSortingAndGroupingSettings = save;
    }

    public void Save()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter is null)
        {
            return;
        }

        RemoveFilter(filter.Name);
        filter.Name = _name ?? "";
        filter.Modified = false;
        OverwriteFilter(filter);

        ComicFilterModel.ExternalModel? model = _filterModel;
        if (model is null)
        {
            return;
        }

        model.LastFilter = filter.Clone();
        ComicFilterModel.Instance.UpdateModel(model);
    }

    public void SaveAsNew()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter is null)
        {
            return;
        }

        filter = filter.Clone();
        filter.Name = _name ?? "";
        filter.Modified = false;
        OverwriteFilter(filter);

        ComicFilterModel.ExternalModel? model = _filterModel;
        if (model is null)
        {
            return;
        }

        model.LastFilter = filter.Clone();
        ComicFilterModel.Instance.UpdateModel(model);
    }

    public void Delete()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter is null)
        {
            return;
        }

        RemoveFilter(filter.Name);

        ComicFilterModel.ExternalModel? model = _filterModel;
        if (model is null)
        {
            return;
        }

        model.LastFilter = null;
        ComicFilterModel.Instance.UpdateModel(model);
    }

    private async Task InitializeAsync(ComicFilterModel.ExternalFilterModel filter)
    {
        {
            void OnClickButton(string text, int cursorPosition = -1)
            {
                var info = new ExpressionTokenInfo()
                {
                    Text = text,
                    CursorPosition = cursorPosition
                };

                AppendToExpressionLiveData.Emit(info);
            }

            ObservableCollection<TagViewModel> buttons = [];
            buttons.Add(new() { Tag = StringResourceProvider.Instance.ExpressionAnd, OnClicked = () => OnClickButton("and ") });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.ExpressionOr, OnClicked = () => OnClickButton("or ") });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.ExpressionNot, OnClicked = () => OnClickButton("not ") });
            buttons.Add(new() { Tag = "=", OnClicked = () => OnClickButton("= ") });
            buttons.Add(new() { Tag = ">", OnClicked = () => OnClickButton("> ") });
            buttons.Add(new() { Tag = "<", OnClicked = () => OnClickButton("< ") });
            buttons.Add(new() { Tag = ">=", OnClicked = () => OnClickButton(">= ") });
            buttons.Add(new() { Tag = "<=", OnClicked = () => OnClickButton("<= ") });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.ExpressionIn, OnClicked = () => OnClickButton("in ()", -2) });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.Title, OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_TITLE}") });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.Rating, OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_RATING}") });

            foreach (CompletionStatusEnum status in CompletionStatusService.AllStatus)
            {
                buttons.Add(new()
                {
                    Tag = CompletionStatusService.EnumToString(status),
                    OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_COMPLETION_STATUS} = {(int)status}")
                });
            }

            buttons.Add(new() { Tag = StringResourceProvider.Instance.Progress, OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_PROGRESS}") });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.Title1, OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_TITLE1}") });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.Title2, OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_TITLE2}") });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.PageCount, OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_PAGE_COUNT}") });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.Tag, OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_TAG}") });
            buttons.Add(new() { Tag = StringResourceProvider.Instance.Hidden, OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_HIDDEN}") });

            List<string> tagCategories = await ComicModel.GetAllTagCategories();
            foreach (string category in tagCategories)
            {
                buttons.Add(new()
                {
                    Tag = $"{StringResourceProvider.Instance.Tag}.{category}",
                    OnClicked = () => OnClickButton($"%{ComicSQLProviderUtils.VAR_TAG}.\"{ExpressionUtils.EscapeString(category)}\""),
                });
            }

            ExpressionButtons = buttons;
        }

        ComicFilterModel.ExternalModel? filterModel = ComicFilterModel.Instance.GetModel();
        _filterModel = filterModel;
        _filter = filter;
        if (filter is not null)
        {
            UpdateName(filter.Name);
            NameLiveData.Emit(filter.Name);
            UpdateExpression(filter.Expression);
            ExpressionLiveData.Emit(filter.Expression);
            IncludeHiddenComics = filter.IncludeHiddenComics;
            SaveViewSettings = filter.SaveViewSettings;
            SaveSortingAndGroupingSettings = filter.SaveSortingAndGroupingSettings;
        }
    }

    private void UpdateSQLQuery()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filter;
        if (filter is null)
        {
            return;
        }

        void OnInvalidExpression(string message)
        {
            _isExpressionValid = false;
            string hintMessage = StringResourceProvider.Instance.ExpressionInvalid.Replace("$reason", message);
            ParseResultLiveData.Emit(hintMessage);
            UpdateButtonStates();
        }

        ExpressionToken token;
        try
        {
            token = ExpressionParser.ParseFilter(filter.Expression, filter.IncludeHiddenComics);
        }
        catch (ExpressionException e)
        {
            OnInvalidExpression(e.Message);
            return;
        }

        ICondition condition;
        try
        {
            condition = SQLGenerator.CreateQuery(token, new ComicFilterSQLProvider());
        }
        catch (ExpressionException e)
        {
            OnInvalidExpression(e.Message);
            return;
        }

        var command = SelectCommand.Create(ComicTable.Instance);
        command.PutQueryInt64(ComicTable.ColumnId);
        command.AppendCondition(condition);

        _isExpressionValid = true;
        string hintMessage = StringResourceProvider.Instance.ExpressionValid.Replace("$query", command.ToString());
        ParseResultLiveData.Emit(hintMessage);
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool isInputValid = _isNameValid && _isExpressionValid;
        SaveEnableLiveData.Emit(isInputValid);
        SaveAsNewEnableLiveData.Emit(isInputValid && !_isNameExists);
    }

    private ComicFilterModel.ExternalFilterModel? FindFilter(string name)
    {
        ComicFilterModel.ExternalModel? filterModel = _filterModel;
        if (filterModel is null)
        {
            return null;
        }

        return filterModel.Filters.Find(x => x.Name == name);
    }

    private void OverwriteFilter(ComicFilterModel.ExternalFilterModel filter)
    {
        ComicFilterModel.ExternalModel? filterModel = _filterModel;
        if (filterModel is null)
        {
            return;
        }

        RemoveFilter(filter.Name);
        filterModel.Filters.Add(filter);
    }

    private void RemoveFilter(string name)
    {
        ComicFilterModel.ExternalModel? filterModel = _filterModel;
        if (filterModel is null)
        {
            return;
        }

        ComicFilterModel.ExternalFilterModel? oldFilter = filterModel.Filters.Find(x => x.Name == name);
        if (oldFilter is not null)
        {
            filterModel.Filters.Remove(oldFilter);
        }
    }

    public struct ExpressionTokenInfo
    {
        public string Text;
        public int CursorPosition;
    }
}
