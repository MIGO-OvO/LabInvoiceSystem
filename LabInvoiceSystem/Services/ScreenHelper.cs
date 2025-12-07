using System;

namespace LabInvoiceSystem.Services
{
    /// <summary>
    /// 屏幕辅助服务类，处理分辨率检测、有效分辨率计算和窗口尺寸算法
    /// 优化版本：确保窗口尺寸永远不超出屏幕有效分辨率
    /// </summary>
    public static class ScreenHelper
    {
        // 设计基准尺寸（理想情况下的窗口尺寸）
        public const double DesignWidth = 1280;
        public const double DesignHeight = 960;

        // 绝对最小可用尺寸（保证UI基本可用性）
        public const double AbsoluteMinWidth = 640;
        public const double AbsoluteMinHeight = 480;

        // 推荐最小尺寸（保证UI良好体验）
        public const double RecommendedMinWidth = 800;
        public const double RecommendedMinHeight = 600;

        // 屏幕边距比例（使用85%屏幕空间，留出任务栏等系统UI空间）
        public const double ScreenMarginRatio = 0.85;

        // 设计宽高比 (4:3)
        public const double DesignAspectRatio = DesignWidth / DesignHeight;

        /// <summary>
        /// 计算有效分辨率（考虑DPI缩放）
        /// </summary>
        /// <param name="physicalWidth">屏幕物理宽度（像素）</param>
        /// <param name="physicalHeight">屏幕物理高度（像素）</param>
        /// <param name="scaling">DPI缩放比例（1.0 = 100%, 1.25 = 125%等）</param>
        /// <returns>有效分辨率（宽度，高度）</returns>
        public static (double width, double height) GetEffectiveResolution(
            double physicalWidth,
            double physicalHeight,
            double scaling)
        {
            // 确保缩放比例有效
            if (scaling <= 0)
                scaling = 1.0;

            var effectiveWidth = physicalWidth / scaling;
            var effectiveHeight = physicalHeight / scaling;

            return (effectiveWidth, effectiveHeight);
        }

        /// <summary>
        /// 获取适合当前屏幕的最优窗口尺寸
        /// 确保窗口尺寸永远不超出屏幕有效分辨率
        /// </summary>
        /// <param name="physicalWidth">屏幕物理宽度（像素）</param>
        /// <param name="physicalHeight">屏幕物理高度（像素）</param>
        /// <param name="scaling">DPI缩放比例</param>
        /// <returns>最优窗口尺寸（宽度，高度）</returns>
        public static (double width, double height) GetOptimalWindowSize(
            double physicalWidth,
            double physicalHeight,
            double scaling)
        {
            var (effectiveWidth, effectiveHeight) = GetEffectiveResolution(
                physicalWidth, physicalHeight, scaling);

            // 计算可用屏幕空间（85%边距，为任务栏等留出空间）
            var availableWidth = effectiveWidth * ScreenMarginRatio;
            var availableHeight = effectiveHeight * ScreenMarginRatio;

            // 确保可用空间不小于绝对最小值
            availableWidth = Math.Max(availableWidth, AbsoluteMinWidth);
            availableHeight = Math.Max(availableHeight, AbsoluteMinHeight);

            double windowWidth, windowHeight;

            // 如果可用空间大于等于设计尺寸，使用设计尺寸
            if (availableWidth >= DesignWidth && availableHeight >= DesignHeight)
            {
                windowWidth = DesignWidth;
                windowHeight = DesignHeight;
            }
            else
            {
                // 否则，按比例缩小窗口尺寸以适应屏幕
                // 计算按宽度限制时的尺寸
                var widthLimitedHeight = availableWidth / DesignAspectRatio;
                // 计算按高度限制时的尺寸
                var heightLimitedWidth = availableHeight * DesignAspectRatio;

                if (widthLimitedHeight <= availableHeight)
                {
                    // 宽度是限制因素
                    windowWidth = availableWidth;
                    windowHeight = widthLimitedHeight;
                }
                else
                {
                    // 高度是限制因素
                    windowWidth = heightLimitedWidth;
                    windowHeight = availableHeight;
                }
            }

            // 最终确保窗口尺寸不超过可用空间
            windowWidth = Math.Min(windowWidth, availableWidth);
            windowHeight = Math.Min(windowHeight, availableHeight);

            // 确保不小于绝对最小尺寸
            windowWidth = Math.Max(windowWidth, AbsoluteMinWidth);
            windowHeight = Math.Max(windowHeight, AbsoluteMinHeight);

            return (Math.Round(windowWidth), Math.Round(windowHeight));
        }

        /// <summary>
        /// 获取动态最小窗口尺寸
        /// 根据屏幕有效分辨率动态调整最小尺寸
        /// </summary>
        /// <param name="physicalWidth">屏幕物理宽度（像素）</param>
        /// <param name="physicalHeight">屏幕物理高度（像素）</param>
        /// <param name="scaling">DPI缩放比例</param>
        /// <returns>最小窗口尺寸（宽度，高度）</returns>
        public static (double minWidth, double minHeight) GetMinWindowSize(
            double physicalWidth,
            double physicalHeight,
            double scaling)
        {
            var (effectiveWidth, effectiveHeight) = GetEffectiveResolution(
                physicalWidth, physicalHeight, scaling);
            var (optimalWidth, optimalHeight) = GetOptimalWindowSize(
                physicalWidth, physicalHeight, scaling);

            // 计算可用屏幕空间
            var availableWidth = effectiveWidth * ScreenMarginRatio;
            var availableHeight = effectiveHeight * ScreenMarginRatio;

            // 最小尺寸为最优尺寸的60%
            var minWidth = optimalWidth * 0.6;
            var minHeight = optimalHeight * 0.6;

            // 确保最小尺寸不小于绝对最小值
            minWidth = Math.Max(minWidth, AbsoluteMinWidth);
            minHeight = Math.Max(minHeight, AbsoluteMinHeight);

            // 确保最小尺寸不大于可用空间
            minWidth = Math.Min(minWidth, availableWidth);
            minHeight = Math.Min(minHeight, availableHeight);

            // 确保最小尺寸不大于最优尺寸
            minWidth = Math.Min(minWidth, optimalWidth);
            minHeight = Math.Min(minHeight, optimalHeight);

            return (Math.Round(minWidth), Math.Round(minHeight));
        }

        /// <summary>
        /// 获取窗口最大尺寸（不超过屏幕有效分辨率）
        /// </summary>
        /// <param name="physicalWidth">屏幕物理宽度（像素）</param>
        /// <param name="physicalHeight">屏幕物理高度（像素）</param>
        /// <param name="scaling">DPI缩放比例</param>
        /// <returns>最大窗口尺寸（宽度，高度）</returns>
        public static (double maxWidth, double maxHeight) GetMaxWindowSize(
            double physicalWidth,
            double physicalHeight,
            double scaling)
        {
            var (effectiveWidth, effectiveHeight) = GetEffectiveResolution(
                physicalWidth, physicalHeight, scaling);

            // 最大尺寸为有效分辨率的95%（留出少量边距）
            var maxWidth = effectiveWidth * 0.95;
            var maxHeight = effectiveHeight * 0.95;

            return (Math.Round(maxWidth), Math.Round(maxHeight));
        }

        /// <summary>
        /// 根据屏幕DPI缩放比例计算PDF渲染DPI
        /// </summary>
        /// <param name="dpiScale">屏幕DPI缩放比例（1.0到3.0之间）</param>
        /// <returns>PDF渲染DPI值</returns>
        public static int CalculatePdfRenderDpi(double dpiScale)
        {
            // 基准DPI为150
            const int baseDpi = 150;

            // 确保缩放比例在有效范围内
            dpiScale = Math.Clamp(dpiScale, 1.0, 3.0);

            // 计算目标DPI，确保不小于基准DPI
            var targetDpi = (int)(baseDpi * dpiScale);
            return Math.Max(targetDpi, baseDpi);
        }

        /// <summary>
        /// 计算UI缩放因子
        /// 用于在窗口尺寸小于设计尺寸时缩放UI元素
        /// </summary>
        /// <param name="actualWidth">实际窗口宽度</param>
        /// <param name="actualHeight">实际窗口高度</param>
        /// <returns>缩放因子（0.5-1.0范围内）</returns>
        public static double CalculateUIScaleFactor(double actualWidth, double actualHeight)
        {
            // 计算宽度和高度的缩放比例
            var scaleX = actualWidth / DesignWidth;
            var scaleY = actualHeight / DesignHeight;

            // 取较小值以确保内容完全可见
            var scale = Math.Min(scaleX, scaleY);

            // 限制缩放范围在0.5到1.0之间
            return Math.Clamp(scale, 0.5, 1.0);
        }

        /// <summary>
        /// 判断当前屏幕是否为小屏幕（需要特殊处理）
        /// </summary>
        /// <param name="physicalWidth">屏幕物理宽度（像素）</param>
        /// <param name="physicalHeight">屏幕物理高度（像素）</param>
        /// <param name="scaling">DPI缩放比例</param>
        /// <returns>是否为小屏幕</returns>
        public static bool IsSmallScreen(double physicalWidth, double physicalHeight, double scaling)
        {
            var (effectiveWidth, effectiveHeight) = GetEffectiveResolution(
                physicalWidth, physicalHeight, scaling);

            // 如果有效分辨率小于推荐最小尺寸的1.2倍，认为是小屏幕
            return effectiveWidth < RecommendedMinWidth * 1.2 || 
                   effectiveHeight < RecommendedMinHeight * 1.2;
        }
    }
}
