namespace StdbModule;

[SpacetimeDB.Type]
public partial struct DbVector2(float x, float y)
{
	public float x = x;
	public float y = y;

	public float SqrMagnitude => x * x + y * y;

	public float Magnitude => MathF.Sqrt(SqrMagnitude);

	public DbVector2 Normalized => this / Magnitude;

	public static DbVector2 operator +(DbVector2 a, DbVector2 b) => new(a.x + b.x, a.y + b.y);
	public static DbVector2 operator -(DbVector2 a, DbVector2 b) => new(a.x - b.x, a.y - b.y);
	public static DbVector2 operator *(DbVector2 a, float b) => new(a.x * b, a.y * b);
	public static DbVector2 operator /(DbVector2 a, float b) => new(a.x / b, a.y / b);
}