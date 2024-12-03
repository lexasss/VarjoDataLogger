namespace VarjoDataLogger;

public class Vector(double x, double y, double z)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;
    public static Vector Zero => new(0, 0, 0);
    public static Vector From(ref readonly Leap.Vector v) => new(v.x, v.y, v.z);
    public static Vector operator / (Vector obj, double div) => new Vector(obj.X / div, obj.Y / div, obj.Z / div);
    public static Vector operator * (Vector obj, double mul) => new Vector(obj.X * mul, obj.Y * mul, obj.Z * mul);
}
public record class Rotation(double Pitch, double Yaw, double Roll)
{
    public static Rotation Zero => new(0, 0, 0);
}

public record class EyeHead(long Timestamp, Rotation Eye, Rotation Head)
{
    public static EyeHead Empty => new(0, Rotation.Zero, Rotation.Zero);
}

public class HandLocation(Vector palm, Vector thumb, Vector index, Vector middle)
{
    public Vector Palm { get; set; } = palm;
    public Vector Thumb { get; set; } = thumb;
    public Vector Index { get; set; } = index;
    public Vector Middle { get; set; } = middle;
    public HandLocation() : this(Vector.Zero, Vector.Zero, Vector.Zero, Vector.Zero) { }
    public void CopyTo(HandLocation rhs)
    {
        rhs.Palm = Palm;
        rhs.Thumb = Thumb;
        rhs.Index = Index;
        rhs.Middle = Middle;
    }
}
