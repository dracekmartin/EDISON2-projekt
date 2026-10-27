using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace EDControls.EDLogo;

/// <summary>
/// Skládání krychle (logo EDISON) - geometrie a časová osa bez WPF.
/// Krychle v mírné perspektivě (stejný pohled jako <c>EDIcons.Molecule</c>), na každém rohu koule.
/// Koule naskakují po hranách krychle (nejbližší, největší přistane poslední), ke každé doroste
/// vazba od předchozí, zbylé vazby krychli uzavřou. Pak krychle chvíli drží, rozloží se
/// (skládání pozpátku), krátká pauza a znovu. Tvary z toho staví <see cref="EDLogo"/>.
/// </summary>
internal sealed class CubeAssembly
{
    /// <summary>Strana plátna v jednotkách loga; <see cref="EDLogo"/> ho škáluje přes Viewbox.</summary>
    internal const double CanvasSize = 64;

    // Kamera: krychle otočená kolem svislé osy, pohled mírně shora, lehká perspektiva
    private const double YawDegrees = 33;
    private const double PitchDegrees = 21;
    private const double PerspectiveDistance = 11;
    private const double CanvasPadding = 1.5;

    // Velikosti v jednotkách krychle (hrana = 2), od nejvzdálenějšího k nejbližšímu prvku
    private const double AtomRadiusFar = 0.30;
    private const double AtomRadiusNear = 0.44;
    private const double BondWidthFar = 0.15;
    private const double BondWidthNear = 0.24;

    // Časová osa skládání (ms)
    private const double AtomStepMs = 110;
    private const double AtomDurationMs = 420;
    private const double PathBondDelayMs = 150;
    private const double PathBondDurationMs = 260;
    private const double ClosingBondDelayMs = 80;
    private const double ClosingBondStepMs = 50;
    private const double ClosingBondDurationMs = 300;

    // Smyčka: složená krychle drží, rozpad = skládání pozpátku v tomto zlomku času, pauza
    internal const double HoldMs = 900;
    internal const double GapMs = 300;
    private const double OutRatio = 0.6;

    // Koule přilétají zvenku (zlomek vzdálenosti od středu) a rostou z tohoto měřítka
    private const double FlyInDistance = 0.35;
    private const double StartScale = 0.2;

    private double _unitScale;

    public CubeAssembly()
    {
        Atoms = CreateAtoms();
        Bonds = CreateBonds();
        DrawOrder = [.. Atoms.Cast<Part>().Concat(Bonds).OrderBy(p => p.DrawKey)];
        CenterX = Atoms.Average(a => a.X);
        CenterY = Atoms.Average(a => a.Y);
        AssemblyMs = DrawOrder.Max(p => p.StartMs + p.DurationMs);
        Apply(0);
    }

    /// <summary>Koule na rozích, index = <see cref="Atom.Vertex"/>.</summary>
    internal IReadOnlyList<Atom> Atoms { get; }

    /// <summary>Vazby po 12 hranách krychle.</summary>
    internal IReadOnlyList<Bond> Bonds { get; }

    /// <summary>Koule i vazby v pořadí kreslení, nejvzdálenější první.</summary>
    internal IReadOnlyList<Part> DrawOrder { get; }

    internal double CenterX { get; }
    internal double CenterY { get; }

    /// <summary>Délka složení (ms).</summary>
    internal double AssemblyMs { get; }

    /// <summary>Délka jedné smyčky: složení, držení, rozpad a pauza (ms).</summary>
    internal double CycleMs => AssemblyMs * (1 + OutRatio) + HoldMs + GapMs;

    // --- Geometrie ---

    /// <summary>Souřadnice rohu: bit 2 = x, bit 1 = y, bit 0 = z; nastavený bit = +1, jinak -1.</summary>
    internal static (double X, double Y, double Z) VertexCoordinates(int vertex) =>
        (((vertex >> 2) & 1) * 2 - 1, ((vertex >> 1) & 1) * 2 - 1, (vertex & 1) * 2 - 1);

    /// <summary>
    /// Pořadí koulí: procházka po hranách krychle přes všechny rohy, končí v <paramref name="lastVertex"/>.
    /// 3bitový Grayův kód 0,1,3,2,6,7,5,4 jde po hranách a končí v rohu 4; XOR s maskou zachová
    /// sousedství a přesune konec do lastVertex.
    /// </summary>
    internal static int[] AssemblyOrder(int lastVertex) =>
        [.. Enumerable.Range(0, 8).Select(i => i ^ (i >> 1) ^ lastVertex ^ 4)];

    private static (double X, double Y, double Z) Project((double X, double Y, double Z) p)
    {
        double yaw = YawDegrees * Math.PI / 180, pitch = PitchDegrees * Math.PI / 180;
        double x1 = p.X * Math.Cos(yaw) + p.Z * Math.Sin(yaw);
        double z1 = -p.X * Math.Sin(yaw) + p.Z * Math.Cos(yaw);
        double y2 = p.Y * Math.Cos(pitch) - z1 * Math.Sin(pitch);
        double z2 = p.Y * Math.Sin(pitch) + z1 * Math.Cos(pitch);
        // Bližší body (vyšší z) se rozestupují víc; Y na obrazovce roste dolů
        double s = PerspectiveDistance / (PerspectiveDistance - z2);
        return (x1 * s, -y2 * s, z2);
    }

    private List<Atom> CreateAtoms()
    {
        var points = Enumerable.Range(0, 8).Select(v => Project(VertexCoordinates(v))).ToArray();
        double zMin = points.Min(p => p.Z), zMax = points.Max(p => p.Z);
        var depth = points.Select(p => (p.Z - zMin) / (zMax - zMin)).ToArray();
        var radius = depth.Select(d => Lerp(AtomRadiusFar, AtomRadiusNear, d)).ToArray();

        // Průmět i s poloměry koulí vycentrovaný do plátna
        double minX = Enumerable.Range(0, 8).Min(v => points[v].X - radius[v]);
        double maxX = Enumerable.Range(0, 8).Max(v => points[v].X + radius[v]);
        double minY = Enumerable.Range(0, 8).Min(v => points[v].Y - radius[v]);
        double maxY = Enumerable.Range(0, 8).Max(v => points[v].Y + radius[v]);
        _unitScale = (CanvasSize - 2 * CanvasPadding) / Math.Max(maxX - minX, maxY - minY);
        double offsetX = (CanvasSize - (maxX - minX) * _unitScale) / 2 - minX * _unitScale;
        double offsetY = (CanvasSize - (maxY - minY) * _unitScale) / 2 - minY * _unitScale;

        var order = AssemblyOrder(Array.IndexOf(depth, depth.Max()));
        return [.. Enumerable.Range(0, 8).Select(v =>
        {
            int position = Array.IndexOf(order, v);
            return new Atom
            {
                Vertex = v,
                X = points[v].X * _unitScale + offsetX,
                Y = points[v].Y * _unitScale + offsetY,
                Radius = radius[v] * _unitScale,
                Depth = depth[v],
                DrawKey = depth[v],
                Order = position,
                StartMs = position * AtomStepMs,
                DurationMs = AtomDurationMs,
            };
        })];
    }

    private List<Bond> CreateBonds()
    {
        var bonds = new List<Bond>();
        for (int a = 0; a < 8; a++)
        {
            for (int b = a + 1; b < 8; b++)
            {
                // Hrana krychle = rohy lišící se právě v jedné souřadnici
                if (BitOperations.PopCount((uint)(a ^ b)) != 1) continue;

                // Vazba roste od koule, která naskočí dřív
                var (from, to) = Atoms[a].Order < Atoms[b].Order ? (Atoms[a], Atoms[b]) : (Atoms[b], Atoms[a]);
                double depth = (from.Depth + to.Depth) / 2;
                bool onPath = to.Order - from.Order == 1;
                bonds.Add(new Bond
                {
                    From = from,
                    To = to,
                    Width = Lerp(BondWidthFar, BondWidthNear, depth) * _unitScale,
                    Depth = depth,
                    // Kreslí se těsně před vzdálenější ze svých koulí - konce vazby obě koule překryjí
                    DrawKey = Math.Min(from.Depth, to.Depth) - 0.001,
                    IsPathBond = onPath,
                    // Vazba na cestě dorůstá k další kouli, zatímco ta naskakuje
                    StartMs = from.Order * AtomStepMs + PathBondDelayMs,
                    DurationMs = PathBondDurationMs,
                });
            }
        }

        // Vazby mimo cestu krychli uzavřou, až jsou všechny koule na místě
        var closing = bonds.Where(b => !b.IsPathBond).OrderBy(b => b.To.Order).ThenBy(b => b.From.Order).ToList();
        for (int i = 0; i < closing.Count; i++)
        {
            closing[i].StartMs = Atoms.Count * AtomStepMs + ClosingBondDelayMs + i * ClosingBondStepMs;
            closing[i].DurationMs = ClosingBondDurationMs;
        }
        return bonds;
    }

    // --- Animace ---

    /// <summary>
    /// Převede čas ve smyčce (0..<see cref="CycleMs"/>) na čas skládání
    /// (0 = nic, <see cref="AssemblyMs"/> = složeno).
    /// </summary>
    internal double TimelineAt(double cycleMs)
    {
        double t = cycleMs % CycleMs;
        if (t < AssemblyMs) return t;
        t -= AssemblyMs;
        if (t < HoldMs) return AssemblyMs;
        t -= HoldMs;
        double outMs = AssemblyMs * OutRatio;
        return t < outMs ? AssemblyMs * (1 - t / outMs) : 0;
    }

    /// <summary>Nastaví stav všech koulí a vazeb pro daný čas skládání.</summary>
    internal void Apply(double timelineMs)
    {
        foreach (var atom in Atoms)
        {
            double p = atom.ProgressAt(timelineMs);
            if (p <= 0)
            {
                atom.Scale = atom.Opacity = atom.OffsetX = atom.OffsetY = 0;
                continue;
            }
            // Naskočí s malým přestřelem, zprůhlední se v prvních 40 % a přiletí zvenku
            atom.Scale = Lerp(StartScale, 1, EaseOutBack(p));
            atom.Opacity = EaseOutCubic(Math.Min(1, p / 0.4));
            double away = (1 - EaseOutCubic(p)) * FlyInDistance;
            atom.OffsetX = (atom.X - CenterX) * away;
            atom.OffsetY = (atom.Y - CenterY) * away;
        }
        foreach (var bond in Bonds)
        {
            double p = bond.ProgressAt(timelineMs);
            bond.Progress = EaseOutCubic(p);
            bond.Opacity = p > 0 ? 1 : 0;
        }
    }

    internal static double EaseOutCubic(double x) => 1 - Math.Pow(1 - x, 3);

    internal static double EaseOutBack(double x)
    {
        const double c1 = 1.70158, c3 = c1 + 1;
        return 1 + c3 * Math.Pow(x - 1, 3) + c1 * Math.Pow(x - 1, 2);
    }

    private static double Lerp(double from, double to, double t) => from + (to - from) * t;

    /// <summary>Společný základ koule a vazby: hloubka, pořadí kreslení, okno v časové ose.</summary>
    internal abstract class Part
    {
        /// <summary>0 = nejdál od diváka, 1 = nejblíž.</summary>
        public double Depth { get; init; }
        public double DrawKey { get; init; }
        public double StartMs { get; set; }
        public double DurationMs { get; set; }
        public double Opacity { get; set; }

        /// <summary>Lineární postup prvku (0..1) v daném čase skládání.</summary>
        public double ProgressAt(double timelineMs) =>
            DurationMs <= 0 ? (timelineMs >= StartMs ? 1 : 0) : Math.Clamp((timelineMs - StartMs) / DurationMs, 0, 1);
    }

    internal sealed class Atom : Part
    {
        public int Vertex { get; init; }
        public double X { get; init; }
        public double Y { get; init; }
        public double Radius { get; init; }
        /// <summary>Pořadí naskočení (0 = první).</summary>
        public int Order { get; init; }
        /// <summary>Přední koule mají sytější barvu; hranice kousek před půlkou jako u ikony.</summary>
        public bool IsFront => Depth > 0.45;
        public double Scale { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
    }

    internal sealed class Bond : Part
    {
        public required Atom From { get; init; }
        public required Atom To { get; init; }
        public double Width { get; init; }
        /// <summary>Vazba mezi koulemi, které naskakují po sobě (ostatní krychli uzavírají).</summary>
        public bool IsPathBond { get; init; }
        /// <summary>Kolik vazby je nakresleno (0..1), roste od <see cref="From"/>.</summary>
        public double Progress { get; set; }
        public double CurrentX2 => From.X + (To.X - From.X) * Progress;
        public double CurrentY2 => From.Y + (To.Y - From.Y) * Progress;
    }
}
