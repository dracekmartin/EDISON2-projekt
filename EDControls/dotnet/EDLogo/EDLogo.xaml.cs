using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace EDControls.EDLogo;

/// <summary>
/// Logo EDISON (krychle z koulí, stejná jako <c>cube_molecule.ico</c> a <c>EDIcons.MoleculeLarge</c>)
/// s volitelnou animací skládání podle <see cref="AnimationMode"/>. Geometrii a časovou osu drží
/// <see cref="CubeAssembly"/>, tady jsou jen tvary a hodiny.
/// </summary>
public sealed partial class EDLogo : UserControl
{
    // Vazby: theme token (kontrast v light i dark), hloubku dělá průhlednost.
    private const string BondBrushKey = "EDTheme.Border.Contrast";
    private const double BondOpacityFar = 0.45;

    public static readonly DependencyProperty AnimationModeProperty =
        DependencyProperty.Register(nameof(AnimationMode), typeof(EDLogoAnimationMode), typeof(EDLogo),
            new PropertyMetadata(EDLogoAnimationMode.None, (d, _) => ((EDLogo)d).Play()));

    // Čas animace (ms). Pohání ho DoubleAnimation, stop přes BeginAnimation(.., null) - žádný
    // vlastní render loop, který by tikal dál.
    private static readonly DependencyProperty TimeProperty =
        DependencyProperty.Register(nameof(Time), typeof(double), typeof(EDLogo),
            new PropertyMetadata(0.0, (d, _) => ((EDLogo)d).ApplyFrame()));

    private readonly CubeAssembly _cube = new();
    private readonly List<(CubeAssembly.Atom Atom, UIElement Visual, ScaleTransform Scale, TranslateTransform Offset)> _atoms = [];
    private readonly List<(CubeAssembly.Bond Bond, Line Line)> _bonds = [];
    private DoubleAnimation? _animation;   // běžící hodiny, null = stojí
    private bool _pending;                 // animace čeká, až bude logo vidět

    public EDLogo()
    {
        InitializeComponent();
        BuildShapes();
        ApplyFrame();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>Režim animace; změna ji spustí znovu od začátku. Výchozí <see cref="EDLogoAnimationMode.None"/>.</summary>
    public EDLogoAnimationMode AnimationMode
    {
        get => (EDLogoAnimationMode)GetValue(AnimationModeProperty);
        set => SetValue(AnimationModeProperty, value);
    }

    /// <summary>
    /// Konec režimu <see cref="EDLogoAnimationMode.Once"/>: po každém spuštění právě jednou, i když se logo
    /// před koncem skryje nebo jsou animace ve Windows vypnuté.
    /// </summary>
    public event EventHandler? AnimationCompleted;

    internal double Time => (double)GetValue(TimeProperty);

    internal CubeAssembly Cube => _cube;

    /// <summary>Spustí animaci podle <see cref="AnimationMode"/> od začátku; skryté logo začne, až bude vidět.</summary>
    public void Play()
    {
        _pending = AnimationMode != EDLogoAnimationMode.None;
        StopClock();
        if (_pending && IsVisible) Start();
        else ApplyFrame();
    }

    // Hodiny běží jen u viditelného loga - clock skrytého okna by tikal dál a držel dispatcher vzhůru.
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            if (_pending) Start();
            return;
        }
        if (_animation is null) return;
        bool loop = AnimationMode == EDLogoAnimationMode.Loop;
        _pending = loop;   // smyčka po zobrazení znovu od začátku, Once skončí složená
        StopClock();
        if (!loop) Complete();
    }

    private void Start()
    {
        _pending = false;
        // Vypnuté animace ve Windows (Výkon ▸ Animovat ovládací prvky) = rovnou složené logo.
        if (!SystemParameters.ClientAreaAnimation)
        {
            Complete();
            return;
        }

        bool loop = AnimationMode == EDLogoAnimationMode.Loop;
        double ms = loop ? _cube.CycleMs : _cube.AssemblyMs;
        var animation = new DoubleAnimation(0, ms, TimeSpan.FromMilliseconds(ms));
        if (loop)
            animation.RepeatBehavior = RepeatBehavior.Forever;
        else
            animation.Completed += (_, _) =>
            {
                if (_animation != animation) return;   // mezitím restart nebo stop
                StopClock();
                Complete();
            };
        _animation = animation;
        BeginAnimation(TimeProperty, animation);
    }

    private void StopClock()
    {
        if (_animation is null) return;
        _animation = null;
        BeginAnimation(TimeProperty, null);
    }

    private void Complete()
    {
        ApplyFrame();
        if (AnimationMode == EDLogoAnimationMode.Once) AnimationCompleted?.Invoke(this, EventArgs.Empty);
    }

    // Koule = Grid (tělo s gradientem + odlesk) s vlastním scale/posunem kolem středu,
    // vazba = Line, jejíž konec se posouvá. Pořadí v plátně = pořadí kreslení (zezadu dopředu).
    private void BuildShapes()
    {
        var front = (Brush)FindResource("AtomFront");
        var back = (Brush)FindResource("AtomBack");
        foreach (var part in _cube.DrawOrder)
        {
            if (part is CubeAssembly.Atom atom)
                LogoCanvas.Children.Add(CreateAtom(atom, atom.IsFront ? front : back));
            else if (part is CubeAssembly.Bond bond)
                LogoCanvas.Children.Add(CreateBond(bond));
        }
    }

    private UIElement CreateAtom(CubeAssembly.Atom atom, Brush fill)
    {
        double r = atom.Radius;
        var scale = new ScaleTransform();
        var offset = new TranslateTransform();
        var visual = new Grid
        {
            Width = 2 * r,
            Height = 2 * r,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { scale, offset } },
        };
        visual.Children.Add(new Ellipse { Fill = fill });
        // Odlesk vlevo nahoře natočený podél koule; bližší koule se lesknou víc.
        visual.Children.Add(new Ellipse
        {
            Width = 0.84 * r,
            Height = 0.52 * r,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0.28 * r, 0.36 * r, 0, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-28),
            Fill = Brushes.White,
            Opacity = 0.38 + 0.2 * atom.Depth,
        });
        Canvas.SetLeft(visual, atom.X - r);
        Canvas.SetTop(visual, atom.Y - r);
        _atoms.Add((atom, visual, scale, offset));
        return visual;
    }

    private Line CreateBond(CubeAssembly.Bond bond)
    {
        var line = new Line
        {
            X1 = bond.From.X,
            Y1 = bond.From.Y,
            StrokeThickness = bond.Width,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        line.SetResourceReference(Shape.StrokeProperty, BondBrushKey);
        _bonds.Add((bond, line));
        return line;
    }

    // Stojící logo: čeká na start = prázdné plátno, jinak složené.
    private void ApplyFrame()
    {
        double timeline = _animation is null
            ? (_pending ? 0 : _cube.AssemblyMs)
            : AnimationMode == EDLogoAnimationMode.Loop ? _cube.TimelineAt(Time) : Math.Min(Time, _cube.AssemblyMs);
        _cube.Apply(timeline);

        foreach (var (atom, visual, scale, offset) in _atoms)
        {
            visual.Opacity = atom.Opacity;
            scale.ScaleX = scale.ScaleY = atom.Scale;
            offset.X = atom.OffsetX;
            offset.Y = atom.OffsetY;
        }
        foreach (var (bond, line) in _bonds)
        {
            line.Opacity = bond.Opacity * (BondOpacityFar + (1 - BondOpacityFar) * bond.Depth);
            line.X2 = bond.CurrentX2;
            line.Y2 = bond.CurrentY2;
        }
    }
}
