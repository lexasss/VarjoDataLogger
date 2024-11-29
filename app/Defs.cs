namespace VarjoDataLogger;

public class Vector(double x, double y, double z)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;
    public static Vector Zero => new(0, 0, 0);
    public static Vector From(ref readonly Leap.Vector v) => new(v.x, v.y, v.z);
}
public record class Rotation(double Pitch, double Yaw, double Roll)
{
    public static Rotation Zero => new(0, 0, 0);
}

public record class EyeHead(long Timestamp, Rotation Eye, Rotation Head)
{
    public static EyeHead Empty => new(0, Rotation.Zero, Rotation.Zero);
}
