using UnityEngine;
using Unity.Netcode;
using System;

public struct BlockData : INetworkSerializable, IEquatable<BlockData>
{
    public int posWidth;
    public int posHeight;
    public bool canBeRaised;
    public bool isFloor;

    public BlockData(int width, int height, bool floor, bool canBeRaisedIn)
    {
        posWidth = width;
        posHeight = height;
        isFloor = floor;
        canBeRaised = canBeRaisedIn; //Output of Tarjans
        if (!isFloor) canBeRaised = false;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref posWidth);
        serializer.SerializeValue(ref posHeight);
        serializer.SerializeValue(ref canBeRaised);
        serializer.SerializeValue(ref isFloor);
    }

    public bool Equals(BlockData other)
    {
        return posWidth == other.posWidth
            && posHeight == other.posHeight
            && canBeRaised == other.canBeRaised
            && isFloor == other.isFloor;
    }
}