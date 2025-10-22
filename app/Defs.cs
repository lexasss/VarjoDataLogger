using System.Text.Json;

namespace VarjoDataLogger;

public class Vector(double x, double y, double z)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;
    public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);
    public bool IsZero => X == 0 && Y == 0 && Z == 0;
    public Vector Copy() => new(X, Y, Z);
    public static Vector Zero => new(0, 0, 0);
    public static Vector From(ref readonly Leap.Vector v) => new(v.x, v.y, v.z);
    public static Vector operator / (Vector obj, double div) => new(obj.X / div, obj.Y / div, obj.Z / div);
    public static Vector operator * (Vector obj, double mul) => new(obj.X * mul, obj.Y * mul, obj.Z * mul);
}

public record class Rotation(double Pitch, double Yaw, double Roll)
{
    public static Rotation Zero => new(0, 0, 0);
}

public record class Pupil(float OpennessLeft, float SizeLeft, float OpennessRight, float SizeRight)
{
    public static Pupil Zero => new(0, 0, 0, 0);
}

public record class EyeHead(long Timestamp, Rotation Eye, Rotation Head, Pupil Pupil)
{
    public static EyeHead Empty => new(0, Rotation.Zero, Rotation.Zero, Pupil.Zero);
}

public class HandLocation(Vector palm, Vector thumb, Vector index, Vector middle)
{
    public Vector Palm { get; set; } = palm;
    public Vector Thumb { get; set; } = thumb;
    public Vector Index { get; set; } = index;
    public Vector Middle { get; set; } = middle;
    public bool IsEmpty => Palm.IsZero && Thumb.IsZero && Index.IsZero && Middle.IsZero;
    public HandLocation() : this(Vector.Zero, Vector.Zero, Vector.Zero, Vector.Zero) { }
    public string AsJson() => JsonSerializer.Serialize(this);
    public void CopyTo(HandLocation rhs)
    {
        rhs.Palm = Palm.Copy();
        rhs.Thumb = Thumb.Copy();
        rhs.Index = Index.Copy();
        rhs.Middle = Middle.Copy();
    }
    public HandLocation Copy()
    {
        HandLocation copy = new();
        CopyTo(copy);
        return copy;
    }

    public static HandLocation Empty => new();
    public static HandLocation? FromJson(string json)
    {
        HandLocation? result = null;

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                result = JsonSerializer.Deserialize<HandLocation>(json);
                App.Debug.WriteLine($"HAND {result?.Palm.X} {result?.Palm.Y} {result?.Palm.Z}");
            }
            catch
            {
                App.Debug.WriteLine($"ERROR in {json}");

                var records = json.Split('\n');
                for (int i = records.Length - 1; i >= 0; i--)
                {
                    try
                    {
                        result = JsonSerializer.Deserialize<HandLocation>(records[i]);
                        if (result is not null)
                        {
                            App.Debug.WriteLine($"  RESTORED from {i+1}/{records.Length}");
                            break;
                        }
                    }
                    catch
                    {
                        App.Debug.WriteLine($"  FAILED at {i+1}: {json}");
                    }
                }
            }
        }

        return result;
    }
}
