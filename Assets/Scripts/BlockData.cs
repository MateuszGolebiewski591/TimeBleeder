using UnityEngine;
using Unity.Netcode;
using System;

public struct BlockData : INetworkSerializable, IEquatable<BlockData>
{
    public Vector3 pos;
    public bool CanBeRaised;
    public bool CanBeLowered;
    public bool CanBeOccupied;

    public BlockData(Vector3 position, bool raised, bool lowered, bool occupied)
    {
        pos = position;
        CanBeRaised = raised;
        CanBeLowered = lowered;
        CanBeOccupied = occupied;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref pos);
        serializer.SerializeValue(ref CanBeRaised);
        serializer.SerializeValue(ref CanBeLowered);
        serializer.SerializeValue(ref CanBeOccupied);
    }

    public bool Equals(BlockData other)
    {
        return pos.Equals(other.pos)
            && CanBeRaised == other.CanBeRaised
            && CanBeLowered == other.CanBeLowered
            && CanBeOccupied == other.CanBeOccupied;
    }
}