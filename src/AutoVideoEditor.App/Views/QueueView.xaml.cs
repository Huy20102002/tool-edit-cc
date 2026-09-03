using System.Windows;
using System.Windows.Controls;
using AutoVideoEditor.App.ViewModels;

namespace AutoVideoEditor.App.Views;

public partial class QueueView : UserControl
{
    private bool _autoScrollEnabled = true;

    public QueueView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is QueueViewModel vm)
        {
            vm.RequestScrollToLatest += () =>
            {
                _autoScrollEnabled = true;
                LogScrollViewer.ScrollToEnd();
            };
        }
    }

    private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange != 0)
        {
            if (_autoScrollEnabled)
            {
                LogScrollViewer.ScrollToEnd();
            }
        }
        else
        {
            // If user scrolled up manually, pause auto-scroll. If user scrolled all the way to bottom, resume auto-scroll.
            if (e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 15)
            {
                _autoScrollEnabled = true;
            }
            else if (e.VerticalChange < 0)
            {
                _autoScrollEnabled = false;
            }
        }
    }
}
