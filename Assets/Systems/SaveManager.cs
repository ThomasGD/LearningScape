﻿using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using Newtonsoft.Json;
using FYFY;
using FYFY_plugins.Monitoring;
using TMPro;
using System.Collections.Generic;

public class SaveManager : FSystem {

    private Family f_idTexts = FamilyManager.getFamily(new AllOfComponents(typeof(TextMeshProUGUI)), new AnyOfTags("SessionIDText"));
    private Family f_componentMonitoring = FamilyManager.getFamily(new AllOfComponents(typeof(ComponentMonitoring)));

	private Family f_queries = FamilyManager.getFamily(new AnyOfTags("Q-R1", "Q-R2", "Q-R3"), new AllOfComponents(typeof(QuerySolution)));

	private Family f_collectable = FamilyManager.getFamily(new AllOfComponents(typeof(LinkedWith), typeof(ComponentMonitoring)), new NoneOfComponents(typeof(DreamFragment), typeof(Locker)), new NoneOfLayers(5), new NoneOfTags("Plank"));
    private Family f_puzzleUI = FamilyManager.getFamily(new AnyOfTags("PuzzleUI"));
    private Family f_dreamFragments = FamilyManager.getFamily(new AllOfComponents(typeof(DreamFragment)));

	private Family f_toggleable = FamilyManager.getFamily(new AllOfComponents(typeof(ToggleableGO), typeof(Animator)));

    private Family f_enabledHintsIAR = FamilyManager.getFamily(new AllOfComponents(typeof(HintContent)), new AllOfProperties(PropertyMatcher.PROPERTY.ACTIVE_SELF));
    private Family f_lockIntroWheel = FamilyManager.getFamily(new AllOfComponents(typeof(WheelFrontFace)), new AnyOfTags("LockIntroWheel"));
    private Family f_lockR2Wheel = FamilyManager.getFamily(new AllOfComponents(typeof(WheelFrontFace)), new AnyOfTags("LockR2Wheel"));

    public static SaveManager instance;

    public GameObject player;
    public Timer timer;
    public UnlockedRoom unlockedRoom;
    public GameObject IAR_Tabs;
    public GameObject gears;
    public GameObject IAR_Room2;
    public GameObject pressY;
    public GameObject eraser;
    public Renderer boardTexture;
    public GameObject crouchHint;
    public GameObject outOfFirstRoom;
    public GameObject inventoryHUD;
    public GameObject dreamFragmentHUD;
    public GameObject HelpHUD;
    public GameObject gameRooms;
    public MovingModeSelector movingModeSelector;

	private string saveFolderPath = "./Savegames";
	private string autoSaveFileName = "auto_save";
	private string saveFilesExtension = ".txt";
    
    public GameObject loadPopup;
	private GameObject loadButtonPrefab;
	private GameObject loadListContainer;
	// the load button in main menu
	public Button menuLoadButton;
	// the load button in the popup window
	private Button popupLoadButton;
	private GameObject selectedLoadButton;

	public GameObject savePopup;
	private GameObject saveButtonPrefab;
	private GameObject saveListContainer;
	// the save button in the in game menu
	public Button menuSaveButton;
	private GameObject menuSaveButtonNotice;
	private TMP_InputField popupSaveInputfield;
	// popup windows displayed when the player clicks on popupSaveButton
	private GameObject popupSaveInvalid;
	private GameObject popupSaveOverride;
	private GameObject popupSaveDone;

	private bool autosaveAlreadyAdded;

	private string tmpPath;
	private GameObject tmpGO;
	private DreamFragmentToggle tmpDFToggle;
	private ComponentMonitoring[] tmpMonitorsArray;
	private FileInfo[] tmpFI;

    private float lastAutoSave;
    private float autoSaveFrequency = 300; // in second

    public SaveManager()
	{
		instance = this;
	}

    protected override void onStart()
    {
        lastAutoSave = -autoSaveFrequency;  // init lastAutoSave to the oposite of autoSaveFrequency will fire autosave when the first room will be unlocked
                                            // look for the load window
        
        loadButtonPrefab = loadPopup.GetComponent<PrefabContainer>().prefab;
        loadListContainer = loadPopup.GetComponentInChildren<VerticalLayoutGroup>().gameObject;
        foreach (Button b in loadPopup.GetComponentsInChildren<Button>())
            if (b.gameObject.name == "LoadButton")
            {
                popupLoadButton = b;
                break;
            }

        saveButtonPrefab = savePopup.GetComponent<PrefabContainer>().prefab;
        saveListContainer = savePopup.GetComponentInChildren<VerticalLayoutGroup>().gameObject;
        popupSaveInputfield = savePopup.GetComponentInChildren<TMP_InputField>();
        foreach (LinkedWith link in savePopup.GetComponents<LinkedWith>())
        {
            switch (link.link.name)
            {
                case "Invalid":
                    popupSaveInvalid = link.link.gameObject;
                    break;

                case "Override":
                    popupSaveOverride = link.link.gameObject;
                    break;

                case "Saved":
                    popupSaveDone = link.link.gameObject;
                    break;

                default:
                    break;
            }
        }
       
        menuSaveButtonNotice = menuSaveButton.transform.GetChild(1).gameObject;

        // browse save folder
        DirectoryInfo di;
        try
        {
            if (!Directory.Exists(saveFolderPath))
                di = Directory.CreateDirectory(saveFolderPath);
            else
                di = new DirectoryInfo(saveFolderPath);
            tmpFI = di.GetFiles(string.Concat("*", saveFilesExtension));
            foreach (FileInfo file in tmpFI)
            {
                AddNewItemToLists(file);
                if (file.Name == autoSaveFileName + saveFilesExtension)
                    autosaveAlreadyAdded = true;
            }
            // enable loading button if there are valid saves
            if (loadListContainer && loadListContainer.transform.childCount > 0)
                menuLoadButton.interactable = true;
        }
        catch (Exception e)
        {
            Debug.LogError("Unable to load savegames: " + e.Message);
            LoadGameContent.internalGameContent.saveAndLoadProgression = false;
            LoadGameContent.internalGameContent.autoSaveProgression = false;
        }

        GameObjectManager.setGameObjectState(menuLoadButton.gameObject, LoadGameContent.internalGameContent.saveAndLoadProgression);
        GameObjectManager.setGameObjectState(menuSaveButton.gameObject, LoadGameContent.internalGameContent.saveAndLoadProgression);
    }

    protected override void onProcess(int familiesUpdateCount)
    {
        if (Time.time - lastAutoSave > autoSaveFrequency && unlockedRoom.roomNumber > 0)
        {
            menuSaveButton.interactable = true;
            lastAutoSave = Time.time;
            AutoSave();
        }
    }

    /// <summary>
    /// Save the game in an auto save file
    /// </summary>
    private void AutoSave()
    {
        // don't save if auto save is disabled or if introduction is not completed
        if (!LoadGameContent.internalGameContent.autoSaveProgression || unlockedRoom.roomNumber == 0)
            return;

        if (File.Exists(saveFolderPath + "/" + autoSaveFileName + "_old" + saveFilesExtension))
            File.Delete(saveFolderPath + "/" + autoSaveFileName + "_old" + saveFilesExtension);
        if (File.Exists(saveFolderPath + "/" + autoSaveFileName + saveFilesExtension))
            File.Move(saveFolderPath + "/" + autoSaveFileName + saveFilesExtension, saveFolderPath + "/" + autoSaveFileName + "_old" + saveFilesExtension);

        DateTime dateTimeStamp;
        string fullpath = SaveOnFile(autoSaveFileName, out dateTimeStamp);
        if (!autosaveAlreadyAdded && fullpath != "")
        {
            // if the auto save didn't already exist, add it to the list in the popup
            tmpPath = string.Concat(saveFolderPath, "/", autoSaveFileName, saveFilesExtension);
            // check if file exists
            FileInfo newSaveGame = new FileInfo(tmpPath);
            if (newSaveGame.Exists)
                AddNewItemToLists(newSaveGame);

            autosaveAlreadyAdded = true;
        }
        if (fullpath != "")
        {
            GameObjectManager.addComponent<ActionPerformedForLRS>(savePopup, new
            {
                verb = "saved",
                objectType = "serious-game",
                activityExtensions = new Dictionary<string, string>() {
                    { "type", "autosave" },
                    { "value", dateTimeStamp.ToString() }
                }
            });
        }
    }

    private void AddNewItemToLists(FileInfo file)
    {
        if (!file.Exists)
        {
            Debug.LogError("The save couldn't be loaded because of invalid file path.");
            return;
        }

        // create button in load UI
        GameObject loadListElem = GameObject.Instantiate(loadButtonPrefab);
        loadListElem.name = Path.GetFileNameWithoutExtension(file.Name);
        loadListElem.GetComponentInChildren<TextMeshProUGUI>().text = loadListElem.name;
        loadListElem.GetComponent<SaveComponent>().fileName = file.FullName;
        loadListElem.GetComponent<Button>().onClick.AddListener(delegate
        {
            selectedLoadButton = loadListElem;
            popupLoadButton.interactable = true;
        });
        loadListElem.transform.SetParent(loadListContainer.transform);
        loadListElem.transform.localScale = Vector3.one;
        GameObjectManager.bind(loadListElem);
        // create button in save UI
        GameObject saveListElem = GameObject.Instantiate(saveButtonPrefab);
        saveListElem.name = Path.GetFileNameWithoutExtension(file.Name);
        saveListElem.GetComponentInChildren<TextMeshProUGUI>().text = saveListElem.name;
        saveListElem.GetComponent<Button>().onClick.AddListener(delegate {
            popupSaveInputfield.text = saveListElem.name;
        });
        saveListElem.transform.SetParent(saveListContainer.transform);
        saveListElem.transform.localScale = Vector3.one;
        GameObjectManager.bind(saveListElem);
    }
    
    /// <summary>
    /// Called when clicking on the save button in the popup or when answering yes to override file
    /// </summary>
    /// <param name="checkName">checkName is false when already checked and this function is called after answering yes to override</param>
    public void TrySaving(bool checkName = true)
    {
        // don't save if introduction is not completed
        if (unlockedRoom.roomNumber == 0)
            return;

        if (checkName && !CheckSaveNameValidity())
            // display popup for invalid name
            GameObjectManager.setGameObjectState(popupSaveInvalid, true);
        else
        {
            if (!checkName) // means overriding
            {
                // We don't have to check name (already done), we proceed to save
                DateTime dateTimeStamp;
                string fullpath = SaveOnFile(popupSaveInputfield.text, out dateTimeStamp);
                if (fullpath != "")
                {
                    GameObjectManager.setGameObjectState(popupSaveDone, true);

                    GameObjectManager.addComponent<ActionPerformedForLRS>(savePopup, new
                    {
                        verb = "saved",
                        objectType = "serious-game",
                        activityExtensions = new Dictionary<string, string>() {
                            { "type", "humansave" },
                            { "value", dateTimeStamp.ToString() }
                        }
                    });
                }
                else
                    // error: display popup for invalid name
                    GameObjectManager.setGameObjectState(popupSaveInvalid, true);
            }
            else
            {
                // check if this name is already used
                bool usedName = false;
                foreach (Transform child in saveListContainer.transform)
                    if (popupSaveInputfield.text == child.gameObject.name)
                    {
                        usedName = true;
                        break;
                    }
                if (usedName)
                    // display popup asking for override
                    GameObjectManager.setGameObjectState(popupSaveOverride, true);
                else
                {
                    DateTime dateTimeStamp;
                    string fullpath = SaveOnFile(popupSaveInputfield.text, out dateTimeStamp);
                    // check if file exists
                    FileInfo newSaveGame = new FileInfo(fullpath);
                    if (fullpath != "" && newSaveGame.Exists)
                    {
                        GameObjectManager.setGameObjectState(popupSaveDone, true);

                        AddNewItemToLists(newSaveGame);

                        GameObjectManager.addComponent<ActionPerformedForLRS>(savePopup, new
                        {
                            verb = "saved",
                            objectType = "serious-game",
                            activityExtensions = new Dictionary<string, string>() {
                                { "type", "humansave" },
                                { "value", dateTimeStamp.ToString() }
                            }
                        });
                    }
                    else
                        // error: display popup for invalid name
                        GameObjectManager.setGameObjectState(popupSaveInvalid, true);
                }
            }
        }
    }

    /// <summary>
    /// Encrypt and save the content of "save" in a file
    /// </summary>
    /// <param name="path"></param>
    private string SaveOnFile(string fileName, out DateTime timestamp)
    {
        SaveContent saveContent = new SaveContent();
        timestamp = saveContent.saveDate;

        // don't save if introduction is not completed
        if (unlockedRoom.roomNumber == 0)
            return "";

        saveContent.sessionID = LoadGameContent.sessionID;
        saveContent.navigationMode = movingModeSelector.currentState;
        saveContent.storyTextCount = StoryDisplaying.instance.GetStoryProgression();
        saveContent.lastRoomUnlocked = unlockedRoom.roomNumber;

        for (int i = 0; i < f_lockIntroWheel.Count; i++)
            saveContent.lockIntroPositions[i] = f_lockIntroWheel.getAt(i).GetComponent<WheelFrontFace>().faceNumber;
        for (int i = 0; i < f_lockR2Wheel.Count; i++)
            saveContent.lockRoom2Positions[i] = f_lockR2Wheel.getAt(i).GetComponent<WheelFrontFace>().faceNumber;

        Vector3 fpsPosition = player.transform.position;
        saveContent.playerPosition[0] = fpsPosition.x;
        saveContent.playerPosition[1] = fpsPosition.y;
        saveContent.playerPosition[2] = fpsPosition.z;
        saveContent.playingDuration = StoryDisplaying.instance.Duration;

        // set collectable objects states
        foreach (GameObject go in f_collectable)
        {
            saveContent.collectableItemsStates.Add(go.name, -1); // state undefined
            if (go.activeSelf)
                saveContent.collectableItemsStates[go.name] = 0; // not collected
            else if (go.GetComponent<LinkedWith>().link.activeSelf)
            {
                saveContent.collectableItemsStates[go.name] = 1; // collected
            }
            else
                saveContent.collectableItemsStates[go.name] = 2; // collected and used (removed from iar)
        }

        //set puzzle position un IAR
        foreach (GameObject go in f_puzzleUI)
        {
            saveContent.puzzlePosition.Add(go.name, new float[3]);
            saveContent.puzzlePosition[go.name][0] = go.transform.localPosition.x;
            saveContent.puzzlePosition[go.name][1] = go.transform.localPosition.y;
            saveContent.puzzlePosition[go.name][2] = go.transform.localPosition.z;
        }

        // set dream fragments states
        foreach (GameObject go in f_dreamFragments)
        {
            saveContent.dreamFragmentsStates.Add(go.name, 0);
            DreamFragment df = go.GetComponent<DreamFragment>();
            if (df.viewed)
            {
                saveContent.dreamFragmentsStates[go.name] = 1;
                LinkedWith linkWith = go.GetComponent<LinkedWith>();
                if (linkWith && linkWith.link.GetComponent<NewDreamFragment>() == null && df.type == 0)
                    saveContent.dreamFragmentsStates[go.name] = 2;
                Animator anim;
                if (go.TryGetComponent<Animator>(out anim))
                    anim.enabled = false;
            }
        }

        saveContent.pressY_displayed = pressY.activeSelf;
        saveContent.ballbox_opened = !BallBoxManager.instance.IsLocked();
        saveContent.wireOnPlank = PlankAndWireManager.instance.IsResolved();
        saveContent.satchel_opened = !SatchelManager.instance.IsLocked();
        saveContent.plankDiscovered = PlankAndMirrorManager.instance.GetPlankDiscovered();
        saveContent.mirrorOnPlank = PlankAndMirrorManager.instance.IsMirrorOnPlank();

        // set toggleables states
        foreach (GameObject go in f_toggleable)
            saveContent.toggleablesStates.Add(go.name, go.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("TurnOn"));

        // Save board texture
        Texture2D tmpTex = (Texture2D)boardTexture.material.mainTexture;
        saveContent.boardEraseTexture = tmpTex.EncodeToPNG();
        // set eraser position
        Vector3 eraserPosition = eraser.transform.position;
        saveContent.boardEraserPosition[0] = eraserPosition.x;
        saveContent.boardEraserPosition[1] = eraserPosition.y;
        saveContent.boardEraserPosition[2] = eraserPosition.z;

        // set queries states
        foreach (GameObject query in f_queries)
        {
            GameObject answerGO = query.transform.GetChild(3).gameObject;
            saveContent.iarQueriesStates.Add(query.name, answerGO.activeSelf);
            if (answerGO.activeSelf)
            {
                saveContent.iarQueriesAnswer.Add(query.name, answerGO.transform.GetChild(0).GetComponent<TMP_Text>().text);
                saveContent.iarQueriesDesc.Add(query.name, answerGO.transform.GetChild(1).GetComponent<TMP_Text>().text);
            }
            else
            {
                saveContent.iarQueriesAnswer.Add(query.name, "");
                saveContent.iarQueriesDesc.Add(query.name, "");
            }
        }

        // Set gears state
        if (IARGearsEnigma.instance.IsResolved())
            saveContent.gearEnigmaState = 2;
        else if (!gears.activeSelf)
            saveContent.gearEnigmaState = 0;
        else
            saveContent.gearEnigmaState = 1; // gears displayed but not resolved
        
        // generate received hints
        foreach (GameObject hint in f_enabledHintsIAR)
        {
            HintContent hintContent = hint.GetComponent<HintContent>();
            SaveContent.HintData hintSave = new SaveContent.HintData(hintContent.monitor.id, hintContent.actionName, hintContent.text, hintContent.link, hintContent.level, hint.GetComponent<NewHint>() == null);
            saveContent.accessibleHints.Add(hintSave);
        }

        saveContent.helpSystemState = !HelpSystem.shouldPause;
        saveContent.petriNetsMarkings = MonitoringManager.getPetriNetsMarkings();
        saveContent.hintCooldown = HelpSystem.instance.HintCooldown < 0 ? 0 : HelpSystem.instance.HintCooldown;
        saveContent.systemHintTimer = Time.time - HelpSystem.instance.SystemHintTimer;
        saveContent.helpLabelCount = HelpSystem.instance.LabelCount;
        saveContent.HintDictionary = HelpSystem.instance.GameHints.dictionary;
        saveContent.HintWrongAnswerFeedbacks = HelpSystem.instance.GameHints.wrongAnswerFeedbacks;
        saveContent.pnNetsRequiredStepsOnStart = HelpSystem.instance.pnNetsRequiredStepsOnStart;

        tmpPath = string.Concat(saveFolderPath, "/", fileName, saveFilesExtension);
        try
        {
            // Create all necessary directories if they don't exist
            Directory.CreateDirectory(Path.GetDirectoryName(tmpPath));
            File.WriteAllText(tmpPath, JsonConvert.SerializeObject(saveContent));
            return tmpPath;
        }
        catch (Exception)
        {
            Debug.LogError("Tried to save with an invalid SaveContent. Nothing was done.");
        }
        return "";
    }

    // Called from UI
    public void LoadSelectedSave()
    {
        SaveContent saveContent;
        try
        {
            saveContent = JsonConvert.DeserializeObject<SaveContent>(File.ReadAllText(selectedLoadButton.GetComponent<SaveComponent>().fileName));
        }
        catch (Exception)
        {
            Debug.LogError("The save couldn't be loaded because of invalid content.");
            return;
        }

        GameObjectManager.setGameObjectState(loadPopup, false);
        popupSaveInputfield.text = selectedLoadButton.name;
        // Check save validity
        if (saveContent == null)
        {
            Debug.LogError("The save couldn't be loaded because of invalid content.");
        }
        else
        {
            // Load save

            // set session ID
            if (saveContent.sessionID != "")
            {
                LoadGameContent.sessionID = saveContent.sessionID;
                if (LoadGameContent.internalGameContent.traceToLRS)
                {
                    SendStatements.instance.initGBLXAPI();
                    SendStatements.instance.initPlayerName(saveContent.sessionID);
                }
                // add the generated session id after the ui text has been set
                foreach (GameObject go in f_idTexts)
                    go.GetComponent<TextMeshProUGUI>().text = LoadGameContent.internalGameContent.sessionIDText+saveContent.sessionID;
            }

            movingModeSelector.currentState = saveContent.navigationMode;
            movingModeSelector.resumeMovingSystems();


            // set story reading progression
            StoryDisplaying.instance.LoadStoryProgression(saveContent.storyTextCount);

            // set room progression
            unlockedRoom.roomNumber = saveContent.lastRoomUnlocked;
            // Enable rooms
            if (saveContent.lastRoomUnlocked > 1)
            {
                GameObjectManager.setGameObjectState(gameRooms.transform.GetChild(2).gameObject, true);
                GameObjectManager.setGameObjectState(gameRooms.transform.GetChild(3).gameObject, true);
            }

            // set locker wheels position
            for (int i = 0; i < f_lockIntroWheel.Count; i++)
            {
                GameObject wheel = f_lockIntroWheel.getAt(i);
                wheel.GetComponent<WheelFrontFace>().faceNumber = saveContent.lockIntroPositions[i];
                wheel.transform.Rotate(36* saveContent.lockIntroPositions[i], 0, 0);
            }
            for (int i = 0; i < f_lockR2Wheel.Count; i++)
            {
                GameObject wheel = f_lockR2Wheel.getAt(i);
                wheel.GetComponent<WheelFrontFace>().faceNumber = saveContent.lockRoom2Positions[i];
                wheel.transform.Rotate(36 * saveContent.lockRoom2Positions[i], 0, 0);
            }

            // set doors states
            if (saveContent.lastRoomUnlocked > 0)
                LockResolver.instance.UnlockIntroWall();
            if (saveContent.lastRoomUnlocked > 1)
                LoginManager.instance.UnlockLoginDoor();
            if (saveContent.lastRoomUnlocked > 2)
                LockResolver.instance.UnlockRoom2Fences();

            // set player position
            player.transform.rotation = Quaternion.Euler(0, 90, 0);
            player.transform.position = new Vector3(saveContent.playerPosition[0], saveContent.playerPosition[1], saveContent.playerPosition[2]);

            // disable black wall at the beginning
            GameObject night = GameObject.Find("Night");
            night.GetComponent<Animator>().enabled = true;
            night.GetComponent<Collider>().enabled = false;
            // display movement HUD
            MovingSystem_FPSMode.instance.UnlockAllHUD();
            // disable movement HUD warnings
            foreach (LinkedWith link in outOfFirstRoom.GetComponents<LinkedWith>())
                GameObjectManager.setGameObjectState(link.link, false);
            foreach (LinkedWith link in crouchHint.GetComponents<LinkedWith>())
                GameObjectManager.setGameObjectState(link.link, false);

            // set starting time
            timer.startingTime = Time.time - saveContent.playingDuration;

            // set pressY state
            GameObjectManager.setGameObjectState(pressY, saveContent.pressY_displayed);
            // set collectable objects states
            foreach (GameObject go in f_collectable)
            {
                if (saveContent.collectableItemsStates.ContainsKey(go.name))
                {
                    int code = saveContent.collectableItemsStates[go.name];
                    // if collected, disable it in the scene
                    GameObjectManager.setGameObjectState(go, code == 0);
                    // if it has been collected, enable linked IAR
                    if (code == 1)
                        GameObjectManager.setGameObjectState(go.GetComponent<LinkedWith>().link, true);
                    // some linked go in IAR is a set of GO (Scrolls and Puzzles). In these cases we have to set state of sublinked go with the same name
                    GameObject iarLink = go.GetComponent<LinkedWith>().link;
                    if (code == 1 && iarLink.GetComponent<LinkedWith>() && iarLink.GetComponent<LinkedWith>().link.transform.Find(go.name))
                        GameObjectManager.setGameObjectState(iarLink.GetComponent<LinkedWith>().link.transform.Find(go.name).gameObject, true);
                }
            }
            // enable inventory HUD
            GameObjectManager.setGameObjectState(inventoryHUD, true);

            //set puzzle position in IAR
            foreach (GameObject go in f_puzzleUI)
            {
                if (saveContent.puzzlePosition.ContainsKey(go.name)) { 
                    float[] puzzlePos = saveContent.puzzlePosition[go.name];
                    go.transform.localPosition = new Vector3(puzzlePos[0], puzzlePos[1], puzzlePos[2]);
                }
            }

            // set dream fragments states
            bool atLeastOneFragmentClicked = false;
            foreach (GameObject go in f_dreamFragments)
            {
                if (saveContent.dreamFragmentsStates.ContainsKey(go.name))
                {
                    int code = saveContent.dreamFragmentsStates[go.name];
                    if (code != 0)
                    {
                        atLeastOneFragmentClicked = true;
                        // if collected, turn off fragment in scene
                        DreamFragmentCollecting.instance.TurnOffDreamFragment(go);
                        // enable in IAR
                        if (go.GetComponent<LinkedWith>())
                        {
                            tmpGO = go.GetComponent<LinkedWith>().link;
                            GameObjectManager.setGameObjectState(tmpGO, true);
                            DreamFragment df = go.GetComponent<DreamFragment>();
                            if (code == 2 && df.type == 0) // case of blue dream fragments
                            {
                                // set as seen in IAR
                                tmpDFToggle = tmpGO.GetComponent<DreamFragmentToggle>();
                                tmpGO.GetComponentInChildren<Image>().sprite = tmpDFToggle.offState;
                                tmpDFToggle.currentState = tmpDFToggle.offState;
                                GameObjectManager.removeComponent<NewDreamFragment>(tmpDFToggle.gameObject);
                            }
                            else if (df.type == 1) // case of green dream fragments
                                CollectObject.instance.enableTargetInIAR(df.gameObject);
                        }
                    }
                }
            }

            // enable IAR tab and HUD
            if (atLeastOneFragmentClicked)
            {
                if (IARDreamFragmentManager.virtualDreamFragment)
                {
                    IARNewDreamFragmentAvailable.instance.firstFragmentOccurs = true;
                    GameObjectManager.setGameObjectState(IAR_Tabs, true);
                    GameObjectManager.setGameObjectState(dreamFragmentHUD, true);
                }
            }

            // set Ballbox lock state
            if (saveContent.ballbox_opened)
                BallBoxManager.instance.UnlockBallBox();

            // set Wire on plank
            if (saveContent.wireOnPlank)
                PlankAndWireManager.instance.DisplayWireOnSolution();

            // set Satchel lock state
            if (saveContent.satchel_opened)
                SatchelManager.instance.UnlockSatchel();

            // set mirror and plank
            PlankAndMirrorManager.instance.SetPlankDiscovered(saveContent.plankDiscovered);
            if (saveContent.mirrorOnPlank)
                PlankAndMirrorManager.instance.PutMirrorOnPlank();

            // set toggleables states
            foreach (GameObject go in f_toggleable)
                if (saveContent.toggleablesStates.ContainsKey(go.name))
                    if (saveContent.toggleablesStates[go.name])
                        go.GetComponent<Animator>().SetTrigger("turnOn");

            // load room 3 board texture
            Texture2D tex = new Texture2D(1, 1);
            tex.LoadImage(saveContent.boardEraseTexture);
            boardTexture.material.mainTexture = tex;
            WhiteBoardManager.instance.SetRenderOrder(null);
            // set eraser position
            eraser.transform.position = new Vector3(saveContent.boardEraserPosition[0], saveContent.boardEraserPosition[1], saveContent.boardEraserPosition[2]);

            bool allR2queriesSolved = true;
            // set queries states
            foreach (GameObject query in f_queries)
                if (saveContent.iarQueriesStates.ContainsKey(query.name) && saveContent.iarQueriesAnswer.ContainsKey(query.name) && saveContent.iarQueriesDesc.ContainsKey(query.name))
                    if (saveContent.iarQueriesStates[query.name])
                        // Toggle all childs of queries except the first one
                        for (int i = 1; i < query.transform.childCount; i++)
                        {
                            GameObject child = query.transform.GetChild(i).gameObject;
                            GameObjectManager.setGameObjectState(child, !child.activeSelf);
                            // For the third set answer and description
                            if (i == 3)
                            {
                                child.transform.GetChild(0).GetComponent<TMP_Text>().text = saveContent.iarQueriesAnswer[query.name];
                                child.transform.GetChild(1).GetComponent<TMP_Text>().text = saveContent.iarQueriesDesc[query.name];
                            }
                        }
                    else if (query.tag == "Q-R2")
                        // We found a query in room 2 not resolved
                        allR2queriesSolved = false;
            // special case if savegame occurs when all R2 queries are solved and player doesn't unlock the last room => display code in IAR
            if (saveContent.lastRoomUnlocked == 1 && allR2queriesSolved)
                IARQueryEvaluator.instance.showR2FinalCode();

            // Set gears state
            // Check if gears enigma was solved
            if (saveContent.gearEnigmaState == 2)
            {
                // Hide question
                GameObjectManager.setGameObjectState(gears.transform.GetChild(0).gameObject, false);
                // Update system
                IARGearsEnigma.instance.SolveGearsEnigma();
            }
            else if (saveContent.gearEnigmaState == 1)
            {
                // display gears
                GameObjectManager.setGameObjectState(gears, true);
                // Hide queries
                GameObjectManager.setGameObjectState(gears.GetComponent<LinkedWith>().link, false);
            }

            // synchronize help system
            if (!HelpSystem.shouldPause != saveContent.helpSystemState)
            {
                HelpSystem.shouldPause = !saveContent.helpSystemState;
                if (!HelpSystem.shouldPause)
                {
                    // check Java
                    CheckJava.instance.checkJava();
                    CheckJava.instance.Pause = false;
                }
                // init HelpSystem
                HelpSystem.instance.initHelpSystem();
            }
            // generate received hints
            foreach (SaveContent.HintData hint in saveContent.accessibleHints)
            {
                // find the monitoring component corresponding to this hint data
                bool monitorFound = false;
                foreach (GameObject monitor in f_componentMonitoring)
                {
                    tmpMonitorsArray = monitor.GetComponents<ComponentMonitoring>();
                    foreach (ComponentMonitoring monitoringComponent in tmpMonitorsArray)
                        if (monitoringComponent.id == hint.monitorID)
                        {
                            Button b = HelpSystem.instance.CreateHintButton(monitoringComponent, hint.name, hint.text, hint.link, hint.level, false);
                            monitorFound = true;
                            if (hint.seen)
                            {
                                IARHintManager.instance.SetNormalColor(b);
                                GameObjectManager.removeComponent<NewHint>(b.gameObject);
                            }
                            break;
                        }
                    if (monitorFound)
                        break;
                }
            }
            // enable help HUD
            GameObjectManager.setGameObjectState(HelpHUD, true);

            // load complete and filtered petri nets markings
            if (saveContent.petriNetsMarkings != null)
                MonitoringManager.setPetriNetsMarkings(saveContent.petriNetsMarkings);

            // set HelpSystem with loaded petri nets
            HelpSystem.instance.LoadHelpSystemValues(saveContent.hintCooldown, Time.time-saveContent.systemHintTimer, saveContent.helpLabelCount);
            HelpSystem.instance.GameHints.dictionary = saveContent.HintDictionary;
            HelpSystem.instance.GameHints.wrongAnswerFeedbacks = saveContent.HintWrongAnswerFeedbacks;
            HelpSystem.instance.pnNetsRequiredStepsOnStart = saveContent.pnNetsRequiredStepsOnStart;

            GameObjectManager.addComponent<ActionPerformedForLRS>(savePopup, new
            {
                verb = "loaded",
                objectType = "serious-game",
                activityExtensions = new Dictionary<string, string>() {
                    { "value", saveContent.saveDate.ToString() }
                }
            });

            MenuSystem.instance.StartGame();
        }
    }

	/// <summary>
	/// Called when trying to save
	/// </summary>
	public bool CheckSaveNameValidity()
    {
		bool isValid;

		// remove file extension
		if (popupSaveInputfield.text.EndsWith(saveFilesExtension))
			popupSaveInputfield.text = popupSaveInputfield.text.Substring(popupSaveInputfield.text.Length - saveFilesExtension.Length, saveFilesExtension.Length);

		isValid = popupSaveInputfield.text != "";

		char[] chars = Path.GetInvalidFileNameChars();

		foreach(char c in chars)
            if (popupSaveInputfield.text.IndexOf(c) != -1)
            {
				isValid = false;
				break;
            }

		return isValid;
    }

	/// <summary>
	/// Called when changing tab or closing IAR
	/// </summary>
	public void CloseSavePopup()
    {
		GameObjectManager.setGameObjectState(savePopup, false);
		GameObjectManager.setGameObjectState(popupSaveInvalid, false);
		GameObjectManager.setGameObjectState(popupSaveOverride, false);
		GameObjectManager.setGameObjectState(popupSaveDone, false);
	}

	/// <summary>
	/// Called when mouse enters or exits the in game save button
	/// </summary>
	/// <param name="enabled"></param>
	public void SetSaveNoticeState(bool enabled)
    {
		if (!menuSaveButton.interactable)
			GameObjectManager.setGameObjectState(menuSaveButtonNotice, enabled);
        else
            GameObjectManager.setGameObjectState(menuSaveButtonNotice, false);
    }
}