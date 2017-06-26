﻿using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using UICompositionAnimations.Behaviours;

namespace Brainf_ck_sharp_UWP.UserControls.Flyouts
{
    public sealed partial class LoadingPopupControl : UserControl
    {
        public LoadingPopupControl()
        {
            Loaded += LoadingPopupControl_Loaded;
            this.InitializeComponent();
            Unloaded += (s, e) =>
            {
                Win2DCanvas.RemoveFromVisualTree();
                Win2DCanvas = null;
            };
        }

        // Setup the effect
        private async void LoadingPopupControl_Loaded(object sender, RoutedEventArgs e)
        {
            await EffectBorder.AttachCompositionInAppCustomAcrylicEffectAsync(EffectBorder,
                6, 400, Colors.Black, 0.5f, 0.2f, Win2DCanvas, new Uri("ms-appx:///Assets/Misc/lightnoise.png"), disposeOnUnload: true);
        }
    }
}
