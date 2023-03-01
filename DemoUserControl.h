#pragma once

#include "DemoUserControl.g.h"

namespace winrt::Xaml_Demo::implementation
{
    struct DemoUserControl : DemoUserControlT<DemoUserControl>
    {
        DemoUserControl() 
        {
            // Xaml objects should not call InitializeComponent during construction.
            // See https://github.com/microsoft/cppwinrt/tree/master/nuget#initializecomponent
        }

        void myButton_Click(Windows::Foundation::IInspectable const& sender, Microsoft::UI::Xaml::RoutedEventArgs const& args);
        void HandleSwapDataTemplates();
    };
}

namespace winrt::Xaml_Demo::factory_implementation
{
    struct DemoUserControl : DemoUserControlT<DemoUserControl, implementation::DemoUserControl>
    {
    };
}
