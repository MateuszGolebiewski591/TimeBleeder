using UnityEngine;
using Unity.Netcode;
using System;

public struct BlockData : INetworkSerializable, IEquatable<BlockData>
{
    public int posWidth;
    public int posHeight;
    public bool canBeRaised;

    public BlockData(int width, int height, bool raised)
    {
        posWidth = width;
        posHeight = height;

        canBeRaised = raised;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref posWidth);
        serializer.SerializeValue(ref posWidth);
        serializer.SerializeValue(ref canBeRaised);
    }

    public bool Equals(BlockData other)
    {
        return posWidth == other.posWidth
            && posHeight == other.posHeight
            && canBeRaised == other.canBeRaised;
    }
}