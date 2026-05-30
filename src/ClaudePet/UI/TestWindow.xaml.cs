using System.Windows;
using System.Windows.Media.Imaging;

namespace ClaudePet.UI;

public partial class TestWindow : Window
{
    public TestWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 1秒后截屏
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                int x = (int)Math.Max(0, Left);
                int y = (int)Math.Max(0, Top);
                int w = (int)ActualWidth;
                int h = (int)ActualHeight;
                using var bmp = new System.Drawing.Bitmap(w, h);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
                bmp.Save(@"D:\dev\claude-pet\screenshot.png", System.Drawing.Imaging.ImageFormat.Png);
            }
            catch { }
        };
        timer.Start();
    }
}
