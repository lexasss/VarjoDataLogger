namespace VarjoDataLogger;

public class Vector(double x = 0, double y = 0, double z = 0)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;
}
public record class Rotation(double Pitch, double Yaw, double Roll);
public record class EyeHead(long Timestamp, Rotation Eye, Rotation Head);
