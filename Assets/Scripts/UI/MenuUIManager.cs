using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine.Events;
using UnityEngine.UIElements;
using UnityEditor;

public class MenuUIManager : MonoBehaviour
{
    // Singleton
    public static GameManager _instance;
    public static GameManager Instance => _instance;

    [SerializeField] private GameObject canvas;
    [SerializeField] private UIDocument menuDocument;
    private Label networkStateLabel;
    private ScrollView lobbiesScrollView;
    private Button joinLobbyByCodeButton, createLobbyButton, quickJoinLobbyButton, fetchLobbiesButton;
    private TextField joinLobbyByCodeTextField;
    public UnityAction JoinMatchById;

    private void Start()
    {
        // subscribe to events
        GameManager.Instance.UpdateState += UpdateState;
        GameManager.Instance.UpdateLobbiesState += UpdateLobbiesState;
        GameManager.Instance.LobbyFound += LobbyFound;
    }

    private void OnEnable()
    {
        networkStateLabel = menuDocument.rootVisualElement.Q<Label>("networkStateLabel");
        lobbiesScrollView = menuDocument.rootVisualElement.Q<ScrollView>("lobbiesScrollView");
        joinLobbyByCodeButton = menuDocument.rootVisualElement.Q<Button>("joinLobbyByCodeButton");
        joinLobbyByCodeTextField = menuDocument.rootVisualElement.Q<TextField>("joinLobbyByCodeTextField");
        createLobbyButton = menuDocument.rootVisualElement.Q<Button>("createLobbyButton");
        quickJoinLobbyButton = menuDocument.rootVisualElement.Q<Button>("quickJoinLobbyButton");
        fetchLobbiesButton = menuDocument.rootVisualElement.Q<Button>("fetchLobbiesButton");

        // setup UI event listeners
        createLobbyButton.RegisterCallback<ClickEvent>(CreateLobbyClick);
        quickJoinLobbyButton.RegisterCallback<ClickEvent>(QuickJoinLobbyClick);
        fetchLobbiesButton.RegisterCallback<ClickEvent>(FetchLobbiesClick);
        joinLobbyByCodeButton.RegisterCallback<ClickEvent>(JoinLobbyByCodeClick);
    }

    private void UpdateState(string newState)
    {
        networkStateLabel.text = newState;
    }

    private void LobbyFound() // hide menu UI once lobby is found/joined
    {
        menuDocument.enabled = false;
    }

    private void UpdateLobbiesState(List<Lobby> newState)
    {
        AddLobbiesToUI(newState);
    }

    private void AddLobbiesToUI(List<Lobby> lobbies)
    {
        lobbiesScrollView.Clear();
        for (int i = 0; i < lobbies.Count; i++)
        {
            Lobby lobby = lobbies[i]; 
            Button lobbyButton = new Button(() => GameManager.Instance.JoinLobbyById(lobby.Id));
            lobbyButton.text = $"{lobby.Name} | {lobby.LobbyCode}  {lobby.Players.Count}/{lobby.MaxPlayers}";
            lobbiesScrollView.Add(lobbyButton);
        }
    }

    #region UI event listeners
    private void CreateLobbyClick(ClickEvent evt)
    {
        Debug.Log("CreateLobbyClick");
        GameManager.Instance.CreateLobby();
    }
    private void QuickJoinLobbyClick(ClickEvent evt)
    {
        GameManager.Instance.QuickJoinLobby();
    }
    private void JoinLobbyByCodeClick(ClickEvent evt)
    {   
        string code = joinLobbyByCodeTextField.text;
        GameManager.Instance.JoinLobbyByCode(code);
    }
    private void FetchLobbiesClick(ClickEvent evt)
    {
        GameManager.Instance.FetchAllLobbies();
    }
    #endregion



    private void OnDisable()
    {
        // TODO: remove event listener from each lobby button in scrollview
            // lobbiesScrollView = menuUIDocument.rootVisualElement.Q<ScrollView>("lobbiesScrollView");
        
        // TODO: add JoinLobbyByCode function to GameManager and add + remove event listener for this button
            // joinLobbyByCodeButton.onClick -= GameManager.Instance.
        
        // unregister UI event listeners    
        createLobbyButton.UnregisterCallback<ClickEvent>(CreateLobbyClick);
        quickJoinLobbyButton.UnregisterCallback<ClickEvent>(QuickJoinLobbyClick);
        fetchLobbiesButton.UnregisterCallback<ClickEvent>(FetchLobbiesClick);
        joinLobbyByCodeButton.UnregisterCallback<ClickEvent>(JoinLobbyByCodeClick);

        // Subscribe to events
        GameManager.Instance.UpdateState -= UpdateState;
        GameManager.Instance.UpdateLobbiesState -= UpdateLobbiesState;
        GameManager.Instance.LobbyFound -= LobbyFound;
    }
}