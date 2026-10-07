using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace DdukDduk.Shop
{
    /// <summary>Scene entry point. Other team systems can use Model to apply research, reputation and capacity changes.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShopView))]
    public sealed class ShopApp : MonoBehaviour
    {
        public ShopModel Model { get; private set; }
        private string SavePath => Path.Combine(Application.persistentDataPath, "shop-v1.json");
        private bool persist;

        private void Awake()
        {
            persist = Array.IndexOf(Environment.GetCommandLineArgs(), "-shopNoSave") < 0;
            Model = new ShopModel();
            if (persist) Load();
            EnsureEventSystem();
            GetComponent<ShopView>().Initialize(Model);
            Model.Changed += Save;
            var flow = GetComponent<ShopDayFlow>();
            if (flow == null) flow = gameObject.AddComponent<ShopDayFlow>();
            flow.Initialize(Model, GetComponent<ShopView>());
        }

        private void OnDestroy()
        {
            if (Model != null) Model.Changed -= Save;
        }

        private void Load()
        {
            if (!File.Exists(SavePath)) return;
            try
            {
                var snapshot = JsonUtility.FromJson<ShopSnapshot>(File.ReadAllText(SavePath));
                if (!Model.TryRestoreSnapshot(snapshot, out var error))
                {
                    // A failed migration must never overwrite the original save with a default model.
                    persist = false;
                    Debug.LogWarning("Saved shop state could not be restored; the original save will be preserved: " + error);
                }
            }
            catch (Exception ex)
            {
                persist = false;
                Debug.LogWarning("Saved shop state could not be read; the original save will be preserved: " + ex.Message);
            }
        }

        private void Save()
        {
            if (!persist || Model == null) return;
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(Model.CreateSnapshot(), true));
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, null);
                else File.Move(temp, SavePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Shop save failed: " + ex.Message);
            }
        }

        public static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
