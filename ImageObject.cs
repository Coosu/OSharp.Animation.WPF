using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using SysAni = System.Windows.Media.Animation;

namespace OSharp.Animation.WPF
{
    public class ImageObject : TransformableObject<double>, IDisposable
    {
        private Image _image;
        private Rectangle _addImage;

        private readonly List<(DependencyProperty prop, AnimationTimeline animation)> _transformList =
            new List<(DependencyProperty, AnimationTimeline)>();

        internal readonly SysAni.Storyboard Storyboard;
        internal readonly List<SysAni.Storyboard> LoopList = new List<SysAni.Storyboard>();
        private AnimateStatus _blendStatus;
        private AnimateStatus _flipStatus;
        private readonly Queue<Vector2<double>> _blendDurationQueue = new Queue<Vector2<double>>();
        private DispatcherTimer _dispatcherT;
        private SolidColorBrush _coverBrush;
        private TransformGroup _group;
        private double _defaultX;
        private double _defaultY;

        private double PlayTime => Storyboard.GetCurrentTime().TotalMilliseconds;

        public Origin<double> Origin { get; }

        public double OriginHeight { get; }

        public double OriginWidth { get; }

        internal StoryboardCanvasHost Host { get; set; }

        internal ImageObject(Image image, double width, double height, Origin<double> origin, double defaultX, double defaultY)
        {
            _image = image;
            Origin = origin;
            _image.RenderTransformOrigin = new Point(origin.X, origin.Y);

            _image.RenderTransform = InitTransformGroup(width, height, defaultX, defaultY);


            Storyboard = new SysAni.Storyboard();
            //Storyboard.CurrentTimeInvalidated += Storyboard_CurrentTimeInvalidated;
            OriginWidth = width;
            OriginHeight = height;
        }

        //private void Storyboard_CurrentTimeInvalidated(object sender, EventArgs e)
        //{
        //    var o = sender as ClockGroup;
        //    PlayTime = (o?.CurrentTime ?? TimeSpan.Zero).TotalMilliseconds;
        //}

        private TransformGroup InitTransformGroup(double width, double height, double defaultX, double defaultY)
        {
            _defaultX = defaultX;
            _defaultY = defaultY;

            if (_group == null)
            {
                var scaleTransform = new ScaleTransform(1, 1);
                var rotateTransform = new RotateTransform(0);
                var flipTransform = new ScaleTransform(1, 1);
                var translateTransform = new TranslateTransform(defaultX - width * Origin.X, defaultY - height * Origin.Y);

                _group = new TransformGroup();
                _group.Children.Add(scaleTransform);
                _group.Children.Add(rotateTransform);
                _group.Children.Add(flipTransform);
                _group.Children.Add(translateTransform);
            }

            return _group;
        }

        public void BeginAnimation()
        {
            if (Storyboard.FillBehavior != FillBehavior.Stop)
            {
                Storyboard.Stop();
                if (_blendStatus == AnimateStatus.None)
                {
                    Host?.Canvas.Children.Remove(_image);
                }
                else if (_blendStatus == AnimateStatus.Static)
                {
                    Host?.Canvas.Children.Remove(_addImage);
                }
                else
                {
                    Host?.Canvas.Children.Remove(_image);
                    Host?.Canvas.Children.Remove(_addImage);
                }
            }

            if (_blendStatus == AnimateStatus.None)
            {
                Host?.Canvas.Children.Add(_image);
                Storyboard.Completed += (sender, e) => { Host?.Canvas.Children.Remove(_image); };
            }
            else if (_blendStatus == AnimateStatus.Static)
            {
                Host?.Canvas.Children.Add(_addImage);
                Storyboard.Completed += (sender, e) => { Host?.Canvas.Children.Remove(_addImage); };
            }
            else
            {
                Host?.Canvas.Children.Add(_image);
                Host?.Canvas.Children.Add(_addImage);
                Storyboard.Completed += (sender, e) =>
                {
                    Host?.Canvas.Children.Remove(_image);
                    Host?.Canvas.Children.Remove(_addImage);
                };
            }

            Storyboard.Begin();
            if (_blendStatus == AnimateStatus.Dynamic)
            {
                Task.Run(() =>
                {
                    var behavior = FillBehavior.HoldEnd;

                    Application.Current.Dispatcher?.Invoke(() => { behavior = Storyboard.FillBehavior; });
                    while (behavior != FillBehavior.Stop && _blendDurationQueue.Count > 0)
                    {
                        var now = _blendDurationQueue.Peek();
                        if (PlayTime < now.X)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(now.X - PlayTime));
                        }

                        if (PlayTime >= now.X && PlayTime <= now.Y)
                        {
                            Application.Current?.Dispatcher?.Invoke(new Action(() =>
                            {
                                _image.Visibility = Visibility.Hidden;
                                _addImage.Visibility = Visibility.Visible;
                            }), null);
                            Console.WriteLine("show");
                            Thread.Sleep(TimeSpan.FromMilliseconds(now.Y - PlayTime));
                            Application.Current?.Dispatcher?.Invoke(new Action(() =>
                            {
                                _image.Visibility = Visibility.Visible;
                                _addImage.Visibility = Visibility.Hidden;
                            }), null);
                            _blendDurationQueue.Dequeue();
                            Console.WriteLine("hidden");
                        }

                        Application.Current.Dispatcher?.Invoke(() => { behavior = Storyboard.FillBehavior; });

                        Thread.Sleep(20);
                    }
                });
            }
            //_dispatcherT = new DispatcherTimer
            //{
            //    Interval = TimeSpan.FromMilliseconds(MinTime)
            //};

            //_dispatcherT.Start();
            //_dispatcherT.Tick += (obj, args) =>
            //{


            //    _dispatcherT.Stop();
            //};

        }

        public void Reset()
        {
            _image.RenderTransform = InitTransformGroup(OriginWidth, OriginHeight, _defaultX, _defaultY);
            if (_addImage != null)
            {
                _addImage.RenderTransform = InitTransformGroup(OriginWidth, OriginHeight, _defaultX, _defaultY);
            }

            _dispatcherT?.Stop();
        }

        protected override void FadeAction(List<TransformAction> actions)
        {
            foreach (var transformAction in actions)
            {
                var dur = TimeSpan.FromMilliseconds(transformAction.EndTime - transformAction.StartTime);
                var duration = new Duration(dur < TimeSpan.Zero ? TimeSpan.Zero : dur);
                _transformList.Add((UIElement.OpacityProperty, new DoubleAnimation
                {
                    From = (double)transformAction.StartParam,
                    To = (double)transformAction.EndParam,
                    EasingFunction = ConvertEasing(transformAction.Easing),
                    BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                    Duration = duration
                }));
            }
        }

        protected override void RotateAction(List<TransformAction> actions)
        {
            foreach (var transformAction in actions)
            {
                var dur = TimeSpan.FromMilliseconds(transformAction.EndTime - transformAction.StartTime);
                var duration = new Duration(dur < TimeSpan.Zero ? TimeSpan.Zero : dur);
                _transformList.Add((RotateTransform.AngleProperty, new DoubleAnimation
                {
                    From = (double)transformAction.StartParam / Math.PI * 360,
                    To = (double)transformAction.EndParam / Math.PI * 360,
                    EasingFunction = ConvertEasing(transformAction.Easing),
                    BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                    Duration = duration
                }));
            }
        }

        protected override void MoveAction(List<TransformAction> actions)
        {
            foreach (var transformAction in actions)
            {
                var dur = TimeSpan.FromMilliseconds(transformAction.EndTime - transformAction.StartTime);
                var duration = new Duration(dur < TimeSpan.Zero ? TimeSpan.Zero : dur);
                var startP = (Vector2<double>)transformAction.StartParam;
                var endP = (Vector2<double>)transformAction.EndParam;
                _transformList.Add((TranslateTransform.XProperty, new DoubleAnimation
                {
                    From = startP.X - OriginWidth * Origin.X,
                    To = endP.X - OriginWidth * Origin.X,
                    EasingFunction = ConvertEasing(transformAction.Easing),
                    BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                    Duration = duration
                }));
                _transformList.Add((TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = startP.Y - OriginHeight * Origin.Y,
                    To = endP.Y - OriginHeight * Origin.Y,
                    EasingFunction = ConvertEasing(transformAction.Easing),
                    BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                    Duration = duration
                }));
            }
        }

        protected override void Move1DAction(List<TransformAction> actions, bool isHorizon)
        {
            foreach (var transformAction in actions)
            {
                var dur = TimeSpan.FromMilliseconds(transformAction.EndTime - transformAction.StartTime);
                var duration = new Duration(dur < TimeSpan.Zero ? TimeSpan.Zero : dur);
                var startP = (double)transformAction.StartParam;
                var endP = (double)transformAction.EndParam;
                if (isHorizon)
                {
                    _transformList.Add((TranslateTransform.XProperty, new DoubleAnimation
                    {
                        From = startP - OriginWidth * Origin.X,
                        To = endP - OriginWidth * Origin.X,
                        EasingFunction = ConvertEasing(transformAction.Easing),
                        BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                        Duration = duration
                    }));
                }
                else
                {
                    _transformList.Add((TranslateTransform.YProperty, new DoubleAnimation
                    {
                        From = startP - OriginHeight * Origin.Y,
                        To = endP - OriginHeight * Origin.Y,
                        EasingFunction = ConvertEasing(transformAction.Easing),
                        BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                        Duration = duration
                    }));
                }
            }
        }

        protected override void ScaleVecAction(List<TransformAction> actions)
        {
            foreach (var transformAction in actions)
            {
                var dur = TimeSpan.FromMilliseconds(transformAction.EndTime - transformAction.StartTime);
                var duration = new Duration(dur < TimeSpan.Zero ? TimeSpan.Zero : dur);
                var startP = (Vector2<double>)transformAction.StartParam;
                var endP = (Vector2<double>)transformAction.EndParam;

                _transformList.Add((ScaleTransform.ScaleXProperty, new DoubleAnimation
                {
                    From = startP.X,
                    To = endP.X,
                    EasingFunction = ConvertEasing(transformAction.Easing),
                    BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                    Duration = duration
                }));
                _transformList.Add((ScaleTransform.ScaleYProperty, new DoubleAnimation
                {
                    From = startP.Y,
                    To = endP.Y,
                    EasingFunction = ConvertEasing(transformAction.Easing),
                    BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                    Duration = duration
                }));
            }
        }

        protected override void ColorAction(List<TransformAction> actions)
        {
            var dv = new DrawingVisual();
            _coverBrush = new SolidColorBrush(Colors.White);
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(_coverBrush, null, new Rect(new Size(1, 1)));
            }

            _image.Effect = new BlendEffect
            {
                Mode = BlendModes.Multiply,
                Amount = 1,
                Blend = new VisualBrush
                {
                    Visual = dv
                }
            };
            //var sb = ((GeometryDrawing)((DrawingVisual)((VisualBrush)((BlendEffect)_image.Effect).Blend).Visual).Drawing
            //    .Children[0]).Brush;
            foreach (var transformAction in actions)
            {
                var dur = TimeSpan.FromMilliseconds(transformAction.EndTime - transformAction.StartTime);
                var duration = new Duration(dur < TimeSpan.Zero ? TimeSpan.Zero : dur);
                var startP = (Vector3<double>)transformAction.StartParam;
                var endP = (Vector3<double>)transformAction.EndParam;
                _transformList.Add((SolidColorBrush.ColorProperty, new ColorAnimation
                {
                    From = Color.FromRgb((byte)startP.X, (byte)startP.Y, (byte)startP.Z),
                    To = Color.FromRgb((byte)endP.X, (byte)endP.Y, (byte)endP.Z),
                    EasingFunction = ConvertEasing(transformAction.Easing),
                    BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                    Duration = duration
                }));
            }
        }

        protected override void BlendAction(List<TransformAction> actions)
        {
            var source = _image.Source;
            //if (!StoryboardCanvasHost.BrushCache.ContainsKey(source))
            //{
            //    StoryboardCanvasHost.BrushCache.Add(source, new VisualBrush { Visual = _image });
            //}

            _addImage = new Rectangle
            {
                Fill = Brushes.Transparent,
                Width = OriginWidth,
                Height = OriginHeight,
                Effect = new BlendEffect
                {
                    Mode = BlendModes.Normal,
                    Amount = 1,
                    //Blend = StoryboardCanvasHost.BrushCache[source],
                    Blend = new VisualBrush { Visual = _image }
                },
                RenderTransform = InitTransformGroup(OriginWidth, OriginHeight, _defaultX, _defaultY),
                RenderTransformOrigin = new Point(Origin.X, Origin.Y)
            };

            foreach (var transformAction in actions)
            {
                _blendDurationQueue.Enqueue(new Vector2<double>(transformAction.StartTime - MinTime,
                    transformAction.EndTime - MinTime));
            }

            _blendStatus = AnimateStatus.Static;
            return;
            if (actions.Count == 1)
            {
                var o = actions[0];
                if (o.StartTime.Equals(o.EndTime))
                {
                    _image.Visibility = Visibility.Hidden;
                    _addImage.Visibility = Visibility.Visible;
                    _blendStatus = AnimateStatus.Static;
                }
                else
                {
                    _blendStatus = AnimateStatus.Dynamic;
                }
            }
            else
            {
                _blendStatus = AnimateStatus.Dynamic;
            }
        }

        protected override void FlipAction(List<TransformAction> actions)
        {
            //    if (_image.RenderTransform is TransformGroup g)
            //    {
            //        g.Children.Add(new ScaleTransform(1, 1));
            //    }

            if (actions.Count == 1)
            {
                var o = actions[0];
                _flipStatus = o.StartTime.Equals(o.EndTime) ? AnimateStatus.Static : AnimateStatus.Dynamic;
            }
            else
            {
                _flipStatus = AnimateStatus.Dynamic;
            }

            foreach (var transformAction in actions)
            {
                var flip = (FlipMode)transformAction.EndParam;
                DependencyProperty prop = null;

                if (flip == FlipMode.FlipX)
                {
                    prop = SkewTransform.AngleXProperty;
                }
                else if (flip == FlipMode.FlipY)
                {
                    prop = SkewTransform.AngleYProperty;
                }

                var dur = TimeSpan.FromMilliseconds(transformAction.EndTime - transformAction.StartTime);
                var duration = new Duration(dur < TimeSpan.Zero ? TimeSpan.Zero : dur);

                _transformList.Add((prop, new DoubleAnimation
                {
                    From = 1,
                    To = -1,
                    BeginTime = TimeSpan.FromMilliseconds(transformAction.StartTime - MinTime),
                    Duration = TimeSpan.Zero
                }));

                if (_flipStatus == AnimateStatus.Dynamic)
                {
                    _transformList.Add((prop, new DoubleAnimation
                    {
                        From = 1,
                        To = 1,
                        BeginTime = TimeSpan.FromMilliseconds(transformAction.EndTime - MinTime),
                        Duration = TimeSpan.Zero
                    }));
                }
            }
        }

        protected override void HandleLoopGroup((double startTime, int loopTimes, ITransformable<double> transformList) tuple)
        {
            var (startTime, loopTimes, transformList) = tuple;
            LoopList.Add(new SysAni.Storyboard());
            if (transformList is InternalTransformableObject<double> sb)
            {

            }
        }

        protected override void StartAnimation() { }

        protected override void EndAnimation()
        {
            foreach (var (prop, animation) in _transformList)
            {
                //if (prop == SolidColorBrush.ColorProperty)
                //{
                //    continue;
                //}

                AnimationTimeline copy = null;
                switch (_blendStatus)
                {
                    case AnimateStatus.None:
                        SysAni.Storyboard.SetTarget(animation, _image);
                        break;
                    case AnimateStatus.Static when prop != SolidColorBrush.ColorProperty:
                        SysAni.Storyboard.SetTarget(animation, _addImage);
                        break;
                    case AnimateStatus.Dynamic:
                        copy = animation.Clone();
                        SysAni.Storyboard.SetTarget(copy, _addImage);
                        SysAni.Storyboard.SetTarget(animation, _image);

                        Storyboard.Children.Add(copy);
                        break;
                        //default:
                        //    throw new ArgumentOutOfRangeException();
                }
                Storyboard.Children.Add(animation);

                if (prop == RotateTransform.AngleProperty)
                {
                    SysAni.Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.Children[1].Angle"));
                    //SetProp("RenderTransform.Children[1].Angle", animation, copy);
                }
                else if (prop == TranslateTransform.XProperty)
                {
                    SysAni.Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.Children[3].X"));
                    //SetProp("RenderTransform.Children[2].X", animation, copy);
                }
                else if (prop == TranslateTransform.YProperty)
                {
                    SysAni.Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.Children[3].Y"));
                    //SetProp("RenderTransform.Children[2].Y", animation, copy);
                }
                else if (prop == ScaleTransform.ScaleXProperty)
                {
                    SysAni.Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.Children[0].ScaleX"));
                    //SetProp("RenderTransform.Children[0].ScaleX", animation, copy);
                }
                else if (prop == ScaleTransform.ScaleYProperty)
                {
                    SysAni.Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.Children[0].ScaleY"));
                    //SetProp("RenderTransform.Children[0].ScaleY", animation, copy);
                }
                else if (prop == SkewTransform.AngleXProperty)
                {
                    SysAni.Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.Children[2].ScaleX"));
                    //SetProp("RenderTransform.Children[0].ScaleX", animation, copy);
                }
                else if (prop == SkewTransform.AngleYProperty)
                {
                    SysAni.Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.Children[2].ScaleY"));
                    //SetProp("RenderTransform.Children[0].ScaleY", animation, copy);
                }
                else if (prop == SolidColorBrush.ColorProperty)
                {
                    SysAni.Storyboard.SetTarget(animation, _image);
                    SysAni.Storyboard.SetTargetProperty(animation, new PropertyPath("Effect.Blend.Visual.Drawing.Children[0].Brush.Color"));
                    //SetProp("RenderTransform.Children[0].ScaleY", animation, copy);
                }
                else
                {
                    SetProp(prop, animation, copy);
                }
            }
        }

        private static void SetProp(DependencyProperty path, params AnimationTimeline[] timelines)
        {
            foreach (var timeline in timelines)
            {
                if (timeline != null)
                    SysAni.Storyboard.SetTargetProperty(timeline, new PropertyPath(path));
            }
        }

        private static void SetProp(string path, params AnimationTimeline[] timelines)
        {
            foreach (var timeline in timelines)
            {
                if (timeline != null)
                    SysAni.Storyboard.SetTargetProperty(timeline, new PropertyPath(path));
            }
        }

        private IEasingFunction ConvertEasing(Easing easing)
        {
            switch (easing)
            {
                case Easing.Linear:
                    return null;
                case Easing.EasingOut:
                    return new SineEase { EasingMode = EasingMode.EaseOut };
                case Easing.EasingIn:
                    return new SineEase { EasingMode = EasingMode.EaseIn };
                case Easing.QuadIn:
                    return new QuadraticEase { EasingMode = EasingMode.EaseIn };
                case Easing.QuadOut:
                    return new QuadraticEase { EasingMode = EasingMode.EaseOut };
                case Easing.QuadInOut:
                    return new QuadraticEase { EasingMode = EasingMode.EaseInOut };
                case Easing.CubicIn:
                    break;
                case Easing.CubicOut:
                    break;
                case Easing.CubicInOut:
                    break;
                case Easing.QuartIn:
                    break;
                case Easing.QuartOut:
                    break;
                case Easing.QuartInOut:
                    break;
                case Easing.QuintIn:
                    break;
                case Easing.QuintOut:
                    break;
                case Easing.QuintInOut:
                    break;
                case Easing.SineIn:
                    break;
                case Easing.SineOut:
                    break;
                case Easing.SineInOut:
                    break;
                case Easing.ExpoIn:
                    break;
                case Easing.ExpoOut:
                    break;
                case Easing.ExpoInOut:
                    break;
                case Easing.CircIn:
                    break;
                case Easing.CircOut:
                    break;
                case Easing.CircInOut:
                    break;
                case Easing.ElasticIn:
                    break;
                case Easing.ElasticOut:
                    break;
                case Easing.ElasticHalfOut:
                    break;
                case Easing.ElasticQuarterOut:
                    break;
                case Easing.ElasticInOut:
                    break;
                case Easing.BackIn:
                    break;
                case Easing.BackOut:
                    break;
                case Easing.BackInOut:
                    break;
                case Easing.BounceIn:
                    break;
                case Easing.BounceOut:
                    break;
                case Easing.BounceInOut:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(easing), easing, null);
            }

            return null;
            throw new NotSupportedException($"不支持{nameof(easing)}", null);
        }

        public void Dispose()
        {

        }
    }

    public class AComparer : IComparer<(ImageObject, double)>
    {
        public int Compare((ImageObject, double) x, (ImageObject, double) y)
        {
            return x.Item2.CompareTo(y.Item2);
        }
    }

    public enum AnimateStatus
    {
        None, Static, Dynamic
    }
}