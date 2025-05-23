﻿using UnityEngine;
using System.Collections.Generic;
using FYFY;
using UnityStandardAssets.Characters.FirstPerson;
using UnityEngine.UI;
using FYFY_plugins.TriggerManager;
using FYFY_plugins.PointerManager;
using UnityEngine.SceneManagement;
using System.Collections;

public class MovingSystem_FPSMode : FSystem
{
    // This system manage HUD on moving, walking speed and state of the FirstPersonController

    private Family f_TransparentOnMove = FamilyManager.getFamily(new AllOfComponents(typeof(HUD_TransparentOnMove)));
    private Family f_waterWalking = FamilyManager.getFamily(new AnyOfLayers(12), new AllOfComponents(typeof(Triggered3D))); // Layer 12 <=> WaterCollider
    private Family f_CrouchHint = FamilyManager.getFamily(new AllOfComponents(typeof(AnimatedSprites), typeof(PointerOver), typeof(LinkedWith), typeof(BoxCollider)));
    private Family f_OutOfFirstRoom = FamilyManager.getFamily(new AllOfComponents(typeof(Triggered3D), typeof(LinkedWith)));

    public GameObject player;
    public float traceMovementFrequency;
    public bool crouching; // true when the player is crouching
    private bool changingPose = false;
    private Vector3 targetScale;
    private Vector3 crouchingScale;
    private Vector3 standingScale = Vector3.one;
    private FirstPersonController playerController;
    private Graphic[] tmpGraphics;
    private AudioBank audioBank;
    private Camera playerCamera;

    public GameObject cursor;

    private bool playerIsWalking = false;
    private bool playerWasWalking = false;
    private bool showHUD;
    private bool hideHUD;
    private float hudShowingSpeed;
    private float hudHidingSpeed;
    private bool walkInWater = false;
    private bool firstCrouchOccurs = false;

    private float walkingTraceTimer = 0;

    public GameObject night;

    public static MovingSystem_FPSMode instance;

    public MovingSystem_FPSMode()
    {
        instance = this;
    }

    protected override void onStart()
    {
        //when crouching, the scale of the player is changed (rather than its position)
        crouchingScale = new Vector3(standingScale.x * 0.5f, standingScale.y * 0.2f, standingScale.z * 0.5f);
        traceMovementFrequency = 0;
        crouching = false;

        playerController = player.GetComponent<FirstPersonController>();

        playerCamera = playerController.transform.GetChild(0).GetComponent<Camera>();
        audioBank = playerController.GetComponent<AudioBank>();
        f_waterWalking.addEntryCallback(onEnterWater);
        f_waterWalking.addExitCallback(onExitWater);

        if (!SceneManager.GetActiveScene().name.Contains("Tuto"))
        {
            f_CrouchHint.addEntryCallback(disableHUDWarning);
            f_OutOfFirstRoom.addEntryCallback(disableHUDWarning);
        }

        //MainLoop.instance.StartCoroutine(testLRS());
    }

    /*    private IEnumerator testLRS()
        {
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForSeconds(0.025f);
                Vector3 pos = new Vector3(i, i, i);
                GameObjectManager.addComponent<ActionPerformedForLRS>(playerController.gameObject, new
                {
                    verb = "moved",
                    objectType = "avatar",
                    objectName = "player",
                    activityExtensions = new Dictionary<string, string>() { { "position", pos.ToString("G4") } }
                });
            }
        }*/

    private void disableHUDWarning(GameObject go)
    {
        foreach (LinkedWith link in go.GetComponents<LinkedWith>())
            GameObjectManager.setGameObjectState(link.link, false);
    }

    private void onEnterWater(GameObject go)
    {
        walkInWater = true;
        playerController.m_FootstepSounds[0] = audioBank.audioBank[2];
        playerController.m_FootstepSounds[1] = audioBank.audioBank[3];
        if (!crouching)
        {
            playerController.m_WalkSpeed = playerController.m_WalkSpeed / 2;
            playerController.m_RunSpeed = playerController.m_RunSpeed / 2;
        }
    }

    private void onExitWater(int instanceId)
    {
        walkInWater = false;
        playerController.m_FootstepSounds[0] = audioBank.audioBank[0];
        playerController.m_FootstepSounds[1] = audioBank.audioBank[1];
        if (crouching)
        {
            playerController.m_FootstepSounds[0] = audioBank.audioBank[4];
            playerController.m_FootstepSounds[1] = audioBank.audioBank[5];
        }
        else
        {
            playerController.m_WalkSpeed = playerController.m_WalkSpeed * 2;
            playerController.m_RunSpeed = playerController.m_RunSpeed * 2;
            playerController.m_FootstepSounds[0] = audioBank.audioBank[0];
            playerController.m_FootstepSounds[1] = audioBank.audioBank[1];
        }
    }

    public void SetWalkSpeed(float speedW)
    {
        if (walkInWater || crouching)
            speedW = speedW / 2;
        playerController.m_WalkSpeed = speedW;
        playerController.m_RunSpeed = speedW;

    }

    // Use this to update member variables when system pause. 
    // Advice: avoid to update your families inside this function.
    protected override void onPause(int currentFrame)
    {
        playerController.enabled = false;
        // Show mouse cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
        // Hide fps cursor
        GameObjectManager.setGameObjectState(cursor, false);
    }

    // Use this to update member variables when system resume.
    // Advice: avoid to update your families inside this function.
    protected override void onResume(int currentFrame)
    {
        playerController.m_MouseLook.m_CameraTargetRot = playerCamera.transform.localRotation;
        playerController.m_MouseLook.m_CharacterTargetRot = player.transform.localRotation;

        playerController.enabled = true;
        // hide mouse cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // Show fps cursor
        GameObjectManager.setGameObjectState(cursor, true);
    }

    public void UnlockAllHUD()
    {
        foreach (GameObject hud in f_TransparentOnMove)
            if (hud.transform.parent.name == "FPSControl" || hud.transform.parent.name == "TeleportControl" || hud.transform.parent.name == "ClickControl")
                GameObjectManager.setGameObjectState(hud, true);
    }

    public void ChangePose(bool animation = true)
    {
        if (!changingPose)
        {
            changingPose = true; //true when the player is crouching or standing
                                 //change moving speed according to the stance
            if (crouching)
            {
                if (!walkInWater)
                {
                    playerController.m_WalkSpeed = playerController.m_WalkSpeed * 2;
                    playerController.m_RunSpeed = playerController.m_RunSpeed * 2;
                    playerController.m_FootstepSounds[0] = audioBank.audioBank[0];
                    playerController.m_FootstepSounds[1] = audioBank.audioBank[1];
                }
                targetScale = standingScale;
            }
            else
            { // standing and want to crouch
                if (!firstCrouchOccurs && night != null)
                {
                    firstCrouchOccurs = true;
                    night.GetComponent<Animator>().enabled = true;
                    night.GetComponent<Collider>().enabled = false;
                    UnlockAllHUD();
                }
                if (!walkInWater)
                {
                    playerController.m_WalkSpeed = playerController.m_WalkSpeed / 2;
                    playerController.m_RunSpeed = playerController.m_RunSpeed / 2;
                    playerController.m_FootstepSounds[0] = audioBank.audioBank[4];
                    playerController.m_FootstepSounds[1] = audioBank.audioBank[5];
                }
                targetScale = crouchingScale;
            }
            if (animation)
                MainLoop.instance.StartCoroutine(animateChangingPose());
            else
                hardChangingPose();
        }
    }

    private void hardChangingPose()
    {
        playerController.transform.localScale = targetScale;

        changingPose = false;
        crouching = !crouching; //true when the player is crouching

        if (crouching)
        {
            GameObjectManager.addComponent<ActionPerformed>(playerController.gameObject, new { name = "turnOn", performedBy = "player" });
            GameObjectManager.addComponent<ActionPerformedForLRS>(playerController.gameObject, new 
            {
                verb = "crouched",
                objectType = "avatar"
            });
        }
        else
        {
            GameObjectManager.addComponent<ActionPerformed>(playerController.gameObject, new { name = "turnOff", performedBy = "player" });
            GameObjectManager.addComponent<ActionPerformedForLRS>(playerController.gameObject, new 
            {
                verb = "stood",
                objectType = "avatar"
            });
        }
    }

    private IEnumerator animateChangingPose()
    {
        while (playerController.transform.localScale != targetScale) //when standing scale is reached
        {
            playerController.transform.localScale = Vector3.MoveTowards(playerController.transform.localScale, targetScale, 0.8f); //change stance gradually
            yield return null;
        }

        hardChangingPose();
    }

    // Use to process your families.
    protected override void onProcess(int familiesUpdateCount)
    {
        if (traceMovementFrequency > 0)
        {
            if (playerController.m_Input != Vector2.zero)
            {
                if (Time.time - walkingTraceTimer > traceMovementFrequency)
                {
                    GameObjectManager.addComponent<ActionPerformedForLRS>(playerController.gameObject, new
                    {
                        verb = "moved",
                        objectType = "avatar",
                        activityExtensions = new Dictionary<string, string>() {
                            { "position", player.transform.position.ToString("G4") } 
                        }
                    });
                    walkingTraceTimer = Time.time;
                }
            }
        }

        // when control button or right click is pressed then the player can alternatively crouch and standing
        if (Input.GetButtonDown("Fire2"))
        {
            ChangePose(true);
        }

        // make HUD transparent on moving
        hudHidingSpeed = -3f * Time.deltaTime;
        hudShowingSpeed = 3f * Time.deltaTime;
        if (hideHUD)
        {
            float aCount = 1;
            foreach (GameObject hud in f_TransparentOnMove)
            {
                tmpGraphics = hud.GetComponentsInChildren<Graphic>();
                foreach (Graphic tmpGraphic in tmpGraphics)
                {
                    if (!(tmpGraphic is RawImage))
                    {
                        tmpGraphic.color = new Color(tmpGraphic.color.r, tmpGraphic.color.g, tmpGraphic.color.b, tmpGraphic.color.a + hudHidingSpeed);
                        aCount = tmpGraphic.color.a;
                    }
                }
            }
            if (aCount < 0.3f)
            {
                foreach (GameObject hud in f_TransparentOnMove)
                {
                    tmpGraphics = hud.GetComponentsInChildren<Graphic>();
                    foreach (Graphic tmpGraphic in tmpGraphics)
                        if (!(tmpGraphic is RawImage))
                            tmpGraphic.color = new Color(tmpGraphic.color.r, tmpGraphic.color.g, tmpGraphic.color.b, 0.3f);
                }
                hideHUD = false;
            }
        }
        else if (showHUD)
        {
            float aCount = 0;
            foreach (GameObject hud in f_TransparentOnMove)
            {
                tmpGraphics = hud.GetComponentsInChildren<Graphic>();
                foreach (Graphic tmpGraphic in tmpGraphics)
                {
                    if (!(tmpGraphic is RawImage))
                    {
                        tmpGraphic.color = new Color(tmpGraphic.color.r, tmpGraphic.color.g, tmpGraphic.color.b, tmpGraphic.color.a + hudShowingSpeed);
                        aCount = tmpGraphic.color.a;
                    }
                }
            }
            if (aCount >= 1f)
            {
                foreach (GameObject hud in f_TransparentOnMove)
                {
                    tmpGraphics = hud.GetComponentsInChildren<Graphic>();
                    foreach (Graphic tmpGraphic in tmpGraphics)
                        if (!(tmpGraphic is RawImage))
                            tmpGraphic.color = new Color(tmpGraphic.color.r, tmpGraphic.color.g, tmpGraphic.color.b, 1f);
                }
                showHUD = false;
            }
        }
        playerIsWalking = playerController.m_Input.x != 0 || playerController.m_Input.y != 0;
        if (playerIsWalking && !playerWasWalking)
        {
            hideHUD = true;
            showHUD = false;
        }
        else if (!playerIsWalking && playerWasWalking)
        {
            showHUD = true;
            hideHUD = false;
        }
        playerWasWalking = playerIsWalking;
    }
}