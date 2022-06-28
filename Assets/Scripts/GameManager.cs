using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    // Singleton
    public static GameManager _instance;
    public static GameManager Instance => _instance;

    public Lobby currentLobby;

    private string _lobbyId;
    private List<Lobby> currentLobbies;
    
    private RelayHostData _hostData;
    private RelayJoinData _joinData;

    // Setup events
    
    // Notify state update
    public UnityAction<string> UpdateState;
    public UnityAction<List<Lobby>> UpdateLobbiesState;
    // Notify Lobby found
    public UnityAction LobbyFound;
    

    private void Awake()
    {
        // Just a basic singleton
        if (_instance is null)
        {
            _instance = this;
            return;
        }
        
        Destroy(this);
    }

    async void Start()
    {
        // Initialize unity services
        await UnityServices.InitializeAsync();
        
        // Setup events listeners
        SetupEvents();
        
        // Unity Login
        await SignInAnonymouslyAsync();
        
        // Subscribe to NetworkManager events
        NetworkManager.Singleton.OnClientConnectedCallback += ClientConnected;

        FetchAllLobbies();
    }

    #region Network events

    private async void ClientConnected(ulong id)
    {
        // Player with id connected to our session
        currentLobby = await Lobbies.Instance.GetLobbyAsync(currentLobby.Id);
        Debug.Log("Connected player with id: " + id);
        
        UpdateState?.Invoke("Client Connected!");
    }

    #endregion
    
    #region UnityLogin

    void SetupEvents() {
        AuthenticationService.Instance.SignedIn += () => {
            // Shows how to get a playerID
            Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}");

            // Shows how to get an access token
            Debug.Log($"Access Token: {AuthenticationService.Instance.AccessToken}");
        };

        AuthenticationService.Instance.SignInFailed += (err) => {
            Debug.LogError(err);
        };

        AuthenticationService.Instance.SignedOut += () => {
            Debug.Log("Player signed out.");
        };
    }
    
    async Task SignInAnonymouslyAsync()
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Sign in anonymously succeeded!");
        }
        catch (Exception ex)
        {
            // Notify the player with the proper error message
            Debug.LogException(ex);
        }
    }

    #endregion

    #region Lobby

    public async void QuickJoinLobby()
    {
        Debug.Log("Looking for a lobby...");

        UpdateState?.Invoke("Looking for a lobby...");
        
        try
        {
            // Looking for a lobby
            
            // Add options to the matchmaking (mode, rank, etc..)
            QuickJoinLobbyOptions options = new QuickJoinLobbyOptions();

            // Quick-join a random lobby
            Lobby lobby = await Lobbies.Instance.QuickJoinLobbyAsync(options);
            currentLobby = lobby;

            Debug.Log("Joined lobby: " + lobby.Id);
            Debug.Log("Lobby Players: " + lobby.Players.Count);
            
            // Retrieve the Relay code previously set in the create lobby
            string joinCode = lobby.Data["joinCode"].Value;
                
            Debug.Log("Received code: " + joinCode);
            
            JoinAllocation allocation = await Relay.Instance.JoinAllocationAsync(joinCode);
                
            await RelaySetup(joinCode);
                
            // Finally start the client
            NetworkManager.Singleton.StartClient();
            
            // Trigger events
            UpdateState?.Invoke("Lobby found!");
            LobbyFound?.Invoke();
        }
        catch (LobbyServiceException e)
        {
            // If we don't find any lobby, let's create a new one
            Debug.Log("Cannot find a lobby: " + e);
            CreateLobby();
        }
    }

    public async void CreateLobby()
    {
        Debug.Log("Creating a new lobby...");
        
        UpdateState?.Invoke("Creating a new lobby...");
        
        // External connections
        int maxConnections = 1;
        
        try
        {
            // Create RELAY object
            Allocation allocation = await Relay.Instance.CreateAllocationAsync(maxConnections);
            _hostData = new RelayHostData
            {
                Key = allocation.Key,
                Port = (ushort) allocation.RelayServer.Port,
                AllocationID = allocation.AllocationId,
                AllocationIDBytes = allocation.AllocationIdBytes,
                ConnectionData = allocation.ConnectionData,
                IPv4Address = allocation.RelayServer.IpV4
            };
            
            // Retrieve JoinCode
            _hostData.JoinCode = await Relay.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            string lobbyName = "game_lobby";
            int maxPlayers = 2;
            CreateLobbyOptions options = new CreateLobbyOptions();
            options.IsPrivate = false;
            
            // Put the JoinCode in the lobby data, visible by every member if private, otherwise public
            DataObject.VisibilityOptions visibility = options.IsPrivate == true
                ? DataObject.VisibilityOptions.Member : DataObject.VisibilityOptions.Public;
            
            options.Data = new Dictionary<string, DataObject>()
            {
                {
                    "joinCode", new DataObject(
                        visibility,
                        value: _hostData.JoinCode)
                },
            };

            // Create the lobby
            var lobby = await Lobbies.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            currentLobby = lobby;
            
            // Save Lobby ID for later uses
            _lobbyId = lobby.Id;
            
            Debug.Log("Created lobby: " + lobby.Id);
            
            // Heartbeat the lobby every 15 seconds.
            StartCoroutine(HeartbeatLobbyCoroutine(lobby.Id, 15));
            
            // Now that RELAY and LOBBY are set...
            
            // Set Transports data
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                _hostData.IPv4Address, 
                _hostData.Port, 
                _hostData.AllocationIDBytes, 
                _hostData.Key, 
                _hostData.ConnectionData);
                
            // Finally start host
            NetworkManager.Singleton.StartHost();
            
            UpdateState?.Invoke("Waiting for players...");
            LobbyFound?.Invoke();
        }
        catch (LobbyServiceException e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async void JoinLobbyByCode(string code) 
    {
        try {
            var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code);
            currentLobby = lobby;

            Debug.Log("Joined lobby: " + lobby.Id);
            Debug.Log("Lobby Players: " + lobby.Players.Count);
            
            // Retrieve the Relay code previously set in the create lobby
            string joinCode = lobby.Data["joinCode"].Value;
                
            Debug.Log("Received code: " + joinCode);
            
            await RelaySetup(joinCode);

            // Finally start the client
            NetworkManager.Singleton.StartClient();
            
            // Trigger events
            UpdateState?.Invoke("Lobby found!");
            LobbyFound?.Invoke();
        } 
        catch (LobbyServiceException e) 
        {
            Debug.Log($"Could not join lobby by code: {e}");
        }   
    }

    public async void JoinLobbyById(string id) 
    {
        Debug.Log("Looking for a lobby...");

        UpdateState?.Invoke("Looking for a lobby...");
        
        try
        {
            // Looking for a lobby
            
            // Add options to the matchmaking (mode, rank, etc..)
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions();

            // join a lobby by its id
            Lobby lobby = await Lobbies.Instance.JoinLobbyByIdAsync(id);
            currentLobby = lobby;
                
            Debug.Log("Joined lobby: " + lobby.Id);
            Debug.Log("Lobby Players: " + lobby.Players.Count);
            
            // Retrieve the Relay code previously set in the create lobby
            string joinCode = lobby.Data["joinCode"].Value;
                
            Debug.Log("Received code: " + joinCode);
            
            await RelaySetup(joinCode);

            // Finally start the client
            NetworkManager.Singleton.StartClient();
            
            // Trigger events
            UpdateState?.Invoke("Lobby found!");
            LobbyFound?.Invoke();
        }
        catch (LobbyServiceException e)
        {
            // If we don't find any lobby, let's create a new one
            string errorText = $"Cannot join lobby with id '{id}'";
            Debug.Log( $"{errorText}: ${e}");
            UpdateState?.Invoke(errorText);
        } 
    }

    // perform relay setup necessary to join a lobby
    private async Task RelaySetup(string joinCode) 
    {
            JoinAllocation allocation = await Relay.Instance.JoinAllocationAsync(joinCode);
                
            // Create Object
            _joinData = new RelayJoinData
            {
                Key = allocation.Key,
                Port = (ushort) allocation.RelayServer.Port,
                AllocationID = allocation.AllocationId,
                AllocationIDBytes = allocation.AllocationIdBytes,
                ConnectionData = allocation.ConnectionData,
                HostConnectionData = allocation.HostConnectionData,
                IPv4Address = allocation.RelayServer.IpV4
            };

            // Set transport data
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                _joinData.IPv4Address, 
                _joinData.Port, 
                _joinData.AllocationIDBytes, 
                _joinData.Key, 
                _joinData.ConnectionData, 
                _joinData.HostConnectionData);
    }
    
    IEnumerator HeartbeatLobbyCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
            Debug.Log("Lobby Heartbeat");
            yield return delay;
        }
    }
    
    private void OnDestroy()
    {
        // We need to delete the lobby when we're not using it
        Lobbies.Instance.DeleteLobbyAsync(_lobbyId);
    }

    #endregion

    #region Matchmaking
    public async void FetchAllLobbies() 
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions();
            options.Count = 25;

            // Filter for open lobbies only
            options.Filters = new List<QueryFilter>()
            {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.AvailableSlots,
                    op: QueryFilter.OpOptions.GT,
                    value: "0")
            };

            // Order by newest lobbies first
            options.Order = new List<QueryOrder>()
            {
                new QueryOrder(
                    asc: false,
                    field: QueryOrder.FieldOptions.Created)
            };

            QueryResponse lobbies = await Lobbies.Instance.QueryLobbiesAsync(options);
            currentLobbies = lobbies.Results;
            UpdateLobbiesState?.Invoke(lobbies.Results);
            //...
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }
    #endregion
    
    /// <summary>
    /// RelayHostData represents the necessary informations
    /// for a Host to host a game on a Relay
    /// </summary>
    public struct RelayHostData
    {
        public string JoinCode;
        public string IPv4Address;
        public ushort Port;
        public Guid AllocationID;
        public byte[] AllocationIDBytes;
        public byte[] ConnectionData;
        public byte[] Key;
    }
    
    /// <summary>
    /// RelayHostData represents the necessary informations
    /// for a Host to host a game on a Relay
    /// </summary>
    public struct RelayJoinData
    {
        public string JoinCode;
        public string IPv4Address;
        public ushort Port;
        public Guid AllocationID;
        public byte[] AllocationIDBytes;
        public byte[] ConnectionData;
        public byte[] HostConnectionData;
        public byte[] Key;
    }
}