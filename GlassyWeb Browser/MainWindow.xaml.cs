using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using Windows.Graphics;
namespace GlassyWeb_Browser
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            if (AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
                this.AppWindow.TitleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
                this.AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                this.AppWindow.TitleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                this.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            }

            SetBackground();
            RootGrid.Loaded += (sender, args) =>
            {
                SetupCustomDragRegions();
            };
        }

        private void SetupCustomDragRegions()
        {
            if (this.Content == null || this.Content.XamlRoot == null) return;
            if (!AppWindowTitleBar.IsCustomizationSupported() || !AppWindow.TitleBar.ExtendsContentIntoTitleBar) return;

            double scale = this.Content.XamlRoot.RasterizationScale;
            double windowWidth = this.Bounds.Width;
            if (windowWidth <= 0 && this.Content is FrameworkElement rootElement)
            {
                windowWidth = rootElement.ActualWidth;
            }
            if (windowWidth <= 0) return;

            int titleBarHeight = (int)(40 * scale);
            int windowWidthInPixels = (int)(windowWidth * scale);

            List<RectInt32> dragRects = new List<RectInt32>();

            RectInt32? GetElementRect(FrameworkElement element)
            {
                if (element == null || element.ActualWidth <= 0) return null;

                var transform = element.TransformToVisual(RootGrid);
                var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));

                return new RectInt32
                {
                    X = (int)(bounds.X * scale),
                    Y = (int)(bounds.Y * scale),
                    Width = (int)(bounds.Width * scale),
                    Height = (int)(bounds.Height * scale)
                };
            }

            var logoRect = GetElementRect(TitleBarLogo);
            var buttonsRect = GetElementRect(TitleBarWindowControls);

            int leftBoundary = logoRect.HasValue ? logoRect.Value.X + logoRect.Value.Width + (int)(5 * scale) : (int)(5 * scale);

            int safeRightBuffer = (int)(180 * scale);
            int rightBoundary = windowWidthInPixels - safeRightBuffer;

            if (buttonsRect.HasValue && buttonsRect.Value.X > 0)
            {
                rightBoundary = Math.Min(rightBoundary, buttonsRect.Value.X - (int)(5 * scale));
            }

            /* ---------------------------------------------------
            int tabsStartX = tabsRect.HasValue ? tabsRect.Value.X : leftBoundary;
            int tabsEndX = tabsRect.HasValue ? (tabsRect.Value.X + tabsRect.Value.Width) : leftBoundary;
            --------------------------------------------------- */

            if (rightBoundary > leftBoundary)
            {
                dragRects.Add(new RectInt32
                {
                    X = leftBoundary,
                    Y = 0,
                    Width = rightBoundary - leftBoundary,
                    Height = titleBarHeight
                });
            }

            /* ---------------------------------------------------
            if (tabsStartX > leftBoundary)
            {
                dragRects.Add(new RectInt32
                {
                    X = leftBoundary,
                    Y = 0,
                    Width = tabsStartX - leftBoundary,
                    Height = titleBarHeight
                });
            }

            if (rightBoundary > tabsEndX)
            {
                dragRects.Add(new RectInt32
                {
                    X = tabsEndX,
                    Y = 0,
                    Width = rightBoundary - tabsEndX,
                    Height = titleBarHeight
                });
            }
            --------------------------------------------------- */

            if (logoRect.HasValue)
            {
                dragRects.Add(new RectInt32
                {
                    X = logoRect.Value.X,
                    Y = 0,
                    Width = logoRect.Value.Width,
                    Height = titleBarHeight
                });
            }

            if (dragRects.Count > 0)
            {
                AppWindow.TitleBar.SetDragRectangles(dragRects.ToArray());
            }
        }

        private void SetBackground()
        {
            try
            {
                var uri = new Uri("ms-appx:///Assets/StandardBackground.webp");
                var bitmap = new BitmapImage(uri);

                var brush = new ImageBrush
                {
                    ImageSource = bitmap,
                    Stretch = Stretch.UniformToFill
                };

                RootGrid.Background = brush;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error while trying to load background: {ex.Message}");
            }
        }
        private void BackgroundImage_ImageFailed(object sender, Microsoft.UI.Xaml.ExceptionRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[BACKGROUND] Error while trying to load background: {e.ErrorMessage}");

            if (sender is Microsoft.UI.Xaml.Controls.Image img && img.Parent is Grid parentGrid)
            {
                parentGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed);
            }
        }

        private void BackgroundImage_ImageOpened(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[BACKGROUND] Background image loaded and rendered successfully!");
        }
        private void GlassPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement container)
            {
                var blurHost = FindChild<Canvas>(container);
                var border = FindChild<Border>(container);

                float cornerRadius = 40.0f;
                if (border != null)
                {
                    cornerRadius = (float)border.CornerRadius.TopLeft;
                }

                if (blurHost != null)
                {
                    SetupGlassPanel(blurHost, container, cornerRadius);
                }
            }
        }

        private void SetupGlassPanel(Canvas blurHost, FrameworkElement container, float cornerRadiusValue)
        {
            var visual = ElementCompositionPreview.GetElementVisual(blurHost);
            var compositor = visual.Compositor;

            var backdropBrush = compositor.CreateBackdropBrush();

            var blurEffect = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 15.0f,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("background")
            };

            var effectFactory = compositor.CreateEffectFactory(blurEffect);
            var effectBrush = effectFactory.CreateBrush();
            effectBrush.SetSourceParameter("background", backdropBrush);

            var spriteVisual = compositor.CreateSpriteVisual();
            spriteVisual.Brush = effectBrush;

            var clip = compositor.CreateRectangleClip();
            var radius = new System.Numerics.Vector2(cornerRadiusValue, cornerRadiusValue);
            clip.TopLeftRadius = radius;
            clip.TopRightRadius = radius;
            clip.BottomLeftRadius = radius;
            clip.BottomRightRadius = radius;
            spriteVisual.Clip = clip;

            var bindSizeAnimation = compositor.CreateExpressionAnimation("visual.Size");
            bindSizeAnimation.SetReferenceParameter("visual", visual);
            spriteVisual.StartAnimation("Size", bindSizeAnimation);

            var bindRightAnimation = compositor.CreateExpressionAnimation("visual.Size.X");
            bindRightAnimation.SetReferenceParameter("visual", visual);
            clip.StartAnimation("Right", bindRightAnimation);

            var bindBottomAnimation = compositor.CreateExpressionAnimation("visual.Size.Y");
            bindBottomAnimation.SetReferenceParameter("visual", visual);
            clip.StartAnimation("Bottom", bindBottomAnimation);

            ElementCompositionPreview.SetElementChildVisual(blurHost, spriteVisual);
        }

        private T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }
                var result = FindChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
