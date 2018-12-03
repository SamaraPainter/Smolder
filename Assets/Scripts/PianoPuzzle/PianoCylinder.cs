﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PianoCylinder : OVRGrabbable {


    //COLORS:
    //None - 0
    //Red - 1
    //Blue - 2
    //Yellow - 3
    //Green - 4
    //Purple - 5
    //Orange - 6
    //

    public int color;

    public GameObject leftHand;
    public GameObject rightHand;

    public PianoCylinderManager parent;

    public GameObject stickyTop;
    public GameObject stickyBottom;

    public PianoCylinder[] attached;

    bool grabbedByLeft;

    public float maxDistanceToSeparate;

    public override void GrabBegin(OVRGrabber hand, Collider grabPoint)
    {
       // transform.parent = null;
        m_grabbedBy = hand;
        m_grabbedCollider = grabPoint;
        gameObject.GetComponent<Rigidbody>().isKinematic = true;

        if(hand.gameObject.name == leftHand.name)
        {
            parent.leftHandGrabbing = this;
            grabbedByLeft = true;
        }
        else if (hand.gameObject.name == rightHand.name)
        {
            grabbedByLeft = false;
            parent.rightHandGrabbing = this;

        }


        grabChild(0);
        grabChild(1);


    }

    /// <summary>
    /// Notifies the object that it has been released.
    /// </summary>
    public override void GrabEnd(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        foreach(PianoCylinder child in gameObject.GetComponentsInChildren<PianoCylinder>())
        {
            child.gameObject.transform.parent = null;

        }

        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        rb.isKinematic = m_grabbedKinematic;
        rb.velocity = linearVelocity;
        rb.angularVelocity = angularVelocity;
        m_grabbedBy = null;
        m_grabbedCollider = null;

        if (grabbedByLeft)
        {

            parent.leftHandGrabbing = null;

        }
        else
        {

            parent.rightHandGrabbing = null;
        }


    }

    public void AttachCylinder(GameObject toAttach, int index, int otherIndex)
    {
        if(attached.Length == 0)
        {
            attached = new PianoCylinder[2];
            attached[0] = null;
            attached[1] = null;
        }

        attached[index] = toAttach.GetComponent<PianoCylinder>();
        toAttach.GetComponent<PianoCylinder>().attached[otherIndex] = this;
            

    }

    public void RemoveAttachedCylinder(int index, int otherIndex)
    {

        if (attached == null)
        {
            attached = new PianoCylinder[2];

        }
        attached[index].attached[otherIndex] = null;
        attached[index] = null;



    }

    void grabChild(int index)
    {
        if (attached.Length > 0)
        {
            if (attached[index] != null)
            {
                attached[index].transform.parent = gameObject.transform;
                if (attached[index].attached[0] != null && attached[index].attached[0].name == gameObject.name)
                {
                    attached[index].grabChild(1);
                }
                else if (attached[index].attached[1] != null && attached[index].attached[1].name == gameObject.name)
                {
                    attached[index].grabChild(0);
                }

            }
        }
    }

    private void Update()
    {
        if(attached.Length > 0)
        {
            if(attached[0] != null)
            {

                if(Vector3.Distance(attached[0].transform.position, transform.position) > maxDistanceToSeparate)
                {
                    if(attached[0].attached[0] != null && attached[0].attached[0].name == gameObject.name)
                    {
                        RemoveAttachedCylinder(0, 0);
                    }
                    else if (attached[0].attached[1] != null && attached[0].attached[1].name == gameObject.name)
                    {
                        RemoveAttachedCylinder(0, 1);

                    }

                }

            }

            if (attached[1] != null)
            {

                if (Vector3.Distance(attached[1].transform.position, transform.position) > maxDistanceToSeparate)
                {
                    if (attached[1].attached[0] != null && attached[1].attached[0].name == gameObject.name)
                    {
                        RemoveAttachedCylinder(1, 0);
                    }
                    else if (attached[1].attached[1] != null && attached[1].attached[1].name == gameObject.name)
                    {
                        RemoveAttachedCylinder(1, 1);

                    }

                }

            }


        }
    }

}