﻿using JetBrains.Annotations;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LoadingIndicator.XP
{
    public sealed class LongOperation : IDisposable
    {
        [NotNull] private readonly Control _parentControl;
        [NotNull] private readonly LongOperationSettings _settings;
        [NotNull] private readonly IDisposable _stopDisposable;

        [CanBeNull] private LayerControl _layerControl;
        [CanBeNull] private Control _previouslyFocusedControl;

        private int _started;
        private CancellationTokenSource _cancelationSource;
        private DateTime? _indicatorShownAt;

        public LongOperation([NotNull] Control parentControl)
            : this(parentControl, LongOperationSettings.Default)
        {
        }

        public LongOperation([NotNull] Control parentControl, [NotNull] LongOperationSettings settings)
        {
            _parentControl = parentControl??throw new ArgumentNullException(nameof(parentControl));
            _settings = settings??throw new ArgumentNullException(nameof(settings));
            _stopDisposable = new DisposableAction(() => Stop());
        }

        [NotNull]
        public IDisposable Start(bool displayIndicatorImmediatley = false)
        {
            if (_parentControl.InvokeIfRequired(() => Start(displayIndicatorImmediatley)))
            {
                return _stopDisposable;
            }

            if (Interlocked.Increment(ref _started) != 1)
            {
                return _stopDisposable;
            }

            _indicatorShownAt = null;

            // HACK: To capture latest screenshot
            // i.e. when we select node in tree view just before start operation
            // without refresh this selection will be displayed only after layer control will disappear
            _parentControl.Refresh();

            _previouslyFocusedControl = FindFocusedControl(_parentControl.FindForm());

            //var parentControlImage = _parentControl.CaptureScreenshot();
            var parentControlImage = _parentControl.PrintClientRectangleToImage();
            parentControlImage.Save("2.png");

            _layerControl = new LayerControl(parentControlImage, _settings.ProcessImage)
            {
                Location = new Point(0, 0),
                Size = _parentControl.Size,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right | AnchorStyles.Top
            };

            _parentControl.Controls.Add(_layerControl);

            _layerControl.SubscribeChildrenControlEnter();
            _layerControl.BringToFront();

            _cancelationSource = new CancellationTokenSource();

            if (displayIndicatorImmediatley)
            {
                DisplayIndicator(_cancelationSource.Token);
                _parentControl.Refresh();
            }
            else
            {
                Task.Factory.StartNew(StartAsync);
            }

            return _stopDisposable;
        }

        public void Stop(bool hideIndicatorImmediatley = false)
        {
            var indicatorShownAt = _indicatorShownAt;
            if (indicatorShownAt.HasValue && !hideIndicatorImmediatley)
            {
                var indicatorDisplayTime = DateTime.UtcNow - indicatorShownAt.Value;
                if (indicatorDisplayTime < _settings.MinIndicatorShowTime)
                {
                    Delay((_settings.MinIndicatorShowTime - indicatorDisplayTime).Milliseconds).Wait();
                }
            }

            StopInternal();
        }

        public void StopIfDisplayed(bool hideIndicatorImmediatley = false)
        {
            if (_parentControl.InvokeIfRequired(() => StopIfDisplayed(hideIndicatorImmediatley)))
            {
                return;
            }

            Stop(hideIndicatorImmediatley);
        }

        public void Dispose()
        {
            StopIfDisplayed(true);
        }

        [CanBeNull]
        private static Control FindFocusedControl([CanBeNull] ContainerControl control)
        {
            if (control == null)
            {
                return null;
            }

            var focusedControl = control.ActiveControl;

            if (focusedControl == control || focusedControl == null)
            {
                return control;
            }

            var compositeFocused = focusedControl as ContainerControl;

            if (compositeFocused == null)
            {
                return focusedControl;
            }

            return FindFocusedControl(compositeFocused);
        }

        private void StopInternal()
        {
            if (_parentControl.InvokeIfRequired(StopInternal))
            {
                if (_parentControl.IsDisposed || _parentControl.Disposing)
                {
                    var layerControl = _layerControl;
                    layerControl?.Remove();
                }

                return;
            }

            var value = Interlocked.Decrement(ref _started);
            if (value > 0)
            {
                return;
            }

            if (value < 0)
            {
                if (_settings.AllowStopBeforeStart)
                {
                    return;
                }

                throw new InvalidOperationException("Stop long operation more times then starts.");
            }

            _cancelationSource.Cancel();

            var form = _parentControl.FindForm();
            var currentFocused = FindFocusedControl(form);

            _layerControl?.Remove();

            if (form != null && currentFocused == _layerControl)
            {
                RestoreFocus(form);
            }

            _layerControl = null;
        }

        private void RestoreFocus([NotNull] ContainerControl containerControl)
        {
            if (_previouslyFocusedControl == null ||
                _previouslyFocusedControl.IsDisposed ||
                _previouslyFocusedControl.Disposing ||
                _previouslyFocusedControl.Parent == null ||
                !_previouslyFocusedControl.Visible ||
                containerControl == _previouslyFocusedControl)
            {
                return;
            }

            containerControl.ActiveControl = _previouslyFocusedControl;
        }

        private void StartAsync()
        {
            var cancelToken = _cancelationSource.Token;

            Delay(_settings.BeforeShowIndicatorDelay.Milliseconds).Wait();

            if (_started == 0 || cancelToken.IsCancellationRequested)
            {
                return;
            }

            DisplayIndicator(cancelToken);
        }

        private void DisplayIndicator(CancellationToken cancelToken)
        {
            if (_parentControl.InvokeIfRequired(() => DisplayIndicator(cancelToken)))
            {
                return;
            }

            if (_started == 0 || cancelToken.IsCancellationRequested || _layerControl == null)
            {
                return;
            }

            if (_settings.MinIndicatorShowTime != TimeSpan.Zero)
            {
                _indicatorShownAt = DateTime.UtcNow;
            }

            _layerControl.PlaceIndicator(_settings.IndicatorFactory());
        }

        private sealed class DisposableAction : IDisposable
        {
            [NotNull] private readonly Action _action;

            public DisposableAction([NotNull] Action action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));

                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }


        private Task Delay(int milliseconds)
        {
            var tcs = new TaskCompletionSource<object>();
            var timer = new System.Timers.Timer(milliseconds) { AutoReset = false };
            timer.Elapsed += delegate { timer.Dispose(); tcs.SetResult(null); };
            timer.Start();
            return tcs.Task;
        }
    }
}