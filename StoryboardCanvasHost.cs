using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OSharp.Animation.WPF
{
    public class StoryboardCanvasHost : IDisposable
    {
        internal static readonly Dictionary<ImageSource, Brush> BrushCache = new Dictionary<ImageSource, Brush>();

        internal readonly Canvas Canvas;
        private readonly List<ImageObject> _eleList = new List<ImageObject>();

        public StoryboardCanvasHost(Canvas canvas)
        {
            Canvas = canvas;
        }

        public ImageObject CreateElement(Image ui,
            Origin<double> origin,
            double width,
            double height,
            double defaultX = 320,
            double defaultY = 240)
        {
            //Canvas.Children.Add(ui);

            var ele = new ImageObject(ui, width, height, origin, defaultX, defaultY)
            {
                Host = this
            };

            //ele.Storyboard.Completed += (sender, e) => { Canvas.Children.Remove(ui); };
            _eleList.Add(ele);
            return ele;
        }

        public void PlayWhole()
        {
            var list = _eleList.OrderBy(k => k.MinTime).ToList();
            var sw = Stopwatch.StartNew();
            Task.Run(() =>
            {
                var index = 0;
                while (index < list.Count)
                {
                    while (sw.ElapsedMilliseconds < list[index].MinTime)
                    {
                        Thread.Sleep(1);
                    }

                    var index1 = index;
                    Application.Current?.Dispatcher?.BeginInvoke(new System.Action(() => { list[index1].BeginAnimation(); }));
                    index++;
                }
            });
        }

        public void Dispose()
        {
            Canvas.Children.Clear();
            foreach (var imageObject in _eleList)
            {
                imageObject?.Dispose();
            }
            BrushCache.Clear();
        }
    }
}