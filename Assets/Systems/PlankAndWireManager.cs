﻿using UnityEngine;
using FYFY;
using FYFY_plugins.PointerManager;
using System.Collections.Generic;
using TMPro;

public class PlankAndWireManager : FSystem {
    
    // this system manage the plank and the wire

    //all selectable objects
    private Family f_focusedPlank = FamilyManager.getFamily(new AnyOfTags("Plank"), new AllOfComponents(typeof(ReadyToWork), typeof(LinkedWith)));
    private Family f_focusedWords = FamilyManager.getFamily(new AnyOfTags("PlankText"), new AllOfComponents(typeof(PointerOver), typeof(TextMeshPro))); // focused words on the plank
    private Family f_allWords = FamilyManager.getFamily(new AnyOfTags("PlankText"), new AllOfComponents(typeof(PointerSensitive), typeof(TextMeshPro))); // all clickable words on the plank
    private Family f_wrongWords = FamilyManager.getFamily(new AnyOfTags("PlankText"), new AllOfComponents(typeof(PointerSensitive), typeof(TextMeshPro)), new NoneOfComponents(typeof(IsSolution)));
    private Family f_closePlank_1 = FamilyManager.getFamily (new AnyOfTags ("Plank", "PlankText", "InventoryElements"), new AllOfComponents(typeof(PointerOver)));
    private Family f_closePlank_2 = FamilyManager.getFamily(new AllOfComponents(typeof(PointerOver), typeof(HUD_TransparentOnMove)));
    private Family f_itemSelected = FamilyManager.getFamily(new AnyOfTags("InventoryElements"), new AllOfComponents(typeof(SelectedInInventory)));
    private Family f_iarBackground = FamilyManager.getFamily(new AnyOfTags("UIBackground"), new AnyOfProperties(PropertyMatcher.PROPERTY.ACTIVE_IN_HIERARCHY));
    private Family f_solutionWords = FamilyManager.getFamily(new AnyOfTags("PlankText"), new AllOfComponents(typeof(PointerSensitive), typeof(TextMeshPro), typeof(IsSolution)));

    //plank
    public LineRenderer lineRenderer;
    private GameObject selectedPlank = null;
    private List<Vector3> lrPositions;
    private int nbWordsSelected;

    private GameObject currentFocusedWord;

    private bool correct = false;

    public static PlankAndWireManager instance;

    public PlankAndWireManager()
    {
        instance = this;
    }

    protected override void onStart()
    {
        //initialise vairables
        lrPositions = new List<Vector3>();
        nbWordsSelected = 0;

        f_focusedPlank.addEntryCallback(onReadyToWorkOnPlank);

        f_focusedWords.addEntryCallback(onWordMouseEnter);
        f_focusedWords.addExitCallback(onWordMouseExit);
    }

    private void onReadyToWorkOnPlank(GameObject go)
    {
        selectedPlank = go;

        // launch this system
        instance.Pause = false;

        // Add Colliders to words and pins
        foreach (GameObject word in f_allWords)
        {
            GameObjectManager.addComponent<BoxCollider>(word, new { isTrigger = true });
            GameObjectManager.addComponent<Rigidbody>(word, new { isKinematic = true });
            GameObjectManager.addComponent<BoxCollider>(word.transform.GetChild(0).gameObject);
        }

        GameObjectManager.addComponent<ActionPerformed>(go, new { name = "turnOn", performedBy = "player" });

        GameObjectManager.addComponent<ActionPerformedForLRS>(go, new
        {
            verb = "accessed",
            objectType = "plankAndWire"
        });
    }

    private void onWordMouseEnter(GameObject go)
    {
        //if mouse over a word and word doesn't already clicked
        if (go.GetComponent<TextMeshPro>().color != Color.yellow && f_iarBackground.Count == 0 && selectedPlank != null)
        {
            //if the word isn't selected change its color to cyan
            go.GetComponent<TextMeshPro>().color = Color.cyan;
        }
        currentFocusedWord = go;
    }

    private void onWordMouseExit(int instanceId)
    {
        if (currentFocusedWord && currentFocusedWord.GetInstanceID() == instanceId && currentFocusedWord.GetComponent<TextMeshPro>().color != Color.yellow && selectedPlank != null)
        {
            //if the word isn't selected change its color to white
            currentFocusedWord.GetComponent<TextMeshPro>().color = Color.white;
        }
        currentFocusedWord = null;
    }

    // Use this to update member variables when system pause. 
    // Advice: avoid to update your families inside this function.
    protected override void onPause(int currentFrame) {
	}

	// Use this to update member variables when system resume.
	// Advice: avoid to update your families inside this function.
	protected override void onResume(int currentFrame){
	}

    // return true if wire is selected into inventory
    private GameObject wireSelected()
    {
        foreach (GameObject go in f_itemSelected)
            if (go.name == "Wire")
                return go;
        return null;
    }

    private void unselectAllWords()
    {
        foreach (GameObject word in f_allWords)
        {
            if (word.GetComponent<TextMeshPro>().color == Color.yellow)
            {
                GameObjectManager.addComponent<ActionPerformed>(word, new { name = "turnOff", performedBy = "player", family = word.GetComponent<IsSolution>() ? null : f_wrongWords });
                GameObjectManager.addComponent<ActionPerformedForLRS>(word, new
                {
                    verb = "deactivated",
                    objectType = "word",
                    activityExtensions = new Dictionary<string, string>() {
                                    { "value", word.GetComponent<TextMeshPro>().text },
                                    { "content", word.GetComponent<TextMeshPro>().text }
                                }
                });
            }
            word.GetComponent<TextMeshPro>().color = Color.white;
            lineRenderer.positionCount = 0;
            nbWordsSelected = 0;
            lrPositions.Clear();
        }
    }

	// Use to process your families.
	protected override void onProcess(int familiesUpdateCount)
    {
        if (selectedPlank)
        {
            // "close" ui (give back control to the player) when clicking on nothing or Escape is pressed and IAR is closed (because Escape close IAR)
            if (((f_closePlank_1.Count == 0 && f_closePlank_2.Count == 0 && Input.GetButtonDown("Fire1")) || (Input.GetButtonDown("Cancel") && f_iarBackground.Count == 0)))
                ExitPlank();
            else
            {
                // Check if the current focused word is clicked
                if (Input.GetButtonDown("Fire1") && currentFocusedWord)
                {
                    //trace action depending on selection state
                    string actionName = "turnOn";
                    if (currentFocusedWord.GetComponent<TextMeshPro>().color == Color.yellow)
                        actionName = "turnOff";
                    GameObjectManager.addComponent<ActionPerformed>(currentFocusedWord, new { name = actionName, performedBy = "player", family = currentFocusedWord.GetComponent<IsSolution>() ? null : f_wrongWords });

                    // if wire is selected
                    if (wireSelected())
                    {
                        //if the word was selected (color yellow)
                        if (currentFocusedWord.GetComponent<TextMeshPro>().color == Color.yellow)
                        {
                            //unselect it, but we are still over (cyan)
                            currentFocusedWord.GetComponent<TextMeshPro>().color = Color.cyan;
                            //remove the vertex from the linerenderer
                            lrPositions.Remove(currentFocusedWord.transform.TransformPoint(Vector3.up * -4));
                            nbWordsSelected--;
                            lineRenderer.positionCount = nbWordsSelected;
                            //set the new positions
                            lineRenderer.SetPositions(lrPositions.ToArray());

                            GameObjectManager.addComponent<ActionPerformedForLRS>(currentFocusedWord, new
                            {
                                verb = "deactivated",
                                objectType = "word",
                                activityExtensions = new Dictionary<string, string>() {
                                    { "value", currentFocusedWord.name },
                                    { "content", currentFocusedWord.GetComponent<TextMeshPro>().text } 
                                }
                            });
                        }
                        else //if the word wasn't selected
                        {
                            // check if there is already 3 selected words, if so unselect all words except current focused word
                            if (nbWordsSelected >= 3)
                            {
                                foreach (GameObject w in f_allWords)
                                {
                                    if (w.GetComponent<TextMeshPro>().color == Color.yellow && w != currentFocusedWord)
                                    {
                                        GameObjectManager.addComponent<ActionPerformed>(w, new { name = "turnOff", performedBy = "system", family = w.GetComponent<IsSolution>() ? null : f_wrongWords });
                                        GameObjectManager.addComponent<ActionPerformedForLRS>(w, new
                                        {
                                            verb = "deactivated",
                                            objectType = "word",
                                            activityExtensions = new Dictionary<string, string>() {
                                                { "value", w.name },
                                                { "content", w.GetComponent<TextMeshPro>().text }
                                            }
                                        });
                                    }
                                    w.GetComponent<TextMeshPro>().color = Color.white;
                                }
                                lineRenderer.positionCount = 0;
                                nbWordsSelected = 0;
                                lrPositions.Clear();
                            }
                            // Now select the new one
                            currentFocusedWord.GetComponent<TextMeshPro>().color = Color.yellow;
                            //update the linerenderer
                            nbWordsSelected++;
                            lineRenderer.positionCount = nbWordsSelected;
                            lrPositions.Add(currentFocusedWord.transform.TransformPoint(Vector3.up * -4));
                            lineRenderer.SetPositions(lrPositions.ToArray());

                            GameObjectManager.addComponent<ActionPerformedForLRS>(currentFocusedWord, new
                            {
                                verb = "activated",
                                objectType = "word",
                                activityExtensions = new Dictionary<string, string>() {
                                    { "value", currentFocusedWord.name },
                                    { "content", currentFocusedWord.GetComponent<TextMeshPro>().text } 
                                },
                                result = true,
                                success = 1
                            });

                            string answers = "";
                            //if 3 words selected
                            if (nbWordsSelected >= 3)
                            {
                                //create a concatenation of the 3 selected answers
                                foreach (GameObject word in f_allWords)
                                    if (word.GetComponent<TextMeshPro>().color == Color.yellow)
                                    {
                                        if (answers == "")
                                            answers = word.GetComponent<TextMeshPro>().text;
                                        else
                                            answers = string.Concat(answers, " - ", word.GetComponent<TextMeshPro>().text);
                                    }

                                // check if all selected words are part of the solution
                                correct = true;
                                foreach (GameObject word in f_allWords)
                                    if (word.GetComponent<IsSolution>() && word.GetComponent<TextMeshPro>().color != Color.yellow)
                                    {
                                        correct = false;
                                        break;
                                    }

                                if (correct)
                                {
                                    // remove the wire from inventory
                                    LinkedWith lw = selectedPlank.GetComponent<LinkedWith>();
                                    GameObjectManager.setGameObjectState(lw.link, false);
                                    GameObjectManager.setGameObjectState(lw.link.GetComponent<HUDItemSelected>().hudGO, false);

                                    // notify player success
                                    GameObjectManager.addComponent<PlayUIEffect>(selectedPlank, new { effectCode = 2 });
                                    GameObjectManager.addComponent<ActionPerformed>(selectedPlank, new { name = "perform", performedBy = "system" });
                                    GameObjectManager.addComponent<ActionPerformedForLRS>(currentFocusedWord, new
                                    {
                                        verb = "completed",
                                        objectType = "plankAndWire"
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        //a word is clicked without using wire
                        GameObjectManager.addComponent<ActionPerformedForLRS>(currentFocusedWord, new
                        {
                            verb = "attempted",
                            objectType = "word",
                            activityExtensions = new Dictionary<string, string>() {
                                { "value", currentFocusedWord.name },
                                { "content", currentFocusedWord.GetComponent<TextMeshPro>().text },
                                // depends if word is selected or not
                                { "state", currentFocusedWord.GetComponent<TextMeshPro>().color == Color.yellow ? "selected" : "unselected" }
                            }
                        });
                    }
                }

                //if click over nothing unselect all
                if (Input.GetButtonDown("Fire1") && !currentFocusedWord && wireSelected())
                    unselectAllWords();

                if (nbWordsSelected > 0 && nbWordsSelected < 3 && wireSelected())
                {
                    //update the linerenderer
                    lineRenderer.positionCount = nbWordsSelected + 1; // one more for mouse position
                    List<Vector3> lrPositionsWithMouse = new List<Vector3>(lrPositions);
                    Vector3 screenPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Input.mousePosition.z);
                    float dist = Vector3.Distance(Camera.main.transform.position, lineRenderer.transform.position);
                    screenPoint.z = dist-0.2f; //distance of the plane from the camera
                    lrPositionsWithMouse.Add(Camera.main.ScreenToWorldPoint(screenPoint));
                    lineRenderer.SetPositions(lrPositionsWithMouse.ToArray());
                }
            }
        }
	}

    private void ExitPlank()
    {
        // remove ReadyToWork component to release selected GameObject
        GameObjectManager.removeComponent<ReadyToWork>(selectedPlank);

        if (!correct && wireSelected())
            unselectAllWords();

        GameObjectManager.addComponent<ActionPerformed>(selectedPlank, new { name = "turnOff", performedBy = "player" });
        GameObjectManager.addComponent<ActionPerformedForLRS>(selectedPlank, new 
        {
            verb = "exited",
            objectType = "plankAndWire"
        });

        selectedPlank = null;

        // Remove Collider2D to words
        foreach (GameObject word in f_allWords)
        {
            GameObjectManager.removeComponent<BoxCollider>(word);
            GameObjectManager.removeComponent<Rigidbody>(word);
            GameObjectManager.removeComponent<BoxCollider>(word.transform.GetChild(0).gameObject);
        }

        // pause this system
        instance.Pause = true;
    }

    public void DisplayWireOnSolution()
    {
        lrPositions.Clear();
        foreach (GameObject solution in f_solutionWords)
            lrPositions.Add(solution.transform.TransformPoint(Vector3.up * -4));
        lineRenderer.positionCount = lrPositions.Count;
        lineRenderer.SetPositions(lrPositions.ToArray());
    }

    public bool IsResolved()
    {
        return correct;
    }
}