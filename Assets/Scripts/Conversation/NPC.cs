﻿using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

// This is a base class meant to be used for all NPC characters
public class NPC : MonoBehaviour
{
    private static int RelationshipLowerBound = 0;
    private static int RelationshipUpperBound = 10;

    public enum relationshipStatus {hate, dislike, neutral, like};

    // NPC traits
    public string characterName;
    public int id;

    private int dislikeThreshold;
    private int likeThreshold;
    private int neutralThreshold;
    private relationshipStatus relStat;
    private int relationshipValue;

    // audio related
    [HideInInspector]
    public string audioFolder;

    public AudioSource conversationAudio;
    public AudioSource playerAudio;
    public SoundtrackLayer soundtrackLayer;

    // database related
    public DatabaseHandler dbHandler;

    // conversation related
    private Accusation accusation;
    public AccusationLights accusationLights;
    public bool isAccusing = false;
    private int promptID;
    private int[] responseIDs = new int[4];

    // UI related
    private ConversationUI conversationUI;

    // Trying something new
    private Dictionary<int, int> choiceToPromptID = new Dictionary<int, int>();
    private int chosenConversationPrompt;
    private HashSet<int> startingPromptIDs = new HashSet<int>();

    public PromptHandler promptHandler;

    public void AddAvailableConversation(int promptID)
    {
        startingPromptIDs.Add(promptID);

        if (this.promptID < 60)
        {
            UpdateNextPrompt(promptID);
        }
    }

    public bool CanTalk()
    {
        foreach (int startingPrompt in startingPromptIDs)
        {
            if (startingPrompt != -1)
            {
                return true;
            }
        }

        return false;
    }

    // Returns true if there are options after the current prompt, false if not.
    private bool CheckCurrentPrompt()
    {
        foreach (int id in responseIDs)
        {
            if (id > -1)
            {
                return true;
            }
        }

        RemoveAvailableConversation(promptID);
        return false;
    }

    // Gets the relevant information from the database about the chosen
    // response and moves the conversation along
    public void ChooseResponse(int choice)
    {
        conversationUI.ClearOptions();

        string query = "SELECT NextPromptID, AudioFile, RelationshipEffect" +
                " FROM Responses WHERE ID ==" + responseIDs[choice - 1];
        IDataReader reader = dbHandler.ExecuteQuery(query);

        reader.Read();
        int nextPromptID = reader.GetInt32(0);
        string responseAudio = reader.IsDBNull(1) ? "" : reader.GetString(1);
        int relationshipEffect = reader.GetInt32(2);
        reader.Close();

        // Play the voice line for the response
        if (responseAudio != "")
        {
            string responseAudioSource = "Audio/Player/" + responseAudio;
            conversationUI.PlayAudio(playerAudio, responseAudioSource);
        }

        UpdateRelationshipValue(relationshipEffect);
        UpdateNextPrompt(nextPromptID, choice);

        if (nextPromptID != -1)
        {
            StartCoroutine(WritePrompt());
        }
        else
        {
            conversationUI.EndConversation();
        }
    }

    private void ClearResponseIDs()
    {
        for (int i = 0; i < 4; i++)
        {
            responseIDs[i] = -1;
        }
    }

    public void ContinueAccusation(int choice)
    {
        isAccusing = accusation.SelectChoice(choice);
        if (!isAccusing)
        {
            conversationUI.EndConversation(true, accusationLights);
        }
    }

    public bool HasStartingPrompt(int prompt)
    {
        return startingPromptIDs.Contains(prompt);
    }

    public void InitializeConversation()
    {
        dbHandler.SetUpDatabase();

        // Finds the ID of the initial prompt
        string query = "SELECT PromptID, AudioFolder, RelationshipValue, DislikeThreshold, " +
            "NeutralThreshold, LikeThreshold FROM 'Characters' WHERE ID == " + id;
        IDataReader reader = dbHandler.ExecuteQuery(query);

        reader.Read();
        promptID = reader.GetInt32(0);
        audioFolder = reader.GetString(1);
        relationshipValue = reader.GetInt32(2);
        dislikeThreshold = reader.GetInt32(3);
        neutralThreshold = reader.GetInt32(4);
        likeThreshold = reader.GetInt32(5);
        reader.Close();

        //UpdateRelationshipStatus();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (CanTalk())
        {
            conversationUI.PromptForConversation(other, characterName);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        conversationUI.ExitConversation(other, isAccusing, accusationLights);
    }

    public int PromptID()
    {
        return promptID;
    }

    public void RemoveAvailableConversation(int promptID)
    {
        startingPromptIDs.Remove(promptID);

        int newPromptID = -1;
        if (promptID == this.promptID)
        {
            if (startingPromptIDs.Count > 1)
            {
                foreach (int id in startingPromptIDs)
                {
                    if (id != -1)
                    {
                        newPromptID = id;
                        break;
                    }
                }
            }
        }

        UpdateNextPrompt(newPromptID);
    }

    public void Start()
    {
        accusation = gameObject.GetComponent<Accusation>();
        conversationAudio = gameObject.GetComponent<AudioSource>();
        conversationUI = gameObject.GetComponent<ConversationUI>();
    }

    public void StartAccusation()
    {
        isAccusing = true;
        accusationLights.TurnOn();
        accusation.StartAccusation();
    }

    public void StartConversation()
    {
        conversationUI.ClearDisplay();
        InitializeConversation();

        if (conversationAudio.isPlaying)
        {
            conversationAudio.Stop();
        }

        StartConversationPrompt();
    }

    // This will only work if the detective has the first line for every conversation
    public void StartConversationPrompt()
    {
        // Add the current promptID to the starting ID set
        startingPromptIDs.Add(promptID);
        ClearResponseIDs();
        choiceToPromptID.Clear();

        int currentResNum = 0;
        // Can add up to but no more than 4 separate conversations
        foreach (int id in startingPromptIDs)
        {
            if (id > -1)
            {
                string query = "SELECT Response1ID FROM Prompts WHERE ID ==" + id;
                IDataReader reader = dbHandler.ExecuteQuery(query);

                reader.Read();
                responseIDs[currentResNum] = reader.GetInt32(0);
                reader.Close();

                choiceToPromptID.Add(currentResNum, id);
                currentResNum++;
            }

            if (currentResNum >= 4)
            {
                break;
            }
        }

        WriteResponses();
    }

    public void UpdateNextPrompt(int promptID, int choice = -1)
    {
        // Remove the starting prompt for the current conversation and update it
        if (choice != -1 && choiceToPromptID.ContainsKey(choice - 1))
        {
            startingPromptIDs.Remove(choiceToPromptID[choice - 1]);
        }
        
        startingPromptIDs.Add(promptID);

        // This conversation is over but there's still another one they can have
        if (promptID == -1 && startingPromptIDs.Count > 1)
        {
            conversationUI.EndConversation();
            foreach (int id in startingPromptIDs)
            {
                if (id != -1)
                {
                    promptID = id;
                    break;
                }
            }
        }

        this.promptID = promptID;
        
        string update = "UPDATE Characters SET PromptID = " + promptID + " WHERE ID ==" + id;
        dbHandler.OpenUpdateClose(update);
    }

    private void UpdateRelationshipStatus()
    {
        if (relationshipValue <= dislikeThreshold)
        {
            relStat = relationshipStatus.dislike;
        }
        else if (relationshipValue <= neutralThreshold)
        {
            relStat = relationshipStatus.neutral;
        }
        else
        {
            relStat = relationshipStatus.like;
        }

        switch (relStat)
        {
            case (relationshipStatus.hate):
                {
                    soundtrackLayer.SwitchTrack(0);
                    break;
                }
            case (relationshipStatus.dislike):
                {
                    soundtrackLayer.SwitchTrack(1);
                    break;
                }
            case (relationshipStatus.neutral):
                {
                    soundtrackLayer.SwitchTrack(2);
                    break;
                }
            case (relationshipStatus.like):
                {
                    soundtrackLayer.SwitchTrack(3);
                    break;
                }
        }
    }

    private void UpdateRelationshipValue(int relationshipEffect)
    {
        relationshipValue += relationshipEffect;

        // relationship value can't go below/above pre-determined values
        relationshipValue = (relationshipValue < RelationshipLowerBound) ? 0 : relationshipValue;
        relationshipValue = (relationshipValue > RelationshipUpperBound) ? 10 : relationshipValue;

        // Commented out because other NPCs don't have soundtrack layers assigned yet
        //UpdateRelationshipStatus();
        string update = "UPDATE Characters SET RelationshipValue = " + relationshipValue +
            " WHERE ID ==" + id;
        dbHandler.ExecuteNonQuery(update);
    }

    public IEnumerator WritePrompt(bool addAccuseOpt = false)
    {
        if (playerAudio.isPlaying)
        {
            yield return new WaitForSeconds(playerAudio.clip.length - playerAudio.time);
        }

        if (promptID != -1)
        {
            choiceToPromptID.Clear();
            string query = "SELECT Response1ID, Response2ID, Response3ID, Response4ID, " +
            "AudioFile FROM Prompts WHERE ID ==" + promptID;
            IDataReader reader = dbHandler.ExecuteQuery(query);

            reader.Read();
            responseIDs[0] = reader.GetInt32(0);
            responseIDs[1] = reader.GetInt32(1);
            responseIDs[2] = reader.GetInt32(2);
            responseIDs[3] = reader.GetInt32(3);
            string audioFile = reader.IsDBNull(4) ? "" : reader.GetString(4);
            reader.Close();

            choiceToPromptID.Add(0, promptID);
            choiceToPromptID.Add(1, promptID);
            choiceToPromptID.Add(2, promptID);
            choiceToPromptID.Add(3, promptID);

            // Play the voice line for the prompt
            if (audioFile != "")
            {
                string promptAudioSource = "Audio/" + audioFolder + "/" + audioFile;
                conversationUI.PlayAudio(conversationAudio, promptAudioSource);

                // Make sure the voice line plays all the way through
                yield return new WaitForSeconds(conversationAudio.clip.length);
            }

            // In charge of "finding" evidence and opening conversations at specific points
            promptHandler.DealWithPromptID(this.promptID, id);
        }

        if (CheckCurrentPrompt())
        {
            WriteResponses();
        }
        else
        {
            conversationUI.EndConversation();
        }
    }

    public void WriteResponses()
    {
        string[] responseDisplays = new string[5];
        responseDisplays[4] = "";

        if (promptID != -1)
        {
            int currentDisplay = 0;
            foreach (int id in responseIDs)
            {
                if (id > -1)
                {
                    string query = "SELECT DisplayText FROM Responses WHERE ID ==" + id;
                    IDataReader reader = dbHandler.ExecuteQuery(query);

                    reader.Read();
                    responseDisplays[currentDisplay] = reader.GetString(0);
                    reader.Close();
                }
                else
                {
                    responseDisplays[currentDisplay] = "";
                }

                currentDisplay++;
            }
        }

        conversationUI.DisplayResponseOptions(responseDisplays);
    }
}
