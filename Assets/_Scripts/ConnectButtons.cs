using FishNet;
using UnityEngine;

public class ConnectButtons : MonoBehaviour
{
    public void StartHost()
    {
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();
    }

    public void StartClient()
    {
        InstanceFinder.ClientManager.StartConnection();
    }
}