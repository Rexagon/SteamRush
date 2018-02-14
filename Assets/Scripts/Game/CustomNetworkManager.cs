﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager
{
    public GameController gameController;

    public SpawnPoint[] spawnPoints;

    public PlayerResources resourcesPrefab;

    public Player playerPrefabDefault;
    public Player playerPrefabVR;

    private struct Connection
    {
        public NetworkConnection networkConnection;
        public short playerControllerId;
        public InputType playerInputType;
    }
    private List<Connection> connections = new List<Connection>();

    public void Awake()
    {
        if (gameController == null)
        {
            Debug.LogError("There is no Game Controller on scene!");
        }

        spawnPoints = FindObjectsOfType<SpawnPoint>();
        if (spawnPoints.Length < 2)
        {
            Debug.LogError("There is not enough spawn points!");
        }

        if (GlobalData.networkType == NetworkType.CLIENT)
        {
            networkAddress = GlobalData.connectionAddress;
            networkPort = GlobalData.connectionPort;
            StartClient();
        }
        else
        {
            networkPort = GlobalData.connectionPort;
            StartHost();
        }
    }

    public override void OnServerAddPlayer(NetworkConnection networkConnection, short playerControllerId)
    {
        if (gameController.isRunning) return;

        Connection connection = new Connection();
        connection.networkConnection = networkConnection;
        connection.playerControllerId = playerControllerId;
        connections.Add(connection);

        if (connections.Count == 2)
        {
            StartGame();
        }
    }

    private void StartGame()
    {
        if (connections == null) return;

        if (spawnPoints.Length != 2 || connections.Count != 2)
        {
            Debug.LogError("There must be exact 2 spawn points in the scene!");
            return;
        }

        ShuffleConnections();

        Player[] players = new Player[connections.Count];

        int currentConnectionIndex = 0;
        foreach (SpawnPoint spawnPoint in spawnPoints)
        {
            Connection connection = connections[currentConnectionIndex];

            Transform playerPosition = spawnPoint.transform;
            Player playerPrefab = (connection.playerInputType == InputType.DEFAULT) ? playerPrefabDefault : playerPrefabVR;

            Player player = Instantiate(playerPrefab, playerPosition.position, playerPosition.rotation);

            NetworkServer.AddPlayerForConnection(connection.networkConnection, player.gameObject, connection.playerControllerId);

            players[currentConnectionIndex] = player;
            ++currentConnectionIndex;
        }

        for (int i = 0; i < players.Length; ++i)
        {
            players[i].RpcSetColor((byte)spawnPoints[i].colorId);
            
            PlayerResources resources = Instantiate(resourcesPrefab);
            NetworkServer.Spawn(resources.gameObject);
            players[i].RpcSetResources(resources.gameObject);
            
            spawnPoints[i].castle.RpcSetOwner(players[i].gameObject);
        }
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        base.OnServerDisconnect(conn);

        Debug.Log("Server disconnect");
        
        StopGame();

        StopHost();
    }

    public override void OnClientDisconnect(NetworkConnection conn)
    {
        base.OnClientDisconnect(conn);

        Debug.Log("Client disconnect");

        StopGame();
    }

    private void StopGame()
    {
        connections = new List<Connection>();

        if (client != null)
        {
            GameObject playerGameObject = client.connection.playerControllers[0].gameObject;
            if (playerGameObject != null)
            {
                Player player = playerGameObject.GetComponent<Player>();
                if (player != null)
                {
                    player.LeaveGame();
                }
            }
        }
    }

    private void ShuffleConnections()
    {
        if (connections == null) return;

        int count = connections.Count;
        int last = count - 1;
        for (int i = 0; i < last; ++i)
        {
            var r = Random.Range(i, count);

            var temp = connections[i];
            connections[i] = connections[r];
            connections[r] = temp;
        }
    }
}
