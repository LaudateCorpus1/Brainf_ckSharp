﻿using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Brainf_ck_sharp_UWP.Helpers.CodeFormatting;
using Brainf_ck_sharp_UWP.Helpers.Extensions;
using Brainf_ck_sharp_UWP.Helpers.UI;
using Brainf_ckSharp.Legacy.ReturnTypes;

namespace Brainf_ck_sharp_UWP.UserControls.CustomControls
{
    public sealed partial class ErrorCharacterDisplayControl : UserControl
    {
        public ErrorCharacterDisplayControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Gets or sets the information to display in the control
        /// </summary>
        public InterpreterExceptionInfo ExceptionInfo
        {
            get => (InterpreterExceptionInfo)GetValue(ExceptionInfoProperty);
            set => SetValue(ExceptionInfoProperty, value);
        }

        public static readonly DependencyProperty ExceptionInfoProperty = DependencyProperty.Register(
            nameof(ExceptionInfo), typeof(InterpreterExceptionInfo), typeof(ErrorCharacterDisplayControl), 
            new PropertyMetadata(default(InterpreterExceptionInfo), OnExceptionInfoPropertyChanged));

        private static void OnExceptionInfoPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ErrorCharacterDisplayControl @this = d.To<ErrorCharacterDisplayControl>();
            if (e.NewValue is InterpreterExceptionInfo info)
            {
                @this.OperatorBlock.Text = info.FaultedOperator.ToString();
                @this.OperatorBlock.Foreground = new SolidColorBrush(Brainf_ckFormatterHelper.Instance.GetSyntaxHighlightColorFromChar(info.FaultedOperator));
                @this.PositionBlock.Text = $"{LocalizationManager.GetResource("Position")} {info.ErrorPosition}";
            }
        }
    }
}