namespace Tomoshibi.Models;

/// <summary>
/// A rectangular mask over an image-occlusion note's picture, stored in
/// normalised 0–1 coordinates so it survives any display size. Each rect
/// becomes one card (its index is the card's <see cref="Card.Ord"/>).
/// </summary>
public class OcclusionRect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
}
