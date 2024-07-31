using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Net;
using Cysharp.Threading.Tasks;

public class EventService : MonoBehaviour
{
    private const string BackupFileName = "BackupEvents.json";
    private const string GoodResponse = "200 OK";
    private const string InvokedMethodName = "POST";
    
    [SerializeField] private string _serverUrl;
    [SerializeField] private float _cooldownBeforeSend = 3f;

    private List<EventData> _eventQueue = new ();
    private bool _isCooldown;

    private string BackupPath => $"{Application.persistentDataPath}/{BackupFileName}";

    private void Awake()
    {
        if (TryLoadBackup())
            StartCooldown().Forget();
    }

    public void TrackEvent(string type, string data)
    {
        var newEvent = new EventData { type = type, data = data };
        _eventQueue.Add(newEvent);
        
        UpdateBackup();

        if (!_isCooldown)
            StartCooldown().Forget();
    }

    private async UniTaskVoid StartCooldown()
    {
        _isCooldown = true;
        
        await UniTask.Delay((int)(_cooldownBeforeSend * 1000));

        var result = await TrySendEventsToServerAsync();

        if (!result)
        {
            StartCooldown().Forget();
            return;
        }
        
        _isCooldown = false;
    }

    private async UniTask<bool> TrySendEventsToServerAsync()
    {
        var eventCollection = new EventCollection { events = _eventQueue };
        var jsonData = JsonUtility.ToJson(eventCollection);

        using var client = new WebClient();
        try
        {
            var response = await client.UploadStringTaskAsync(_serverUrl, InvokedMethodName, jsonData);
            if (response.Equals(GoodResponse))
            {
                _eventQueue.Clear();
                
                File.Delete(BackupPath);
                return true;
            }
        }
        catch (WebException ex)
        {
            Debug.LogError($"Error sending events to server: {ex.Message}");
        }

        return false;
    }
    
    private void UpdateBackup()
    {
        var eventCollection = new EventCollection { events = _eventQueue };
        var jsonData = JsonUtility.ToJson(eventCollection);
        File.WriteAllText(BackupPath, jsonData);
    }
    
    private bool TryLoadBackup()
    {
        if (!File.Exists(BackupPath)) 
            return false;
        
        var jsonData = File.ReadAllText(BackupPath);
        var eventCollection = JsonUtility.FromJson<EventCollection>(jsonData);
        _eventQueue = eventCollection.events;

        return true;
    }
}

[System.Serializable]
public class EventData
{
    public string type;
    public string data;
}

[System.Serializable]
public class EventCollection
{
    public List<EventData> events;
}