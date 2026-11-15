using System.Numerics;
using Kartster.Driving;

namespace Kartster.Effects;

/// <summary>
/// Where the four tyres touch the ground, in world space: front left, front right, rear left, rear right. From the
/// tuning's axle offsets and track width, around the kart's position (its centre of mass).
/// </summary>
public readonly struct KartWheels
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Count = 4;

    private readonly Vector3 _frontLeft;
    private readonly Vector3 _frontRight;
    private readonly Vector3 _rearLeft;
    private readonly Vector3 _rearRight;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="forward">The kart's forward on the ground, unit length.</param>
    public KartWheels(Vector3 position, Vector3 forward, KartTuning tuning)
    {
        var right = Vector3.Cross(forward, Vector3.UnitY) * (tuning.TrackWidthMeters / 2f);
        var front = position + forward * tuning.FrontAxleOffsetMeters;
        var rear = position - forward * tuning.RearAxleOffsetMeters;
        _frontLeft = front - right;
        _frontRight = front + right;
        _rearLeft = rear - right;
        _rearRight = rear + right;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public Vector3 this[int wheel] => wheel switch
    {
        0 => _frontLeft,
        1 => _frontRight,
        2 => _rearLeft,
        _ => _rearRight
    };

    //**********************************************************
    //** public methods
    //**********************************************************

    public static bool IsFront(int wheel) => wheel < 2;
}
