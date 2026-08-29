using System;

using Microsoft.Maui.Controls;

using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Controls;

public partial class PageInformationButton
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title),
        typeof(string),
        typeof(PageInformationButton),
        string.Empty);

    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message),
        typeof(string),
        typeof(PageInformationButton),
        string.Empty);

    public PageInformationButton()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private async void OnClicked(object? sender, EventArgs e)
    {
        await Shell.Current!.DisplayAlertAsync(Title, Message, MobileTexts.ButtonOk);
    }
}
