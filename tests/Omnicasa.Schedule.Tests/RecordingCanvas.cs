using System.Numerics;
using Microsoft.Maui.Graphics;

namespace Omnicasa.Schedule.Tests;

/// <summary>
/// An <see cref="ICanvas"/> test double that records the drawing calls the renderers make, so
/// painting can be asserted on a desktop test host without a real graphics surface.
/// Extension helpers (FillCircle, FillRoundedRectangle(RectF, …), DrawString(value, RectF, …))
/// route into these core members, so recording the core surface captures them too.
/// </summary>
internal sealed class RecordingCanvas : ICanvas
{
    public List<string> Ops { get; } = new List<string>();

    public List<RectF> FilledRoundedRectangles { get; } = new List<RectF>();

    public List<RectF> FilledRectangles { get; } = new List<RectF>();

    public List<(float X, float Y, float W, float H)> FilledEllipses { get; } = new List<(float, float, float, float)>();

    public List<string> Strings { get; } = new List<string>();

    public int DrawLineCount { get; private set; }

    public int SaveStateCount { get; private set; }

    public int RestoreStateCount { get; private set; }

    public int ShadowCount { get; private set; }

    public Color? LastFillColor { get; private set; }

    // ---- Mutable state properties (set-only on the interface) ----
    public float DisplayScale { get; set; } = 1f;

    public float StrokeSize { set { } }

    public float MiterLimit { set { } }

    public Color StrokeColor { set { } }

    public LineCap StrokeLineCap { set { } }

    public LineJoin StrokeLineJoin { set { } }

    public float[] StrokeDashPattern { set { } }

    public float StrokeDashOffset { set { } }

    public Color FillColor { set => LastFillColor = value; }

    public Color FontColor { set { } }

    public IFont Font { set { } }

    public float FontSize { set { } }

    public float Alpha { set { } }

    public bool Antialias { set { } }

    public BlendMode BlendMode { set { } }

    // ---- Drawing ----
    public void DrawLine(float x1, float y1, float x2, float y2)
    {
        DrawLineCount++;
        Ops.Add("DrawLine");
    }

    public void DrawArc(float x, float y, float width, float height, float startAngle, float endAngle, bool clockwise, bool closed) => Ops.Add("DrawArc");

    public void FillArc(float x, float y, float width, float height, float startAngle, float endAngle, bool clockwise) => Ops.Add("FillArc");

    public void DrawRectangle(float x, float y, float width, float height) => Ops.Add("DrawRectangle");

    public void FillRectangle(float x, float y, float width, float height)
    {
        FilledRectangles.Add(new RectF(x, y, width, height));
        Ops.Add("FillRectangle");
    }

    public void DrawRoundedRectangle(float x, float y, float width, float height, float cornerRadius) => Ops.Add("DrawRoundedRectangle");

    public void FillRoundedRectangle(float x, float y, float width, float height, float cornerRadius)
    {
        FilledRoundedRectangles.Add(new RectF(x, y, width, height));
        Ops.Add("FillRoundedRectangle");
    }

    public void DrawEllipse(float x, float y, float width, float height) => Ops.Add("DrawEllipse");

    public void FillEllipse(float x, float y, float width, float height)
    {
        FilledEllipses.Add((x, y, width, height));
        Ops.Add("FillEllipse");
    }

    public void DrawString(string value, float x, float y, HorizontalAlignment horizontalAlignment)
    {
        Strings.Add(value);
        Ops.Add("DrawString");
    }

    public void DrawString(
        string value,
        float x,
        float y,
        float width,
        float height,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment,
        TextFlow textFlow = TextFlow.ClipBounds,
        float lineSpacingAdjustment = 0)
    {
        Strings.Add(value);
        Ops.Add("DrawString");
    }

    public void DrawText(Microsoft.Maui.Graphics.Text.IAttributedText value, float x, float y, float width, float height) => Ops.Add("DrawText");

    public SizeF GetStringSize(string value, IFont font, float fontSize) => new SizeF((value?.Length ?? 0) * fontSize, fontSize);

    public SizeF GetStringSize(string value, IFont font, float fontSize, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
        => new SizeF((value?.Length ?? 0) * fontSize, fontSize);

    public void SetShadow(SizeF offset, float blur, Color color)
    {
        ShadowCount++;
        Ops.Add("SetShadow");
    }

    public void SetFillPaint(Paint paint, RectF rectangle) => Ops.Add("SetFillPaint");

    public void DrawImage(Microsoft.Maui.Graphics.IImage image, float x, float y, float width, float height) => Ops.Add("DrawImage");

    public void DrawPath(PathF path) => Ops.Add("DrawPath");

    public void FillPath(PathF path, WindingMode windingMode) => Ops.Add("FillPath");

    public void ClipPath(PathF path, WindingMode windingMode = WindingMode.NonZero) => Ops.Add("ClipPath");

    public void ClipRectangle(float x, float y, float width, float height) => Ops.Add("ClipRectangle");

    public void SubtractFromClip(float x, float y, float width, float height) => Ops.Add("SubtractFromClip");

    public void SaveState()
    {
        SaveStateCount++;
        Ops.Add("SaveState");
    }

    public bool RestoreState()
    {
        RestoreStateCount++;
        Ops.Add("RestoreState");
        return true;
    }

    public void ResetState() => Ops.Add("ResetState");

    public void Rotate(float degrees, float x, float y) => Ops.Add("Rotate");

    public void Rotate(float degrees) => Ops.Add("Rotate");

    public void Scale(float sx, float sy) => Ops.Add("Scale");

    public void Translate(float tx, float ty) => Ops.Add("Translate");

    public void ConcatenateTransform(Matrix3x2 transform) => Ops.Add("ConcatenateTransform");
}
