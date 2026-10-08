using UnityEngine;
using System.Collections.Generic;

namespace Player.Equipment
{
    public class SDCardItem : Equipment
    {
        public bool isUsedCard = false;
        public string recordedFileName = "";
        public float videoDuration = 0f;
        public List<FootageData> recordings = new List<FootageData>();
        private Sprite blankCardIcon;
        [SerializeField, HideInInspector] private int purchaseNumber;
        private static int nextPurchaseNumber = 1;
        private static object numberingScope;
        private static bool numberingRoom;
        public int CardNumber { get { EnsureCardNumber(); return purchaseNumber; } }
        public string DisplayName => "SD Card " + CardNumber;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCardNumbering()
        {
            nextPurchaseNumber = 1; numberingScope = null; numberingRoom = false;
        }

        private void EnsureCardNumber()
        {
            if (!object.ReferenceEquals(numberingScope, GameSavePrefs.Values) || numberingRoom != GameSavePrefs.IsRoomSession)
            {
                numberingScope = GameSavePrefs.Values; numberingRoom = GameSavePrefs.IsRoomSession;
                nextPurchaseNumber = 1;
            }
            if (purchaseNumber > 0) { nextPurchaseNumber = Mathf.Max(nextPurchaseNumber, purchaseNumber + 1); return; }
            // Reserve existing IDs too, including inactive mounted/inventory cards
            // after a script reload. Never number by computer insertion order.
            foreach (var card in FindObjectsOfType<SDCardItem>(true))
                if (card.purchaseNumber > 0) nextPurchaseNumber = Mathf.Max(nextPurchaseNumber, card.purchaseNumber + 1);
            purchaseNumber = nextPurchaseNumber++;
        }

        protected override void Awake()
        {
            base.Awake();
            blankCardIcon = EquipmentIcon;
            EnsurePickupCollider();
        }

        public List<FootageData> GetRecordings()
        {
            if (recordings == null) recordings = new List<FootageData>();
            recordings.RemoveAll(x => x == null);
            if (recordings.Count == 0 && isUsedCard && !string.IsNullOrEmpty(recordedFileName))
            {
                var data = SDCardStorage.ReadMetadata(recordedFileName) ?? new FootageData {
                    fileName = recordedFileName, camScore = cameraScore, lightScore = lightScore,
                    campaignLevel = campaignLevel, shotType = shotType, screenDirection = screenDirection,
                    actorPose = actorPose, requiredSubjectsVisible = requiredSubjectsVisible,
                    usedSoftLight = usedSoftLight, hasThreePointRoles = hasThreePointRoles
                };
                data.fileName = recordedFileName;
                if (data.duration <= 0) data.duration = SDCardStorage.ReadDuration(recordedFileName);
                if (data.duration <= 0) data.duration = Mathf.Max(0, videoDuration);
                recordings.Add(data);
            }
            return recordings;
        }

        public float UsedSeconds
        {
            get { float seconds = 0; foreach (var clip in GetRecordings()) if (clip != null) seconds += Mathf.Max(0, clip.duration); return seconds; }
        }
        public float RemainingSeconds => Mathf.Max(0, SDCardStorage.CapacitySeconds - UsedSeconds);
        public bool HasSpace => RemainingSeconds + .0001f >= 1f / TapeSettings.framesPerSecond;

        public bool AddRecording(FootageData data)
        {
            if (data == null || string.IsNullOrEmpty(data.fileName) || float.IsNaN(data.duration) || float.IsInfinity(data.duration) ||
                data.duration <= 0 || data.duration > RemainingSeconds + .0001f) return false;
            if (GetRecordings().Exists(x => string.Equals(x.fileName, data.fileName, System.StringComparison.OrdinalIgnoreCase))) return false;
            recordings.Add(data);
            RefreshLatestRecording();
            try { SDCardStorage.SaveMetadata(data); } catch (System.Exception e) { Debug.LogWarning("Recording metadata: " + e.Message); }
            return true;
        }

        public void RemoveRecording(string name)
        {
            GetRecordings().RemoveAll(x => string.Equals(x.fileName, name, System.StringComparison.OrdinalIgnoreCase));
            RefreshLatestRecording();
        }

        public void RefreshLatestRecording()
        {
            var clips = recordings;
            isUsedCard = clips != null && clips.Count > 0;
            if (!isUsedCard)
            {
                recordedFileName = ""; videoDuration = videoScore = cameraScore = lightScore = 0;
                EquipmentIcon = EquipmentIconArt.Get("SD Card", blankCardIcon);
                return;
            }
            var last = clips[clips.Count - 1];
            recordedFileName = last.fileName; videoDuration = last.duration;
            cameraScore = last.camScore; lightScore = last.lightScore; videoScore = cameraScore + lightScore;
            campaignLevel = last.campaignLevel; shotType = last.shotType; screenDirection = last.screenDirection;
            actorPose = last.actorPose; requiredSubjectsVisible = last.requiredSubjectsVisible;
            usedSoftLight = last.usedSoftLight; hasThreePointRoles = last.hasThreePointRoles;
            MarkAsUsed();
        }

        public float videoScore = 0f; // The Total (out of 100)
        public float cameraScore = 0f; // NEW: Camera only (out of 70)
        public float lightScore = 0f;  // NEW: Light only (out of 30)

        [Header("Production Evidence")]
        public int campaignLevel = 1;
        public int shotType = 2;
        public float screenDirection = 0f;
        public string actorPose = "";
        public bool requiredSubjectsVisible = false;
        public bool usedSoftLight = false;
        public bool hasThreePointRoles = false;

        [Header("Icons")]
        [Tooltip("Drag the icon for a RECORDED SD card here")]
        public Sprite usedCardIcon;

        public void PrepareShopDelivery(Vector3 position)
        {
            EnsureCardNumber(); // Called in the shop's successful purchase/delivery order.
            PrepareWorldPhysics();
            transform.position = position;
            // Imported mesh pivots are offset; place the visible card, not its root pivot.
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                transform.position += new Vector3(position.x - bounds.center.x,
                    position.y - bounds.min.y, position.z - bounds.center.z);
            }
            itemRigidbody.velocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
            itemRigidbody.useGravity = false;
            itemRigidbody.isKinematic = true;
        }

        public override void OnPickedUp(Transform holdPoint)
        {
            if (holdPoint == null || HasPickupState) return;
            // Ejected/device-stored cards can still have frozen physics. Snapshot
            // their normal world state, not that temporary presentation state.
            PrepareWorldPhysics();
            base.OnPickedUp(holdPoint);
        }

        public override void OnDropped(Camera playerCamera)
        {
            base.OnDropped(playerCamera);
            PrepareWorldPhysics();
        }

        private void PrepareWorldPhysics()
        {
            if (itemRigidbody == null) itemRigidbody = GetComponent<Rigidbody>();
            if (itemRigidbody != null)
            {
                itemRigidbody.isKinematic = false;
                itemRigidbody.useGravity = true;
                itemRigidbody.detectCollisions = true;
                itemRigidbody.constraints = RigidbodyConstraints.None;
                itemRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                itemRigidbody.velocity = Vector3.zero;
                itemRigidbody.angularVelocity = Vector3.zero;
                itemRigidbody.WakeUp();
            }
            EnsurePickupCollider();
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = true;
        }

        private void EnsurePickupCollider()
        {
            var mesh = GetComponentInChildren<MeshFilter>(true);
            var host = mesh != null && mesh.sharedMesh != null ? mesh.gameObject : gameObject;
            var box = host.GetComponent<BoxCollider>();
            if (box == null) box = host.AddComponent<BoxCollider>();
            if (mesh != null && mesh.sharedMesh != null)
            {
                // Imported card meshes have offset pivots and a large child scale.
                var bounds = mesh.sharedMesh.bounds;
                var scale = host.transform.lossyScale;
                box.center = bounds.center;
                box.size = new Vector3(Mathf.Max(bounds.size.x, .025f / Mathf.Max(.001f, Mathf.Abs(scale.x))),
                    Mathf.Max(bounds.size.y, .025f / Mathf.Max(.001f, Mathf.Abs(scale.y))),
                    Mathf.Max(bounds.size.z, .025f / Mathf.Max(.001f, Mathf.Abs(scale.z))));
            }
            else { box.center = Vector3.zero; box.size = new Vector3(.08f, .095f, .025f); }
            box.isTrigger = false;
            box.enabled = true;
            if ((Physics.DefaultRaycastLayers & (1 << host.layer)) == 0) host.layer = 0;
        }

        public override void OnUse(Camera playerCamera)
        {
            GameFeedback.Show($"{DisplayName.ToUpperInvariant()} · {GetRecordings().Count} CLIPS\n{UsedSeconds:0.#} / 60 seconds used");
            if (isUsedCard)
            {
                // Tells you the split score when you click to inspect it!
                Debug.Log($"Card: {recordedFileName} | Cam: {cameraScore:F0}/70 | Light: {lightScore:F0}/30 | Total: {videoScore:F0}/100");
            }
            else
            {
                Debug.Log("Inspecting SD Card. It is BLANK.");
            }
        }

        // --- NEW: The Camera calls this when ejecting the tape! ---
        public void MarkAsUsed()
        {
            isUsedCard = true;
            usedCardIcon = EquipmentIconArt.Get("RECORDED SD CARD", usedCardIcon);

            if (usedCardIcon != null)
            {
                EquipmentIcon = usedCardIcon; // Swaps the base icon so the hotbar reads the new one!
            }
        }
    }
}
