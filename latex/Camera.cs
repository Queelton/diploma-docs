using Amethyst_game_engine.Core.Utilities;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Amethyst_game_engine.Core.CameraModule;

public sealed class Camera : IDisposable
{
    private float _aspectRatio;
    private float _yaw = -float.Pi / 2.0f;
    private float _orthographicBorder;
    private float _fov;
    private float _pitch;

    private readonly CameraTypes _type;

    private readonly unsafe float* _viewMatrix = (float*)Marshal.AllocHGlobal(Mathematics.MATRIX_SIZE);
    private readonly unsafe float* _projectionMatrix = (float*)Marshal.AllocHGlobal(Mathematics.MATRIX_SIZE);

    public string? Tag { get; set; }
    public float Near { get; set; }
    public float Far { get; set; }
    public Vector3 Position { get; set; }

    public Vector3 Up { get; private set; } = Vector3.UnitY;
    public Vector3 RightVector { get; private set; } = Vector3.UnitX;
    public Vector3 Front { get; private set; } = -Vector3.UnitZ;

    public float RightSide { get; set; }
    public float LeftSide { get; set; }
    public float BottomSide { get; set; }
    public float TopSide { get; set; }

    public float AspectRatio
    {
        get => _aspectRatio;
        set => _aspectRatio = value;
    }

    public float Fov
    {
        get => Mathematics.RadiansToDegrees(_fov);
        set => _fov = Mathematics.DegreesToRadians(Mathematics.Clamp(value, -180.0f, 180.0f));
    }

    public float Yaw
    {
        get => Mathematics.RadiansToDegrees(_yaw);

        set
        {
            _yaw = Mathematics.DegreesToRadians(value);
            CalculateVectors();
        }
    }

    public float Pitch
    {
        get => Mathematics.RadiansToDegrees(_pitch);

        set
        {
            _pitch = Mathematics.DegreesToRadians(Mathematics.Clamp(value, -89.9f, 89.9f));
            CalculateVectors();
        }
    }

    public float OrthographicBorders
    {
        get => _orthographicBorder;

        set
        {
            _orthographicBorder = value;

            LeftSide = -value * _aspectRatio;
            RightSide = value * _aspectRatio;
            TopSide = value / _aspectRatio;
            BottomSide = -value / _aspectRatio;
        }
    }

    internal unsafe float* ProjectionMatrix
    {
        get
        {
            if (_type == CameraTypes.Perspective)
            {
                var scaleY = 1.0f / MathF.Tan(_fov / 2.0f);
                var scaleX = scaleY / _aspectRatio;

                var item1 = -((Far + Near) / (Far - Near));
                var item2 = -(2.0f * Far * Near / (Far - Near));

                *_projectionMatrix = scaleX;
                *(_projectionMatrix + 5) = scaleY;
                *(_projectionMatrix + 10) = item1;
                *(_projectionMatrix + 11) = item2;
                *(_projectionMatrix + 14) = -1.0f;
            }

            else
            {
                *_projectionMatrix = 2.0f / (RightSide - LeftSide);
                *(_projectionMatrix + 3) = -((RightSide + LeftSide) / (RightSide - LeftSide));
                *(_projectionMatrix + 5) = 2.0f / (TopSide - BottomSide);
                *(_projectionMatrix + 7) = -((TopSide + BottomSide) / (TopSide - BottomSide));
                *(_projectionMatrix + 10) = -(2.0f / (Far - Near));
                *(_projectionMatrix + 11) = -((Far + Near) / (Far - Near));
                *(_projectionMatrix + 15) = 1.0f;
            }

            return _projectionMatrix;
        }
    }

    internal unsafe float* ViewMatrix
    {
        get
        {
            Vector3 row0 = new(RightVector.X, RightVector.Y, RightVector.Z);
            Vector3 row1 = new(Up.X, Up.Y, Up.Z);
            Vector3 row2 = new(-Front.X, -Front.Y, -Front.Z);

            float* matrixA = stackalloc float[16]
            {
                RightVector.X, RightVector.Y, RightVector.Z, 0.0f,
                Up.X,          Up.Y,          Up.Z,          0.0f,
               -Front.X,      -Front.Y,      -Front.Z,       0.0f,
                0.0f,          0.0f,          0.0f,          1.0f
            };

            float* matrixB = stackalloc float[16]
            {
                 1.0f, 0.0f, 0.0f, -Position.X,
                 0.0f, 1.0f, 0.0f, -Position.Y,
                 0.0f, 0.0f, 1.0f, -Position.Z,
                 0.0f, 0.0f, 0.0f,  1.0f
            };

            Mathematics.MultiplyMatrices4(matrixA, matrixB, _viewMatrix);

            return _viewMatrix;
        }
    }

    public Camera(CameraTypes type, Vector3 position, float aspectRatio)
    {
        unsafe
        {
            Unsafe.InitBlock(_viewMatrix, 0, Mathematics.MATRIX_SIZE);
            Unsafe.InitBlock(_projectionMatrix, 0, Mathematics.MATRIX_SIZE);
        }

        _type = type;
        _aspectRatio = aspectRatio;

        Position = position;
        Near = 1.0f;
        Far = 5000.0f;

        if (type == CameraTypes.Orthographic)
            OrthographicBorders = 500.0f;
        else
            _fov = 0.7854f;
    }

    internal unsafe Vector3 GetPickRay(float mouseX, float mouseY, int screenWidth, int screenHeight)
    {
        float x = (2.0f * mouseX) / screenWidth - 1.0f;
        float y = 1.0f - (2.0f * mouseY) / screenHeight;

        float* viewPtr = ViewMatrix;
        float* projPtr = ProjectionMatrix;

        Matrix4 viewMatrix = new(viewPtr[0], viewPtr[4], viewPtr[8], viewPtr[12],
                                 viewPtr[1], viewPtr[5], viewPtr[9], viewPtr[13],
                                 viewPtr[2], viewPtr[6], viewPtr[10], viewPtr[14],
                                 viewPtr[3], viewPtr[7], viewPtr[11], viewPtr[15]);

        Matrix4 projectionMatrix = new(projPtr[0], projPtr[4], projPtr[8], projPtr[12],
                                       projPtr[1], projPtr[5], projPtr[9], projPtr[13],
                                       projPtr[2], projPtr[6], projPtr[10], projPtr[14],
                                       projPtr[3], projPtr[7], projPtr[11], projPtr[15]);

        Matrix4 invVP = Matrix4.Invert(projectionMatrix * viewMatrix);

        Vector4 rayStart_NDS = new(x, y, -1.0f, 1.0f);
        Vector4 rayEnd_NDS = new(x, y, 1.0f, 1.0f);

        Vector4 rayStartWorld = invVP * rayStart_NDS;
        rayStartWorld /= rayStartWorld.W;
        Vector4 rayEndWorld = invVP * rayEnd_NDS;
        rayEndWorld /= rayEndWorld.W;

        Vector3 rayDirection = Vector3.Normalize(new Vector3(
            rayEndWorld.X - rayStartWorld.X,
            rayEndWorld.Y - rayStartWorld.Y,
            rayEndWorld.Z - rayStartWorld.Z
        ));

        return rayDirection;
    }

    private void CalculateVectors()
    {
        var x = MathF.Cos(_pitch) * MathF.Cos(_yaw);
        var y = MathF.Sin(_pitch);
        var z = MathF.Cos(_pitch) * MathF.Sin(_yaw);

        Front = Vector3.Normalize(new Vector3(x, y, z));
        RightVector = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
        Up = Vector3.Cross(RightVector, Front);
    }

    internal void Cleanup()
    {
        unsafe
        {
            Marshal.FreeHGlobal((nint)_viewMatrix);
            Marshal.FreeHGlobal((nint)_projectionMatrix);
        }
    }

    void IDisposable.Dispose()
    {
        Cleanup();
    }
}
