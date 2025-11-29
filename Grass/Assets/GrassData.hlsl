// GrassData.hlsl - FORCED READ VERSION

// Define the buffer directly (No ifdef guards)
StructuredBuffer<float4x4> _PositionBuffer;

void GetMatrix_float(float InstanceID, out float4x4 OutMatrix)
{
    // 1. Force read the data for this ID
    // We cast to uint because array indices must be integers
    OutMatrix = _PositionBuffer[(uint)InstanceID];
}