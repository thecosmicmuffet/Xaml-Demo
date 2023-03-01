// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

#include "pch.h"
#include "App.xaml.h"
#include "MainWindow.xaml.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "DemoUserControl.h"

using namespace winrt;
using namespace Microsoft::UI::Xaml;
using namespace Microsoft::UI::Xaml::Controls;
using namespace Microsoft::UI::Xaml::Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace winrt::Xaml_Demo::implementation
{
    MainWindow::MainWindow()
    {
        InitializeComponent();
    }

    void MainWindow::HandleSwapDataTemplates()
    {
        
            if (auto currentTemplate = DemoListView().ItemTemplate())
            {
                OutputTextBlock().Text(SomeButton().Name());
                // StringTemplate1().Name doesn't work
                if (StringTemplate1() == currentTemplate)
                {
                    DemoListView().ItemTemplate(StringTemplateRed());
                }
                else if (RootGrid().Resources().Lookup(box_value(L"StringTemplateRed")).try_as<DataTemplate>() == currentTemplate)
                {
                    DemoListView().ItemTemplate(RootGrid().Resources().Lookup(box_value(L"StringTemplateButton")).try_as<DataTemplate>());
                }
                else
                {
                    DemoListView().ItemTemplate(RootGrid().Resources().Lookup(box_value(L"StringTemplate1")).try_as<DataTemplate>());
                }
            }
    }

    void MainWindow::HandleGoToUserControl()
    {
        this->Content(make<DemoUserControl>());
    }

    void MainWindow::myButton_Click(IInspectable const& sender, RoutedEventArgs const&)
    {
        m_clickCount++;
        CounterButton().Content(box_value((L"count " + to_hstring(m_clickCount)).data()));
        if (auto button = sender.try_as<Button>())
        {
            auto buttonName = button.Name();
            if (buttonName == L"swapDataTemplates")
            {
                HandleSwapDataTemplates();
            }
            else if (sender == myButton2())
            {
                HandleGoToUserControl();
            }
            else if (sender == CounterButton())
            {
                // just count
            }
            else if (buttonName == L"myButton3")
            {
                //SolidColorBrush brush = App::Resources().Lookup;
                //button.Background(brush);
            }
        }
    }
}
