#pragma once
#include "pch.h"
#include "AnimatedColorBrush.g.h"

namespace winrt::Xaml_Demo::implementation
{
    struct AnimatedColorBrush : AnimatedColorBrushT<AnimatedColorBrush>
    {
        AnimatedColorBrush()
        {
            if (!m_colorProperty)
            {

                m_colorProperty = Microsoft::UI::Xaml::DependencyProperty::Register(
                    L"Color",
                    ::winrt::xaml_typename<winrt::Windows::UI::Color>(),
                    ::winrt::xaml_typename<Xaml_Demo::AnimatedColorBrush>(),
                    Microsoft::UI::Xaml::PropertyMetadata{ winrt::box_value(winrt::Windows::UI::Colors::Transparent()),
                                                           Microsoft::UI::Xaml::PropertyChangedCallback{ &OnColorPropertyChanged } });
            }
        }

        winrt::Windows::UI::Color Color() { return winrt::unbox_value<winrt::Windows::UI::Color>(GetValue(m_colorProperty)); }
        void Color(winrt::Windows::UI::Color const& value) { SetValue(m_colorProperty, winrt::box_value(value)); }

        static Microsoft::UI::Xaml::DependencyProperty ColorProperty() { return m_colorProperty; }

        // XamlCompositionBrushBase overrides
        void OnConnected();
        void OnDisconnected();

        static void OnColorPropertyChanged(Microsoft::UI::Xaml::DependencyObject const& sender,
                                           Microsoft::UI::Xaml::DependencyPropertyChangedEventArgs const& args);

    private:
        // Color Dependency property field
        static Microsoft::UI::Xaml::DependencyProperty m_colorProperty;
        
        winrt::Microsoft::UI::Composition::ColorKeyFrameAnimation m_animation{ nullptr };
    };
}
namespace winrt::Xaml_Demo::factory_implementation
{
    struct AnimatedColorBrush : AnimatedColorBrushT<AnimatedColorBrush, implementation::AnimatedColorBrush>
    {
    };
}
