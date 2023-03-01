#include "pch.h"
#include "DemoUserControl.h"
#if __has_include("DemoUserControl.g.cpp")
#include "DemoUserControl.g.cpp"
#endif

using namespace winrt;
using namespace winrt::Windows::Foundation;
using namespace Microsoft::UI;
using namespace Microsoft::UI::Xaml;
using namespace Microsoft::UI::Xaml::Controls;
using namespace Microsoft::UI::Xaml::Interop;
using namespace Microsoft::UI::Xaml::Markup;
using namespace Microsoft::UI::Xaml::Media;

namespace winrt::Xaml_Demo::implementation
{
    void DemoUserControl::HandleSwapDataTemplates()
    {
        auto vsGroups = VisualStateManager::GetVisualStateGroups(RootGrid());
        for (auto group : vsGroups)
        {
            if (auto currentState = group.CurrentState()) {

                if (currentState.Name() == StringTemplate1().Name())
                {
                    VisualStateManager::GoToState(*this, AllRed().Name(), false);
                }
                else if (currentState.Name() == AllRed().Name())
                {
                    VisualStateManager::GoToState(*this, CustomTemplate().Name(), false);
                }
                else
                {
                    VisualStateManager::GoToState(*this, StringTemplate1().Name(), false);
                }
            }
            else
            {
                VisualStateManager::GoToState(*this, CustomTemplate().Name(), false);
            }
        }
    }


    void DemoUserControl::myButton_Click(IInspectable const& sender, RoutedEventArgs const&)
    {
        if (auto button = sender.try_as<Button>())
        {
            auto buttonName = button.Name();
            if (buttonName == L"swapDataTemplates")
            {
                HandleSwapDataTemplates();
            }
            else if (buttonName == L"myButton3")
            {
                if (auto dataString = button.DataContext().try_as<hstring>())
                {
                    try
                    {
                        if (auto conversion = XamlBindingHelper::ConvertValue(xaml_typename<SolidColorBrush>(), box_value(dataString.value())))
                        {
                            //XamlBindingHelper::SetPropertyFromString(button, Control::BackgroundProperty(), box_value(conversion));
                            button.SetValue(Control::BackgroundProperty(), conversion);
                        }
                    }
                    catch (...)
                    {
                        button.SetValue(Control::BorderBrushProperty(), box_value(SolidColorBrush(Colors::Red())));
                        button.SetValue(Control::BorderThicknessProperty(), box_value(Thickness{ 1.0,2.0,3.0,4.0 }));
                    }
                }
            }
        }
    }
}
