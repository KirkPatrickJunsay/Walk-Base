using SkiaSharp;
using SkiaSharp.Views.Maui;
using WalkBase.Rendering;
using WalkBase.Services;
using WalkBase.ViewModels;

namespace WalkBase.Views;

public partial class BasePage : ContentPage
{
    private readonly BaseViewModel _vm;
    private readonly IAmbientService _ambient;

    public BasePage(BaseViewModel vm, IAmbientService ambient)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _ambient = ambient;

        Canvas.PaintSurface += OnPaintSurface;
        Canvas.Touch += OnTouch;
        _vm.StateChanged += () => Canvas.InvalidateSurface();
        _vm.PopupOpened += AnimatePopupIn;
        _vm.GoalReached += AnimateCelebration;
    }

    private async void AnimateCelebration()
    {
        CelebrationView.IsVisible = true;
        CelebrationView.Opacity = 0;
        CelebrationView.Scale = 0.8;
        await Task.WhenAll(
            CelebrationView.FadeTo(1, 200, Easing.CubicOut),
            CelebrationView.ScaleTo(1, 320, Easing.SpringOut));
        await Task.Delay(2200);
        await CelebrationView.FadeTo(0, 350);
        CelebrationView.IsVisible = false;
    }

    private async void AnimatePopupIn()
    {
        PopupScrim.Opacity = 0;
        PopupCard.Scale = 0.9;
        PopupCard.Opacity = 0;
        await Task.WhenAll(
            PopupScrim.FadeTo(1, 120, Easing.CubicOut),
            PopupCard.FadeTo(1, 160, Easing.CubicOut),
            PopupCard.ScaleTo(1, 180, Easing.CubicOut));
    }

    private IDispatcherTimer? _frameTimer;
    private const float FrameSeconds = 1f / 24f;   // 24fps — smooth enough, ~20% less work than 30

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingCommand.ExecuteAsync(null);
        StartFrameLoop();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.DisappearingCommand.Execute(null);
        _frameTimer?.Stop();   // stop animating when the base isn't visible (battery)
        _ambient.Stop();       // and silence the ambience
    }

    /// <summary>Pick the ambience that matches the current scene (festival &gt; rain &gt; night &gt; day).</summary>
    private void UpdateAmbience()
    {
        int hour = DateTime.Now.Hour;
        var scene =
            _vm.Townsfolk.IsFestival ? AmbientScene.Festival :
            _vm.Renderer.IsRaining ? AmbientScene.Rain :
            (hour < 6 || hour >= 19) ? AmbientScene.Night :
            AmbientScene.Day;
        _ambient.SetScene(scene);
    }

    /// <summary>~30 fps loop that ambles the townsfolk and repaints the base.</summary>
    private void StartFrameLoop()
    {
        // Accessibility / battery: keep the town completely static.
        if (_vm.ReduceMotion)
        {
            _frameTimer?.Stop();
            Canvas.InvalidateSurface(); // one static render
            UpdateAmbience();
            return;
        }

        if (_frameTimer is null)
        {
            _frameTimer = Dispatcher.CreateTimer();
            _frameTimer.Interval = TimeSpan.FromSeconds(FrameSeconds);
            _frameTimer.IsRepeating = true;
            _frameTimer.Tick += (_, _) =>
            {
                if (_vm.IsPopupVisible)
                    return; // pause the stroll while a popup is up
                _vm.Townsfolk.Update(FrameSeconds, _vm.GridSize);
                Canvas.InvalidateSurface();
                UpdateAmbience();
            };
        }
        if (!_frameTimer.IsRunning)
            _frameTimer.Start();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _vm.Renderer.Draw(e.Surface.Canvas, e.Info, _vm.GridSize, _vm.Placed, _vm.SelectedPlot,
            _vm.Zoom, _vm.PanX, _vm.PanY, _vm.CanExpand, _vm.Townsfolk.Walkers, _vm.Townsfolk.Pets,
            _vm.Townsfolk.SpecialVisitor, _vm.Townsfolk.IsFestival);
    }

    // --- gesture state ---
    // Pinch is handled here (not via PinchGestureRecognizer) because the SKCanvasView
    // consumes touch events, which made the recognizer fire unreliably.
    private const float MoveThreshold = 18f;   // px before a press becomes a drag
    private readonly Dictionary<long, SKPoint> _pointers = new();
    private long? _panId;                       // pointer driving a single-finger pan/tap
    private SKPoint _pressPx;
    private bool _moved;
    private bool _sawTwo;                        // a 2-finger pinch happened this gesture
    private float _panStartX, _panStartY;
    private float _pinchStartDist;
    private double _pinchStartZoom = 1;

    private static float Distance(SKPoint a, SKPoint b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;
        if (_vm.IsPopupVisible)
            return;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _pointers[e.Id] = e.Location;
                if (_pointers.Count == 1)
                {
                    _panId = e.Id;
                    _pressPx = e.Location;
                    _moved = false;
                    _panStartX = _vm.PanX;
                    _panStartY = _vm.PanY;
                }
                else if (_pointers.Count == 2)
                {
                    BeginPinch();
                }
                break;

            case SKTouchAction.Moved:
                if (!_pointers.ContainsKey(e.Id))
                    break;
                _pointers[e.Id] = e.Location;

                if (_sawTwo && _pointers.Count >= 2)
                {
                    var pts = _pointers.Values.ToArray();
                    float dist = Distance(pts[0], pts[1]);
                    if (_pinchStartDist > 1f)
                        _vm.SetZoom((float)(_pinchStartZoom * dist / _pinchStartDist));
                    Canvas.InvalidateSurface();
                }
                else if (!_sawTwo && e.Id == _panId)
                {
                    float dx = e.Location.X - _pressPx.X;
                    float dy = e.Location.Y - _pressPx.Y;
                    if (!_moved && MathF.Abs(dx) + MathF.Abs(dy) > MoveThreshold)
                        _moved = true;
                    if (_moved && _vm.Zoom > 1.001f)
                    {
                        float maxX = Canvas.CanvasSize.Width * (_vm.Zoom - 1f) / 2f;
                        float maxY = Canvas.CanvasSize.Height * (_vm.Zoom - 1f) / 2f;
                        _vm.PanX = Math.Clamp(_panStartX + dx, -maxX, maxX);
                        _vm.PanY = Math.Clamp(_panStartY + dy, -maxY, maxY);
                        Canvas.InvalidateSurface();
                    }
                }
                break;

            case SKTouchAction.Released:
                if (e.Id == _panId && !_moved && !_sawTwo)
                    HandleTap(e.Location);
                EndPointer(e.Id);
                break;

            case SKTouchAction.Cancelled:
                EndPointer(e.Id);
                break;
        }
    }

    private void BeginPinch()
    {
        _sawTwo = true;
        _moved = true;                          // suppress the tap that would otherwise fire
        var pts = _pointers.Values.ToArray();
        _pinchStartDist = Distance(pts[0], pts[1]);
        _pinchStartZoom = _vm.Zoom;
    }

    private void EndPointer(long id)
    {
        _pointers.Remove(id);
        if (id == _panId)
            _panId = null;
        if (_pointers.Count == 0)
        {
            // gesture fully ended — reset
            _sawTwo = false;
            _moved = false;
        }
        else if (_pointers.Count == 1 && _sawTwo)
        {
            // Dropped from a pinch back to one finger — re-anchor so the leftover finger can
            // keep panning, but stay "moved" so lifting it never fires a tap (would open a popup).
            _panId = _pointers.Keys.First();
            _pressPx = _pointers[_panId.Value];
            _panStartX = _vm.PanX;
            _panStartY = _vm.PanY;
            _sawTwo = false;
            _moved = true;
        }
    }

    private async void HandleTap(SKPoint location)
    {
        var (gx, gy) = _vm.Renderer.GridFromScreen(location);
        int size = _vm.GridSize;

        // While relocating a building, a tap drops it (or cancels) — handle that first.
        if (_vm.IsMoving)
        {
            await _vm.TryMoveToAsync(gx, gy);
            return;
        }

        // Tap on the translucent "buyable" ring (a cell just outside the base) → buy land.
        if (_vm.CanExpand && gx >= 0 && gy >= 0 && gx <= size && gy <= size &&
            (gx == size || gy == size))
        {
            _vm.ExpandCommand.Execute(null);
            return;
        }

        if (gx < 0 || gy < 0 || gx >= size || gy >= size)
            return;

        var existing = _vm.BuildingAt(gx, gy);
        if (existing is not null)
            _vm.ShowBuildingPopup(existing);
        else
            _vm.ShowEmptyPlotPopup(gx, gy);
    }

}
