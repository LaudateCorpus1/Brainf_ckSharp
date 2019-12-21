﻿using System;
using Windows.UI.Xaml.Data;
using Brainf_ck_sharp.Legacy.UWP.DataModels.SQLite.Enums;
using Brainf_ck_sharp.Legacy.UWP.Helpers.Extensions;
using Brainf_ck_sharp.Legacy.UWP.Helpers.UI;

namespace Brainf_ck_sharp.Legacy.UWP.Converters
{
    /// <summary>
    /// A simple converter that returns a string that indicates the status for a desired saved code title
    /// </summary>
    public class SourceCodeTitleScoreToDisplayStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch (value.To<SourceCodeTitleScore>())
            {
                case SourceCodeTitleScore.Empty:
                    return LocalizationManager.GetResource("Empty");
                case SourceCodeTitleScore.AlreadyUsed:
                    return LocalizationManager.GetResource("AlreadyUsed");
                case SourceCodeTitleScore.Valid:
                    return LocalizationManager.GetResource("Available");
                case SourceCodeTitleScore.NotModified:
                    return LocalizationManager.GetResource("NotModified");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}