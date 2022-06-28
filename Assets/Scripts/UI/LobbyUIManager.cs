using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine.Events;
using UnityEngine.UIElements;
using UnityEditor;

public class LobbyUIManager : MonoBehaviour
{
    // Singleton
    public static GameManager _instance;
    public static GameManager Instance => _instance;

    [SerializeField] private GameObject canvas;
    [SerializeField] private UIDocument lobbyDocument;
    private Label lobbyCodeLabel;
    private ScrollView playersScrollView;
    private Button readyButton;
    private TextField lobbyNameTextField;
    public UnityAction JoinMatchById;
    private bool initialized = false;

    private void Start()
    {
        // subscribe to events
        GameManager.Instance.LobbyFound += LobbyFound;
        GameManager.Instance.UpdateState += UpdateState;
        // TODO: GameManager.Instance.UpdateLobbyReadyState += UpdateLobbyReadyState;
    }

    private void OnEnable()
    {

    }

    private void InitializeLobbyUI()
    {
        lobbyDocument.enabled = true;
        lobbyNameTextField = lobbyDocument.rootVisualElement.Q<TextField>("lobbyNameTextField");
        lobbyCodeLabel = lobbyDocument.rootVisualElement.Q<Label>("lobbyCodeLabel");
        playersScrollView = lobbyDocument.rootVisualElement.Q<ScrollView>("playersScrollView");
        readyButton = lobbyDocument.rootVisualElement.Q<Button>("readyButton");

        // // setup UI event listeners
        readyButton.RegisterCallback<ClickEvent>(ReadyLobbyPlayerClick);

        initialized = true;
    }

    private void AddPlayersToUI(List<Player> players)
    {
        playersScrollView.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];
            VisualElement playerRow = new VisualElement();
            playerRow.AddToClassList("playerRow");

            GroupBox playerNameGroupBox = new GroupBox();
            playerNameGroupBox.AddToClassList("playerNameGroupBox");
            
            // add host icon if player is host
            IMGUIContainer isHostIcon = new IMGUIContainer();
            isHostIcon.AddToClassList("isHostIcon");
            if (player.Id == GameManager.Instance.currentLobby.HostId)
                isHostIcon.AddToClassList("hostIcon");
            
            playerRow.Add(isHostIcon);

            // add player name (use id for now)
            TextField playerNameTextField = new TextField();
            playerNameTextField.AddToClassList("playerNameTextField");
            playerNameTextField.value = player.Id;
            playerRow.Add(playerNameTextField);

            // add player connection status
            Label playerConnectionStatusLabel = new Label();
            playerConnectionStatusLabel.text = player.Joined != null ? "In Lobby" : "Connecting";
            playerRow.Add(playerConnectionStatusLabel);

            playersScrollView.Add(playerRow);
        }
    }

    private void LobbyFound()
    {
        InitializeLobbyUI(); // queries doc for elements and setup up event listeners
        
        // setup header
        lobbyNameTextField.value = GameManager.Instance.currentLobby.Name;
        lobbyCodeLabel.text = $"Lobby Code: {GameManager.Instance.currentLobby.LobbyCode}";

        // setup player list
        List<Player> players = GameManager.Instance.currentLobby.Players;
        AddPlayersToUI(players);
    }

    private void UpdateState(string newState) 
    {
        List<Player> players = GameManager.Instance.currentLobby?.Players;
        if (players != null && initialized)
            AddPlayersToUI(players);
    }

    #region UI event listeners
    private void ReadyLobbyPlayerClick(ClickEvent evt)
    {
        // TODO: GameManager.Instance.ReadyPlayer(playerId, lobbyId);
    }
    #endregion



    private void OnDisable()
    {
        // unregister UI event listeners    
        readyButton.UnregisterCallback<ClickEvent>(ReadyLobbyPlayerClick);

        // Subscribe to events
        // TODO: GameManager.Instance.UpdateLobbyReadyState += UpdateLobbyReadyState;

        initialized = false;
    }
}