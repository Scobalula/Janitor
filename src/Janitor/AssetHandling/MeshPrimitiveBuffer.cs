using RedFox.Graphics3D;
using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Janitor.AssetHandling;

public class MeshPrimitiveBuffer
{
    public required MeshPrimitive Primitive { get; init; }
    public required MeshAttributeBuffer PositionBuffer { get; init; }
    public required MeshAttributeBuffer NormalBuffer { get; init; }
    public required MeshAttributeBuffer UVBuffer { get; init; }

    public List<MeshAttributeBuffer> BlendIndicesBuffers { get; init; } = [];
    public List<MeshAttributeBuffer> BlendWeightsBuffers { get; init; } = [];

    public List<int> BoneMap { get; init; } = [];

    public int CreateVertexOnOutputMesh(int vertexIndex, Mesh outputMesh)
    {
        var positionX = PositionBuffer.Read<short>(vertexIndex, 0) * (1 / 32767.0f);
        var positionY = PositionBuffer.Read<short>(vertexIndex, 1) * (1 / 32767.0f);
        var positionZ = PositionBuffer.Read<short>(vertexIndex, 2) * (1 / 32767.0f);
        var positionW = PositionBuffer.Read<short>(vertexIndex, 3) * (1 / 32767.0f);
        var packedNorm = NormalBuffer.Read<uint>(vertexIndex, 0);
        var uvX = UVBuffer.Read<float>(vertexIndex, 0) * 0.000244200258748606f;
        var uvY = UVBuffer.Read<float>(vertexIndex, 1) * 0.000244200258748606f;
        // oct xy, high precision part in 16bit position buffer
        var octNormalX = positionW;
        var octNormalY = (packedNorm & 0x3FF) / 1023.0f * 2 - 1;
        var octTangentX = ((packedNorm >> 10) & 0x3FF) / 1023.0f * 2 - 1; // TODO: Tangent
        var octTangentY = ((packedNorm >> 20) & 0x3FF) / 1023.0f * 2 - 1;

        var v = new Vector3(octNormalX, octNormalY, 1.0f - MathF.Abs(octNormalX) - MathF.Abs(octNormalY));

        if (v.Z < 0.0f)
        {
            float newX = (1.0f - MathF.Abs(v[1])) * (v[0] >= 0.0f ? 1.0f : -1.0f);
            float newY = (1.0f - MathF.Abs(v[0])) * (v[1] >= 0.0f ? 1.0f : -1.0f);

            v[0] = newX;
            v[1] = newY;
        }

        outputMesh.Positions!.Add((new Vector3(positionX, positionY, positionZ) * Primitive.Scale + Primitive.Center) * 100);
        outputMesh.Normals!.Add(v);
        outputMesh.UVLayers!.Add(new Vector2(uvX, uvY));

        if (BlendIndicesBuffers.Count > 0)
        {
            var blendIndices = outputMesh.BoneIndices!.Add();
            var blendWeights = outputMesh.BoneWeights!.Add();

            for (int i = 0; i < BlendIndicesBuffers.Count; i++)
            {
                // R32_UINT indices (data type 15) are rigid skinning: one bone per vertex, no weight attribute.
                var rigid = BlendIndicesBuffers[i].Attribute?.DataType == 15;

                for (int j = 0; j < 4; j++)
                {
                    int blendIndex;
                    float blendWeight;

                    if (rigid)
                    {
                        blendIndex = j == 0 ? BlendIndicesBuffers[i].Read<int>(vertexIndex, 0) : 0;
                        blendWeight = j == 0 ? 1.0f : 0.0f;
                    }
                    else
                    {
                        blendIndex = BlendIndicesBuffers[i].Read<int>(vertexIndex, j);
                        blendWeight = i < BlendWeightsBuffers.Count ? BlendWeightsBuffers[i].Read<byte>(vertexIndex, j) / 255.0f : 0.0f;
                    }

                    blendIndices.Add(BoneMap.Count > 0 ? BoneMap[blendIndex] : blendIndex);
                    blendWeights.Add(blendWeight);
                }
            }
        }

        return outputMesh.Positions!.ElementCount;
    }
}
