#include "pch.h"
#include "AnimatedColorBrush.h"
#include "AnimatedColorBrush.g.cpp"

using namespace winrt::Microsoft::UI::Composition;
using namespace winrt::Windows::Foundation;
using namespace winrt::Microsoft::UI;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Media;

namespace winrt::Xaml_Demo::implementation
{
    Microsoft::UI::Xaml::DependencyProperty AnimatedColorBrush::m_colorProperty{ nullptr };

    void AnimatedColorBrush::OnColorPropertyChanged(Microsoft::UI::Xaml::DependencyObject const& sender,
                                                    Microsoft::UI::Xaml::DependencyPropertyChangedEventArgs const& args)
    {
        // create composition brush if necessary and animate over 1 second from previous color to new color
        if (auto color = sender.try_as<AnimatedColorBrush>())
        {
            auto oldColor = winrt::unbox_value<winrt::Windows::UI::Color>(args.OldValue());
            auto newColor = winrt::unbox_value<winrt::Windows::UI::Color>(args.NewValue());

            auto compositionBrush = color->CompositionBrush();
            if (compositionBrush)
            {
                // animate the existing composition brush
                Compositor compositor = CompositionTarget::GetCompositorForCurrentThread();
                auto animation = compositor.CreateColorKeyFrameAnimation();
                animation.InsertKeyFrame(1.0f, newColor);
                // one second animation
                animation.Duration(std::chrono::milliseconds(1000));
                color->m_animation = animation;
            }
        }
    }

    void AnimatedColorBrush::OnConnected()
    {
        Compositor compositor = CompositionTarget::GetCompositorForCurrentThread();
        if (m_animation == nullptr)
        {
            m_animation = compositor.CreateColorKeyFrameAnimation();
            m_animation.Duration(std::chrono::milliseconds(1000));
            m_animation.InsertKeyFrame(1.0f, Color());
            m_animation.InsertKeyFrame(0.0f, Colors::Transparent());
        }

        if (CompositionBrush() == nullptr)
        {
            auto colorBrush = compositor.CreateColorBrush(Colors::Transparent());
            CompositionBrush(colorBrush);
        }

        // start animation
        CompositionBrush().StartAnimation(L"Color", m_animation);
    }

    void AnimatedColorBrush::OnDisconnected()
    {
        if (CompositionBrush())
        {
            CompositionBrush().Close();
            CompositionBrush(nullptr);
        }
    }
}
