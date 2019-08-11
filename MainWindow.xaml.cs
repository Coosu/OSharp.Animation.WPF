using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OSharp.Animation.WPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _canvasHost = new StoryboardCanvasHost(MainCanvas);
            //_bitmaps.Add(new BitmapImage(new Uri("C:\\Program Files (x86)\\osu!\\Songs\\1241412 Ariabl'eyeS - Kegare naki Bara Juuji\\SB\\firefly2.png")));
            //_bitmaps.Add(new BitmapImage(new Uri("C:\\Program Files (x86)\\osu!\\Songs\\1241412 Ariabl'eyeS - Kegare naki Bara Juuji\\SB\\waht.png")));
            _bitmaps.Add(new BitmapImage(new Uri("D:\\游戏资料\\osu thing\\sb\\SB素材\\123.jpg")));

            Stopwatch sw = Stopwatch.StartNew();
            int r = 400;
            int deg = 5;
            double interval = 16.666667*3;
            for (int i = 0; i < 1200; i++)
            {
                var bitmap = _bitmaps[_rnd.Next(_bitmaps.Count)];
                var uii = new Image { Source = bitmap };

                var ele = _canvasHost.CreateElement(uii, Origins.Center, bitmap.Width, bitmap.Height);

                double i1 = i;
                ele.ApplyAnimation(k =>
                {
                    k.Blend(0 + interval * i1, 0 + interval * i1, BlendMode.Normal);
                    k.Color(Easing.Linear,
                        0 + interval * i1, 1000 + interval * i1,
                        (255, 255, 128), (128, 128,128));
                    k.Color(Easing.Linear,
                        1000 + interval * i1, 2000 + interval * i1,
                        (128, 128, 128), (0, 0, 0));

                    k.ScaleVec(Easing.Linear,
                        0 + interval * i1, 2000 + interval * i1,
                        (1, 1), (1.2, 1.2)
                    );
                    k.Move(Easing.Linear,
                        0 + interval * i1, 2000 + interval * i1,
                        (-107 + (Math.Sin(i1 / 30) + 1) / 2 * 854, 240),
                        (-107 + (Math.Sin(i1 / 30) + 1) / 2 * 854 + r * Math.Sin(i1 * deg / 180 * Math.PI), 240 + r * Math.Cos(i1 * deg / 180 * Math.PI))
                    );
                    //k.Rotate(0, 0, 2000 + _rnd.Next(300),
                    //    0,
                    //    Math.PI * 4 * _rnd.NextDouble() - Math.PI * 2);
                    //
                });

                _list.Add(ele);
            }


            //var bitmap = _bitmaps[_rnd.Next(_bitmaps.Count)];
            //var uii = new Image { Source = bitmap };

            //var ele = _canvasHost.CreateElement(uii, Origins.Center, bitmap.Width, bitmap.Height);

            //ele.ApplyAnimation(k =>
            //{
            //    k.ScaleVec(Easing.Linear,
            //        500, 2000,
            //        (1, 1), (1.2, 1.2)
            //    );
            //    k.Move(Easing.Linear,
            //        500, 2000,
            //        (320, 240),
            //        (320 + r * Math.Sin(0), 240 + r * Math.Cos(0))
            //    );
            //});

            //_list.Add(ele);
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        private Random _rnd = new Random();
        private StoryboardCanvasHost _canvasHost;
        private List<BitmapImage> _bitmaps = new List<BitmapImage>();
        private List<ImageObject> _list = new List<ImageObject>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //var group = image.RenderTransform as TransformGroup;
            //var children = group.Children;

            //var ui = new Rectangle()
            //{
            //    Width = 100,
            //    OriginHeight = 100,
            //    Fill = Brushes.Red,
            //    Stroke = Brushes.Black,
            //    StrokeThickness = 3
            //};
            //var ele = _canvasHost.CreateElement(uii, Origins.Center, bitmap.Width, bitmap.Height);

            //ele.ApplyAnimation(k =>
            //{
            //    k.Move(0, 0, 0, new Vector2<double>(320, 240), new Vector2<double>(320, 240));
            //    k.Rotate(0, 0, 10000, 0, Math.PI * 10);
            //});
            foreach (var imageObject in _list)
            {
                imageObject.Reset();
            }

            //foreach (var imageObject in _list)
            //{
            //    imageObject.BeginAnimation();
            //}
            _canvasHost.PlayWhole();
        }
    }
}
