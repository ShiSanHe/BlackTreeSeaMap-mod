namespace TreeSeaMap.Map;

/// <summary>
/// mulberry32 确定性 PRNG（与 map.html 使用的 JS 版逐位对齐）。
/// 用 uint 算术，溢出自动回绕，等价于 JS 的 int32/uint32 位运算。
/// </summary>
public sealed class Mulberry32
{
    private uint _a;

    public Mulberry32(uint seed)
    {
        _a = seed;
    }

    /// <summary>返回 [0, 1) 的 double。</summary>
    public double Next()
    {
        _a += 0x6D2B79F5u;
        uint t = (_a ^ (_a >> 15)) * (_a | 1u);
        t = (t + ((t ^ (t >> 7)) * (t | 61u))) ^ t;
        return (t ^ (t >> 14)) / 4294967296.0;
    }

    /// <summary>返回 [0, n) 的 int（等价 JS Math.floor(rng()*n)）。</summary>
    public int NextInt(int n) => (int)(Next() * n);
}
