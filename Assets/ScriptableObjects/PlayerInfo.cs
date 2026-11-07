using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInfo", menuName = "Scriptable Objects/PlayerInfo")]
public class PlayerInfo : ScriptableObject
{
    public Vector3 mainPosition;
    public Vector3 otherPosition;
    public Quaternion angle;
}
