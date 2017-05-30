﻿using UnityEngine;
using UnityEngine.Networking;

public class PlayerController : NetworkBehaviour
{
    public GameObject head;
    public GameObject body;
    public Transform bulletSpawn;

    public OVRInput.Controller dominantController = OVRInput.Controller.RTouch;
    public OVRInput.Controller supportController = OVRInput.Controller.LTouch;

    protected GameObject dominantControllerObject;
    protected GameObject supportControllerObject;

    public GameObject primaryWeapon;
    public GameObject secondaryWeapon;

    public float moveSpeed = 1.6f;

    protected Transform headPosition;

    private float nextPrimaryFireTime = 0f;
    private float nextSecondaryFireTime = 0f;

    private Rigidbody rb;

    private RaycastHit hit;

    private Bullet primaryBulletInstance;
    private Bullet secondaryBulletInstance;

    private OVRInput.Controller dominantTouch;
    private OVRInput.Controller supportTouch;

    private Vector3 moveDirection;

    void Start()
    {
        // Disable the MouseLook component by default
        var mouseLook = GetComponent<SmoothMouseLook>();
        mouseLook.enabled = false;

        // Get the rigidbody associated with the player
        rb = GetComponent<Rigidbody>();

        // Don't rotate unless we say so
        rb.freezeRotation = true;

        // Get the associated game objects
        dominantControllerObject = transform.Find("BodyRoot/CameraRoot/DominantTouch").gameObject;
        supportControllerObject = transform.Find("BodyRoot/CameraRoot/SupportTouch").gameObject;

        // Get instances from the objects
        primaryBulletInstance = primaryWeapon.GetComponent(typeof(Bullet)) as Bullet;
        secondaryBulletInstance = secondaryWeapon.GetComponent(typeof(Bullet)) as Bullet;

        if (isLocalPlayer)
        {
            var cameraRoot = transform.Find("BodyRoot/CameraRoot");

            // Put the camera inside of the player
            // This makes it so moving the player moves the headset
            Camera.main.transform.parent = cameraRoot;
            Camera.main.transform.localPosition = Vector3.zero;
            Camera.main.transform.localRotation = Quaternion.Euler(Vector3.zero);

            // Put the head inside of the camera's head position
            // This handles rotation and position of the head relative to the headset
            headPosition = Camera.main.transform.Find("HeadPosition");

            if (!OVRManager.isHmdPresent)
            {
                // Enable MouseLook
                mouseLook.enabled = true;

                // Move the camera root back so the head rotates in the right place 
                cameraRoot.localPosition = new Vector3(0f, cameraRoot.localPosition.y, 0f);

                // Attach the dominant hand to the head
                transform.Find("BodyRoot/CameraRoot/DominantTouch").parent = Camera.main.transform;

                // Remove the second hand
                transform.Find("BodyRoot/CameraRoot/SupportTouch").gameObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        // Movement
        if (OVRManager.isHmdPresent)
        {
            // Move hands into the right place
            dominantControllerObject.transform.localPosition = OVRInput.GetLocalControllerPosition(dominantController);
            dominantControllerObject.transform.localRotation = OVRInput.GetLocalControllerRotation(dominantController);
            supportControllerObject.transform.localPosition = OVRInput.GetLocalControllerPosition(supportController);
            supportControllerObject.transform.localRotation = OVRInput.GetLocalControllerRotation(supportController);

            // Get the position of the stick
            var moveStickPosition = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, supportController);

            // Keep the body under the head
            body.transform.position = new Vector3(head.transform.position.x, body.transform.position.y, head.transform.position.z);
            
            // Turn the body to face towards the hand
            // This will generally match the position of the body when holding a weapon
            var bodyRotateTowards = supportControllerObject.transform.position;

            // Reset the Y position so the body doesn't tilt up
            bodyRotateTowards.y = body.transform.position.y;

            // Get direction to the hand
            var direction = (bodyRotateTowards - body.transform.position).normalized;

            // Rotate the body towards the hand
            body.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // Set the move direction relative to the position of the leading hand
            moveDirection = body.transform.rotation * new Vector3(
                moveStickPosition.x,
                0,
                moveStickPosition.y
            );
        }
        else
        {
            // Use sticks to control movement
            moveDirection = transform.rotation * new Vector3(
                Input.GetAxis("Horizontal"),
                0,
                Input.GetAxis("Vertical")
            );
        }

        moveDirection = Vector3.ClampMagnitude(moveDirection * moveSpeed, moveSpeed);

        // Move the head object to the head position
        // Instead of having the head as a child, we need to do this for network purposes
        head.transform.position = headPosition.position;
        head.transform.rotation = headPosition.rotation;

        // This causes the character to ramp off of hills, but makes all hills climbable
        // if (Physics.Raycast(transform.position, -transform.up, out hit, 2f))
        // {
        //     // Always move parallel to the ground below us if we're grounded
        //     // This makes the character not slow down when climbing hills
        //     moveDirection = Quaternion.FromToRotation(transform.up, hit.normal) * moveDirection;
        // }

        // Apply drag
        var vel = rb.velocity;
        vel.x *= 0.8f;
        vel.z *= 0.8f;
        rb.velocity = vel;

        // Add forces to move the body relative to the position of the leading hand
        rb.AddForce(moveDirection, ForceMode.Impulse);

        // Add some extra gravity
        // This makes it impossible for the character to climb hills
        // rb.AddForce(-transform.up * 10000);

        // Firing
        if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, dominantController) > 0 || Input.GetButton("Fire1"))
        {
            if (Time.time > nextPrimaryFireTime)
            {
                //spawnBullet();
                CmdFirePrimary();

                nextPrimaryFireTime = Time.time + (1f / primaryBulletInstance.rate);
            }
        }
        else
        {
            if (OVRInput.Get(OVRInput.Button.One, dominantController) || Input.GetButton("Fire2"))
            {
                if (Time.time > nextSecondaryFireTime)
                {
                    CmdFireSecondary();

                    nextSecondaryFireTime = Time.time + (1f / secondaryBulletInstance.rate);
                }
            }
        }
    }

    public override void OnStartLocalPlayer()
    {
        transform.Find("Healthbar Canvas").gameObject.SetActive(false);
        transform.Find("BodyRoot/Body").GetComponent<MeshRenderer>().material.color = Color.blue;
    }

    GameObject spawnBullet()
    {
        var prefab = primaryWeapon;

        // Create the Bullet from the Bullet Prefab
        var instance = (GameObject)Instantiate(
            prefab,
            bulletSpawn.position,
            bulletSpawn.rotation
        );

        var bullet = instance.GetComponent<Bullet>();

        // Add velocity to the bullet
        instance.GetComponent<Rigidbody>().velocity = instance.transform.forward * bullet.speed;

        return instance;
    }

    [Command]
    void CmdFirePrimary()
    {
        var prefab = primaryWeapon;

        // Create the Bullet from the Bullet Prefab
        var instance = (GameObject)Instantiate(
            prefab,
            bulletSpawn.position,
            bulletSpawn.rotation
        );

        var bullet = instance.GetComponent<Bullet>();

        // Add velocity to the bullet
        instance.GetComponent<Rigidbody>().velocity = instance.transform.forward * bullet.speed;

        // Spawn the bullet on the Clients
        NetworkServer.Spawn(instance);

        // Destroy the bullet after it times out
        Destroy(instance, bullet.time);
    }

    [Command]
    void CmdFireSecondary()
    {
        var prefab = secondaryWeapon;

        // Create the Bullet from the Bullet Prefab
        var instance = (GameObject)Instantiate(
            prefab,
            bulletSpawn.position,
            bulletSpawn.rotation
        );

        var bullet = instance.GetComponent<Bullet>();

        // Add velocity to the bullet
        instance.GetComponent<Rigidbody>().velocity = instance.transform.forward * bullet.speed;

        // Spawn the bullet on the Clients
        NetworkServer.Spawn(instance);

        // Destroy the bullet after it times out
        Destroy(instance, bullet.time);
    }


    public static float ClampAngle (float angle, float min, float max)
    {
        if (angle < -360F)
            angle += 360F;
        if (angle > 360F)
            angle -= 360F;
        return Mathf.Clamp (angle, min, max);
    }
}
