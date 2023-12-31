using Assets.Code;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlienScript : MonoBehaviour
{
    public static AlienScript instance;

    static Vector2 Z_RANGE = new(8, 13);
    static Vector2 X_RANGE_PER_Z = new(-.3f, .3f);
    static Vector2 Y_RANGE_PER_Z = new(0, .9f);
    static Vector2 KNOCK_TIMER_RANGE = new(8, 10);
    static Vector2 REPOSITION_TIMER_RANGE = new(5, 15);
    static Vector2 BLINK_TIMER_RANGE = new(1, 5);
    static float BLINK_TIME = .1f;
    static float FLOAT_STRENGTH = .05f;
    static Vector3 GRIN_PUPIL_SCALE = new Vector3(.2f, .2f, 1);
    static float GRIN_IRIS_SHAKE_FACTOR = .02f;

    static float INTRO_WAIT_SECONDS_AWAKEN = 1;
    static float INTRO_WAIT_SECONDS_APPEAR = 1;
    static Vector2 IDLE_SPEAK_TIMER_RANGE = new(25, 40);

    public Transform cameraTransform;
    public Transform bobAnchor;
    public Transform[] sclarae, irises, pupils;
    public Transform mouthTransform;
    public AudioSource sfxSourceKnock, sfxSourceSpeech, sfxSourceWhisper, sfxSourceNotes;
    public AudioClip[] sfxClipsKnock;
    public VOScriptableObject[] voIntroTalking, voIntroWaitingToProgress, voIdle;
    public AudioSource sfxAmbience;
    public GameOverScript gameOver;

    RoomScript currentRoom;
    AlienState state;
    float stateTimer;
    float knockTimer;
    float blinkTimer, repositionTimer;
    Vector3 targetPosition;
    Vector3 vTranslate, vRotate;
    Vector3 lookOverride;
    Vector3 vGrin, vPupils;
    float vAmbientVolume, vSpeechBlend, vNotesDistance;
    float voTimeSinceLast, voIdleTimeTarget, voWait;

    Queue<VOScriptableObject> voQueue;
    public VOScriptableObject voActive;

    void Start() {
        if (instance == null) {
            instance = this;
        }
        // Start at max distance, in the middle of the XY range.
        float zStart = Z_RANGE.y;
        float zMid = (Z_RANGE.x + Z_RANGE.y) / 2;
        float xMid = zStart * (X_RANGE_PER_Z.x + X_RANGE_PER_Z.y) / 2;
        float yTarget = zStart * Mathf.Lerp(Y_RANGE_PER_Z.x, Y_RANGE_PER_Z.y, .8f);
        transform.localPosition = new Vector3(xMid, Y_RANGE_PER_Z.x * zStart, zStart);
        targetPosition = new Vector3(xMid, yTarget, zMid);
        foreach (Transform t in sclarae) {
            t.localScale = new Vector3(1, 0, 1);
        }
        SetRepositionTimer();
        voQueue = new();
        knockTimer = 2;
        voIdleTimeTarget = Util.SampleRangeVector(IDLE_SPEAK_TIMER_RANGE);
    }
    void SetRepositionTimer() {
        repositionTimer = Util.SampleRangeVector(REPOSITION_TIMER_RANGE);
    }
    public void ChangeState(AlienState newState) {
        state = newState;
        stateTimer = 0;
    }
    public void SetCurrentRoom(RoomScript roomScript) {
        currentRoom = roomScript;
        if (currentRoom.prevRoomScript?.tag == "IntroRoom") {
            voQueue.Clear();
        }
        lookOverride = Vector3.zero;
        if (!currentRoom.isIntroRoom) {
            state = AlienState.Main;
        }
    }
    public void EnqueueVO(params VOScriptableObject[] vos) {
        foreach (VOScriptableObject vo in vos) {
            voQueue.Enqueue(vo);
        }
    }
    public void ClearVOQueue() {
        voQueue.Clear();
    }

    void Update() {
        UpdateVO();
        stateTimer += Time.deltaTime;
        if (state == AlienState.IntroWaitingToAwaken) {
            if (stateTimer > INTRO_WAIT_SECONDS_AWAKEN) {
                ChangeState(AlienState.IntroKnocking);
            }
        } else if (state == AlienState.IntroKnocking) {
            if (Vector3.Dot(cameraTransform.forward, Vector3.forward) < .66f) {
                stateTimer = 0; // Player isn't looking at the window.
                knockTimer -= Time.deltaTime;
            } else if (cameraTransform.position.z < -5) {
                stateTimer = 0; // Player is too far from the window.
                knockTimer -= Time.deltaTime;
            } else {
                knockTimer = Util.SampleRangeVector(KNOCK_TIMER_RANGE);
            }
            if (knockTimer <= 0) {
                SFXKnock();
                knockTimer = VignetteScript.instance.dismissed ? Util.SampleRangeVector(KNOCK_TIMER_RANGE) : 5;
            }
            if (stateTimer > 2) {
                ChangeState(AlienState.IntroAppearing);
            }
        } else if (state == AlienState.IntroAppearing) {
            transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetPosition, ref vTranslate, .5f);
            if (stateTimer > INTRO_WAIT_SECONDS_APPEAR) {
                ChangeState(AlienState.IntroTalking);
                voQueue = new Queue<VOScriptableObject>(voIntroTalking);
            }
        } else if (state == AlienState.IntroTalking) {
            if (voActive == null && voQueue.Count == 0) {
                ChangeState(AlienState.IntroWaitingToProgress);
            }
        }
        UpdateMain();
        if (state >= AlienState.EndLooming) {
            sfxAmbience.volume = Mathf.SmoothDamp(sfxAmbience.volume, 0, ref vAmbientVolume, 5);
            sfxSourceNotes.maxDistance = Mathf.SmoothDamp(sfxSourceNotes.maxDistance, 30, ref vNotesDistance, 2);
            //sfxSourceSpeech.spatialBlend = Mathf.SmoothDamp(sfxSourceSpeech.spatialBlend, 1, ref vSpeechBlend, 3);
        }
        if (state == AlienState.EndGrin) {
            mouthTransform.localScale = Vector3.SmoothDamp(mouthTransform.localScale, Vector3.one, ref vGrin, 1f);
            pupils[0].localScale = Vector3.SmoothDamp(pupils[0].localScale, GRIN_PUPIL_SCALE, ref vPupils, 1f);
            pupils[1].localScale = pupils[0].localScale;
        }
    }

    void UpdateMain() {
        // Choose look target.
        Vector3 lookTarget = (lookOverride == Vector3.zero) ? cameraTransform.position : lookOverride;
        // Rotation and iris movement.
        Quaternion lookRotation = Quaternion.LookRotation(transform.position - lookTarget);
        transform.localRotation = Util.SmoothDampQuaternion(transform.localRotation, lookRotation, ref vRotate, .33f);
        Vector3 lookDistance = (lookRotation * Quaternion.Inverse(transform.localRotation)).eulerAngles;
        if (lookDistance.x < -180) lookDistance.x += 360;
        if (lookDistance.x > 180) lookDistance.x -= 360;
        if (lookDistance.y < -180) lookDistance.y += 360;
        if (lookDistance.y > 180) lookDistance.y -= 360;
        Vector3 irisPosition = new Vector3(lookDistance.y * -.0066f, lookDistance.x * .0066f, -.01f);
        irisPosition.x += Random.Range(-1f, 1f) * GRIN_IRIS_SHAKE_FACTOR * mouthTransform.localScale.y;
        irisPosition.y += Random.Range(-1f, 1f) * GRIN_IRIS_SHAKE_FACTOR * mouthTransform.localScale.y;
        foreach (Transform t in irises) {
            t.localPosition = irisPosition;
        }
        // Position.
        if (state == AlienState.Main) {
            repositionTimer -= Time.deltaTime;
            if (repositionTimer <= 0) {
                float z = Util.SampleRangeVector(Z_RANGE);
                float x = Util.SampleRangeVector(X_RANGE_PER_Z) * z;
                float y = Util.SampleRangeVector(Y_RANGE_PER_Z) * z;
                targetPosition = new Vector3(x, y, z);
                SetRepositionTimer();
            }
        } else if (state == AlienState.EndElevator) {
            float z = Z_RANGE.x;
            targetPosition = new(0, Y_RANGE_PER_Z.x * z, z);
        } else if (state == AlienState.EndLooming) {
            targetPosition = new(0, 20, 7);
        }
        Vector3 offsetTargetPosition = targetPosition;
        float roomX = Mathf.RoundToInt(cameraTransform.position.x / 8) * 8;
        offsetTargetPosition.x += roomX;
        if (state <= AlienState.EndElevator) {
            float xOffset = 1 * (roomX - lookTarget.x);
            xOffset = Mathf.Clamp(xOffset, -4, 4);
            float yOffset = lookTarget.y;
            offsetTargetPosition.x += xOffset;
            offsetTargetPosition.y -= yOffset * .66f;
            offsetTargetPosition.z += lookTarget.z * .1f;
        }
        if (state >= AlienState.IntroAppearing) {
            transform.localPosition = Vector3.SmoothDamp(transform.localPosition, offsetTargetPosition, ref vTranslate, 2, 8);
        }
        bobAnchor.localPosition = new Vector3(
            Mathf.Sin(Time.time),
            Mathf.Sin(Time.time * 2.1f),
            state <= AlienState.EndElevator ? Mathf.Sin(Time.time * .5f) : 0
        ) * FLOAT_STRENGTH;
        // Blink.
        if (state >= AlienState.IntroAppearing) {
            blinkTimer -= Time.deltaTime;
        }
        if (blinkTimer <= 0) {
            blinkTimer = Util.SampleRangeVector(BLINK_TIMER_RANGE);
        }
        float blinkT = blinkTimer < BLINK_TIME ? 1 - ((blinkTimer / BLINK_TIME) - BLINK_TIME / 2) * 2 : 1;
        float blinkScale = Mathf.Min(1, blinkT, .9f + .1f * Mathf.Sin(Time.time * .4f));
        if (state < AlienState.IntroAppearing) {
            blinkScale = 0;
        } else if (state == AlienState.IntroAppearing) {
            blinkScale = Mathf.Lerp(0, blinkScale, stateTimer * 4);
        }
        foreach (Transform t in sclarae) {
            t.localScale = new Vector3(1, blinkScale, 1);
        }
    }

    public bool IsVODone() {
        return voActive == null && voQueue.Count == 0;
    }
    void UpdateVO() {
        if (!sfxSourceSpeech.isPlaying && !sfxSourceWhisper.isPlaying) {
            if (voActive != null) {
                HandleScriptActions(voActive.endActions);
                voWait = 1 + voActive.wait;
            }
            voActive = null;
        }
        if (voWait <= 0 && voActive == null && voQueue.Count > 0) {
            SFXSpeak(voQueue.Dequeue());
        }
        voWait -= Time.deltaTime;
        if (state == AlienState.Main && IsVODone()) {
            voTimeSinceLast += Time.deltaTime;
        }
        if (voTimeSinceLast >= voIdleTimeTarget) {
            SFXSpeak(voIdle[Random.Range(0, voIdle.Length - 1)]);
            voIdleTimeTarget = Util.SampleRangeVector(IDLE_SPEAK_TIMER_RANGE);
        }
    }
    public void SFXSpeak(VOScriptableObject vo) {
        voActive = vo;
        sfxSourceSpeech.PlayOneShot(vo.voiceClip);
        sfxSourceWhisper.PlayOneShot(vo.whisperClip);
        HandleScriptActions(vo.startActions);
        voTimeSinceLast = 0;
    }
    void HandleScriptActions(VOScriptAction[] actions) {
        foreach (VOScriptAction action in actions) {
            if (action == VOScriptAction.Replay) {
                voQueue.Enqueue(voActive);
            } else if (action == VOScriptAction.OpenDoor) {
                currentRoom.OpenDoor();
            } else if (action == VOScriptAction.LookAtDoor) {
                lookOverride = currentRoom.transform.position + new Vector3(6, 2, -6);
            } else if (action == VOScriptAction.LookAtPlayer) {
                lookOverride = Vector3.zero;
            } else if (action == VOScriptAction.DelayedDeploy) {
                currentRoom.OpenPanels(true);
            } else if (action == VOScriptAction.Grin) {
                ChangeState(AlienState.EndGrin);
            } else if (action == VOScriptAction.GameOver) {
                gameOver.enabled = true;
            }
        }
    }
    void SFXKnock() {
        sfxSourceKnock.PlayOneShot(sfxClipsKnock[Random.Range(0, sfxClipsKnock.Length)], 2);
        if (VignetteScript.instance != null) {
            VignetteScript.instance.Knock();
        }
    }
}

public enum AlienState
{
    IntroWaitingToAwaken,
    IntroKnocking,
    IntroAppearing,
    IntroTalking,
    IntroWaitingToProgress,
    Main,
    EndElevator,
    EndLooming,
    EndGrin,
}