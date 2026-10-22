namespace Aetherphone.Harness.Profiling;

internal readonly record struct FrameCost(long UpdateBytes, long DrawBytes, double DrawMilliseconds);
