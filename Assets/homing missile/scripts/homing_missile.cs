using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace HomingMissile
{
public class homing_missile : MonoBehaviour
{
    public int speed = 60;
    public int downspeed = 30;
    public int damage = 35;
    public bool fully_active = false;
    public int timebeforeactivition = 20;
    public int timebeforebursting = 40;
    public int timebeforedestruction = 450;
    public int timealive;
    public GameObject target;
    public GameObject shooter;
    public Rigidbody projectilerb;
    public bool isactive = false;
    public Vector3 sleepposition;
    public GameObject targetpointer;
    public float turnSpeed = 0.035f;
    public AudioSource launch_sound;
    public AudioSource thrust_sound;
    public GameObject smoke_obj;
    public ParticleSystem smoke;
    public GameObject smoke_position;
    public GameObject destroy_effect;
    private void Start()
    {
        projectilerb = this.GetComponent<Rigidbody>();
    }
    public void call_destroy_effects()
    {
        if (destroy_effect == null)
        {
            return;
        }

        Instantiate(destroy_effect, transform.position, transform.rotation);
    }
    public void setmissile()
    {
        timealive = 0;
        if (shooter != null)
        {
            transform.rotation = shooter.transform.rotation;
            transform.Rotate(0, 90, 0);
            transform.position = shooter.transform.position;
        }

        sleepposition = transform.position;
    }
    public void DestroyMe()
    {
        isactive = false;
        fully_active = false;
        timealive = 0;
        if (smoke != null)
        {
            smoke.transform.SetParent(null);
            smoke.Pause();
            smoke.transform.position = sleepposition;
            smoke.Play();
            Destroy(smoke.gameObject,3);
        }

        if (projectilerb != null)
        {
            projectilerb.velocity = Vector3.zero;
        }

        if (thrust_sound != null)
        {
            thrust_sound.Pause();
        }

        call_destroy_effects();
        transform.position = sleepposition;
        Destroy(this.gameObject);
    }
    public void usemissile()
    {
        if (launch_sound != null)
        {
            launch_sound.Play();
        }

        isactive = true;
        setmissile();

    }
    private void OnTriggerEnter(Collider other)
    {
        if (!isactive || other == null)
        {
            return;
        }

        if (target != null && (other.gameObject == target || other.transform.IsChildOf(target.transform)))
        {
            DestroyMe();
            return;
        }

        if (shooter != null && (other.gameObject == shooter || other.transform.IsChildOf(shooter.transform)))
        {
            if (fully_active)
            {
                DestroyMe();
            }
            return;
        }

        if (other.gameObject.CompareTag("Player"))
        {
            if (other.gameObject == shooter)
            {
                if (fully_active)
                {
                    DestroyMe();
                }
            }
            else
            {
                DestroyMe();
            }
        }
    }
    void FixedUpdate()
    {
        if (isactive)
        {
            if (target == null || !target.activeInHierarchy)
            {
                DestroyMe();
                return;
            }
            if (timealive == timebeforeactivition)
            {
                fully_active = true;
                if (thrust_sound != null)
                {
                    thrust_sound.Play();
                }
            }
            timealive++;
            if (timealive < timebeforebursting)
            {
                if (projectilerb != null)
                {
                    projectilerb.velocity = transform.up * -1 * downspeed;
                }
            }
            if (timealive == timebeforebursting)
            {
                if (smoke_obj != null)
                {
                    Vector3 smokeSpawnPosition = smoke_position != null ? smoke_position.transform.position : transform.position;
                    Quaternion smokeSpawnRotation = smoke_position != null ? smoke_position.transform.rotation : transform.rotation;
                    smoke=(Instantiate(smoke_obj, smokeSpawnPosition, smokeSpawnRotation)).GetComponent<ParticleSystem>();
                    if (smoke != null)
                    {
                        smoke.Play();
                        smoke.transform.SetParent(this.transform);
                    }
                }
            }
            if (timealive == timebeforedestruction)
            {
                DestroyMe();
            }
            if (timealive >= timebeforebursting && timealive < timebeforedestruction)
            {
                if (targetpointer != null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetpointer.transform.rotation, turnSpeed);
                }

                if (projectilerb != null)
                {
                    projectilerb.velocity = transform.forward * speed;
                }
            }
        }
    }
}
}