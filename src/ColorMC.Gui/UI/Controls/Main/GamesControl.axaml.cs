using Avalonia.Controls;
using Avalonia.Input;
using ColorMC.Gui.UI.Model.Main;
using ColorMC.Gui.Utils;
using System;

namespace ColorMC.Gui.UI.Controls.Main;

public partial class GamesControl : UserControl
{
    public GamesControl()
    {
        InitializeComponent();

        LayoutUpdated += GamesControl_LayoutUpdated;
        Expander_Head.ContentTransition = App.CrossFade300;

        AddHandler(DragDrop.DragEnterEvent, DragEnter);
        AddHandler(DragDrop.DragLeaveEvent, DragLeave);
        AddHandler(DragDrop.DropEvent, Drop);
    }

    private void DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Source is Control && DataContext is GamesModel model)
        {
            Grid1.IsVisible = model.DropIn(e.Data);
        }
    }

    private void DragLeave(object? sender, DragEventArgs e)
    {
        Grid1.IsVisible = false;
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        Grid1.IsVisible = false;
        if (e.Source is Control && DataContext is GamesModel model)
        {
            model.Drop(e.Data);
        }
    }

    private void GamesControl_LayoutUpdated(object? sender, EventArgs e)
    {
        Expander_Head.MakeTran();
    }
}
