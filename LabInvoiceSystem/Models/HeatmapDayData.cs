using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LabInvoiceSystem.Models
{
    public partial class HeatmapDayData : ObservableObject
    {
        [ObservableProperty]
        private DateTime _date;

        [ObservableProperty]
        private decimal _amount;

        [ObservableProperty]
        private int _level; // 0-4: 5个颜色级别 (0=无数据, 1=很少, 2=较少, 3=中等, 4=较多)

        [ObservableProperty]
        private string _colorHex = "#F1F5F9"; // 默认最浅色 (Slate100)
    }
}
