﻿using UnityEngine;
using FYFY;
using FYFY_plugins.PointerManager;
using System.Collections.Generic;
using TMPro;
using FYFY_plugins.Monitoring;

public class WhiteBoardManager : FSystem {
    
    // this system manage the plank and the wire

    //all selectable objects
    private Family f_whiteBoard = FamilyManager.getFamily(new AnyOfTags("Board"));
    private Family f_focusedWhiteBoard = FamilyManager.getFamily(new AnyOfTags("Board"), new AllOfComponents(typeof(ReadyToWork)));
    private Family f_closeWhiteBoard = FamilyManager.getFamily (new AnyOfTags ("Board", "Eraser", "BoardTexture", "InventoryElements"), new AllOfComponents(typeof(PointerOver)));
    private Family f_boardRemovableWords = FamilyManager.getFamily(new AnyOfTags("BoardRemovableWords"));
    private Family f_itemSelected = FamilyManager.getFamily(new AnyOfTags("InventoryElements"), new AllOfComponents(typeof(SelectedInInventory)));
    private Family f_iarBackground = FamilyManager.getFamily(new AnyOfTags("UIBackground"), new AnyOfProperties(PropertyMatcher.PROPERTY.ACTIVE_IN_HIERARCHY));

    private Family f_player = FamilyManager.getFamily(new AnyOfTags("Player"));

    //information for animations
    private Vector3 camNewDir;
    private Vector3 newDir;
    private Vector3 playerLocalScale;

    //board
    private GameObject board;
    private GameObject selectedBoard;
    private bool onBoard = false;
    private bool moveToBoard = false;
    private Vector3 boardPos;
    private GameObject eraser;
    public static bool eraserDragged = false;
    private float distToBoard;

    private GameObject currentFocusedBoard;

    public static WhiteBoardManager instance;

    public WhiteBoardManager()
    {
        if (Application.isPlaying)
        {
            //initialise variables
            eraser = f_whiteBoard.First().transform.GetChild(2).gameObject;

            // set all board's removable words to "occludable"
            // the occlusion is then made by an invisible material that hides all objects behind it having this setting
            foreach (GameObject word in f_boardRemovableWords)
            {
                foreach (Renderer r in word.GetComponentsInChildren<Renderer>())
                    r.material.renderQueue = 2001;
            }

            f_focusedWhiteBoard.addEntryCallback(onReadyToWorkOnWhiteBoard);
        }
        instance = this;
    }

    private void onReadyToWorkOnWhiteBoard(GameObject go)
    {
        selectedBoard = go;
        distToBoard = (f_player.First().transform.position - selectedBoard.transform.position).magnitude;

        // Launch this system
        instance.Pause = false;
    }

    // Use this to update member variables when system pause. 
    // Advice: avoid to update your families inside this function.
    protected override void onPause(int currentFrame) {
	}

	// Use this to update member variables when system resume.
	// Advice: avoid to update your families inside this function.
	protected override void onResume(int currentFrame){
	}

	// Use to process your families.
	protected override void onProcess(int familiesUpdateCount)
    {
        if (selectedBoard)
        {
            // "close" ui (give back control to the player) when clicking on nothing or Escape is pressed and IAR is closed (because Escape close IAR)
            if (((f_closeWhiteBoard.Count == 0 && Input.GetMouseButtonDown(0)) || (Input.GetKeyDown(KeyCode.Escape) && f_iarBackground.Count == 0)))
                ExitWhiteBoard();
            else
            {
                if (eraser.GetComponent<PointerOver>() && Input.GetMouseButtonDown(0))
                {
                    //start dragging eraser when it s clicked
                    eraserDragged = true;
                }
                if (eraserDragged)
                {
                    if (Input.GetMouseButtonUp(0))
                    {
                        //stop dragging eraser when the click is released
                        eraserDragged = false;
                    }
                    else
                    {
                        //move eraser to mouse position
                        Vector3 mousePos = selectedBoard.transform.InverseTransformPoint(Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distToBoard)));
                        eraser.transform.localPosition = new Vector3(mousePos.x, eraser.transform.localPosition.y, mousePos.z);
                        //prevent eraser from going out of the board
                        if (eraser.transform.localPosition.x > 0.021f)
                        {
                            eraser.transform.localPosition += Vector3.right * (0.021f - eraser.transform.localPosition.x);
                        }
                        else if (eraser.transform.localPosition.x < -0.021f)
                        {
                            eraser.transform.localPosition += Vector3.right * (-0.021f - eraser.transform.localPosition.x);
                        }
                        if (eraser.transform.localPosition.z > 0.016f)
                        {
                            eraser.transform.localPosition += Vector3.forward * (0.016f - eraser.transform.localPosition.z);
                        }
                        else if (eraser.transform.localPosition.z < -0.016f)
                        {
                            eraser.transform.localPosition += Vector3.forward * (-0.016f - eraser.transform.localPosition.z);
                        }
                    }
                }
            }
        }
	}

    private void ExitWhiteBoard()
    {
        // remove ReadyToWork component to release selected GameObject
        GameObjectManager.removeComponent<ReadyToWork>(selectedBoard);

        selectedBoard = null;

        // Pause this system
        instance.Pause = true;
    }
}