using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorScript : MonoBehaviour
{
    static float OPENCLOSE_TIME = .4f;

    public GameObject doorCollision;

    Vector3 closedPos, openPos;
    bool opening;
    Vector3 v;

    void Start() {
        closedPos = transform.localPosition;
        openPos = closedPos + new Vector3(0, 2.9f, 0);
    }
    public bool Open() {
        bool wasOpening = opening;
        opening = true;
        doorCollision.SetActive(false);
        return !wasOpening;
    }
    public bool Close() {
        bool wasOpening = opening;
        opening = false;
        doorCollision.SetActive(true);
        return wasOpening;
    }

    void Update() {
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, opening ? openPos : closedPos, ref v, OPENCLOSE_TIME);
    }
}
