﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static System.Windows.Controls.Canvas;

namespace OSharp.Animation.WPF
{
    public class StoryboardCanvasHost : IDisposable
    {
        internal static readonly Dictionary<ImageSource, Brush> BrushCache = new Dictionary<ImageSource, Brush>();

        internal readonly Canvas Canvas;
        protected readonly List<ImageObject> EleList = new List<ImageObject>();

        public StoryboardCanvasHost(Canvas canvas)
        {
            Canvas = canvas;
        }

        public virtual ImageObject CreateElement(Image ui,
            Origin<double> origin,
            double width,
            double height,
            int zIndex,
            double defaultX = 320,
            double defaultY = 240)
        {
            //Canvas.Children.Add(ui);

            Panel.SetZIndex(ui, zIndex);
            var ele = new ImageObject(ui, width, height, origin, defaultX, defaultY)
            {
                Host = this
            };

            //ele.Storyboard.Completed += (sender, e) => { Canvas.Children.Remove(ui); };
            EleList.Add(ele);
            return ele;
        }

        public virtual void PlayWhole()
        {
            var list = EleList.OrderBy(k => k.MinTime).ToList();
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
                    Application.Current?.Dispatcher?.BeginInvoke(new System.Action(() =>
                    {
                        list[index1].BeginAnimation();
                    }));
                    index++;
                }
            });
        }

        public StoryboardGroup CreateStoryboardGroup()
        {
            return new StoryboardGroup(Canvas);
        }

        public void Dispose()
        {
            Canvas.Children.Clear();
            foreach (var imageObject in EleList)
            {
                imageObject?.Dispose();
            }

            BrushCache.Clear();
        }
    }

    public class StoryboardGroup : StoryboardCanvasHost
    {
        public System.Windows.Media.Animation.Storyboard Storyboard;

        public StoryboardGroup(Canvas canvas) : base(canvas)
        {
            Storyboard = new System.Windows.Media.Animation.Storyboard();
        }

        public override ImageObject CreateElement(Image ui,
            Origin<double> origin,
            double width,
            double height,
             int zIndex,
            double defaultX = 320,
            double defaultY = 240)
        {
            Panel.SetZIndex(ui, zIndex);
            //Canvas.Children.Add(ui);
            var ele = new ImageObject(ui, width, height, origin, defaultX, defaultY, Storyboard)
            {
                Host = this
            };

            EleList.Add(ele);
            //Storyboard.Completed += (sender, e) => { ele.ClearObj(); };
            return ele;

        }

        public override void PlayWhole()
        {
            //Canvas.Children.Clear();
            Storyboard.Begin();
            var list = EleList.OrderBy(k => k.MinTime).ToList();
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
                    Application.Current?.Dispatcher?.BeginInvoke(new System.Action(() =>
                    {
                        list[index1].AddToCanvas();
                        Storyboard.Completed += (sender, e) => { list[index1].ClearObj(); };
                        //list[index1].BeginAnimation();
                    }));
                    index++;
                }
            });
        }
    }
}